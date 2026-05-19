# GuideMarket — Tiến độ thực hiện

> Cập nhật lần cuối: 2026-05-19

---

## ✅ Phase 1 — Database Schema (Supabase)

**Migration:** `database/migrations/001_initial_schema.sql` — đã apply thành công lên Supabase project `gzwsjihtiskxgmjggubi`.

| Hạng mục | Chi tiết |
|----------|----------|
| **Extensions** | `pgcrypto` |
| **ENUM types** (15) | `user_role`, `verification_status`, `subscription_plan`, `tour_category`, `tour_status`, `booking_status`, `payment_status`, `cancellation_by`, `boost_plan`, `boost_status`, `withdrawal_method`, `withdrawal_status`, `application_status`, `ticket_type`, `ticket_status` |
| **Tables** (15) | `users`, `guide_profiles`, `guide_applications`, `tours`, `tour_availabilities`, `bookings`, `conversations`, `messages`, `reviews`, `boosts`, `subscriptions`, `withdrawals`, `wishlists`, `notifications`, `tickets` |
| **Indexes** | `idx_tours_search`, `idx_tours_boost`, `idx_bookings_customer`, `idx_bookings_guide`, `idx_messages_conv` |
| **Triggers** | `set_updated_at`, rating trigger, conversation update trigger, email confirmation sync |
| **RLS** | Enabled trên tất cả 15 bảng — backend bypass bằng service role key |
| **Realtime** | `messages` table trong `supabase_realtime` publication |

---

## ✅ Phase 2 — ASP.NET Backend Scaffold

**Project:** `GuideMarket.Api` (.NET 8 Web API)

| Package | Mục đích |
|---------|----------|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | EF Core + PostgreSQL native enums |
| `FluentValidation.AspNetCore` | Validation Request DTOs |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT từ Supabase |
| `Hangfire.AspNetCore` + `Hangfire.PostgreSql` | Background jobs |
| `Serilog.AspNetCore` | Structured logging |

**Cấu hình:**
- EF Core + NpgsqlDataSource với native enum mappings (8 enums đã map)
- JWT Bearer — Authority = Supabase Auth, Audience = `authenticated`
- CORS whitelist `http://localhost:3000`
- Hangfire PostgreSQL, `WorkerCount = 3` (tránh quá 15 connections Supabase free tier)
- FluentValidation auto-validation, Serilog request logging

---

## ✅ Phase 3 — Auth & User Endpoints

### Auth (`/api/v1/auth`)

| Method | Endpoint | Trạng thái |
|--------|----------|-----------|
| POST | `/auth/register` | ✅ |
| POST | `/auth/login` | ✅ |
| POST | `/auth/logout` | ✅ |
| POST | `/auth/refresh` | ✅ |
| POST | `/auth/forgot-password` | ✅ |
| POST | `/auth/reset-password` | ✅ |
| POST | `/auth/request-otp` | ✅ Supabase phone OTP |
| POST | `/auth/verify-phone` | ✅ verify OTP + update users.phone |
| POST | `/internal/auth/user-created` | ✅ Supabase webhook |
| POST | `/internal/auth/sync-role` | ✅ Service Key protected |

### Users (`/api/v1/users`)

| Method | Endpoint | Trạng thái |
|--------|----------|-----------|
| GET | `/users/me` | ✅ |
| PUT | `/users/me` | ✅ |
| PUT | `/users/me/avatar` | ✅ Supabase Storage bucket `avatars/` |

---

## ✅ Phase 4 — Tours

| Method | Endpoint | Trạng thái |
|--------|----------|-----------|
| GET | `/tours` | ✅ search + filter + pagination + boosted sort |
| GET | `/tours/{id}` | ✅ public (active); guide owner xem được draft/inactive |
| GET | `/tours/{id}/availabilities` | ✅ upcoming + not blocked |
| POST | `/tours` | ✅ guide only, auto slug |
| PUT | `/tours/{id}` | ✅ guide owner |
| DELETE | `/tours/{id}` | ✅ soft delete |
| PUT | `/tours/{id}/status` | ✅ |
| GET | `/guides/me/tours` | ✅ paginated |
| POST | `/tours/{id}/availabilities` | ✅ |
| PUT | `/tours/{id}/availabilities/{date}` | ✅ |
| DELETE | `/tours/{id}/availabilities/{date}` | ✅ |

**Models:** `Tour`, `TourAvailability` + enums `TourCategory`, `TourStatus`

---

## ✅ Phase 5 — Guide Profiles & Applications

### Guide Profiles (`/api/v1/guides`)

| Method | Endpoint | Trạng thái |
|--------|----------|-----------|
| GET | `/guides/{id}` | ✅ public view (approved only) |
| PUT | `/guides/me/profile` | ✅ guide only |

### Guide Applications

| Method | Endpoint | Trạng thái |
|--------|----------|-----------|
| POST | `/guide-applications` | ✅ customer |
| GET | `/guide-applications/my` | ✅ customer |
| GET | `/admin/guide-applications` | ✅ admin + filter by status |
| GET | `/admin/guide-applications/{id}` | ✅ admin |
| POST | `/admin/guide-applications/{id}/approve` | ✅ tạo profile + nâng role + sync Supabase |
| POST | `/admin/guide-applications/{id}/reject` | ✅ |

**Models:** `GuideProfile`, `GuideApplication` + enums `VerificationStatus`, `SubscriptionPlan`, `ApplicationStatus`

**Approve flow:** Insert `guide_profiles` → Update `users.role=guide` → SaveChanges → `AdminUpdateUserRoleAsync` (Supabase app_metadata sync)

---

## 🐛 Bugs đã fix

| Lỗi | Fix |
|-----|-----|
| Hangfire URI format | `ConvertUriToNpgsql()` helper |
| EF Core URI format | Áp dụng cùng helper |
| `column "role" is of type user_role but expression is of type text` | `NpgsqlDataSourceBuilder.MapEnum<>()` + `HasPostgresEnum<>()` |
| Hangfire max connections (EMAXCONNSESSION) | `WorkerCount = 3` |
| Swagger `IFormFile` error | Wrap trong class + `[Consumes("multipart/form-data")]` |

---

## ✅ Phase 6 — Bookings & Payment

| Method | Endpoint | Trạng thái |
|--------|----------|-----------|
| POST | `/bookings` | ✅ customer, validate slots |
| GET | `/bookings/my` | ✅ customer, paginated, filter by status |
| GET | `/bookings/{id}` | ✅ owner (customer/guide) hoặc admin |
| POST | `/bookings/{id}/confirm` | ✅ guide owner, auto-tạo conversation |
| POST | `/bookings/{id}/reject` | ✅ guide owner, auto-refund 100% |
| POST | `/bookings/{id}/complete` | ✅ guide owner, credit balance - commission |
| POST | `/bookings/{id}/cancel` | ✅ customer/admin, time-based refund |
| GET | `/guides/me/bookings` | ✅ guide, paginated, filter by status |
| POST | `/payments/vnpay/create` | ✅ customer, trả VNPay payment URL |
| GET | `/payments/vnpay/ipn` | ✅ HMAC-SHA512 verify, idempotent |
| GET | `/payments/vnpay/return` | ✅ redirect về frontend |

**Models mới:** `Booking`, `Conversation` + enums `BookingStatus`, `PaymentStatus`, `CancellationBy`

**Business rules đã implement:**
- Slot check khi tạo booking
- booked_slots++ sau IPN paid (idempotent, overflow guard)
- Commission: free=15%, premium=10%, pro=8% tại complete
- Refund policy: >7 ngày=100%, 48h-7d=50%, <48h=0%
- Conversation auto-tạo khi confirmed
- VNPay config trong `appsettings.json` (cần điền TmnCode + HashSecret thật)

---

## ✅ Phase 7 — Reviews, Wishlist, Chat, Boost, Subscription, Finance

### Reviews (`/api/v1/reviews`)
| Method | Endpoint | Trạng thái |
|--------|----------|-----------|
| GET | `/tours/{id}/reviews` | ✅ public, visible only |
| GET | `/customers/me/reviews` | ✅ |
| POST | `/reviews` | ✅ customer, completed booking only, 1 per booking |
| PUT | `/reviews/{id}/reply` | ✅ guide owner, one-time reply |
| PUT | `/admin/reviews/{id}/visibility` | ✅ admin toggle |

### Wishlist (`/api/v1/wishlists`)
| Method | Endpoint | Trạng thái |
|--------|----------|-----------|
| GET | `/wishlists` | ✅ |
| POST | `/wishlists` | ✅ active tour only, no duplicates |
| DELETE | `/wishlists/{tourId}` | ✅ |

### Chat (`/api/v1/conversations`)
| Method | Endpoint | Trạng thái |
|--------|----------|-----------|
| GET | `/conversations` | ✅ paginated |
| GET | `/conversations/{id}` | ✅ member only |
| GET | `/conversations/{id}/messages` | ✅ cursor-based (before param) |
| POST | `/conversations/{id}/messages` | ✅ member only |
| PUT | `/conversations/{id}/read` | ✅ clears unread counter |

### Boost & Subscription (`/api/v1/boosts`, `/api/v1/subscriptions`)
| Method | Endpoint | Trạng thái |
|--------|----------|-----------|
| GET | `/boosts/plans` | ✅ public |
| POST | `/boosts` | ✅ guide, returns VNPay URL |
| GET | `/guides/me/boosts` | ✅ |
| GET | `/subscriptions/plans` | ✅ public |
| POST | `/subscriptions` | ✅ guide, returns VNPay URL |
| GET | `/guides/me/subscription` | ✅ active subscription only |

**Boost plans:** basic (50k/1d), standard (100k/3d), premium (200k/7d)
**Subscription plans:** premium (299k/30d, 10% commission), pro (799k/90d, 8% commission)
**VNPay prefix:** `bt` = boost, `sb` = subscription — IPN handler routes accordingly

### Finance & Withdrawals (`/api/v1/withdrawals`, `/api/v1/admin/withdrawals`)
| Method | Endpoint | Trạng thái |
|--------|----------|-----------|
| GET | `/guides/me/finance` | ✅ balance + subscription info |
| GET | `/withdrawals/my` | ✅ guide history |
| POST | `/withdrawals` | ✅ min 100k, fee 2%, deduct balance immediately |
| GET | `/admin/withdrawals` | ✅ admin, filter by status |
| POST | `/admin/withdrawals/{id}/approve` | ✅ updates TotalWithdrawn |
| POST | `/admin/withdrawals/{id}/reject` | ✅ refunds balance |

**New enums mapped:** `boost_plan`, `boost_status`, `withdrawal_method`, `withdrawal_status`

---

## 🔲 Còn lại (backend hoàn thành, chỉ còn frontend)


**Notification events đã tích hợp:**
- Booking mới → notify guide (in-app + email)
- Booking confirmed → notify customer (in-app + email)
- Booking rejected → notify customer (in-app + email)
- Booking completed → notify customer (in-app + email)
- Booking cancelled → notify guide (in-app + email)
- Guide application approved → notify applicant (in-app + email)
- Guide application rejected → notify applicant (in-app + email)

**Cần điền để email hoạt động:** `SendGrid:ApiKey` trong `appsettings.json`
- [ ] **Frontend** — Next.js (chưa bắt đầu)
- [ ] **Tickets** — Dispute/support tickets (POST /tickets, GET /tickets/my, admin CRUD)

---

## ⚙️ Cấu hình môi trường

```
ConnectionStrings.DefaultConnection  — Supabase PostgreSQL (Pooler, Transaction mode)
Supabase.Url                         — https://gzwsjihtiskxgmjggubi.supabase.co
Supabase.AnonKey                     — ✅
Supabase.ServiceRoleKey              — ✅
Jwt.Authority                        — https://gzwsjihtiskxgmjggubi.supabase.co/auth/v1
Jwt.Audience                         — authenticated
Cors.AllowedOrigins                  — ["http://localhost:3000"]
```

### Chạy local
```bash
dotnet run --project GuideMarket.Api --launch-profile http
# Swagger:  http://localhost:5129/swagger
# Hangfire: http://localhost:5129/hangfire
```
