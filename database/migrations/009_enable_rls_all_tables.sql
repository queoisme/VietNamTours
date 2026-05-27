-- Enable RLS on all tables that are currently unprotected.
-- Backend connects as postgres superuser → bypasses RLS automatically.
-- Policies below cover only direct Supabase client access (Realtime + public reads).

-- ── 1. conversations ─────────────────────────────────────────────────────────
-- Frontend subscribes via Supabase Realtime; only members should see their convos.
ALTER TABLE public.conversations ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS conversations_select ON public.conversations;
CREATE POLICY conversations_select ON public.conversations
  FOR SELECT USING (
    customer_id = auth.uid() OR guide_id = auth.uid()
  );

-- ── 2. messages ───────────────────────────────────────────────────────────────
-- Realtime subscriptions in Chat.tsx; only conversation members may read/insert.
ALTER TABLE public.messages ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS messages_select ON public.messages;
CREATE POLICY messages_select ON public.messages
  FOR SELECT USING (
    EXISTS (
      SELECT 1 FROM public.conversations c
      WHERE c.id = messages.conversation_id
        AND (c.customer_id = auth.uid() OR c.guide_id = auth.uid())
    )
  );

DROP POLICY IF EXISTS messages_insert ON public.messages;
CREATE POLICY messages_insert ON public.messages
  FOR INSERT WITH CHECK (
    sender_id = auth.uid()
    AND EXISTS (
      SELECT 1 FROM public.conversations c
      WHERE c.id = messages.conversation_id
        AND (c.customer_id = auth.uid() OR c.guide_id = auth.uid())
    )
  );

-- ── 3. otp_verifications ──────────────────────────────────────────────────────
-- Backend-only table; no direct client access needed.
ALTER TABLE public.otp_verifications ENABLE ROW LEVEL SECURITY;

-- ── 4. subscription_plan_configs ──────────────────────────────────────────────
-- Public pricing info; anyone (anon or authenticated) may read.
ALTER TABLE public.subscription_plan_configs ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS subscription_plan_configs_select ON public.subscription_plan_configs;
CREATE POLICY subscription_plan_configs_select ON public.subscription_plan_configs
  FOR SELECT USING (true);

-- ── 5. boost_plan_configs ─────────────────────────────────────────────────────
-- Public pricing info; anyone may read.
ALTER TABLE public.boost_plan_configs ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS boost_plan_configs_select ON public.boost_plan_configs;
CREATE POLICY boost_plan_configs_select ON public.boost_plan_configs
  FOR SELECT USING (true);

-- ── 6. support_conversations ─────────────────────────────────────────────────
-- Only the ticket owner (user_id) or an admin may see a support conversation.
ALTER TABLE public.support_conversations ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS support_conversations_select ON public.support_conversations;
CREATE POLICY support_conversations_select ON public.support_conversations
  FOR SELECT USING (
    user_id = auth.uid()
    OR EXISTS (
      SELECT 1 FROM public.users
      WHERE id = auth.uid() AND role = 'admin'
    )
  );

-- ── 7. support_messages ───────────────────────────────────────────────────────
-- Realtime subscriptions in SupportChat.tsx / AdminSupport.tsx.
-- Only the conversation owner or an admin may read messages.
ALTER TABLE public.support_messages ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS support_messages_select ON public.support_messages;
CREATE POLICY support_messages_select ON public.support_messages
  FOR SELECT USING (
    EXISTS (
      SELECT 1 FROM public.support_conversations sc
      WHERE sc.id = support_messages.support_conversation_id
        AND (sc.user_id = auth.uid()
             OR EXISTS (
               SELECT 1 FROM public.users
               WHERE id = auth.uid() AND role = 'admin'
             ))
    )
  );

-- Ensure support_messages receives Realtime CDC events
ALTER PUBLICATION supabase_realtime ADD TABLE public.support_messages;
