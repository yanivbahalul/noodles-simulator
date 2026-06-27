-- Data optimization: FK cascades, slim ProgressData, drop duplicate gamification columns from users.

-- Orphan cleanup before FK constraints.
DELETE FROM user_achievements a
WHERE NOT EXISTS (SELECT 1 FROM users u WHERE u."Username" = a.username);

DELETE FROM activity_events e
WHERE NOT EXISTS (SELECT 1 FROM users u WHERE u."Username" = e.username);

ALTER TABLE user_achievements DROP CONSTRAINT IF EXISTS user_achievements_username_fkey;
ALTER TABLE user_achievements
    ADD CONSTRAINT user_achievements_username_fkey
    FOREIGN KEY (username) REFERENCES users ("Username") ON DELETE CASCADE;

ALTER TABLE activity_events DROP CONSTRAINT IF EXISTS activity_events_username_fkey;
ALTER TABLE activity_events
    ADD CONSTRAINT activity_events_username_fkey
    FOREIGN KEY (username) REFERENCES users ("Username") ON DELETE CASCADE;

-- Strip legacy keys already normalized into dedicated tables.
UPDATE user_progress
SET "ProgressData" = "ProgressData"
    - 'QuestionStats'
    - 'Xp'
    - 'Level'
    - 'WeeklyCorrect'
    - 'WeekKey'
    - 'DailyCorrect'
    - 'DayKey'
    - 'DailyChallengeScore'
    - 'DailyChallengeDate'
    - 'BestExamScore'
    - 'BestExamCorrect'
    - 'Achievements'
WHERE "ProgressData" ?| ARRAY[
    'QuestionStats', 'Xp', 'Level', 'WeeklyCorrect', 'WeekKey',
    'DailyCorrect', 'DayKey', 'DailyChallengeScore', 'DailyChallengeDate',
    'BestExamScore', 'BestExamCorrect', 'Achievements'
];

-- Auto-create user_stats row when a user registers.
CREATE OR REPLACE FUNCTION ensure_user_stats_row()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO user_stats ("Username")
    VALUES (NEW."Username")
    ON CONFLICT ("Username") DO NOTHING;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS users_ensure_stats ON users;
CREATE TRIGGER users_ensure_stats
    AFTER INSERT ON users
    FOR EACH ROW
    EXECUTE FUNCTION ensure_user_stats_row();

-- Ensure every existing user has a stats row before dropping legacy columns.
INSERT INTO user_stats ("Username", "Xp", "Level", "WeeklyCorrect", "WeekKey", "DailyCorrect", "DayKey",
    "DailyChallengeScore", "DailyChallengeDate", "BestExamScore", "BestExamCorrect")
SELECT
    "Username",
    COALESCE("Xp", 0),
    COALESCE("Level", 1),
    COALESCE("WeeklyCorrect", 0),
    COALESCE("WeekKey", ''),
    COALESCE("DailyCorrect", 0),
    COALESCE("DayKey", ''),
    COALESCE("DailyChallengeScore", 0),
    COALESCE("DailyChallengeDate", ''),
    COALESCE("BestExamScore", 0),
    COALESCE("BestExamCorrect", 0)
FROM users
ON CONFLICT ("Username") DO UPDATE SET
    "Xp" = GREATEST(user_stats."Xp", EXCLUDED."Xp"),
    "Level" = GREATEST(user_stats."Level", EXCLUDED."Level"),
    "WeeklyCorrect" = GREATEST(user_stats."WeeklyCorrect", EXCLUDED."WeeklyCorrect"),
    "WeekKey" = CASE WHEN EXCLUDED."WeekKey" <> '' THEN EXCLUDED."WeekKey" ELSE user_stats."WeekKey" END,
    "DailyCorrect" = GREATEST(user_stats."DailyCorrect", EXCLUDED."DailyCorrect"),
    "DayKey" = CASE WHEN EXCLUDED."DayKey" <> '' THEN EXCLUDED."DayKey" ELSE user_stats."DayKey" END,
    "DailyChallengeScore" = GREATEST(user_stats."DailyChallengeScore", EXCLUDED."DailyChallengeScore"),
    "DailyChallengeDate" = CASE WHEN EXCLUDED."DailyChallengeDate" <> '' THEN EXCLUDED."DailyChallengeDate" ELSE user_stats."DailyChallengeDate" END,
    "BestExamScore" = GREATEST(user_stats."BestExamScore", EXCLUDED."BestExamScore"),
    "BestExamCorrect" = GREATEST(user_stats."BestExamCorrect", EXCLUDED."BestExamCorrect"),
    "UpdatedAt" = now();

ALTER TABLE users DROP COLUMN IF EXISTS "Xp";
ALTER TABLE users DROP COLUMN IF EXISTS "Level";
ALTER TABLE users DROP COLUMN IF EXISTS "WeeklyCorrect";
ALTER TABLE users DROP COLUMN IF EXISTS "WeekKey";
ALTER TABLE users DROP COLUMN IF EXISTS "DailyCorrect";
ALTER TABLE users DROP COLUMN IF EXISTS "DayKey";
ALTER TABLE users DROP COLUMN IF EXISTS "DailyChallengeScore";
ALTER TABLE users DROP COLUMN IF EXISTS "DailyChallengeDate";
ALTER TABLE users DROP COLUMN IF EXISTS "BestExamScore";
ALTER TABLE users DROP COLUMN IF EXISTS "BestExamCorrect";

CREATE INDEX IF NOT EXISTS test_sessions_username_idx
    ON test_sessions ("Username");

CREATE INDEX IF NOT EXISTS activity_events_created_at_purge_idx
    ON activity_events (created_at);
