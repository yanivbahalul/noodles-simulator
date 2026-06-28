using System;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

/// <summary>Runnable assert checks for core gamification math — run with <c>dotnet run -- --ponytail-check</c>.</summary>
public static class PonytailSelfCheck
{
    public static void Run()
    {
        CheckExamScoring();
        CheckQuizGamification();
        CheckQuestionExplanationPaths();
        Console.WriteLine("[ponytail] all self-checks passed");
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok)
            throw new InvalidOperationException($"ponytail self-check failed: {message}");
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

    private static void CheckQuestionExplanationPaths()
    {
        Assert(
            QuestionExplanationService.VideoObjectPath("foo/bar.png") == "explanations/foo_bar.png.mp4",
            "explanation video path sanitizes slashes");
        Assert(
            QuestionExplanationService.VideoObjectPath("  q1.PNG  ").Contains("q1.PNG"),
            "explanation video path keeps basename");
    }
}
