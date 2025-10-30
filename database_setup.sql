-- Create test_sessions table in Supabase
-- This table stores all test sessions with token-based access

CREATE TABLE IF NOT EXISTS test_sessions (
    Token VARCHAR(255) PRIMARY KEY,
    Username VARCHAR(255) NOT NULL,
    StartedUtc TIMESTAMP WITH TIME ZONE NOT NULL,
    CompletedUtc TIMESTAMP WITH TIME ZONE,
    Status VARCHAR(50) NOT NULL DEFAULT 'active',
    QuestionsJson TEXT NOT NULL,
    AnswersJson TEXT NOT NULL DEFAULT '[]',
    CurrentIndex INTEGER NOT NULL DEFAULT 0,
    Score INTEGER NOT NULL DEFAULT 0,
    MaxScore INTEGER NOT NULL DEFAULT 0,
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    UpdatedAt TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Create indexes for better performance
CREATE INDEX IF NOT EXISTS idx_test_sessions_username ON test_sessions(Username);
CREATE INDEX IF NOT EXISTS idx_test_sessions_status ON test_sessions(Status);
CREATE INDEX IF NOT EXISTS idx_test_sessions_started ON test_sessions(StartedUtc DESC);
CREATE INDEX IF NOT EXISTS idx_test_sessions_username_status ON test_sessions(Username, Status);

-- Add RLS (Row Level Security) policies if needed
-- ALTER TABLE test_sessions ENABLE ROW LEVEL SECURITY;

-- Policy to allow users to view only their own sessions
-- CREATE POLICY "Users can view own sessions"
--   ON test_sessions
--   FOR SELECT
--   USING (auth.uid()::text = Username);

-- Policy to allow service role to do everything (for your backend)
-- CREATE POLICY "Service role full access"
--   ON test_sessions
--   FOR ALL
--   USING (auth.role() = 'service_role');

-- Create a function to auto-update UpdatedAt timestamp
CREATE OR REPLACE FUNCTION update_test_sessions_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.UpdatedAt = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update UpdatedAt
DROP TRIGGER IF EXISTS test_sessions_updated_at ON test_sessions;
CREATE TRIGGER test_sessions_updated_at
    BEFORE UPDATE ON test_sessions
    FOR EACH ROW
    EXECUTE FUNCTION update_test_sessions_updated_at();

-- Grant permissions to service role
GRANT ALL ON test_sessions TO service_role;
GRANT SELECT, INSERT, UPDATE ON test_sessions TO anon;
GRANT SELECT, INSERT, UPDATE ON test_sessions TO authenticated;

