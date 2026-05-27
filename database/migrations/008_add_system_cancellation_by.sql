-- Add 'system' value to cancellation_by enum for auto-cancellation by background jobs
ALTER TYPE cancellation_by ADD VALUE IF NOT EXISTS 'system';
