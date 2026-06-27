-- Single source of truth for leaderboard / gamification counters (replaces duplicate columns in users + ProgressData JSON).

CREATE TABLE IF NOT EXISTS user_stats (
    "Username" text PRIMARY KEY REFERENCES users ("Username") ON DELETE CASCADE,
    "Xp" int NOT NULL DEFAULT 0,
    "Level" int NOT NULL DEFAULT 1,
    "WeeklyCorrect" int NOT NULL DEFAULT 0,
    "WeekKey" text NOT NULL DEFAULT '',
    "DailyCorrect" int NOT NULL DEFAULT 0,
    "DayKey" text NOT NULL DEFAULT '',
    "DailyChallengeScore" int NOT NULL DEFAULT 0,
    "DailyChallengeDate" text NOT NULL DEFAULT '',
    "BestExamScore" int NOT NULL DEFAULT 0,
    "BestExamCorrect" int NOT NULL DEFAULT 0,
    "UpdatedAt" timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS user_stats_xp_idx ON user_stats ("Xp" DESC);
CREATE INDEX IF NOT EXISTS user_stats_weekly_idx ON user_stats ("WeekKey", "WeeklyCorrect" DESC);
CREATE INDEX IF NOT EXISTS user_stats_daily_idx ON user_stats ("DayKey", "DailyCorrect" DESC);

ALTER TABLE user_stats ENABLE ROW LEVEL SECURITY;

-- Backfill from users table (legacy location).
INSERT INTO user_stats (
    "Username", "Xp", "Level", "WeeklyCorrect", "WeekKey", "DailyCorrect", "DayKey",
    "DailyChallengeScore", "DailyChallengeDate", "BestExamScore", "BestExamCorrect"
)
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
    "WeeklyCorrect" = CASE WHEN EXCLUDED."WeekKey" = user_stats."WeekKey"
        THEN GREATEST(user_stats."WeeklyCorrect", EXCLUDED."WeeklyCorrect")
        ELSE EXCLUDED."WeeklyCorrect" END,
    "WeekKey" = EXCLUDED."WeekKey",
    "DailyCorrect" = CASE WHEN EXCLUDED."DayKey" = user_stats."DayKey"
        THEN GREATEST(user_stats."DailyCorrect", EXCLUDED."DailyCorrect")
        ELSE EXCLUDED."DailyCorrect" END,
    "DayKey" = EXCLUDED."DayKey",
    "DailyChallengeScore" = GREATEST(user_stats."DailyChallengeScore", EXCLUDED."DailyChallengeScore"),
    "DailyChallengeDate" = EXCLUDED."DailyChallengeDate",
    "BestExamScore" = GREATEST(user_stats."BestExamScore", EXCLUDED."BestExamScore"),
    "BestExamCorrect" = GREATEST(user_stats."BestExamCorrect", EXCLUDED."BestExamCorrect"),
    "UpdatedAt" = now();

-- Backfill from user_progress JSON where stats are higher.
INSERT INTO user_stats ("Username", "Xp", "Level", "WeeklyCorrect", "WeekKey", "DailyCorrect", "DayKey",
    "DailyChallengeScore", "DailyChallengeDate", "BestExamScore", "BestExamCorrect")
SELECT
    p."Username",
    COALESCE((p."ProgressData"->>'Xp')::int, 0),
    COALESCE((p."ProgressData"->>'Level')::int, 1),
    COALESCE((p."ProgressData"->>'WeeklyCorrect')::int, 0),
    COALESCE(p."ProgressData"->>'WeekKey', ''),
    COALESCE((p."ProgressData"->>'DailyCorrect')::int, 0),
    COALESCE(p."ProgressData"->>'DayKey', ''),
    COALESCE((p."ProgressData"->>'DailyChallengeScore')::int, 0),
    COALESCE(p."ProgressData"->>'DailyChallengeDate', ''),
    COALESCE((p."ProgressData"->>'BestExamScore')::int, 0),
    COALESCE((p."ProgressData"->>'BestExamCorrect')::int, 0)
FROM user_progress p
WHERE p."ProgressData" IS NOT NULL AND p."ProgressData" <> '{}'::jsonb
ON CONFLICT ("Username") DO UPDATE SET
    "Xp" = GREATEST(user_stats."Xp", EXCLUDED."Xp"),
    "Level" = GREATEST(user_stats."Level", EXCLUDED."Level"),
    "WeeklyCorrect" = CASE WHEN EXCLUDED."WeekKey" <> '' AND EXCLUDED."WeekKey" = user_stats."WeekKey"
        THEN GREATEST(user_stats."WeeklyCorrect", EXCLUDED."WeeklyCorrect")
        WHEN EXCLUDED."WeeklyCorrect" > user_stats."WeeklyCorrect" THEN EXCLUDED."WeeklyCorrect"
        ELSE user_stats."WeeklyCorrect" END,
    "WeekKey" = CASE WHEN EXCLUDED."WeekKey" <> '' THEN EXCLUDED."WeekKey" ELSE user_stats."WeekKey" END,
    "DailyCorrect" = CASE WHEN EXCLUDED."DayKey" <> '' AND EXCLUDED."DayKey" = user_stats."DayKey"
        THEN GREATEST(user_stats."DailyCorrect", EXCLUDED."DailyCorrect")
        WHEN EXCLUDED."DailyCorrect" > user_stats."DailyCorrect" THEN EXCLUDED."DailyCorrect"
        ELSE user_stats."DailyCorrect" END,
    "DayKey" = CASE WHEN EXCLUDED."DayKey" <> '' THEN EXCLUDED."DayKey" ELSE user_stats."DayKey" END,
    "DailyChallengeScore" = GREATEST(user_stats."DailyChallengeScore", EXCLUDED."DailyChallengeScore"),
    "DailyChallengeDate" = CASE WHEN EXCLUDED."DailyChallengeDate" <> '' THEN EXCLUDED."DailyChallengeDate" ELSE user_stats."DailyChallengeDate" END,
    "BestExamScore" = GREATEST(user_stats."BestExamScore", EXCLUDED."BestExamScore"),
    "BestExamCorrect" = GREATEST(user_stats."BestExamCorrect", EXCLUDED."BestExamCorrect"),
    "UpdatedAt" = now();
