-- ============================================================
-- Tour Guide Marketplace — Initial Schema
-- Migration: 001_initial_schema
-- ============================================================

-- Extensions
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ============================================================
-- ENUM TYPES
-- ============================================================

CREATE TYPE user_role AS ENUM ('customer', 'guide', 'admin');
CREATE TYPE verification_status AS ENUM ('pending', 'approved', 'rejected');
CREATE TYPE subscription_plan AS ENUM ('free', 'premium', 'pro');
CREATE TYPE tour_category AS ENUM ('nature', 'culture', 'food', 'resort', 'adventure', 'other');
CREATE TYPE tour_status AS ENUM ('draft', 'active', 'inactive');
CREATE TYPE booking_status AS ENUM ('pending', 'confirmed', 'completed', 'cancelled', 'rejected');
CREATE TYPE payment_status AS ENUM ('unpaid', 'paid', 'refunded');
CREATE TYPE cancellation_by AS ENUM ('customer', 'guide', 'admin');
CREATE TYPE boost_plan AS ENUM ('basic', 'standard', 'premium');
CREATE TYPE boost_status AS ENUM ('active', 'expired', 'cancelled');
CREATE TYPE withdrawal_method AS ENUM ('bank', 'momo', 'zalopay', 'vnpay');
CREATE TYPE withdrawal_status AS ENUM ('pending', 'approved', 'rejected', 'completed');
CREATE TYPE application_status AS ENUM ('pending', 'approved', 'rejected');
CREATE TYPE ticket_type AS ENUM ('report_guide', 'report_customer', 'tour_issue', 'payment_issue', 'other');
CREATE TYPE ticket_status AS ENUM ('open', 'in_progress', 'resolved', 'closed');

-- ============================================================
-- SHARED TRIGGER FUNCTION: set_updated_at
-- ============================================================

CREATE OR REPLACE FUNCTION set_updated_at()
RETURNS TRIGGER AS $$
BEGIN
  NEW.updated_at = now();
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- ============================================================
-- TABLE: users
-- ============================================================

CREATE TABLE users (
  id              UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
  email           VARCHAR(255) UNIQUE NOT NULL,
  full_name       VARCHAR(150) NOT NULL,
  phone           VARCHAR(20) UNIQUE,
  avatar_url      TEXT,
  role            user_role NOT NULL DEFAULT 'customer',
  is_verified     BOOLEAN NOT NULL DEFAULT false,
  is_banned       BOOLEAN NOT NULL DEFAULT false,
  ban_reason      TEXT,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  deleted_at      TIMESTAMPTZ
);

CREATE TRIGGER trg_users_updated_at
  BEFORE UPDATE ON users
  FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ============================================================
-- TABLE: guide_profiles
-- ============================================================

CREATE TABLE guide_profiles (
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id               UUID UNIQUE NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  bio                   TEXT,
  experience_years      SMALLINT NOT NULL DEFAULT 0,
  languages             TEXT[] NOT NULL DEFAULT '{}',
  certifications        JSONB NOT NULL DEFAULT '[]',
  identity_doc_url      TEXT,
  verification_status   verification_status NOT NULL DEFAULT 'pending',
  rejection_reason      TEXT,
  avg_rating            NUMERIC(3,2) NOT NULL DEFAULT 0.00,
  total_reviews         INT NOT NULL DEFAULT 0,
  subscription_plan     subscription_plan NOT NULL DEFAULT 'free',
  subscription_expires_at TIMESTAMPTZ,
  balance               NUMERIC(15,2) NOT NULL DEFAULT 0,
  total_earned          NUMERIC(15,2) NOT NULL DEFAULT 0,
  total_withdrawn       NUMERIC(15,2) NOT NULL DEFAULT 0,
  created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at            TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TRIGGER trg_guide_profiles_updated_at
  BEFORE UPDATE ON guide_profiles
  FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ============================================================
-- TABLE: guide_applications
-- ============================================================

CREATE TABLE guide_applications (
  id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id           UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  full_name         VARCHAR(150) NOT NULL,
  phone             VARCHAR(20) NOT NULL,
  location          VARCHAR(150),
  bio               TEXT NOT NULL,
  experience_years  SMALLINT NOT NULL DEFAULT 0,
  languages         TEXT[] NOT NULL,
  certifications    JSONB,
  identity_doc_url  TEXT NOT NULL,
  certificate_urls  TEXT[],
  status            application_status NOT NULL DEFAULT 'pending',
  rejection_reason  TEXT,
  reviewed_by       UUID REFERENCES users(id),
  reviewed_at       TIMESTAMPTZ,
  created_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- ============================================================
-- TABLE: tours
-- ============================================================

CREATE TABLE tours (
  id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  guide_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  title            VARCHAR(200) NOT NULL,
  slug             VARCHAR(250) UNIQUE NOT NULL,
  description      TEXT NOT NULL,
  category         tour_category NOT NULL,
  location_city    VARCHAR(100) NOT NULL,
  location_address TEXT,
  lat              NUMERIC(10,7),
  lng              NUMERIC(10,7),
  price_per_person NUMERIC(15,2) NOT NULL CHECK (price_per_person > 0),
  duration_hours   NUMERIC(5,1) NOT NULL CHECK (duration_hours > 0),
  max_group_size   SMALLINT NOT NULL DEFAULT 10,
  highlights       TEXT[] NOT NULL DEFAULT '{}',
  included         TEXT[] NOT NULL DEFAULT '{}',
  excluded         TEXT[] NOT NULL DEFAULT '{}',
  itinerary        JSONB NOT NULL DEFAULT '[]',
  images           TEXT[] NOT NULL DEFAULT '{}',
  cover_image_url  TEXT,
  status           tour_status NOT NULL DEFAULT 'draft',
  avg_rating       NUMERIC(3,2) NOT NULL DEFAULT 0.00,
  total_reviews    INT NOT NULL DEFAULT 0,
  total_bookings   INT NOT NULL DEFAULT 0,
  is_boosted       BOOLEAN NOT NULL DEFAULT false,
  boost_expires_at TIMESTAMPTZ,
  created_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
  deleted_at       TIMESTAMPTZ
);

CREATE INDEX idx_tours_search ON tours(status, location_city, category) WHERE deleted_at IS NULL;
CREATE INDEX idx_tours_boost  ON tours(is_boosted, boost_expires_at) WHERE is_boosted = true;

CREATE TRIGGER trg_tours_updated_at
  BEFORE UPDATE ON tours
  FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ============================================================
-- TABLE: tour_availabilities
-- ============================================================

CREATE TABLE tour_availabilities (
  id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tour_id        UUID NOT NULL REFERENCES tours(id) ON DELETE CASCADE,
  available_date DATE NOT NULL,
  max_slots      SMALLINT NOT NULL,
  booked_slots   SMALLINT NOT NULL DEFAULT 0,
  is_blocked     BOOLEAN NOT NULL DEFAULT false,
  UNIQUE(tour_id, available_date),
  CHECK(booked_slots <= max_slots)
);

-- ============================================================
-- TABLE: bookings
-- ============================================================

CREATE TABLE bookings (
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tour_id             UUID NOT NULL REFERENCES tours(id),
  customer_id         UUID NOT NULL REFERENCES users(id),
  guide_id            UUID NOT NULL REFERENCES users(id),
  tour_date           DATE NOT NULL,
  num_people          SMALLINT NOT NULL CHECK (num_people > 0),
  total_price         NUMERIC(15,2) NOT NULL,
  contact_name        VARCHAR(150) NOT NULL,
  contact_phone       VARCHAR(20) NOT NULL,
  contact_email       VARCHAR(255),
  note                TEXT,
  status              booking_status NOT NULL DEFAULT 'pending',
  rejection_reason    TEXT,
  cancellation_by     cancellation_by,
  cancellation_reason TEXT,
  refund_amount       NUMERIC(15,2) NOT NULL DEFAULT 0,
  refund_policy       VARCHAR(10),
  payment_status      payment_status NOT NULL DEFAULT 'unpaid',
  payment_method      VARCHAR(50),
  payment_txn_id      VARCHAR(150) UNIQUE,
  payment_paid_at     TIMESTAMPTZ,
  confirmed_at        TIMESTAMPTZ,
  completed_at        TIMESTAMPTZ,
  created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_bookings_customer ON bookings(customer_id, status);
CREATE INDEX idx_bookings_guide    ON bookings(guide_id, status);

CREATE TRIGGER trg_bookings_updated_at
  BEFORE UPDATE ON bookings
  FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ============================================================
-- TABLE: conversations
-- ============================================================

CREATE TABLE conversations (
  id                   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  booking_id           UUID UNIQUE NOT NULL REFERENCES bookings(id) ON DELETE CASCADE,
  customer_id          UUID NOT NULL REFERENCES users(id),
  guide_id             UUID NOT NULL REFERENCES users(id),
  customer_unread      INT NOT NULL DEFAULT 0,
  guide_unread         INT NOT NULL DEFAULT 0,
  last_message_at      TIMESTAMPTZ,
  last_message_preview VARCHAR(200),
  created_at           TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- ============================================================
-- TABLE: messages
-- ============================================================

CREATE TABLE messages (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  conversation_id UUID NOT NULL REFERENCES conversations(id) ON DELETE CASCADE,
  sender_id       UUID NOT NULL REFERENCES users(id),
  content         TEXT NOT NULL,
  is_read         BOOLEAN NOT NULL DEFAULT false,
  read_at         TIMESTAMPTZ,
  sent_at         TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_messages_conv ON messages(conversation_id, sent_at DESC);

-- Enable Realtime for messages
ALTER PUBLICATION supabase_realtime ADD TABLE messages;

-- ============================================================
-- TABLE: reviews
-- ============================================================

CREATE TABLE reviews (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  booking_id  UUID UNIQUE NOT NULL REFERENCES bookings(id) ON DELETE CASCADE,
  tour_id     UUID NOT NULL REFERENCES tours(id),
  customer_id UUID NOT NULL REFERENCES users(id),
  guide_id    UUID NOT NULL REFERENCES users(id),
  rating      SMALLINT NOT NULL CHECK (rating BETWEEN 1 AND 5),
  comment     TEXT,
  guide_reply TEXT,
  is_visible  BOOLEAN NOT NULL DEFAULT true,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- ============================================================
-- TABLE: boosts
-- ============================================================

CREATE TABLE boosts (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tour_id         UUID NOT NULL REFERENCES tours(id) ON DELETE CASCADE,
  guide_id        UUID NOT NULL REFERENCES users(id),
  plan            boost_plan NOT NULL,
  price_paid      NUMERIC(15,2) NOT NULL,
  duration_days   SMALLINT NOT NULL,
  starts_at       TIMESTAMPTZ NOT NULL,
  expires_at      TIMESTAMPTZ NOT NULL,
  payment_txn_id  VARCHAR(150) UNIQUE,
  status          boost_status NOT NULL DEFAULT 'active'
);

CREATE INDEX idx_boosts_expires ON boosts(expires_at) WHERE status = 'active';

-- ============================================================
-- TABLE: subscriptions
-- ============================================================

CREATE TABLE subscriptions (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  guide_id        UUID NOT NULL REFERENCES users(id),
  plan            subscription_plan NOT NULL,
  price_paid      NUMERIC(15,2) NOT NULL,
  starts_at       TIMESTAMPTZ NOT NULL,
  expires_at      TIMESTAMPTZ NOT NULL,
  payment_txn_id  VARCHAR(150) UNIQUE,
  status          boost_status NOT NULL DEFAULT 'active'
);

-- ============================================================
-- TABLE: withdrawals
-- ============================================================

CREATE TABLE withdrawals (
  id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  guide_id     UUID NOT NULL REFERENCES users(id),
  amount       NUMERIC(15,2) NOT NULL CHECK (amount > 0),
  fee          NUMERIC(15,2) NOT NULL,
  net_amount   NUMERIC(15,2) NOT NULL,
  method       withdrawal_method NOT NULL,
  account_info JSONB NOT NULL,
  note         TEXT,
  status       withdrawal_status NOT NULL DEFAULT 'pending',
  admin_note   TEXT,
  created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
  processed_at TIMESTAMPTZ
);

-- ============================================================
-- TABLE: wishlists
-- ============================================================

CREATE TABLE wishlists (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  customer_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  tour_id     UUID NOT NULL REFERENCES tours(id) ON DELETE CASCADE,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE(customer_id, tour_id)
);

-- ============================================================
-- TABLE: notifications
-- ============================================================

CREATE TABLE notifications (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id     UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  type        VARCHAR(60) NOT NULL,
  title       VARCHAR(200) NOT NULL,
  body        TEXT,
  entity_type VARCHAR(50),
  entity_id   UUID,
  is_read     BOOLEAN NOT NULL DEFAULT false,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_notifications_user ON notifications(user_id, is_read, created_at DESC);

-- ============================================================
-- TABLE: tickets
-- ============================================================

CREATE TABLE tickets (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  created_by      UUID NOT NULL REFERENCES users(id),
  against_user_id UUID REFERENCES users(id),
  booking_id      UUID REFERENCES bookings(id),
  type            ticket_type NOT NULL,
  title           VARCHAR(200) NOT NULL,
  description     TEXT NOT NULL,
  evidence_urls   TEXT[],
  status          ticket_status NOT NULL DEFAULT 'open',
  admin_id        UUID REFERENCES users(id),
  admin_response  TEXT,
  resolved_at     TIMESTAMPTZ,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- ============================================================
-- TRIGGER: Update avg_rating after review INSERT/UPDATE
-- ============================================================

CREATE OR REPLACE FUNCTION update_ratings_on_review()
RETURNS TRIGGER AS $$
BEGIN
  -- Update tour rating
  UPDATE tours SET
    avg_rating   = (SELECT COALESCE(AVG(rating), 0) FROM reviews WHERE tour_id = NEW.tour_id AND is_visible = true),
    total_reviews = (SELECT COUNT(*) FROM reviews WHERE tour_id = NEW.tour_id AND is_visible = true)
  WHERE id = NEW.tour_id;

  -- Update guide profile rating
  UPDATE guide_profiles SET
    avg_rating    = (SELECT COALESCE(AVG(rating), 0) FROM reviews WHERE guide_id = NEW.guide_id AND is_visible = true),
    total_reviews = (SELECT COUNT(*) FROM reviews WHERE guide_id = NEW.guide_id AND is_visible = true)
  WHERE user_id = NEW.guide_id;

  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_update_ratings
  AFTER INSERT OR UPDATE ON reviews
  FOR EACH ROW EXECUTE FUNCTION update_ratings_on_review();

-- ============================================================
-- TRIGGER: Update conversation on new message
-- ============================================================

CREATE OR REPLACE FUNCTION update_conversation_on_message()
RETURNS TRIGGER AS $$
DECLARE
  v_customer_id UUID;
  v_guide_id    UUID;
BEGIN
  SELECT customer_id, guide_id
    INTO v_customer_id, v_guide_id
    FROM conversations
   WHERE id = NEW.conversation_id;

  IF NEW.sender_id = v_customer_id THEN
    UPDATE conversations SET
      last_message_at      = NEW.sent_at,
      last_message_preview = LEFT(NEW.content, 200),
      guide_unread         = guide_unread + 1
    WHERE id = NEW.conversation_id;
  ELSE
    UPDATE conversations SET
      last_message_at      = NEW.sent_at,
      last_message_preview = LEFT(NEW.content, 200),
      customer_unread      = customer_unread + 1
    WHERE id = NEW.conversation_id;
  END IF;

  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_conversation_on_message
  AFTER INSERT ON messages
  FOR EACH ROW EXECUTE FUNCTION update_conversation_on_message();

-- ============================================================
-- RLS POLICIES
-- ============================================================

ALTER TABLE messages ENABLE ROW LEVEL SECURITY;
ALTER TABLE conversations ENABLE ROW LEVEL SECURITY;
ALTER TABLE guide_profiles ENABLE ROW LEVEL SECURITY;

-- messages: only conversation members can SELECT/INSERT
CREATE POLICY messages_select ON messages FOR SELECT
  USING (
    EXISTS (
      SELECT 1 FROM conversations c
      WHERE c.id = messages.conversation_id
        AND (c.customer_id = auth.uid() OR c.guide_id = auth.uid())
    )
  );

CREATE POLICY messages_insert ON messages FOR INSERT
  WITH CHECK (
    sender_id = auth.uid()
    AND EXISTS (
      SELECT 1 FROM conversations c
      WHERE c.id = conversation_id
        AND (c.customer_id = auth.uid() OR c.guide_id = auth.uid())
    )
  );

-- conversations: only members can SELECT
CREATE POLICY conversations_select ON conversations FOR SELECT
  USING (customer_id = auth.uid() OR guide_id = auth.uid());

-- guide_profiles: identity_doc_url visible only to owner and admin
CREATE POLICY guide_profiles_select ON guide_profiles FOR SELECT
  USING (
    user_id = auth.uid()
    OR EXISTS (SELECT 1 FROM users WHERE id = auth.uid() AND role = 'admin')
    OR identity_doc_url IS NULL
  );

CREATE POLICY guide_profiles_update ON guide_profiles FOR UPDATE
  USING (user_id = auth.uid());
