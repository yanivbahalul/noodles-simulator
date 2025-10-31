-- ============================================================
-- Update question_difficulties table with new statistics
-- ============================================================
-- This script updates TotalAttempts, CorrectAttempts, and SuccessRate
-- The trigger will automatically update Difficulty based on SuccessRate
-- 
-- Classification Rules:
--   - Easy:   Success Rate ≥ 65%
--   - Medium: Success Rate 35-65%
--   - Hard:   Success Rate < 35%
--
-- Total questions to update: 315
--   - Hard questions: 74
--   - Medium questions: 53
--   - Easy questions: 188
--
-- Usage: Run this script in Supabase SQL Editor
-- ============================================================

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 26 13-46-17.png', 4, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-50-39.png', 4, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-24-33.png', 4, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Jul 20 11-41-16.png', 3, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-30-16.png', 3, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 18 19-44-48.png', 3, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-25-17.png', 3, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-59-18.png', 3, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 16-21-11.png', 3, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-38-27.png', 3, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-08-31.png', 3, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Sep 30 16-53-26.png', 3, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-00-45.png', 3, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-52-48.png', 3, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-29-36.png', 3, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 26 13-43-08.png', 3, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-25-33.png', 2, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 16-22-22.png', 2, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-31-15.png', 2, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 26 13-47-02.png', 2, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 17-39-20.png', 2, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-30-39.png', 2, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 26 13-54-50.png', 2, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 18 19-53-05.png', 2, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 16-25-48.png', 2, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 18 19-56-57.png', 2, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-58-29.png', 1, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-08-55.png', 1, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 16-32-00.png', 1, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-27-38.png', 1, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-37-49.png', 1, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-29-29.png', 1, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-21-12.png', 1, 0, 0.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-58-42.png', 5, 1, 20.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Sep 30 16-54-36.png', 5, 1, 20.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Jul 20 11-34-20.png', 5, 1, 20.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 18 19-58-19.png', 4, 1, 25.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 16 00-08-24.png', 4, 1, 25.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 18 19-43-35.png', 4, 1, 25.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-46-32.png', 4, 1, 25.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-25-10.png', 4, 1, 25.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 26 13-48-53.png', 4, 1, 25.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 16 00-10-56.png', 4, 1, 25.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 16-24-08.png', 4, 1, 25.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-37-34.png', 7, 2, 28.57, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 16-28-49.png', 7, 2, 28.57, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-37-18.png', 6, 2, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-28-45.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-25-37.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-26-17.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-27-22.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 17-44-22.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-46-47.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 16-28-18.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 16 00-13-19.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-14-06.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Jul 20 11-39-11.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-38-13.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-42-18.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-27-52.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 18 19-41-44.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-41-20.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Sep 30 16-55-27.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-25-50.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-23-33.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-47-22.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-34-44.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Sep 30 16-41-29.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-59-58.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-15-13.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-40-53.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-42-10.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-59-01.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-27-55.png', 3, 1, 33.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-30-36.png', 5, 2, 40.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 17-41-25.png', 5, 2, 40.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-10-27.png', 5, 2, 40.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-51-32.png', 5, 2, 40.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-20-49.png', 5, 2, 40.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 16 00-16-08.png', 5, 2, 40.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 26 13-51-46.png', 6, 3, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-44-42.png', 6, 3, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-30-16.png', 4, 2, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-26-21.png', 4, 2, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-24-55.png', 4, 2, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-37-02.png', 4, 2, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-18-14.png', 4, 2, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-01-03.png', 4, 2, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Jul 20 11-45-32.png', 4, 2, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-31-12.png', 4, 2, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-19-30.png', 4, 2, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-25-38.png', 4, 2, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-29-04.png', 4, 2, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 18 19-42-44.png', 4, 2, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-46-15.png', 4, 2, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 18 19-55-51.png', 4, 2, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-44-09.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-09-36.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-47-34.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-59-42.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-22-30.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-49-15.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 16-32-59.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-23-02.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 16 00-11-24.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Sep 30 16-55-52.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-43-34.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Jul 20 11-36-09.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 16-27-05.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Sep 30 16-43-43.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-11-05.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 18 19-51-58.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 26 13-53-50.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-19-56.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Jul 20 11-36-47.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-53-18.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 17-45-45.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 16 00-09-59.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-58-06.png', 2, 1, 50.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-56-58.png', 5, 3, 60.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 16 00-12-53.png', 5, 3, 60.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Jul 20 11-42-47.png', 5, 3, 60.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-26-21.png', 5, 3, 60.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-29-54.png', 5, 3, 60.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-09-52.png', 5, 3, 60.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-28-24.png', 5, 3, 60.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-14-29.png', 5, 3, 60.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-17-21.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-41-36.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 16-23-24.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Sep 30 16-56-35.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-51-08.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-14-53.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 18 19-45-55.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 16-27-51.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-12-16.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 16 00-07-42.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 16-26-23.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Jul 20 11-38-17.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-16-55.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-55-16.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-13-27.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-11-57.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-45-17.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 26 13-55-39.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-39-15.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-39-27.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-38-33.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-40-42.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 26 13-42-07.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-48-51.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Jul 20 11-39-26.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-41-36.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-38-59.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-33-23.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-45-36.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-53-44.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-40-32.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-17-57.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 16 00-07-22.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Sep 30 16-40-50.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Sep 30 16-55-06.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-43-18.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 26 13-52-27.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-45-13.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-55-41.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 16 00-11-53.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 18 19-43-06.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-00-34.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Sep 30 16-44-56.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-48-06.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Jul 20 11-38-56.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-20-16.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-01-25.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-41-58.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-56-46.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 16-20-56.png', 3, 2, 66.67, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-42-46.png', 4, 3, 75.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-49-29.png', 4, 3, 75.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-46-01.png', 4, 3, 75.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-21-59.png', 4, 3, 75.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 26 13-53-12.png', 4, 3, 75.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-44-13.png', 4, 3, 75.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-11-30.png', 4, 3, 75.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 16 00-15-46.png', 4, 3, 75.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-27-25.png', 4, 3, 75.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-53-31.png', 4, 3, 75.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Jul 20 11-33-42.png', 4, 3, 75.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-22-08.png', 4, 3, 75.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Sep 30 16-53-42.png', 4, 3, 75.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-12-46.png', 4, 3, 75.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-39-11.png', 4, 3, 75.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-54-47.png', 4, 3, 75.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-44-03.png', 4, 3, 75.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-38-13.png', 4, 3, 75.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 26 13-45-06.png', 4, 3, 75.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 18 19-50-50.png', 4, 3, 75.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-28-47.png', 4, 3, 75.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 16-29-21.png', 5, 4, 80.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 16-24-44.png', 5, 4, 80.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 16 00-06-45.png', 5, 4, 80.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-33-04.png', 5, 4, 80.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 26 13-50-34.png', 5, 4, 80.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-57-36.png', 5, 4, 80.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-36-13.png', 5, 4, 80.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 26 13-49-39.png', 5, 4, 80.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-49-42.png', 5, 4, 80.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-57-53.png', 5, 4, 80.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-25-22.png', 6, 5, 83.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 18 19-47-01.png', 6, 5, 83.33, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Sep 30 16-44-03.png', 5, 5, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-55-57.png', 5, 5, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-07-49.png', 5, 5, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-08-08.png', 4, 4, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-26-53.png', 4, 4, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-27-09.png', 4, 4, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-52-26.png', 4, 4, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-22-46.png', 4, 4, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-10-08.png', 4, 4, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-50-51.png', 4, 4, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-26-07.png', 4, 4, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Sep 30 16-52-57.png', 4, 4, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-34-24.png', 4, 4, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-43-03.png', 4, 4, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-41-08.png', 4, 4, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-15-54.png', 4, 4, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-26-40.png', 4, 4, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-47-05.png', 4, 4, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-55-02.png', 4, 4, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 26 13-51-01.png', 4, 4, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 18 19-48-53.png', 4, 4, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Sep 30 16-44-28.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-29-56.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-53-09.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-52-05.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 16 00-04-56.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 16-30-09.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 16-25-19.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-38-43.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 16-36-37.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-41-55.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-33-37.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-25-53.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Sep 30 16-43-09.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-24-11.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 16-36-13.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-13-47.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-26-06.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-40-11.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-20-08.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 26 13-52-59.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-50-26.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 16-32-28.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-33-52.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-44-59.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-09-18.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-34-08.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-33-05.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 16 00-10-26.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Sep 30 16-45-23.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-39-55.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-40-36.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 18 19-53-47.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-40-12.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-51-48.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-32-28.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-58-17.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-27-06.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-43-33.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Sep 30 16-54-11.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-38-59.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-26-28.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Jul 20 11-45-12.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-01-12.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-44-29.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-30-59.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-52-39.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 17-42-39.png', 3, 3, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-36-46.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-28-27.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Jul 20 11-48-06.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-44-56.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 20-35-15.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-34-30.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-37-45.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-13-06.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-10-42.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-49-55.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-00-10.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-20-38.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-23-48.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-43-13.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-43-40.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-41-36.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-43-48.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Jul 20 11-40-27.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-01-44.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 18 19-57-40.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-55-30.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-49-02.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-24-35.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-27-37.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 18 19-56-19.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 20 16-30-41.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-53-58.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Jul 20 11-41-30.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-31-00.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 16 00-09-23.png', 2, 2, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-29-22.png', 1, 1, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-38-46.png', 1, 1, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 22-41-14.png', 1, 1, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Aug 26 13-43-59.png', 1, 1, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-42-33.png', 1, 1, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 21-24-32.png', 1, 1, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();

INSERT INTO question_difficulties ("QuestionFile", "TotalAttempts", "CorrectAttempts", "SuccessRate", "LastUpdated")
VALUES ('Screenshot at Apr 15 23-26-46.png', 1, 1, 100.00, NOW())
ON CONFLICT ("QuestionFile")
DO UPDATE SET
    "TotalAttempts" = EXCLUDED."TotalAttempts",
    "CorrectAttempts" = EXCLUDED."CorrectAttempts",
    "SuccessRate" = EXCLUDED."SuccessRate",
    "LastUpdated" = NOW();