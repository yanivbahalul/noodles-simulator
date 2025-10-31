-- Create question_difficulties table for dynamic difficulty management
-- This table stores difficulty levels based on user performance statistics

CREATE TABLE IF NOT EXISTS question_difficulties (
    "QuestionFile" VARCHAR(500) PRIMARY KEY,
    "Difficulty" VARCHAR(20) NOT NULL DEFAULT 'medium',
    "SuccessRate" DECIMAL(5,2) DEFAULT 0.00,
    "TotalAttempts" INTEGER DEFAULT 0,
    "CorrectAttempts" INTEGER DEFAULT 0,
    "LastUpdated" TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    "ManualOverride" BOOLEAN DEFAULT FALSE,
    "CreatedAt" TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Create indexes for better performance
CREATE INDEX IF NOT EXISTS idx_question_difficulties_difficulty ON question_difficulties("Difficulty");
CREATE INDEX IF NOT EXISTS idx_question_difficulties_success_rate ON question_difficulties("SuccessRate" DESC);
CREATE INDEX IF NOT EXISTS idx_question_difficulties_updated ON question_difficulties("LastUpdated" DESC);

-- Function to auto-update difficulty based on success rate
-- Easy ≥ 65% · Medium 35–65% · Hard < 35%
CREATE OR REPLACE FUNCTION update_question_difficulty()
RETURNS TRIGGER AS $$
BEGIN
    -- Only update if not manually overridden
    IF NEW."ManualOverride" = FALSE THEN
        IF NEW."SuccessRate" >= 65.0 THEN
            NEW."Difficulty" = 'easy';
        ELSIF NEW."SuccessRate" >= 35.0 THEN
            NEW."Difficulty" = 'medium';
        ELSE
            NEW."Difficulty" = 'hard';
        END IF;
    END IF;
    
    NEW."LastUpdated" = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Trigger to auto-update difficulty when success rate changes
DROP TRIGGER IF EXISTS auto_update_difficulty ON question_difficulties;
CREATE TRIGGER auto_update_difficulty
    BEFORE INSERT OR UPDATE OF "SuccessRate", "TotalAttempts", "CorrectAttempts"
    ON question_difficulties
    FOR EACH ROW
    EXECUTE FUNCTION update_question_difficulty();

-- Grant permissions
GRANT ALL ON question_difficulties TO service_role;
GRANT SELECT ON question_difficulties TO anon;
GRANT SELECT ON question_difficulties TO authenticated;

-- Create a view for easy querying by difficulty
CREATE OR REPLACE VIEW vw_questions_by_difficulty AS
SELECT 
    "Difficulty",
    COUNT(*) as "QuestionCount",
    AVG("SuccessRate") as "AverageSuccessRate",
    SUM("TotalAttempts") as "TotalAttempts"
FROM question_difficulties
GROUP BY "Difficulty";

GRANT SELECT ON vw_questions_by_difficulty TO service_role;
GRANT SELECT ON vw_questions_by_difficulty TO anon;
GRANT SELECT ON vw_questions_by_difficulty TO authenticated;

-- Function to recalculate success rate
CREATE OR REPLACE FUNCTION recalculate_success_rate(question_file VARCHAR)
RETURNS VOID AS $$
BEGIN
    UPDATE question_difficulties
    SET "SuccessRate" = 
        CASE 
            WHEN "TotalAttempts" > 0 THEN 
                ROUND(("CorrectAttempts"::DECIMAL / "TotalAttempts"::DECIMAL) * 100, 2)
            ELSE 0.00
        END
    WHERE "QuestionFile" = question_file;
END;
$$ LANGUAGE plpgsql;

-- Bulk recalculate all success rates
CREATE OR REPLACE FUNCTION recalculate_all_difficulties()
RETURNS INTEGER AS $$
DECLARE
    updated_count INTEGER;
BEGIN
    UPDATE question_difficulties
    SET "SuccessRate" = 
        CASE 
            WHEN "TotalAttempts" > 0 THEN 
                ROUND(("CorrectAttempts"::DECIMAL / "TotalAttempts"::DECIMAL) * 100, 2)
            ELSE 0.00
        END;
    
    GET DIAGNOSTICS updated_count = ROW_COUNT;
    RETURN updated_count;
END;
$$ LANGUAGE plpgsql;

