-- Fix recalculate_all_difficulties: Supabase requires UPDATE ... WHERE.
-- Thresholds match Dashboard UI: Easy >= 65%, Medium 35-65%, Hard < 35%.
-- Skips rows with ManualOverride = true.

CREATE OR REPLACE FUNCTION public.recalculate_all_difficulties()
RETURNS integer
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
  updated_count integer;
BEGIN
  UPDATE question_difficulties
  SET "SuccessRate" = CASE
      WHEN "TotalAttempts" > 0 THEN
        ROUND(("CorrectAttempts"::numeric / "TotalAttempts"::numeric) * 100, 2)
      ELSE 0
    END
  WHERE "QuestionFile" IS NOT NULL;

  UPDATE question_difficulties
  SET
    "Difficulty" = CASE
      WHEN "SuccessRate" >= 65 THEN 'easy'
      WHEN "SuccessRate" >= 35 THEN 'medium'
      ELSE 'hard'
    END,
    "LastUpdated" = now()
  WHERE "QuestionFile" IS NOT NULL
    AND COALESCE("ManualOverride", false) = false
    AND "TotalAttempts" > 0;

  GET DIAGNOSTICS updated_count = ROW_COUNT;
  RETURN updated_count;
END;
$$;

GRANT EXECUTE ON FUNCTION public.recalculate_all_difficulties() TO anon, authenticated, service_role;
