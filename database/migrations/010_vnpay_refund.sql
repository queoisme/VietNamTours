-- Migration 010: VNPay refund support.
-- Adds vnpay_transaction_no column (required to call VNPay Refund API)
-- and refund_failed value to payment_status enum for tracking failed refund attempts.

ALTER TABLE bookings
    ADD COLUMN IF NOT EXISTS vnpay_transaction_no VARCHAR(50);

ALTER TYPE payment_status ADD VALUE IF NOT EXISTS 'refund_failed';
