-- Migration 011: home_category_displays
-- Cho phép admin quản lý các danh mục hiển thị trên trang chủ.
-- category_filter tham chiếu enum tour_category đã có sẵn.

CREATE TABLE IF NOT EXISTS home_category_displays (
    id              SERIAL PRIMARY KEY,
    name            VARCHAR(100)  NOT NULL,
    description     TEXT          NOT NULL DEFAULT '',
    category_filter tour_category NOT NULL,
    is_visible      BOOLEAN       NOT NULL DEFAULT true,
    sort_order      SMALLINT      NOT NULL DEFAULT 0,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ   NOT NULL DEFAULT now()
);

-- Seed: 4 danh mục hiện đang hardcode trong Home.tsx
INSERT INTO home_category_displays (name, description, category_filter, is_visible, sort_order) VALUES
    ('Thiên nhiên', 'Núi rừng, biển đảo',  'nature',  true, 1),
    ('Văn hóa',     'Di tích, lễ hội',      'culture', true, 2),
    ('Ẩm thực',     'Ẩm thực địa phương',   'food',    true, 3),
    ('Nghỉ dưỡng',  'Resort, spa',           'resort',  true, 4)
ON CONFLICT DO NOTHING;

ALTER TABLE public.home_category_displays ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS home_category_displays_select ON public.home_category_displays;
CREATE POLICY home_category_displays_select ON public.home_category_displays
    FOR SELECT USING (true);
