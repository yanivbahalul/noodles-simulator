using System.Linq;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

internal static class DashboardApiPayload
{
    internal static object ToWidget(DashboardDataService.DashboardSnapshot snapshot)
    {
        var s = snapshot.Summary;
        return new
        {
            allUsersCount = s.AllUsersCount,
            onlineUsersCount = s.OnlineUsersCount,
            cheatersCount = s.CheatersCount,
            bannedUsersCount = s.BannedUsersCount,
            averageSuccessRate = s.AverageSuccessRate,
            activeToday = s.ActiveToday,
            answersToday = s.AnswersToday,
            dailySuccessRate = s.DailySuccessRate,
            activeThisWeek = s.ActiveThisWeek,
            answersThisWeek = s.AnswersThisWeek,
            weeklySuccessRate = s.WeeklySuccessRate,
            newUsersToday = s.NewUsersToday,
            newUsersThisWeek = s.NewUsersThisWeek,
            inactive7Days = s.Inactive7Days,
            inactive30Days = s.Inactive30Days,
            openQuestionReports = s.OpenQuestionReports,
            health = new
            {
                allOk = snapshot.Health.AllOk,
                checkedAtIso = snapshot.Health.CheckedAtIso
            }
        };
    }

    internal static object ToApi(DashboardDataService.DashboardSnapshot snapshot)
    {
        var s = snapshot.Summary;
        return new
        {
            allUsersCount = s.AllUsersCount,
            onlineUsersCount = s.OnlineUsersCount,
            cheatersCount = s.CheatersCount,
            bannedUsersCount = s.BannedUsersCount,
            averageSuccessRate = s.AverageSuccessRate,
            activeToday = s.ActiveToday,
            answersToday = s.AnswersToday,
            dailySuccessRate = s.DailySuccessRate,
            activeThisWeek = s.ActiveThisWeek,
            answersThisWeek = s.AnswersThisWeek,
            weeklySuccessRate = s.WeeklySuccessRate,
            newUsersToday = s.NewUsersToday,
            newUsersThisWeek = s.NewUsersThisWeek,
            inactive7Days = s.Inactive7Days,
            inactive30Days = s.Inactive30Days,
            openQuestionReports = s.OpenQuestionReports,
            allUsersList = snapshot.AllUsersList.Select(ToUser).ToList(),
            onlineUsersList = snapshot.OnlineUsersList.Select(ToUser).ToList(),
            topUsersList = snapshot.TopUsersList.Select(ToUser).ToList(),
            activeExams = snapshot.ActiveExams.Select(ToExam).ToList(),
            recentActivity = snapshot.RecentActivity.Select(ToActivity).ToList(),
            liveActivity = snapshot.LiveActivity.Select(ToActivity).ToList(),
            health = new
            {
                allOk = snapshot.Health.AllOk,
                checkedAtIso = snapshot.Health.CheckedAtIso,
                checks = snapshot.Health.Checks.Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    ok = c.Ok,
                    detail = c.Detail
                })
            },
            questionReports = snapshot.QuestionReports.Select(r => new
            {
                id = r.Id,
                username = r.Username,
                questionId = r.QuestionId,
                questionLabel = QuestionLabel.Format(r.QuestionId),
                explanation = r.Explanation,
                status = r.Status,
                createdAtIso = r.CreatedAtIso,
                resolvedAtIso = r.ResolvedAtIso
            }),
            problematicQuestions = snapshot.ProblematicQuestions.Select(q => new
            {
                questionId = q.QuestionId,
                questionLabel = QuestionLabel.Format(q.QuestionId),
                difficulty = q.Difficulty,
                successRate = q.SuccessRate,
                totalAttempts = q.TotalAttempts,
                openReports = q.OpenReports,
                reason = q.Reason
            })
        };
    }

    internal static object ToUserDetail(DashboardDataService.UserDetailSnapshot detail) => new
    {
        user = ToUser(detail.User),
        recentQuestions = detail.RecentQuestions.Select(q => new
        {
            questionId = q.QuestionId,
            questionLabel = QuestionLabel.Format(q.QuestionId),
            attempts = q.Attempts,
            correct = q.Correct,
            lastAnsweredIso = q.LastAnsweredIso,
            lastWasCorrect = q.LastWasCorrect
        }),
        achievements = detail.Achievements.Select(a => new
        {
            key = a.Key,
            title = a.Title,
            emoji = a.Emoji
        }),
        exams = detail.Exams.Select(ToExamRow)
    };

    private static object ToUser(DashboardDataService.UserRow u) => new
    {
        username = u.Username,
        isOnline = u.IsOnline,
        lastSeenIso = u.LastSeenIso,
        level = u.Level,
        xp = u.Xp,
        dailyCorrect = u.DailyCorrect,
        weeklyCorrect = u.WeeklyCorrect,
        totalAnswered = u.TotalAnswered,
        correctAnswers = u.CorrectAnswers,
        successRate = u.SuccessRate,
        bestExamCorrect = u.BestExamCorrect,
        bestExamScore = u.BestExamScore,
        isCheater = u.IsCheater,
        isBanned = u.IsBanned
    };

    private static object ToExam(DashboardDataService.ActiveExamRow e) => new
    {
        username = e.Username,
        token = e.Token,
        startedIso = e.StartedIso,
        updatedIso = e.UpdatedIso,
        currentIndex = e.CurrentIndex,
        totalQuestions = e.TotalQuestions,
        score = e.Score,
        maxScore = e.MaxScore,
        remainingMinutes = e.RemainingMinutes
    };

    private static object ToExamRow(DashboardDataService.UserExamRow e) => new
    {
        token = e.Token,
        status = e.Status,
        score = e.Score,
        maxScore = e.MaxScore,
        startedIso = e.StartedIso,
        completedIso = e.CompletedIso
    };

    private static object ToActivity(DashboardDataService.ActivityRow a) => new
    {
        kind = a.Kind,
        kindLabel = string.IsNullOrWhiteSpace(a.KindLabel) ? a.Kind : a.KindLabel,
        category = string.IsNullOrWhiteSpace(a.Category) ? ActivityEventCatalog.GetCategory(a.Kind) : a.Category,
        username = a.Username,
        message = a.Message,
        timestampIso = a.TimestampIso
    };
}
