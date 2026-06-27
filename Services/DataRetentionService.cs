using System;
using System.Threading.Tasks;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public class DataRetentionService
{
    public const int ActivityEventsRetentionDays = 90;
    public const int TestSessionsRetentionDays = 180;

    private readonly ActivityEventService _activityEvents;
    private readonly TestSessionService _testSessions;

    public DataRetentionService(
        ActivityEventService activityEvents = null,
        TestSessionService testSessions = null)
    {
        _activityEvents = activityEvents;
        _testSessions = testSessions;
    }

    public async Task<(int ActivityEvents, int TestSessions)> RunCleanupAsync()
    {
        var activityDeleted = _activityEvents != null
            ? await _activityEvents.PurgeOlderThanAsync(ActivityEventsRetentionDays)
            : 0;
        var sessionsDeleted = _testSessions != null
            ? await _testSessions.PurgeOldSessionsAsync(TestSessionsRetentionDays)
            : 0;

        if (activityDeleted > 0 || sessionsDeleted > 0)
        {
            Console.WriteLine(
                $"[DataRetention] Purged {activityDeleted} activity_events, {sessionsDeleted} old test_sessions");
        }

        return (activityDeleted, sessionsDeleted);
    }
}

public class DataRetentionHostedService : Microsoft.Extensions.Hosting.BackgroundService
{
    private readonly DataRetentionService _retention;

    public DataRetentionHostedService(DataRetentionService retention)
    {
        _retention = retention;
    }

    protected override async Task ExecuteAsync(System.Threading.CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _retention.RunCleanupAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DataRetentionHostedService] {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
