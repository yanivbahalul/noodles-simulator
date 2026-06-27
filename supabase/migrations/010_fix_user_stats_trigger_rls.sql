-- Trigger must bypass RLS when auto-creating user_stats (e.g. if users insert uses anon key).
CREATE OR REPLACE FUNCTION ensure_user_stats_row()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    INSERT INTO user_stats ("Username")
    VALUES (NEW."Username")
    ON CONFLICT ("Username") DO NOTHING;
    RETURN NEW;
END;
$$;
