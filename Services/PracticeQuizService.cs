using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public class PracticeQuestionUrls
{
    public string QuestionImageUrl { get; set; } = "";
    public Dictionary<string, string> AnswerImageUrls { get; set; } = new();
    public string QuestionImageOriginalName { get; set; } = "";
    public Dictionary<string, string> AnswerImageOriginalNames { get; set; } = new();
}

public sealed class PracticeQuestionDisplay
{
    public PracticeQuizService.PracticeQuestionState State { get; init; } = new();
    public PracticeQuestionUrls Urls { get; init; } = new();
}

public class PracticeQuizService
{
    public const string FlashQuestionKey = "AnswerFlash_QuestionImage";
    public const string FlashSelectedKey = "AnswerFlash_SelectedAnswer";
    public const string FlashAnswersKey = "AnswerFlash_AnswersJson";
    public const string FlashCorrectKey = "AnswerFlash_IsCorrect";
    public const string FlashCorrectKeyKey = "AnswerFlash_CorrectKey";
    public const string PracticeQuestionKey = "Practice_QuestionImage";
    public const string PracticeOptionsKey = "Practice_OptionsJson";
    public const string PracticeCorrectKey = "Practice_CorrectKey";
    public const string PrefetchQuestionKey = "Practice_Prefetch_QuestionImage";
    public const string PrefetchOptionsKey = "Practice_Prefetch_OptionsJson";
    public const string PrefetchCorrectKey = "Practice_Prefetch_CorrectKey";
    public const string PrefetchAnchorKey = "Practice_Prefetch_Anchor";
    public const string LastSubmittedQuestionKey = "LastSubmittedQuestion";

    private readonly PracticeQuestionPickerService _picker;
    private readonly SupabaseStorageService? _storage;

    public PracticeQuizService(
        PracticeQuestionPickerService picker,
        SupabaseStorageService? storage = null)
    {
        _picker = picker;
        _storage = storage;
    }

    public void EnsureDailyChallengeSession(ISession session)
    {
        var today = UserProgressService.TodayKey();
        var existingDate = session.GetString("DailyDate");
        if (existingDate == today) return;

        session.SetString("DailyDate", today);
        session.SetInt32("DailyQuestionIndex", 0);
        session.SetInt32("DailyScore", 0);
        session.Remove("DailyQuestions");
    }

    public List<string> GetRecentQuestions(ISession session)
    {
        try
        {
            var json = session.GetString("RecentQuestions");
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            var list = JsonSerializer.Deserialize<List<string>>(json, AppJson.Options) ?? new List<string>();
            return list.TakeLast(10).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    public void AddRecentQuestion(ISession session, string questionImage)
    {
        try
        {
            var list = GetRecentQuestions(session);
            list.Add(questionImage);
            if (list.Count > 20)
                list = list.TakeLast(20).ToList();
            session.SetString("RecentQuestions", JsonSerializer.Serialize(list, AppJson.Options));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AddRecentQuestion Error] {ex.Message}");
        }
    }

    public void SavePracticeQuestionState(
        ISession session,
        string questionImage,
        Dictionary<string, string>? shuffledAnswers,
        string correctAnswerKey)
    {
        session.SetString(PracticeQuestionKey, questionImage ?? "");
        session.SetString(PracticeOptionsKey,
            JsonSerializer.Serialize(shuffledAnswers ?? new Dictionary<string, string>(), AppJson.Options));
        session.SetString(PracticeCorrectKey, correctAnswerKey ?? "");
    }

    public (string questionImage, string correctKey, Dictionary<string, string> options) LoadPracticeFromSession(ISession session)
    {
        var questionImage = session.GetString(PracticeQuestionKey) ?? "";
        var correctKey = session.GetString(PracticeCorrectKey) ?? "";
        var optionsJson = session.GetString(PracticeOptionsKey);
        var options = DeserializeAnswerOptions(optionsJson);
        return (questionImage, correctKey, options);
    }

    public bool HasValidPrefetch(ISession session)
    {
        var anchor = session.GetString(PrefetchAnchorKey);
        var current = session.GetString(PracticeQuestionKey);
        var prefetchQuestion = session.GetString(PrefetchQuestionKey);
        return !string.IsNullOrWhiteSpace(prefetchQuestion)
               && !string.IsNullOrWhiteSpace(anchor)
               && string.Equals(anchor, current, StringComparison.Ordinal);
    }

    public void ClearPrefetch(ISession session)
    {
        session.Remove(PrefetchQuestionKey);
        session.Remove(PrefetchOptionsKey);
        session.Remove(PrefetchCorrectKey);
        session.Remove(PrefetchAnchorKey);
    }

    public (string questionImage, string correctKey, Dictionary<string, string> options) ReadPrefetch(ISession session)
    {
        var questionImage = session.GetString(PrefetchQuestionKey) ?? "";
        var correctKey = session.GetString(PrefetchCorrectKey) ?? "";
        var optionsJson = session.GetString(PrefetchOptionsKey);
        return (questionImage, correctKey, DeserializeAnswerOptions(optionsJson));
    }

    public async Task<PracticePickResult?> PickForSessionAsync(ISession session)
    {
        EnsureDailyChallengeSession(session);

        var request = new PracticePickRequest
        {
            Mode = session.GetString("PracticeMode") ?? "normal",
            Difficulty = session.GetString("PracticeDifficulty") ?? "",
            Username = session.GetString("Username") ?? "",
            RecentQuestions = GetRecentQuestions(session),
            LastSubmittedQuestion = session.GetString(LastSubmittedQuestionKey) ?? "",
            DailyQuestionsJson = session.GetString("DailyQuestions") ?? "",
            DailyQuestionIndex = session.GetInt32("DailyQuestionIndex") ?? 0,
            DailyTotal = 10
        };

        var result = await _picker.PickQuestionAsync(request);
        if (result?.NewDailyQuestionsJson != null)
            session.SetString("DailyQuestions", result.NewDailyQuestionsJson);

        return result;
    }

    public async Task<PracticePickResult?> LoadRandomQuestionAsync(ISession session)
    {
        var picked = await PickForSessionAsync(session);
        if (picked == null)
            return new PracticePickResult
            {
                QuestionImage = "placeholder.jpg",
                ShuffledAnswers = new Dictionary<string, string>(),
                CorrectAnswerKey = ""
            };

        PracticeQuestionPickerService.RecordQuestionShown(picked.QuestionImage);
        PracticeQuestionPickerService.IncrementGroupShown(picked.QuestionImage);
        AddRecentQuestion(session, picked.QuestionImage);
        session.Remove(LastSubmittedQuestionKey);
        SavePracticeQuestionState(session, picked.QuestionImage, picked.ShuffledAnswers, picked.CorrectAnswerKey);
        return picked;
    }

    public async Task<bool> TryPromotePrefetchAsync(ISession session)
    {
        if (!HasValidPrefetch(session))
            return false;

        var (questionImage, correctKey, options) = ReadPrefetch(session);
        SavePracticeQuestionState(session, questionImage, options, correctKey);
        PracticeQuestionPickerService.RecordQuestionShown(questionImage);
        PracticeQuestionPickerService.IncrementGroupShown(questionImage);
        AddRecentQuestion(session, questionImage);
        session.Remove(LastSubmittedQuestionKey);
        ClearPrefetch(session);
        return true;
    }

    public async Task BuildPrefetchIfNeededAsync(ISession session)
    {
        if (HasValidPrefetch(session))
            return;

        ClearPrefetch(session);
        var anchor = session.GetString(PracticeQuestionKey);
        if (string.IsNullOrWhiteSpace(anchor))
            return;

        var picked = await PickForSessionAsync(session);
        if (picked == null)
            return;

        session.SetString(PrefetchQuestionKey, picked.QuestionImage ?? "");
        session.SetString(PrefetchOptionsKey,
            JsonSerializer.Serialize(picked.ShuffledAnswers ?? new Dictionary<string, string>(), AppJson.Options));
        session.SetString(PrefetchCorrectKey, picked.CorrectAnswerKey ?? "");
        session.SetString(PrefetchAnchorKey, anchor);
    }

    public async Task<PracticeQuestionDisplay> BuildDisplayAsync(
        string questionImage,
        Dictionary<string, string>? shuffledAnswers,
        string correctAnswerKey)
    {
        var urls = await PopulateUrlsAsync(questionImage, shuffledAnswers);
        return new PracticeQuestionDisplay
        {
            State = new PracticeQuestionState
            {
                QuestionImage = questionImage ?? "",
                ShuffledAnswers = shuffledAnswers ?? new Dictionary<string, string>(),
                CorrectAnswerKey = correctAnswerKey ?? ""
            },
            Urls = urls
        };
    }

    public async Task<PracticeQuestionDisplay> LoadRandomQuestionDisplayAsync(ISession session)
    {
        var picked = await LoadRandomQuestionAsync(session);
        return await BuildDisplayAsync(picked.QuestionImage, picked.ShuffledAnswers, picked.CorrectAnswerKey);
    }

    public async Task<PracticeQuestionDisplay> LoadPracticeDisplayFromSessionAsync(ISession session)
    {
        var (questionImage, correctKey, options) = LoadPracticeFromSession(session);
        return await BuildDisplayAsync(questionImage, options, correctKey);
    }

    public async Task<PracticeQuestionDisplay?> TryLoadPrefetchDisplayAsync(ISession session)
    {
        if (!HasValidPrefetch(session))
            return null;

        var (questionImage, correctKey, options) = ReadPrefetch(session);
        return await BuildDisplayAsync(questionImage, options, correctKey);
    }

    public async Task<PracticeQuestionDisplay> AdvanceToNextQuestionDisplayAsync(ISession session)
    {
        if (await TryPromotePrefetchAsync(session))
            return await LoadPracticeDisplayFromSessionAsync(session);

        return await LoadRandomQuestionDisplayAsync(session);
    }

    public async Task<object?> BuildPrefetchApiResponseAsync(ISession session, PracticeNextQuestionSnapshot meta)
    {
        await BuildPrefetchIfNeededAsync(session);
        var prefetch = await TryLoadPrefetchDisplayAsync(session);
        if (prefetch == null)
            return null;

        var response = PracticeQuizApiResponses.BuildNextQuestion(MergeNextQuestionSnapshot(prefetch, meta));
        await LoadPracticeDisplayFromSessionAsync(session);
        return response;
    }

    public static PracticeNextQuestionSnapshot MergeNextQuestionSnapshot(
        PracticeQuestionDisplay display,
        PracticeNextQuestionSnapshot meta)
    {
        var state = display.State;
        var urls = display.Urls;
        return new PracticeNextQuestionSnapshot
        {
            QuestionImage = state.QuestionImage,
            QuestionImageUrl = urls.QuestionImageUrl,
            QuestionImageOriginalName = urls.QuestionImageOriginalName ?? state.QuestionImage,
            ShuffledAnswers = state.ShuffledAnswers,
            AnswerImageUrls = urls.AnswerImageUrls,
            AnswerImageOriginalNames = urls.AnswerImageOriginalNames,
            PracticeMode = meta.PracticeMode,
            PracticeDifficulty = meta.PracticeDifficulty,
            DailyProgress = meta.DailyProgress,
            DailyTotal = meta.DailyTotal,
            CurrentStreak = meta.CurrentStreak
        };
    }

    public async Task<PracticeQuestionUrls> PopulateUrlsAsync(
        string questionImage,
        Dictionary<string, string>? shuffledAnswers)
    {
        var urls = new PracticeQuestionUrls
        {
            QuestionImageOriginalName = questionImage,
            AnswerImageOriginalNames = new Dictionary<string, string>()
        };

        foreach (var kv in shuffledAnswers ?? new Dictionary<string, string>())
        {
            if (!string.IsNullOrWhiteSpace(kv.Value))
                urls.AnswerImageOriginalNames[kv.Key] = kv.Value;
        }

        try
        {
            var resolved = await ImageUrlResolver.ResolveQuestionAndAnswersAsync(
                _storage, questionImage, shuffledAnswers);
            urls.QuestionImageUrl = resolved.QuestionUrl;
            urls.AnswerImageUrls = resolved.AnswerUrls;
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException)
        {
            Console.WriteLine($"[PopulateUrls Error] {ex.Message}");
            urls.QuestionImageUrl = "";
            urls.AnswerImageUrls = new Dictionary<string, string>();
        }

        return urls;
    }

    public static Dictionary<string, string> DeserializeAnswerOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
            return new Dictionary<string, string>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(optionsJson, AppJson.Options)
                   ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    public sealed class PracticeQuestionState
    {
        public string QuestionImage { get; init; } = "";
        public string CorrectAnswerKey { get; init; } = "";
        public Dictionary<string, string> ShuffledAnswers { get; init; } = new();
    }

    public sealed class PracticeAnswerFlash
    {
        public string QuestionImage { get; init; } = "";
        public string SelectedAnswer { get; init; } = "";
        public bool IsCorrect { get; init; }
        public string CorrectAnswerKey { get; init; } = "";
        public Dictionary<string, string> ShuffledAnswers { get; init; } = new();
    }

    public static bool ShouldClearAnswerFlashOnGet(HttpRequest request)
    {
        if (request.Query.ContainsKey("next"))
            return true;
        if (!string.IsNullOrEmpty(request.Query["mode"]))
            return true;
        return !string.IsNullOrEmpty(request.Query["difficulty"]);
    }

    public void SaveAnswerFlash(ISession session, PracticeAnswerFlash flash)
    {
        session.SetString(FlashQuestionKey, flash.QuestionImage ?? "");
        session.SetString(FlashSelectedKey, flash.SelectedAnswer ?? "");
        session.SetString(FlashAnswersKey,
            JsonSerializer.Serialize(flash.ShuffledAnswers ?? new Dictionary<string, string>(), AppJson.Options));
        session.SetString(FlashCorrectKeyKey, flash.CorrectAnswerKey ?? "");
        session.SetString(FlashCorrectKey, flash.IsCorrect ? "1" : "0");
        session.SetString(LastSubmittedQuestionKey, flash.QuestionImage ?? "");
    }

    public void ClearAnswerFlash(ISession session)
    {
        session.Remove(FlashQuestionKey);
        session.Remove(FlashSelectedKey);
        session.Remove(FlashAnswersKey);
        session.Remove(FlashCorrectKey);
        session.Remove(FlashCorrectKeyKey);
    }

    public bool TryReadAnswerFlash(ISession session, out PracticeAnswerFlash flash)
    {
        var questionImage = session.GetString(FlashQuestionKey);
        if (string.IsNullOrWhiteSpace(questionImage))
        {
            flash = null!;
            return false;
        }

        flash = new PracticeAnswerFlash
        {
            QuestionImage = questionImage,
            SelectedAnswer = session.GetString(FlashSelectedKey) ?? "",
            IsCorrect = session.GetString(FlashCorrectKey) == "1",
            CorrectAnswerKey = session.GetString(FlashCorrectKeyKey) ?? "",
            ShuffledAnswers = DeserializeAnswerOptions(session.GetString(FlashAnswersKey))
        };
        return true;
    }

    public bool TryHydrateFromPracticeSession(ISession session, out PracticeQuestionState state)
    {
        var (questionImage, correctKey, options) = LoadPracticeFromSession(session);
        if (string.IsNullOrEmpty(correctKey) || options.Count == 0)
        {
            state = null!;
            return false;
        }

        state = new PracticeQuestionState
        {
            QuestionImage = questionImage,
            CorrectAnswerKey = correctKey,
            ShuffledAnswers = options
        };
        return true;
    }

    public bool TryHydrateFromFlash(ISession session, bool persistToPractice, out PracticeQuestionState state)
    {
        var optionsJson = session.GetString(FlashAnswersKey);
        var correctKey = session.GetString(FlashCorrectKeyKey);
        if (string.IsNullOrWhiteSpace(optionsJson) || string.IsNullOrWhiteSpace(correctKey))
        {
            state = null!;
            return false;
        }

        var options = DeserializeAnswerOptions(optionsJson);
        if (options.Count == 0)
        {
            state = null!;
            return false;
        }

        if (persistToPractice)
            SavePracticeQuestionState(session, session.GetString(FlashQuestionKey) ?? "", options, correctKey);

        state = new PracticeQuestionState
        {
            QuestionImage = session.GetString(FlashQuestionKey) ?? "",
            CorrectAnswerKey = correctKey,
            ShuffledAnswers = options
        };
        return true;
    }

    public void ClearQuizProgressSession(ISession session)
    {
        session.SetInt32("CurrentStreak", 0);
        session.Remove("RecentQuestions");
        session.Remove("DailyDate");
        session.Remove("DailyQuestionIndex");
        session.Remove("DailyScore");
        session.Remove("DailyQuestions");
        session.Remove("PendingAchievements");
        session.Remove("CheaterCount");
        session.SetInt32("RapidTotal", 0);
        session.SetInt32("RapidCorrect", 0);
        session.SetString("SessionStart", DateTime.UtcNow.ToString("o"));
        session.Remove(LastSubmittedQuestionKey);
        ClearAnswerFlash(session);
    }

    public void EnrichReportFromFlash(ISession session, ErrorReportBuilder.ReportPayload payload)
    {
        var flashQuestion = session.GetString(FlashQuestionKey);
        if (!string.Equals(flashQuestion, payload.QuestionImage, StringComparison.OrdinalIgnoreCase))
            return;

        var optionsJson = session.GetString(FlashAnswersKey);
        var correctKey = session.GetString(FlashCorrectKeyKey);
        if (string.IsNullOrWhiteSpace(optionsJson) || string.IsNullOrWhiteSpace(correctKey))
            return;

        var options = DeserializeAnswerOptions(optionsJson);
        if (payload.AnswersDict.Count == 0)
            payload.AnswersDict = options;
        if (string.IsNullOrWhiteSpace(payload.CorrectAnswer)
            && options.TryGetValue(correctKey, out var correctFile))
            payload.CorrectAnswer = correctFile;

        if (string.IsNullOrWhiteSpace(payload.SelectedAnswer))
        {
            var selected = session.GetString(FlashSelectedKey);
            if (!string.IsNullOrWhiteSpace(selected))
                payload.SelectedAnswer = selected;
        }
    }

    public static string GetPracticeModeLabel(string practiceMode, string practiceDifficulty)
    {
        return practiceMode switch
        {
            "weak" => "תרגול חולשות",
            "review" => "סקירת טעויות",
            "daily" => "אתגר יומי",
            "normal" => practiceDifficulty switch
            {
                "easy" => "תרגול — קל",
                "medium" => "תרגול — בינוני",
                "hard" => "תרגול — קשה",
                _ => "תרגול חופשי"
            },
            _ => practiceDifficulty switch
            {
                "easy" => "תרגול — קל",
                "medium" => "תרגול — בינוני",
                "hard" => "תרגול — קשה",
                _ => "תרגול חופשי"
            }
        };
    }
}
