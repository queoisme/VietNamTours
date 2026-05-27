-- Migration: 007_performance_indexes
-- Purpose: Add missing indexes for hot query paths and enable pg_trgm for city search

-- pg_trgm extension for ILIKE city search (used by EF.Functions.ILike in TourRepository)
CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE INDEX IF NOT EXISTS idx_tours_location_city_trgm
    ON tours USING GIN (location_city gin_trgm_ops)
    WHERE deleted_at IS NULL;

-- Bookings by customer with sort (replace existing to add created_at for ORDER BY)
DROP INDEX IF EXISTS idx_bookings_customer;
CREATE INDEX idx_bookings_customer ON bookings(customer_id, created_at DESC);

-- Bookings by guide with sort
DROP INDEX IF EXISTS idx_bookings_guide;
CREATE INDEX idx_bookings_guide ON bookings(guide_id, created_at DESC);

-- Conversation lookup by (customer_id, guide_id) pair — used in BookingService and ConversationService
CREATE INDEX IF NOT EXISTS idx_conversations_pair ON conversations(customer_id, guide_id);

-- Messages by conversation and time (cursor-based pagination)
CREATE INDEX IF NOT EXISTS idx_messages_conv ON messages(conversation_id, sent_at DESC);

-- Notifications unread count — called on every page load via GET /notifications/unread-count
CREATE INDEX IF NOT EXISTS idx_notifications_user_unread
    ON notifications(user_id, is_read)
    WHERE is_read = false;

-- Boosts expiry — used by hourly ExpireBoostJob and BoostExpiringWarningJob
CREATE INDEX IF NOT EXISTS idx_boosts_expiry
    ON boosts(status, expires_at)
    WHERE status = 'active';

-- Subscriptions expiry — used by daily ExpireSubscriptionJob and SubscriptionExpiringWarningJob
CREATE INDEX IF NOT EXISTS idx_subscriptions_expiry
    ON subscriptions(status, expires_at)
    WHERE status = 'active';
