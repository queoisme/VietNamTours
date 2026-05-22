-- Migration 003: subscription_plan_configs
-- Lưu cấu hình giá các gói subscription trong DB thay vì hardcode trong code

CREATE TABLE IF NOT EXISTS subscription_plan_configs (
    plan        VARCHAR(20) PRIMARY KEY,         -- 'premium' | 'pro'
    price       NUMERIC(15,2) NOT NULL CHECK (price > 0),
    days        SMALLINT NOT NULL CHECK (days > 0),
    description TEXT NOT NULL DEFAULT '',
    is_active   BOOLEAN NOT NULL DEFAULT true,
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

INSERT INTO subscription_plan_configs (plan, price, days, description) VALUES
    ('premium', 299000, 30,  '10% commission, unlimited tours'),
    ('pro',     799000, 90,  '8% commission, unlimited tours, priority support')
ON CONFLICT (plan) DO NOTHING;
