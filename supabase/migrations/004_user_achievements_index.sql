-- Faster achievement lookups per user; server writes via service_role only.

CREATE INDEX IF NOT EXISTS user_achievements_username_idx
    ON user_achievements (username);

-- Ensure upsert on achievement unlock is unambiguous
CREATE UNIQUE INDEX IF NOT EXISTS user_achievements_username_key_idx
    ON user_achievements (username, achievement_key);
