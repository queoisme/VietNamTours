-- Migration 006: Track which booking caused an availability date to be blocked.
-- Used to auto-block consecutive dates for multi-day private tours, and to
-- precisely unblock them when the booking is cancelled or rejected.

ALTER TABLE tour_availabilities
    ADD COLUMN IF NOT EXISTS blocked_by_booking_id UUID REFERENCES bookings(id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS idx_avail_blocked_by_booking
    ON tour_availabilities(blocked_by_booking_id)
    WHERE blocked_by_booking_id IS NOT NULL;
