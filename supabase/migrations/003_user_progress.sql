-- Persist per-user progress in Supabase (local progress/*.json is gitignored).
-- App writes via service_role; RLS blocks direct client access.

ALTER TABLE users ADD COLUMN IF NOT EXISTS "DailyCorrect" int DEFAULT 0;
ALTER TABLE users ADD COLUMN IF NOT EXISTS "DayKey" text DEFAULT '';

CREATE TABLE IF NOT EXISTS user_progress (
    "Username" text PRIMARY KEY REFERENCES users ("Username") ON DELETE CASCADE,
    "ProgressData" jsonb NOT NULL DEFAULT '{}'::jsonb,
    "UpdatedAt" timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS user_progress_updated_at_idx
    ON user_progress ("UpdatedAt" DESC);

ALTER TABLE user_progress ENABLE ROW LEVEL SECURITY;

-- user_achievements: ensure RLS (no client policies — server-only via service_role)
ALTER TABLE user_achievements ENABLE ROW LEVEL SECURITY;
