using System;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

/// <summary>ponytail: runnable self-check for quiz submit stat hydration.</summary>
internal static class QuizStatsHydrateCheck
{
    internal static bool Run()
    {
        var data = new UserProgressService.UserProgressData();
        data.QuestionStats["q1"] = new UserProgressService.UserQuestionStat { Attempts = 10, Correct = 8 };
        data.Xp = 250;

        var (total, correct) = UserProgressService.SumQuestionStats(data);
        if (total != 10 || correct != 8)
            return false;

        var user = new User { TotalAnswered = 5, CorrectAnswers = 4, Xp = 100 };
        user.TotalAnswered = Math.Max(user.TotalAnswered, total);
        user.CorrectAnswers = Math.Max(user.CorrectAnswers, correct);
        user.Xp = Math.Max(user.Xp, data.Xp);

        return user.TotalAnswered == 10
            && user.CorrectAnswers == 8
            && user.Xp == 250;
    }
}
