-- Query performance indexes for frequently filtered columns.

CREATE INDEX IF NOT EXISTS test_sessions_token_idx
    ON test_sessions ("Token");

CREATE INDEX IF NOT EXISTS test_sessions_status_updated_idx
    ON test_sessions ("Status", "UpdatedAt" DESC);

CREATE INDEX IF NOT EXISTS users_last_seen_idx
    ON users ("LastSeen" DESC);

CREATE INDEX IF NOT EXISTS question_difficulties_difficulty_idx
    ON question_difficulties ("Difficulty");

-- Lightweight metadata for dashboard list queries (avoid pulling JSON blobs).
ALTER TABLE test_sessions ADD COLUMN IF NOT EXISTS "QuestionCount" int DEFAULT 0;
ALTER TABLE test_sessions ADD COLUMN IF NOT EXISTS "QuestionsStoragePath" text DEFAULT '';
ALTER TABLE test_sessions ADD COLUMN IF NOT EXISTS "AnswersStoragePath" text DEFAULT '';
