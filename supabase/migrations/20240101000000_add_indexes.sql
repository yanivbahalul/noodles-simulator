-- Add indexes for better performance
CREATE INDEX IF NOT EXISTS idx_users_username ON public.users(username);
CREATE INDEX IF NOT EXISTS idx_users_last_seen ON public.users(last_seen);
CREATE INDEX IF NOT EXISTS idx_users_is_banned ON public.users(is_banned);
CREATE INDEX IF NOT EXISTS idx_users_is_cheater ON public.users(is_cheater);

-- Add composite index for authentication
CREATE INDEX IF NOT EXISTS idx_users_auth ON public.users(username, password);

-- Add index for online users query
CREATE INDEX IF NOT EXISTS idx_users_online ON public.users(last_seen) 
WHERE last_seen > (CURRENT_TIMESTAMP - INTERVAL '5 minutes'); 