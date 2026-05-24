-- Migration 004: Support pre-booking chat (customer → guide from tour detail page)
-- Allows conversations to exist without a booking by making booking_id nullable
-- and adding tour_id as the primary context reference.

-- Step 1: Add tour_id column (nullable first for backfill)
ALTER TABLE conversations ADD COLUMN IF NOT EXISTS tour_id UUID REFERENCES tours(id) ON DELETE CASCADE;

-- Step 2: Backfill tour_id from existing booking conversations
UPDATE conversations c SET tour_id = b.tour_id FROM bookings b WHERE c.booking_id = b.id AND c.tour_id IS NULL;

-- Step 3: Make tour_id NOT NULL (all existing rows now have it)
ALTER TABLE conversations ALTER COLUMN tour_id SET NOT NULL;

-- Step 4: Drop NOT NULL from booking_id (allows pre-booking conversations)
ALTER TABLE conversations ALTER COLUMN booking_id DROP NOT NULL;

-- Step 5: Drop old UNIQUE constraint on booking_id
ALTER TABLE conversations DROP CONSTRAINT IF EXISTS conversations_booking_id_key;

-- Step 6: Partial unique index: one conversation per booking
CREATE UNIQUE INDEX IF NOT EXISTS idx_conversations_booking_id ON conversations(booking_id) WHERE booking_id IS NOT NULL;

-- Step 7: Partial unique index: one pre-booking conversation per (customer, tour)
CREATE UNIQUE INDEX IF NOT EXISTS idx_conversations_inquiry ON conversations(customer_id, tour_id) WHERE booking_id IS NULL;
