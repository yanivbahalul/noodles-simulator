using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NoodlesSimulator.Services;

public class DataRetentionHostedService : BackgroundService
{
    private const int ActivityEventsRetentionDays = 90;
    private const int TestSessionsRetentionDays = 180;

    private readonly IServiceProvider _services;

    public DataRetentionHostedService(IServiceProvider services)
    {
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var activityEvents = scope.ServiceProvider.GetService<ActivityEventService>();
                var testSessions = scope.ServiceProvider.GetService<TestSessionService>();

                var activityDeleted = activityEvents != null
                    ? await activityEvents.PurgeOlderThanAsync(ActivityEventsRetentionDays)
                    : 0;
                var sessionsDeleted = testSessions != null
                    ? await testSessions.PurgeOldSessionsAsync(TestSessionsRetentionDays)
                    : 0;

                if (activityDeleted > 0 || sessionsDeleted > 0)
                {
                    Console.WriteLine(
                        $"[DataRetention] Purged {activityDeleted} activity_events, {sessionsDeleted} old test_sessions");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DataRetentionHostedService] {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
