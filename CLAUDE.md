# Tour Guide Marketplace — Project Guide

## Project Overview

Tour Guide Marketplace is a platform connecting tour guides (Guide) with travelers (Customer). Full flow: search tours → book → payment → chat → review → admin management.

**SRS Source:** `TourGuideMarketplace_SRS_v2.docx` (v1.0, 2025)

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend | React / Next.js (SPA) |
| Backend API | ASP.NET Web API (.NET 8) |
| Database | PostgreSQL via Supabase |
| Auth | Supabase Auth (JWT, OAuth social) |
| File Storage | Supabase Storage |
| Realtime (Chat) | Supabase Realtime (Postgres CDC) |
| Email | SendGrid / SMTP |
| Payment | VNPay |
| Background Jobs | Hangfire (.NET) |

---

## User Roles

| Role | Description | Key Permissions |
|------|-------------|-----------------|
| Customer | Traveler | Search, book, chat, review, wishlist |
| Guide | Approved tour guide | CRUD tours, manage bookings, finance, boost, subscription |
| Admin | System administrator | Approve guides, resolve disputes, revenue reports, full access |

---

## Database Schema

**Conventions:** UUID PKs (`gen_random_uuid()`), TIMESTAMPTZ for all timestamps, soft-delete via `deleted_at`, auto-update trigger for `updated_at`.

### Tables

#### users
1-1 extension of `auth.users`. Created via Supabase Webhook on email confirmation.

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | FK → auth.users |
| email | VARCHAR(255) UNIQUE NOT NULL | |
| full_name | VARCHAR(150) NOT NULL | |
| phone | VARCHAR(20) UNIQUE | |
| avatar_url | TEXT | Supabase Storage |
| role | ENUM | customer \| guide \| admin (DEFAULT customer) |
| is_verified | BOOLEAN DEFAULT false | |
| is_banned | BOOLEAN DEFAULT false | |
| ban_reason | TEXT | |
| created_at, updated_at, deleted_at | TIMESTAMPTZ | |

#### guide_profiles
Created when admin approves a guide_applications record.

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| user_id | UUID UNIQUE NOT NULL FK users | |
| bio | TEXT | |
| experience_years | SMALLINT DEFAULT 0 | |
| languages | TEXT[] | e.g. {vi, en, fr} |
| certifications | JSONB DEFAULT '[]' | [{name, issued_by, year}] |
| identity_doc_url | TEXT | Private bucket |
| verification_status | ENUM | pending \| approved \| rejected |
| rejection_reason | TEXT | |
| avg_rating | NUMERIC(3,2) DEFAULT 0.00 | Updated by trigger |
| total_reviews | INT DEFAULT 0 | Updated by trigger |
| subscription_plan | ENUM | free \| premium \| pro |
| subscription_expires_at | TIMESTAMPTZ | NULL if free |
| balance | NUMERIC(15,2) DEFAULT 0 | VND, not yet withdrawn |
| total_earned | NUMERIC(15,2) DEFAULT 0 | |
| total_withdrawn | NUMERIC(15,2) DEFAULT 0 | |

#### tours

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| guide_id | UUID NOT NULL FK users | |
| title | VARCHAR(200) NOT NULL | |
| slug | VARCHAR(250) UNIQUE NOT NULL | title + 6 random chars |
| description | TEXT NOT NULL | |
| category | ENUM | nature\|culture\|food\|resort\|adventure\|other |
| location_city | VARCHAR(100) NOT NULL | |
| location_address | TEXT | |
| lat, lng | NUMERIC(10,7) | Start point coords |
| price_per_person | NUMERIC(15,2) NOT NULL CHECK > 0 | VND |
| duration_hours | NUMERIC(5,1) NOT NULL CHECK > 0 | |
| max_group_size | SMALLINT NOT NULL DEFAULT 10 | |
| highlights | TEXT[] DEFAULT '{}' | |
| included | TEXT[] DEFAULT '{}' | |
| excluded | TEXT[] DEFAULT '{}' | |
| itinerary | JSONB DEFAULT '[]' | [{time, activity, description}] |
| images | TEXT[] DEFAULT '{}' | Max 10 URLs |
| cover_image_url | TEXT | |
| status | ENUM DEFAULT draft | draft \| active \| inactive |
| avg_rating | NUMERIC(3,2) DEFAULT 0.00 | Updated by trigger |
| total_reviews | INT DEFAULT 0 | |
| total_bookings | INT DEFAULT 0 | |
| is_boosted | BOOLEAN DEFAULT false | |
| boost_expires_at | TIMESTAMPTZ | |
| created_at, updated_at, deleted_at | TIMESTAMPTZ | |

**Indexes:**
```sql
CREATE INDEX idx_tours_search ON tours(status, location_city, category) WHERE deleted_at IS NULL;
CREATE INDEX idx_tours_boost  ON tours(is_boosted, boost_expires_at) WHERE is_boosted = true;
```

#### tour_availabilities

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| tour_id | UUID NOT NULL FK tours | |
| available_date | DATE NOT NULL | |
| max_slots | SMALLINT NOT NULL | |
| booked_slots | SMALLINT DEFAULT 0 | |
| is_blocked | BOOLEAN DEFAULT false | |
| | UNIQUE(tour_id, available_date) | |
| | CHECK(booked_slots <= max_slots) | |

#### bookings

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| tour_id | UUID NOT NULL FK tours | |
| customer_id | UUID NOT NULL FK users | |
| guide_id | UUID NOT NULL FK users | Denormalized |
| tour_date | DATE NOT NULL | |
| num_people | SMALLINT NOT NULL CHECK > 0 | |
| total_price | NUMERIC(15,2) NOT NULL | price_per_person × num_people |
| contact_name | VARCHAR(150) NOT NULL | |
| contact_phone | VARCHAR(20) NOT NULL | |
| contact_email | VARCHAR(255) | |
| note | TEXT | |
| status | ENUM DEFAULT pending | pending\|confirmed\|completed\|cancelled\|rejected |
| rejection_reason | TEXT | |
| cancellation_by | ENUM | customer \| guide \| admin |
| cancellation_reason | TEXT | |
| refund_amount | NUMERIC(15,2) DEFAULT 0 | |
| refund_policy | VARCHAR(10) | 100% \| 50% \| 0% |
| payment_status | ENUM DEFAULT unpaid | unpaid \| paid \| refunded |
| payment_method | VARCHAR(50) | vnpay \| momo \| ... |
| payment_txn_id | VARCHAR(150) UNIQUE | VNPay txn ID |
| payment_paid_at, confirmed_at, completed_at, created_at | TIMESTAMPTZ | |

**Indexes:**
```sql
CREATE INDEX idx_bookings_customer ON bookings(customer_id, status);
CREATE INDEX idx_bookings_guide    ON bookings(guide_id, status);
```

#### conversations
Auto-created when booking confirmed. 1 booking = 1 conversation.

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| booking_id | UUID UNIQUE NOT NULL FK bookings | |
| customer_id | UUID NOT NULL FK users | |
| guide_id | UUID NOT NULL FK users | |
| customer_unread | INT DEFAULT 0 | |
| guide_unread | INT DEFAULT 0 | |
| last_message_at | TIMESTAMPTZ | |
| last_message_preview | VARCHAR(200) | |
| created_at | TIMESTAMPTZ | |

#### messages
Realtime enabled. RLS: only conversation members can SELECT/INSERT.

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| conversation_id | UUID NOT NULL FK conversations | |
| sender_id | UUID NOT NULL FK users | |
| content | TEXT NOT NULL | |
| is_read | BOOLEAN DEFAULT false | |
| read_at | TIMESTAMPTZ | |
| sent_at | TIMESTAMPTZ DEFAULT now() | |

```sql
ALTER PUBLICATION supabase_realtime ADD TABLE messages;
CREATE INDEX idx_messages_conv ON messages(conversation_id, sent_at DESC);
```

#### reviews

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| booking_id | UUID UNIQUE NOT NULL FK bookings | 1 booking = max 1 review |
| tour_id | UUID NOT NULL FK tours | Denormalized |
| customer_id | UUID NOT NULL FK users | |
| guide_id | UUID NOT NULL FK users | |
| rating | SMALLINT NOT NULL CHECK (1-5) | |
| comment | TEXT | |
| guide_reply | TEXT | No edits after reply |
| is_visible | BOOLEAN DEFAULT true | Admin can hide |
| created_at | TIMESTAMPTZ | |

#### boosts

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| tour_id | UUID NOT NULL FK tours | |
| guide_id | UUID NOT NULL FK users | |
| plan | ENUM NOT NULL | basic \| standard \| premium |
| price_paid | NUMERIC(15,2) NOT NULL | |
| duration_days | SMALLINT NOT NULL | |
| starts_at | TIMESTAMPTZ NOT NULL | |
| expires_at | TIMESTAMPTZ NOT NULL | Indexed for cron |
| payment_txn_id | VARCHAR(150) UNIQUE | |
| status | ENUM DEFAULT active | active \| expired \| cancelled |

#### subscriptions

| Column | Type |
|--------|------|
| id | UUID PK |
| guide_id | UUID NOT NULL FK users |
| plan | ENUM NOT NULL (premium \| pro) |
| price_paid | NUMERIC(15,2) NOT NULL |
| starts_at | TIMESTAMPTZ NOT NULL |
| expires_at | TIMESTAMPTZ NOT NULL |
| payment_txn_id | VARCHAR(150) UNIQUE |
| status | ENUM DEFAULT active (active \| expired \| cancelled) |

#### withdrawals

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| guide_id | UUID NOT NULL FK users | |
| amount | NUMERIC(15,2) NOT NULL CHECK > 0 | |
| fee | NUMERIC(15,2) NOT NULL | amount × 0.02 |
| net_amount | NUMERIC(15,2) NOT NULL | amount - fee |
| method | ENUM NOT NULL | bank \| momo \| zalopay \| vnpay |
| account_info | JSONB NOT NULL | {account_no, account_name, bank_name / phone} |
| note | TEXT | |
| status | ENUM DEFAULT pending | pending \| approved \| rejected \| completed |
| admin_note | TEXT | |
| created_at, processed_at | TIMESTAMPTZ | |

#### guide_applications

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| user_id | UUID NOT NULL FK users | |
| full_name | VARCHAR(150) NOT NULL | |
| phone | VARCHAR(20) NOT NULL | |
| location | VARCHAR(150) | Operating region |
| bio | TEXT NOT NULL | |
| experience_years | SMALLINT DEFAULT 0 | |
| languages | TEXT[] NOT NULL | |
| certifications | JSONB | |
| identity_doc_url | TEXT NOT NULL | Private bucket |
| certificate_urls | TEXT[] | |
| status | ENUM DEFAULT pending | pending \| approved \| rejected |
| rejection_reason | TEXT | |
| reviewed_by | UUID FK users | Admin |
| reviewed_at | TIMESTAMPTZ | |
| created_at | TIMESTAMPTZ | |

#### wishlists

| Column | Type |
|--------|------|
| id | UUID PK |
| customer_id | UUID NOT NULL FK users |
| tour_id | UUID NOT NULL FK tours |
| created_at | TIMESTAMPTZ |
| | UNIQUE(customer_id, tour_id) |

#### notifications

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| user_id | UUID NOT NULL FK users | |
| type | VARCHAR(60) NOT NULL | booking_confirmed \| new_message \| review_received \| ... |
| title | VARCHAR(200) NOT NULL | |
| body | TEXT | |
| entity_type | VARCHAR(50) | booking \| tour \| review \| withdrawal |
| entity_id | UUID | ID of related entity |
| is_read | BOOLEAN DEFAULT false | |
| created_at | TIMESTAMPTZ | |

#### tickets (Disputes & Support)

| Column | Type | Notes |
|--------|------|-------|
| id | UUID PK | |
| created_by | UUID NOT NULL FK users | |
| against_user_id | UUID FK users | |
| booking_id | UUID FK bookings | |
| type | ENUM NOT NULL | report_guide\|report_customer\|tour_issue\|payment_issue\|other |
| title | VARCHAR(200) NOT NULL | |
| description | TEXT NOT NULL | |
| evidence_urls | TEXT[] | |
| status | ENUM DEFAULT open | open \| in_progress \| resolved \| closed |
| admin_id | UUID FK users | |
| admin_response | TEXT | |
| resolved_at | TIMESTAMPTZ | |
| created_at | TIMESTAMPTZ | |

---

## Key SQL: Triggers & RLS

### Auto-update `updated_at`
```sql
CREATE OR REPLACE FUNCTION set_updated_at()
RETURNS TRIGGER AS $$
BEGIN NEW.updated_at = now(); RETURN NEW; END;
$$ LANGUAGE plpgsql;
```

### Rating trigger (fires after review INSERT/UPDATE)
Updates `avg_rating` and `total_reviews` on both `tours` and `guide_profiles`.

### Conversation update trigger (fires after message INSERT)
Updates `last_message_at`, `last_message_preview`, and increments `customer_unread` / `guide_unread`.

### RLS Policies
- **messages**: Only conversation members (customer_id or guide_id) can SELECT/INSERT
- **guide_profiles**: Document URLs only visible to owner and admin
- **conversations**: Only members can read

---

## Business Rules

### Authentication Flow
1. Client → `supabase.auth.signUp()` → Supabase sends confirmation email
2. User confirms → Supabase triggers Webhook → `POST /internal/auth/user-created`
3. ASP.NET inserts into `users` (role=customer, is_verified=true)
4. Client calls `signInWithPassword()` → receives JWT (1h) + refresh_token (7 days)
5. JWT claim `app_metadata.role` used for role-based authorization

### Booking Flow
1. Customer creates booking → `status=pending`, `payment_status=unpaid`
2. VNPay payment → IPN callback → `payment_status=paid`, increment `booked_slots`
3. Guide confirms → `status=confirmed`, conversation auto-created
4. Guide completes → `status=completed`, revenue credited to guide balance, review unlocked
5. Guide can reject → 100% auto-refund

### Cancellation Refund Policy
| Timing | Refund % | Guide Balance Impact |
|--------|----------|---------------------|
| > 7 days before tour | 100% | No deduction |
| 48 hrs – 7 days | 50% | Deduct 50% if confirmed |
| < 48 hrs | 0% | Guide keeps all |
| Guide no-show | 100% | Admin handles via ticket |

### Commission Rates (applied at booking completion)
| Plan | Commission | Max Active Tours |
|------|-----------|-----------------|
| free | 15% | 5 |
| premium (299,000 VND/mo) | 10% | Unlimited |
| pro (799,000 VND/3mo) | 8% | Unlimited |

Revenue credited: `total_price × (1 - commission_rate)`

### Boost Packages
| Plan | Price (VND) | Duration | Display |
|------|------------|----------|---------|
| basic | 50,000 | 24 hrs | SPONSORED badge, homepage priority |
| standard | 100,000 | 3 days | Color border |
| premium | 200,000 | 7 days | Gold border, shadow |

Boosted tours appear at top of same sort group. Hangfire cron runs hourly to expire boosts.

### Withdrawal
- Min: 100,000 VND, Max: current balance
- Fee: 2% of amount
- Balance deducted immediately on request (prevent double withdrawal)
- Admin reject → refund balance

---

## API Reference

**Base URL:** `/api/v1`
**Auth:** `Authorization: Bearer <supabase_access_token>`

### Standard Response
```json
// Success
{ "success": true, "data": <T>, "message": "OK", "meta": { "page": 1, "size": 20, "total": 150 } }
// Error
{ "success": false, "data": null, "message": "Validation failed", "errors": ["..."] }
```

### HTTP Status Codes
| Code | Meaning |
|------|---------|
| 200 | Success |
| 201 | Created |
| 400 | Validation error |
| 401 | Not authenticated |
| 403 | Wrong role/not owner |
| 404 | Not found / soft-deleted |
| 409 | Conflict (duplicate) |
| 422 | Business rule violated |
| 429 | Rate limit exceeded |
| 500 | Server error |

### Auth & Internal
| Method | Endpoint | Auth |
|--------|----------|------|
| POST | /internal/auth/user-created | Service Key (Webhook) |
| POST | /internal/auth/sync-role | Service Key |
| POST | /auth/request-otp | Public |
| POST | /auth/verify-phone | Public |

### Users & Profiles
| Method | Endpoint | Auth |
|--------|----------|------|
| GET | /users/me | Bearer |
| PUT | /users/me | Bearer |
| PUT | /users/me/avatar | Bearer |
| GET | /guides/{id} | Public |
| PUT | /guides/me/profile | Guide |

### Tours
| Method | Endpoint | Auth |
|--------|----------|------|
| GET | /tours | Public |
| GET | /tours/{id} | Public |
| GET | /tours/{id}/availabilities | Public |
| POST | /tours | Guide |
| PUT | /tours/{id} | Guide (owner) |
| DELETE | /tours/{id} | Guide (owner) |
| PUT | /tours/{id}/status | Guide (owner) |
| GET | /guides/me/tours | Guide |
| POST | /tours/{id}/availabilities | Guide (owner) |
| PUT | /tours/{id}/availabilities/{date} | Guide (owner) |
| DELETE | /tours/{id}/availabilities/{date} | Guide (owner) |

**Search params:** `q`, `city`, `category`, `min_price`, `max_price`, `min_rating`, `min_duration`, `max_duration`, `sort` (newest/price_asc/price_desc/rating_desc), `page`, `size`

### Bookings & Payments
| Method | Endpoint | Auth |
|--------|----------|------|
| POST | /bookings | Customer |
| GET | /bookings/my | Customer |
| GET | /bookings/{id} | Bearer (owner) |
| POST | /bookings/{id}/confirm | Guide (owner) |
| POST | /bookings/{id}/reject | Guide (owner) |
| POST | /bookings/{id}/complete | Guide (owner) |
| POST | /bookings/{id}/cancel | Bearer (owner/admin) |
| GET | /guides/me/bookings | Guide |
| POST | /payments/vnpay/create | Customer |
| GET | /payments/vnpay/ipn | Public (VNPay callback) |
| GET | /payments/vnpay/return | Public (VNPay redirect) |

### Chat
| Method | Endpoint | Auth |
|--------|----------|------|
| GET | /conversations | Bearer |
| GET | /conversations/{id} | Bearer (member) |
| GET | /conversations/{id}/messages | Bearer (member) |
| POST | /conversations/{id}/messages | Bearer (member) |
| PUT | /conversations/{id}/read | Bearer (member) |

**Realtime:** Client subscribes directly via Supabase JS SDK:
```js
supabase.channel("conversation:" + id)
  .on("postgres_changes", { event: "INSERT", schema: "public", table: "messages",
      filter: "conversation_id=eq." + id }, callback)
  .subscribe()
```

### Reviews
| Method | Endpoint | Auth |
|--------|----------|------|
| GET | /tours/{id}/reviews | Public |
| POST | /reviews | Customer |
| PUT | /reviews/{id}/reply | Guide (tour owner) |
| GET | /customers/me/reviews | Customer |
| PUT | /admin/reviews/{id}/visibility | Admin |

### Wishlist
| Method | Endpoint | Auth |
|--------|----------|------|
| GET | /wishlists | Customer |
| POST | /wishlists | Customer |
| DELETE | /wishlists/{tourId} | Customer |

### Boost & Subscription
| Method | Endpoint | Auth |
|--------|----------|------|
| GET | /boosts/plans | Public |
| POST | /boosts | Guide |
| GET | /guides/me/boosts | Guide |
| GET | /subscriptions/plans | Public |
| POST | /subscriptions | Guide |
| GET | /guides/me/subscription | Guide |

### Finance (Guide)
| Method | Endpoint | Auth |
|--------|----------|------|
| GET | /guides/me/finance | Guide |
| GET | /guides/me/finance/transactions | Guide |
| POST | /withdrawals | Guide |
| GET | /withdrawals/my | Guide |

### Notifications
| Method | Endpoint | Auth |
|--------|----------|------|
| GET | /notifications | Bearer |
| GET | /notifications/unread-count | Bearer |
| PUT | /notifications/{id}/read | Bearer |
| PUT | /notifications/read-all | Bearer |

### Guide Applications
| Method | Endpoint | Auth |
|--------|----------|------|
| POST | /guide-applications | Customer |
| GET | /guide-applications/my | Customer |
| GET | /admin/guide-applications | Admin |
| GET | /admin/guide-applications/{id} | Admin |
| POST | /admin/guide-applications/{id}/approve | Admin |
| POST | /admin/guide-applications/{id}/reject | Admin |

### Tickets
| Method | Endpoint | Auth |
|--------|----------|------|
| POST | /tickets | Bearer |
| GET | /tickets/my | Bearer |
| GET | /tickets/{id} | Bearer (owner/admin) |
| GET | /admin/tickets | Admin |
| PUT | /admin/tickets/{id} | Admin |

### Admin
| Method | Endpoint | Auth |
|--------|----------|------|
| GET | /admin/stats | Admin |
| GET | /admin/stats/revenue | Admin |
| GET | /admin/stats/bookings | Admin |
| GET | /admin/users | Admin |
| GET | /admin/users/{id} | Admin |
| PUT | /admin/users/{id}/ban | Admin |
| GET | /admin/tours | Admin |
| PUT | /admin/tours/{id}/status | Admin |
| GET | /admin/withdrawals | Admin |
| POST | /admin/withdrawals/{id}/approve | Admin |
| POST | /admin/withdrawals/{id}/reject | Admin |
| GET | /admin/reports/export | Admin |

---

## ASP.NET Project Structure

```
Controllers/        # HTTP endpoints only, delegate to Services
Services/           # Business logic (BookingService, TourService, etc.) — 1 service per domain
Repositories/       # EF Core data access — IRepository<T> + UnitOfWork
Models/             # EF Core entity classes (1-1 with tables)
DTOs/               # Request/Response DTOs — FluentValidation on Request DTOs
Middleware/         # ErrorHandling, RequestLogging, RateLimiting (global exception → standard response)
BackgroundJobs/     # Hangfire recurring jobs: ExpireBoostJob, ExpireSubscriptionJob, SendEmailJob
Infrastructure/     # Supabase, SendGrid, VNPay integrations (implement Service interfaces)
```

---

## Security Requirements

- **JWT:** Authority = `https://<project>.supabase.co/auth/v1`, Audience = `authenticated`. Cache JWKS 24h.
- **Service Role Key:** Server-side only. Never expose to client. Used for: update Supabase Auth `app_metadata`, create Storage signed URLs.
- **Private Bucket:** `guide-documents`. Serve via signed URL (TTL=15 min) — never permanent URLs.
- **Rate Limiting:** 100 req/min/IP (public), 300 req/min (authenticated).
- **Input Validation:** FluentValidation on all Request DTOs.
- **VNPay:** Verify HMAC-SHA512 on all IPN callbacks before any processing.
- **CORS:** Whitelist production frontend domain + localhost:3000 (dev only).

---

## Performance Guidelines

- All list endpoints require `page` + `size` params (default 20, max 100).
- Use `AsNoTracking()` for read-only EF Core queries.
- Use `Include()` / `ThenInclude()` or DTO projection to avoid N+1.
- Pagination on messages: cursor-based by `sent_at` (default 50/page).

---

## Notification Events

| Event | Recipient | In-app | Email |
|-------|-----------|--------|-------|
| New booking (pending) | Guide | ✓ | ✓ |
| Booking confirmed | Customer | ✓ | ✓ |
| Booking rejected | Customer | ✓ | ✓ |
| Booking completed | Customer | ✓ | ✓ (with review link) |
| Booking cancelled (by customer) | Guide | ✓ | ✓ |
| Booking cancelled (by guide) | Customer | ✓ | ✓ |
| New message (offline) | Recipient | ✓ | ✓ (15-min digest) |
| Boost expiring (24h) | Guide | ✓ | ✓ |
| Subscription expiring (3 days) | Guide | ✓ | ✓ |
| Profile approved | Applicant | ✓ | ✓ |
| Profile rejected | Applicant | ✓ | ✓ (with reason) |
| Withdrawal processed | Guide | ✓ | ✓ |
| New review on tour | Guide | ✓ | — |

**Delivery:** ASP.NET writes to DB after business events → Supabase Realtime broadcasts to online clients (subscribe notifications by user_id) → Hangfire sends email via SendGrid (non-blocking).
