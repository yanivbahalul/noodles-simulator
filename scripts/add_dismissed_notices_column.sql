-- Run once in Supabase SQL editor before deploying the exam-fix notice.
ALTER TABLE users
ADD COLUMN IF NOT EXISTS "DismissedNotices" jsonb NOT NULL DEFAULT '[]'::jsonb;
