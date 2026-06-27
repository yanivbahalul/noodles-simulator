using System;
using System.Threading.Tasks;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

/// <summary>
/// Removes a user and all related data from Supabase and local caches.
/// </summary>
public class UserDeletionService
{
    private readonly AuthService _auth;
    private readonly TestSessionService _testSessions;
    private readonly UserProgressService _progress;
    private readonly UserStatsService _stats;

    public UserDeletionService(
        AuthService auth,
        TestSessionService testSessions = null,
        UserProgressService progress = null,
        UserStatsService stats = null)
    {
        _auth = auth;
        _testSessions = testSessions;
        _progress = progress;
        _stats = stats;
    }

    public async Task<(bool Success, string Error)> DeleteUserCompletelyAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return (false, "Missing username");

        username = username.Trim();
        if (string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase))
            return (false, "Cannot delete admin user");

        var user = await _auth.GetUserAsync(username);
        if (user == null)
            return (false, "User not found");

        if (_testSessions != null)
            await _testSessions.DeleteUserSessionsAsync(username);

        if (!await _auth.DeleteUserAsync(username))
            return (false, "Failed to delete user from database");

        _progress?.DeleteLocal(username);
        _stats?.InvalidateCache();

        return (true, null);
    }
}
