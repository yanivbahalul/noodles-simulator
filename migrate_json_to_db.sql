-- Migration script to populate question_difficulties from JSON files
-- Run this ONCE after creating the question_difficulties table

-- This is a template - you'll need to insert the actual data from your JSON files

-- Easy questions (189 questions with default 70% success rate)
INSERT INTO question_difficulties ("QuestionFile", "Difficulty", "SuccessRate", "TotalAttempts", "CorrectAttempts", "ManualOverride")
VALUES
  ('Screenshot at Apr 15 20-17-21.png', 'easy', 70.00, 0, 0, true),
  ('Screenshot at Apr 15 22-41-36.png', 'easy', 70.00, 0, 0, true),
  ('Screenshot at Apr 20 16-23-24.png', 'easy', 70.00, 0, 0, true)
  -- Add all easy questions here...
ON CONFLICT ("QuestionFile") DO NOTHING;

-- Medium questions (52 questions with default 50% success rate)
INSERT INTO question_difficulties ("QuestionFile", "Difficulty", "SuccessRate", "TotalAttempts", "CorrectAttempts", "ManualOverride")
VALUES
  ('Screenshot at Apr 15 20-30-36.png', 'medium', 50.00, 0, 0, true),
  ('Screenshot at Apr 20 17-41-25.png', 'medium', 50.00, 0, 0, true)
  -- Add all medium questions here...
ON CONFLICT ("QuestionFile") DO NOTHING;

-- Hard questions (73 questions with default 30% success rate)
INSERT INTO question_difficulties ("QuestionFile", "Difficulty", "SuccessRate", "TotalAttempts", "CorrectAttempts", "ManualOverride")
VALUES
  ('Screenshot at Aug 26 13-46-17.png', 'hard', 30.00, 0, 0, true),
  ('Screenshot at Apr 15 21-50-39.png', 'hard', 30.00, 0, 0, true)
  -- Add all hard questions here...
ON CONFLICT ("QuestionFile") DO NOTHING;

-- Note: ManualOverride is set to true so the auto-update won't change these
-- until they have real user data (TotalAttempts > 0)

