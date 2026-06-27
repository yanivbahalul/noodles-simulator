using System;
using System.Threading.Tasks;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public class AdminUserSupportService
{
    private readonly AuthService _auth;
    private readonly UserProgressService _progress;
    private readonly TestSessionService _testSessions;
    private readonly ActivityEventService _activityEvents;

    public AdminUserSupportService(
        AuthService auth,
        UserProgressService progress = null,
        TestSessionService testSessions = null,
        ActivityEventService activityEvents = null)
    {
        _auth = auth;
        _progress = progress;
        _testSessions = testSessions;
        _activityEvents = activityEvents;
    }

    public async Task<(bool Success, string Error)> ResetUserProgressAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return (false, "Missing username");

        username = username.Trim();
        if (string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase))
            return (false, "Cannot reset admin user");

        var user = await _auth.GetUserAsync(username);
        if (user == null)
            return (false, "User not found");

        user.CorrectAnswers = 0;
        user.TotalAnswered = 0;
        user.IsCheater = false;
        user.Xp = 0;
        user.Level = 1;
        user.WeeklyCorrect = 0;
        user.WeekKey = UserProgressService.GetWeekKey();
        user.DailyCorrect = 0;
        user.DayKey = UserProgressService.TodayKey();
        user.DailyChallengeScore = 0;
        user.DailyChallengeDate = "";
        user.BestExamScore = 0;
        user.BestExamCorrect = 0;

        if (!await _auth.UpdateUserAsync(user))
            return (false, "Failed to update user");

        try
        {
            await _auth.SyncLeaderboardStatsAsync(
                user.Username, 0, user.WeekKey, 0, user.DayKey, 0, "", 0, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminUserSupport] SyncLeaderboardStats failed: {ex.Message}");
        }

        try { _progress?.ResetAll(user.Username); }
        catch (Exception ex) { Console.WriteLine($"[AdminUserSupport] ResetAll failed: {ex.Message}"); }

        _activityEvents?.Log(user.Username, ActivityEventCatalog.ProgressReset);
        return (true, null);
    }

    public async Task<(bool Success, string Error)> ExpireExamAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (false, "Missing token");

        if (_testSessions == null)
            return (false, "Test session service not available");

        var session = await _testSessions.GetSessionAsync(token.Trim());
        if (session == null)
            return (false, "Exam not found");

        if (!string.Equals(session.Status, "active", StringComparison.OrdinalIgnoreCase))
            return (false, "Exam is not active");

        var ok = await _testSessions.ExpireSessionAsync(session);
        return ok ? (true, null) : (false, "Failed to expire exam");
    }
}
