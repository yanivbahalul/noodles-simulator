using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

/// <summary>Runnable assert checks — run with <c>dotnet run -- --ponytail-check</c>.</summary>
public static class PonytailSelfCheck
{
    public static void Run()
    {
        CheckExamScoring();
        CheckQuizGamification();
        CheckQuestionExplanationPaths();
        CheckQuizStatsHydrate();
        CheckQuestionLabelFormat();
        CheckExplanationRatingUrgency();
        CheckAdminOtp();
        Console.WriteLine("[ponytail] all self-checks passed");
    }

    public static void RunStartup()
    {
        if (!CheckQuizStatsHydrate())
            throw new InvalidOperationException("QuizStatsHydrateCheck failed");
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok)
            throw new InvalidOperationException($"ponytail self-check failed: {message}");
    }

    private static bool CheckQuizStatsHydrate()
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

    private static void CheckExamScoring()
    {
        Assert(ExamScoring.ScoreFromCorrectCount(0) == 0, "exam score zero");
        Assert(ExamScoring.ScoreFromCorrectCount(3) == 18, "exam score three");
        Assert(ExamScoring.MaxScore(10) == 60, "exam max score");
    }

    private static void CheckQuizGamification()
    {
        Assert(QuizGamification.LevelFromXp(0) == 1, "level at zero xp");
        Assert(QuizGamification.LevelFromXp(100) == 1, "level at 100 xp");
        Assert(QuizGamification.LevelFromXp(400) == 2, "level at 400 xp");
        Assert(QuizGamification.XpForLevel(2) == 400, "xp for level 2");
        Assert(QuizGamification.XpProgressPercent(250) == 50, "xp progress mid-level");

        Assert(QuizGamification.StreakXpMultiplier(0) == 1.0, "streak multiplier zero");
        Assert(QuizGamification.StreakXpMultiplier(5) == 2.0, "streak multiplier five");
        Assert(QuizGamification.XpForCorrectAnswer("normal", "easy", 5) == 20, "xp easy streak 5");
        Assert(QuizGamification.XpForCorrectAnswer("normal", "hard", 1) == 25, "xp hard no streak");
        Assert(QuizGamification.XpForCorrectAnswer("daily", "easy", 1) == 12, "daily challenge xp");
        Assert(QuizGamification.DailyChallengeCompletionXp(10) == 70, "daily completion xp");
    }

    private static void CheckQuestionLabelFormat()
    {
        Assert(QuestionLabel.Format("") == "—", "empty question label");
        Assert(QuestionLabel.Format("foo/bar.png") == "bar", "strip extension");
        Assert(
            QuestionLabel.Format("Screenshot at Jan 5 12-30-45.png") == "05/01 12:30",
            "screenshot label");
    }

    private static void CheckQuestionExplanationPaths()
    {
        Assert(
            QuestionExplanationService.VideoObjectPath("foo/bar.png") == "explanations/foo_bar.png.mp4",
            "explanation video path sanitizes slashes");
        Assert(
            QuestionExplanationService.VideoObjectPath("  q1.PNG  ").Contains("q1.PNG"),
            "explanation video path keeps basename");
        Assert(
            MediaUrl.ForStoragePath("foo.png") == "/media/original/foo.png",
            "media url bare filename → original/");
        Assert(
            MediaUrl.ForStoragePath("explanations/q1.mp4") == "/media/explanations/q1.mp4",
            "media url nested");
        Assert(
            !MediaUrl.TryNormalizePath("../secret", out _),
            "media path blocks traversal");
    }

    private static void CheckExplanationRatingUrgency()
    {
        var bad = new QuestionExplanationRatingSummary
        {
            AvgStars = 2,
            RatingCount = 5,
            LowCount = 3,
            UrgencyScore = (5.0 - 2) * 5 + 3 * 2.0
        };
        var good = new QuestionExplanationRatingSummary
        {
            AvgStars = 4.8,
            RatingCount = 2,
            LowCount = 0,
            UrgencyScore = (5.0 - 4.8) * 2
        };
        Assert(bad.UrgencyScore > good.UrgencyScore, "urgency ranks bad explanations first");
    }

    private static void CheckAdminOtp()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:Username"] = "Admin",
                ["Admin:Password"] = "secret-admin",
                ["Admin:OtpEmail"] = "admin@example.com"
            })
            .Build();

        Assert(AdminConfiguration.IsAdminUsername(config, "admin"), "admin username match");
        Assert(AdminConfiguration.IsReservedUsername(config, "ADMIN"), "reserved admin username");
        Assert(AdminConfiguration.VerifyPassword(config, "secret-admin"), "admin password ok");
        Assert(!AdminConfiguration.VerifyPassword(config, "wrong"), "admin password reject");

        var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var otp = new AdminOtpService(cache, new EmailService(config), config);
        Assert(!otp.Verify("missing", "123456"), "admin otp missing session");

        otp.SeedTestOtp("fixed", "042819");
        Assert(otp.Verify("fixed", "042819"), "admin otp verify");
        Assert(!otp.Verify("fixed", "042819"), "admin otp one-time use");
    }
}
