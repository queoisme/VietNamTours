-- Migration 005: Add tour_type to distinguish private vs group tours

CREATE TYPE tour_type AS ENUM ('private', 'group');

ALTER TABLE tours
    ADD COLUMN IF NOT EXISTS tour_type tour_type NOT NULL DEFAULT 'group';

CREATE INDEX IF NOT EXISTS idx_tours_type ON tours(tour_type) WHERE deleted_at IS NULL;
