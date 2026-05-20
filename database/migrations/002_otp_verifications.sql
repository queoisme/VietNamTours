-- Migration: 002_otp_verifications
-- Bảng lưu OTP do backend tự sinh (email registration, password reset, phone verification)

CREATE TABLE IF NOT EXISTS public.otp_verifications (
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    target      VARCHAR(255) NOT NULL,
    type        VARCHAR(30)  NOT NULL,   -- email_registration | password_reset | phone_verification
    code        VARCHAR(6)   NOT NULL,
    expires_at  TIMESTAMPTZ  NOT NULL,
    is_used     BOOLEAN      NOT NULL DEFAULT FALSE,
    attempts    SMALLINT     NOT NULL DEFAULT 0,
    ip_address  VARCHAR(50),
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_otp_target
    ON public.otp_verifications (target, type)
    WHERE is_used = FALSE;
