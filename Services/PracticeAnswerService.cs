using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public class PracticeAnswerFeedback
{
    public int XpGain { get; set; }
    public int LevelUpTo { get; set; }
    public int BrokenStreakAt { get; set; }
    public bool DailyJustCompleted { get; set; }
    public int DailyFinalScore { get; set; }
    public bool IsDailyComplete { get; set; }
    public int CurrentStreak { get; set; }
    public List<string> NewAchievements { get; set; } = new();
}

public enum CheaterDetectionAction
{
    None,
    RedirectCheater,
    RedirectLogin
}

public sealed class PracticeAnswerEvaluation
{
    public string QuestionImage { get; set; } = "";
    public string SelectedAnswer { get; set; } = "";
    public string CorrectAnswerKey { get; set; } = "";
    public Dictionary<string, string> ShuffledAnswers { get; set; } = new();
    public bool IsCorrect { get; set; }
}

public class PracticeAnswerService
{
    private readonly UserProgressService? _userProgress;
    private readonly AchievementService? _achievements;
    private readonly ActivityEventService? _activityEvents;
    private readonly QuestionDifficultyService? _difficultyService;
    private readonly PracticeQuizService? _practiceQuiz;
    private readonly AuthService _authService;

    public PracticeAnswerService(
        AuthService authService,
        UserProgressService? userProgress = null,
        AchievementService? achievements = null,
        ActivityEventService? activityEvents = null,
        QuestionDifficultyService? difficultyService = null,
        PracticeQuizService? practiceQuiz = null)
    {
        _authService = authService;
        _userProgress = userProgress;
        _achievements = achievements;
        _activityEvents = activityEvents;
        _difficultyService = difficultyService;
        _practiceQuiz = practiceQuiz;
    }

    public static int XpGainForCorrectAnswer(string practiceMode, string practiceDifficulty, int streak) =>
        QuizGamification.XpForCorrectAnswer(practiceMode, practiceDifficulty, streak);

    public static bool EvaluateAnswer(string? correctKey, string selected, Dictionary<string, string>? options)
    {
        if (string.IsNullOrEmpty(correctKey)
            || options == null
            || options.Count == 0
            || !options.ContainsKey(selected))
            return false;

        return string.Equals(selected, correctKey, StringComparison.Ordinal);
    }

    public PracticeAnswerEvaluation PrepareSubmittedAnswer(
        ISession session,
        string questionImage,
        string answer,
        bool tryRecover = false)
    {
        var evaluation = new PracticeAnswerEvaluation
        {
            QuestionImage = questionImage,
            SelectedAnswer = answer
        };

        if (tryRecover && TryRecoverQuestionState(session, questionImage, answer, evaluation))
            return FinalizeEvaluation(session, evaluation, answer);

        HydrateQuestionState(session, questionImage, evaluation);
        return FinalizeEvaluation(session, evaluation, answer);
    }

    private bool TryRecoverQuestionState(
        ISession session,
        string questionImage,
        string answer,
        PracticeAnswerEvaluation evaluation)
    {
        if (!string.IsNullOrEmpty(evaluation.CorrectAnswerKey)
            && evaluation.ShuffledAnswers.Count > 0
            && evaluation.ShuffledAnswers.ContainsKey(answer))
            return true;

        if (string.Equals(session.GetString(PracticeQuizService.PracticeQuestionKey), questionImage, StringComparison.OrdinalIgnoreCase)
            && _practiceQuiz?.TryHydrateFromPracticeSession(session, out var fromPractice) == true)
        {
            ApplyQuestionState(evaluation, fromPractice);
            return true;
        }

        if (string.Equals(session.GetString(PracticeQuizService.FlashQuestionKey), questionImage, StringComparison.OrdinalIgnoreCase)
            && _practiceQuiz?.TryHydrateFromFlash(session, persistToPractice: true, out var fromFlash) == true)
        {
            ApplyQuestionState(evaluation, fromFlash);
            return true;
        }

        return false;
    }

    private void HydrateQuestionState(ISession session, string questionImage, PracticeAnswerEvaluation evaluation)
    {
        EnsurePracticeHydrated(session, questionImage, evaluation);

        if (string.Equals(session.GetString(PracticeQuizService.PracticeQuestionKey), questionImage, StringComparison.OrdinalIgnoreCase)
            && _practiceQuiz?.TryHydrateFromPracticeSession(session, out var fromPractice) == true)
        {
            ApplyQuestionState(evaluation, fromPractice);
            return;
        }

        if (string.Equals(session.GetString(PracticeQuizService.FlashQuestionKey), questionImage, StringComparison.OrdinalIgnoreCase)
            && _practiceQuiz?.TryHydrateFromFlash(session, persistToPractice: true, out var fromFlash) == true)
            ApplyQuestionState(evaluation, fromFlash);
    }

    private void EnsurePracticeHydrated(ISession session, string questionImage, PracticeAnswerEvaluation evaluation)
    {
        var sessionQuestion = session.GetString(PracticeQuizService.PracticeQuestionKey);
        if (string.Equals(sessionQuestion, questionImage, StringComparison.OrdinalIgnoreCase))
            return;

        if (_practiceQuiz?.TryHydrateFromFlash(session, persistToPractice: true, out var fromFlash) == true)
            ApplyQuestionState(evaluation, fromFlash);
    }

    private static void ApplyQuestionState(
        PracticeAnswerEvaluation evaluation,
        PracticeQuizService.PracticeQuestionState state)
    {
        evaluation.QuestionImage = state.QuestionImage;
        evaluation.CorrectAnswerKey = state.CorrectAnswerKey;
        evaluation.ShuffledAnswers = state.ShuffledAnswers;
    }

    private PracticeAnswerEvaluation FinalizeEvaluation(
        ISession session,
        PracticeAnswerEvaluation evaluation,
        string answer)
    {
        evaluation.SelectedAnswer = answer;
        evaluation.IsCorrect = EvaluateAnswer(evaluation.CorrectAnswerKey, answer, evaluation.ShuffledAnswers);
        _practiceQuiz?.SavePracticeQuestionState(
            session,
            evaluation.QuestionImage,
            evaluation.ShuffledAnswers,
            evaluation.CorrectAnswerKey);
        return evaluation;
    }

    public void ApplyAnswerToUserStats(User user, string questionImage, bool isCorrect)
    {
        user.TotalAnswered++;
        if (isCorrect)
            user.CorrectAnswers++;

        try { _ = _difficultyService?.UpdateQuestionStatsAsync(questionImage, isCorrect); }
        catch (Exception ex) { Console.WriteLine($"[PracticeAnswerService RecordStats Error] {ex.Message}"); }
    }

    public void HydrateUserFromProgress(User user)
    {
        if (user == null || _userProgress == null) return;
        if (!_userProgress.TryGetProgressTotals(user.Username, out var correct, out var total, out var xp))
            return;

        user.CorrectAnswers = Math.Max(user.CorrectAnswers, correct);
        user.TotalAnswered = Math.Max(user.TotalAnswered, total);
        user.Xp = Math.Max(user.Xp, xp);
        user.Level = QuizGamification.LevelFromXp(user.Xp);
    }

    public async Task HydrateUserFromProgressAsync(User user)
    {
        if (user == null || _userProgress == null) return;

        var data = _userProgress.TryGetCached(user.Username, out var cached)
            ? cached
            : await _userProgress.LoadAsync(user.Username);

        var (total, correct) = UserProgressService.SumQuestionStats(data);
        user.CorrectAnswers = Math.Max(user.CorrectAnswers, correct);
        user.TotalAnswered = Math.Max(user.TotalAnswered, total);
        user.Xp = Math.Max(user.Xp, data.Xp);
        user.Level = QuizGamification.LevelFromXp(user.Xp);
    }

    public async Task<PracticeAnswerFeedback> ProcessAnswerAsync(
        ISession session,
        User user,
        string questionImage,
        bool isCorrect,
        int dailyTotal = 10)
    {
        var feedback = new PracticeAnswerFeedback();
        var practiceMode = session.GetString("PracticeMode") ?? "normal";
        var practiceDifficulty = session.GetString("PracticeDifficulty") ?? "";

        var streak = UpdateAnswerStreak(session, isCorrect, feedback);
        feedback.CurrentStreak = streak;
        var xpGain = isCorrect ? XpGainForCorrectAnswer(practiceMode, practiceDifficulty, streak) : 0;
        feedback.XpGain = xpGain;

        await RecordPracticeProgressAsync(
            session,
            practiceMode,
            practiceDifficulty,
            user,
            questionImage,
            isCorrect,
            streak,
            xpGain,
            dailyTotal,
            feedback);
        await SyncUserLevelFromProgressAsync(user, feedback);

        if (_achievements != null && _userProgress != null)
        {
            var progress = await _userProgress.LoadAsync(user.Username);
            var (progressTotal, _) = UserProgressService.SumQuestionStats(progress);
            var totalForAchievements = Math.Max(user.TotalAnswered, progressTotal);
            var xpForAchievements = Math.Max(user.Xp, progress.Xp);
            feedback.NewAchievements.AddRange(
                await _achievements.CheckPracticeAchievementsAsync(
                    user.Username, streak, totalForAchievements, xpForAchievements));
        }
        else if (_achievements != null)
        {
            feedback.NewAchievements.AddRange(
                await _achievements.CheckPracticeAchievementsAsync(user.Username, streak, user.TotalAnswered, user.Xp));
        }

        if (feedback.NewAchievements.Count > 0)
            session.SetString("PendingAchievements", JsonSerializer.Serialize(feedback.NewAchievements, AppJson.Options));

        _ = SyncUserToAuthAsync(user);
        UpdateRapidAnswerCounters(session, isCorrect);

        return feedback;
    }

    public CheaterDetectionAction DetectCheater(ISession session, User user)
    {
        if (IsCheaterDetectionExempt(user.Username))
            return CheaterDetectionAction.None;

        var rapidTotal = session.GetInt32("RapidTotal") ?? 0;
        var rapidCorrect = session.GetInt32("RapidCorrect") ?? 0;
        if (rapidTotal < 20 && rapidCorrect < 15)
            return CheaterDetectionAction.None;

        Console.WriteLine($"[CHEATER DETECTED] User: {user.Username} | RapidTotal: {rapidTotal} | RapidCorrect: {rapidCorrect}");
        user.IsCheater = true;
        _ = SyncUserToAuthAsync(user);

        var cheaterCount = (session.GetInt32("CheaterCount") ?? 0) + 1;
        session.SetInt32("CheaterCount", cheaterCount);

        if (cheaterCount >= 3)
        {
            user.IsBanned = true;
            _ = SyncUserToAuthAsync(user);
            return CheaterDetectionAction.RedirectLogin;
        }

        session.SetInt32("RapidTotal", 0);
        session.SetInt32("RapidCorrect", 0);
        return CheaterDetectionAction.RedirectCheater;
    }

    public Task<CheaterDetectionAction> DetectCheaterAsync(ISession session, User user) =>
        Task.FromResult(DetectCheater(session, user));

    private static bool IsCheaterDetectionExempt(string username) =>
        !string.IsNullOrWhiteSpace(username) &&
        username.StartsWith("e2etest", StringComparison.OrdinalIgnoreCase);

    private static int UpdateAnswerStreak(ISession session, bool isCorrect, PracticeAnswerFeedback feedback)
    {
        var previous = session.GetInt32("CurrentStreak") ?? 0;
        var streak = previous;
        if (isCorrect)
        {
            streak++;
            session.SetInt32("CurrentStreak", streak);
        }
        else
        {
            if (previous >= 3)
                feedback.BrokenStreakAt = previous;
            streak = 0;
            session.SetInt32("CurrentStreak", 0);
        }

        return streak;
    }

    private async Task RecordPracticeProgressAsync(
        ISession session,
        string practiceMode,
        string practiceDifficulty,
        User user,
        string questionImage,
        bool isCorrect,
        int streak,
        int xpGain,
        int dailyTotal,
        PracticeAnswerFeedback feedback)
    {
        if (_userProgress == null) return;

        await _userProgress.RecordAnswerAsync(user.Username, questionImage, isCorrect, xpGain, streak);

        if (isCorrect)
            await RecordCorrectAnswerProgressAsync(user, questionImage, practiceMode, practiceDifficulty, feedback);

        if (practiceMode == "daily" && session != null)
            await RecordDailyChallengeProgressAsync(session, user, isCorrect, dailyTotal, feedback);

        if (practiceMode != "daily")
        {
            _activityEvents?.Log(user.Username, ActivityEventCatalog.Answer, new Dictionary<string, object>
            {
                ["questionId"] = questionImage ?? "",
                ["correct"] = isCorrect,
                ["mode"] = practiceMode ?? "normal",
                ["difficulty"] = practiceDifficulty ?? "easy"
            });
        }
    }

    private async Task RecordCorrectAnswerProgressAsync(
        User user,
        string questionImage,
        string practiceMode,
        string practiceDifficulty,
        PracticeAnswerFeedback feedback)
    {
        if (_userProgress == null) return;

        if (practiceMode == "weak")
            await _userProgress.IncrementWeakCorrectAsync(user.Username);
        if (practiceDifficulty == "hard")
            await _userProgress.IncrementHardCorrectAsync(user.Username);
        if (practiceMode != "review" || _achievements == null
            || !await _userProgress.RemoveSessionMistakeAsync(user.Username, questionImage))
            return;

        await _userProgress.IncrementReviewClearAsync(user.Username);
        feedback.NewAchievements.AddRange(await _achievements.CheckReviewClearAsync(user.Username));
    }

    private async Task RecordDailyChallengeProgressAsync(
        ISession session,
        User user,
        bool isCorrect,
        int dailyTotal,
        PracticeAnswerFeedback feedback)
    {
        if (_userProgress == null) return;

        await _userProgress.RecordDailyChallengeAnswerAsync(user.Username, isCorrect);
        var dailyIdx = (session.GetInt32("DailyQuestionIndex") ?? 0) + 1;
        session.SetInt32("DailyQuestionIndex", dailyIdx);

        if (isCorrect)
        {
            var dailyScore = (session.GetInt32("DailyScore") ?? 0) + 1;
            session.SetInt32("DailyScore", dailyScore);
        }

        if (dailyIdx < dailyTotal || _achievements == null) return;

        var finalScore = session.GetInt32("DailyScore") ?? 0;
        await _userProgress.RecordDailyChallengeCompleteAsync(user.Username, finalScore >= dailyTotal);
        feedback.NewAchievements.AddRange(await _achievements.CheckDailyAchievementsAsync(user.Username));
        _activityEvents?.Log(user.Username, ActivityEventCatalog.DailyComplete, new Dictionary<string, object>
        {
            ["score"] = finalScore,
            ["total"] = dailyTotal
        });
        feedback.DailyJustCompleted = true;
        feedback.DailyFinalScore = finalScore;
        feedback.IsDailyComplete = true;
    }

    private async Task SyncUserLevelFromProgressAsync(User user, PracticeAnswerFeedback feedback)
    {
        if (_userProgress == null) return;

        var progress = _userProgress.TryGetCached(user.Username, out var cached)
            ? cached
            : await _userProgress.LoadAsync(user.Username);
        var oldLevel = user.Level > 0 ? user.Level : QuizGamification.LevelFromXp(user.Xp);
        user.Xp = progress.Xp;
        user.Level = QuizGamification.LevelFromXp(user.Xp);
        if (user.Level > oldLevel)
        {
            feedback.LevelUpTo = user.Level;
            _activityEvents?.Log(user.Username, ActivityEventCatalog.LevelUp, new Dictionary<string, object>
            {
                ["level"] = user.Level
            });
        }
    }

    private async Task SyncUserToAuthAsync(User user)
    {
        try { await _authService.UpdateUserAsync(user); }
        catch (Exception ex) { Console.WriteLine($"[PracticeAnswerService UpdateUser Error] {ex.Message}"); }
    }

    private static void UpdateRapidAnswerCounters(ISession session, bool isCorrect)
    {
        var sessionStartStr = session.GetString("SessionStart");
        DateTime.TryParse(sessionStartStr, out var sessionStart);
        var now = DateTime.UtcNow;
        var elapsedSeconds = (now - sessionStart).TotalSeconds;

        var rapidTotal = session.GetInt32("RapidTotal") ?? 0;
        var rapidCorrect = session.GetInt32("RapidCorrect") ?? 0;

        if (elapsedSeconds <= 120)
        {
            session.SetInt32("RapidTotal", rapidTotal + 1);
            if (isCorrect)
                session.SetInt32("RapidCorrect", rapidCorrect + 1);
        }
        else
        {
            session.SetString("SessionStart", now.ToString());
            session.SetInt32("RapidTotal", 1);
            session.SetInt32("RapidCorrect", isCorrect ? 1 : 0);
        }
    }
}
