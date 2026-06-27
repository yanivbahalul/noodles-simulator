-- Per-question stats (extracted from unbounded ProgressData.QuestionStats JSON map).

CREATE TABLE IF NOT EXISTS user_question_stats (
    username text NOT NULL REFERENCES users ("Username") ON DELETE CASCADE,
    question_id text NOT NULL,
    attempts int NOT NULL DEFAULT 0,
    correct int NOT NULL DEFAULT 0,
    last_answered_utc timestamptz,
    last_was_correct bool NOT NULL DEFAULT false,
    PRIMARY KEY (username, question_id)
);

CREATE INDEX IF NOT EXISTS user_question_stats_username_idx
    ON user_question_stats (username);

CREATE INDEX IF NOT EXISTS user_question_stats_last_answered_idx
    ON user_question_stats (username, last_answered_utc DESC);

ALTER TABLE user_question_stats ENABLE ROW LEVEL SECURITY;

-- Backfill from ProgressData.QuestionStats JSON.
INSERT INTO user_question_stats (username, question_id, attempts, correct, last_answered_utc, last_was_correct)
SELECT
    p."Username",
    kv.key AS question_id,
    COALESCE((kv.value->>'Attempts')::int, 0),
    COALESCE((kv.value->>'Correct')::int, 0),
    CASE
        WHEN kv.value->>'LastAnsweredUtc' IS NOT NULL AND kv.value->>'LastAnsweredUtc' <> ''
        THEN (kv.value->>'LastAnsweredUtc')::timestamptz
        ELSE NULL
    END,
    COALESCE((kv.value->>'LastWasCorrect')::boolean, false)
FROM user_progress p
CROSS JOIN LATERAL jsonb_each(p."ProgressData"->'QuestionStats') AS kv(key, value)
WHERE p."ProgressData" ? 'QuestionStats'
ON CONFLICT (username, question_id) DO UPDATE SET
    attempts = GREATEST(user_question_stats.attempts, EXCLUDED.attempts),
    correct = GREATEST(user_question_stats.correct, EXCLUDED.correct),
    last_answered_utc = CASE
        WHEN EXCLUDED.last_answered_utc IS NULL THEN user_question_stats.last_answered_utc
        WHEN user_question_stats.last_answered_utc IS NULL THEN EXCLUDED.last_answered_utc
        WHEN EXCLUDED.last_answered_utc > user_question_stats.last_answered_utc THEN EXCLUDED.last_answered_utc
        ELSE user_question_stats.last_answered_utc END,
    last_was_correct = CASE
        WHEN EXCLUDED.last_answered_utc IS NOT NULL AND (
            user_question_stats.last_answered_utc IS NULL OR EXCLUDED.last_answered_utc >= user_question_stats.last_answered_utc
        ) THEN EXCLUDED.last_was_correct
        ELSE user_question_stats.last_was_correct END;
