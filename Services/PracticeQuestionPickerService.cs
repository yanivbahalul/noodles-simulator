using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public class PracticePickRequest
{
    public string Mode { get; init; } = "normal";
    public string Difficulty { get; init; } = "";
    public string Username { get; init; } = "";
    public IReadOnlyList<string> RecentQuestions { get; init; } = Array.Empty<string>();
    public string LastSubmittedQuestion { get; init; } = "";
    public string DailyQuestionsJson { get; init; } = "";
    public int DailyQuestionIndex { get; init; }
    public int DailyTotal { get; init; } = 10;
}

public class PracticePickResult
{
    public string QuestionImage { get; init; } = "";
    public Dictionary<string, string> ShuffledAnswers { get; init; } = new();
    public string CorrectAnswerKey { get; init; } = "";
    public string? NewDailyQuestionsJson { get; init; }
}

public class PracticeQuestionPickerService
{
    private static readonly object QuestionRateLock = new();
    private static readonly Dictionary<string, List<DateTime>> QuestionShownTimes = new();

    private static readonly object BagLock = new();
    private static List<int>? BagOrder;
    private static int BagIndex = 0;
    private static int BagSourceCount = 0;
    private static DateTime BagBuiltAt;
    private static readonly TimeSpan BagTtl = TimeSpan.FromMinutes(30);
    private static readonly Dictionary<string, int> GroupShownCount = new();

    private readonly QuestionGroupLoader? _questionGroups;
    private readonly QuestionDifficultyService? _difficultyService;
    private readonly UserProgressService? _userProgress;

    public PracticeQuestionPickerService(
        QuestionGroupLoader? questionGroups = null,
        QuestionDifficultyService? difficultyService = null,
        UserProgressService? userProgress = null)
    {
        _questionGroups = questionGroups;
        _difficultyService = difficultyService;
        _userProgress = userProgress;
    }

    public static (int trackedQuestions, int throttledNow) GetThrottleSnapshot()
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddHours(-1);
        lock (QuestionRateLock)
        {
            int throttled = 0;
            foreach (var kv in QuestionShownTimes)
            {
                var list = kv.Value;
                list.RemoveAll(t => t < cutoff);
                if (list.Count >= 3) throttled++;
            }

            return (QuestionShownTimes.Count, throttled);
        }
    }

    public static Dictionary<int, int> GetGroupShownHistogramSnapshot()
    {
        lock (BagLock)
        {
            var hist = new Dictionary<int, int>();
            foreach (var kv in GroupShownCount)
            {
                var c = kv.Value;
                if (!hist.ContainsKey(c)) hist[c] = 0;
                hist[c]++;
            }

            return hist;
        }
    }

    public static void RecordQuestionShown(string questionImage)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddHours(-1);
        lock (QuestionRateLock)
        {
            if (!QuestionShownTimes.TryGetValue(questionImage, out var times))
            {
                times = new List<DateTime>();
                QuestionShownTimes[questionImage] = times;
            }

            times.RemoveAll(t => t < cutoff);
            times.Add(now);
        }
    }

    public static void IncrementGroupShown(string questionImage)
    {
        lock (BagLock)
        {
            if (!GroupShownCount.ContainsKey(questionImage))
                GroupShownCount[questionImage] = 0;
            GroupShownCount[questionImage]++;
        }
    }

    public async Task<List<List<string>>> ListAllGroupsAsync()
    {
        if (_questionGroups == null)
            return new List<List<string>>();
        return await _questionGroups.ListAllGroupsAsync();
    }

    public async Task<PracticePickResult?> PickQuestionAsync(PracticePickRequest request)
    {
        var grouped = await ListAllGroupsAsync();
        if (grouped.Count == 0)
            return null;

        List<string>? chosen;
        string? newDailyJson = null;

        if (request.Mode == "daily")
        {
            var (group, dailyJson) = PickDailyQuestion(grouped, request);
            chosen = group;
            newDailyJson = dailyJson;
        }
        else if (request.Mode == "review" && _userProgress != null)
        {
            chosen = await PickReviewQuestionAsync(grouped, request);
        }
        else
        {
            var pool = await FilterGroupsAsync(grouped, request.Mode, request.Difficulty, request.Username);
            if (pool.Count == 0) pool = grouped;
            chosen = await PickFromPoolAsync(pool, request, useSpaced: request.Mode == "review");
        }

        if (chosen == null || chosen.Count < 2)
            return null;

        var correct = chosen[1];
        var wrong = chosen.Skip(2).Take(3).ToList();
        var shuffled = AnswerOptionShuffle.Create(correct, wrong);

        return new PracticePickResult
        {
            QuestionImage = chosen[0],
            ShuffledAnswers = shuffled.Options,
            CorrectAnswerKey = shuffled.CorrectKey,
            NewDailyQuestionsJson = newDailyJson
        };
    }

    private async Task<List<List<string>>> FilterGroupsAsync(
        List<List<string>> grouped,
        string mode,
        string difficulty,
        string username)
    {
        var pool = grouped;

        if (!string.IsNullOrEmpty(difficulty) && _difficultyService != null)
        {
            var allowed = await _difficultyService.GetQuestionsByDifficultyAsync(difficulty);
            if (allowed.Count > 0)
            {
                var allowedSet = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
                pool = pool.Where(g => g.Count > 0 && allowedSet.Contains(g[0])).ToList();
            }
        }

        if (mode == "weak" && _userProgress != null && !string.IsNullOrEmpty(username))
        {
            var weak = new HashSet<string>(await _userProgress.GetWeakQuestionsAsync(username), StringComparer.OrdinalIgnoreCase);
            if (weak.Count > 0)
                pool = pool.Where(g => g.Count > 0 && weak.Contains(g[0])).ToList();
        }

        return pool;
    }

    private async Task<List<string>?> PickReviewQuestionAsync(List<List<string>> grouped, PracticePickRequest request)
    {
        if (_userProgress == null || string.IsNullOrEmpty(request.Username))
            return grouped[RandomNumberGenerator.GetInt32(grouped.Count)];

        var progress = await _userProgress.LoadAsync(request.Username);
        var mistakes = progress.SessionMistakes;
        if (mistakes.Count == 0)
            return await PickFromPoolAsync(grouped, request, useSpaced: true);

        var mistakeSet = new HashSet<string>(mistakes, StringComparer.OrdinalIgnoreCase);
        var reviewGroups = grouped.Where(g => g.Count > 0 && mistakeSet.Contains(g[0])).ToList();
        if (reviewGroups.Count == 0)
            return await PickFromPoolAsync(grouped, request, useSpaced: true);

        var withoutLast = reviewGroups
            .Where(g => !string.Equals(g[0], request.LastSubmittedQuestion, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (withoutLast.Count > 0)
            reviewGroups = withoutLast;

        return reviewGroups[RandomNumberGenerator.GetInt32(reviewGroups.Count)];
    }

    private static (List<string>? group, string? newDailyJson) PickDailyQuestion(
        List<List<string>> grouped,
        PracticePickRequest request)
    {
        List<string> dailyList;

        if (string.IsNullOrWhiteSpace(request.DailyQuestionsJson))
        {
            var today = UserProgressService.TodayKey();
            var seed = today.GetHashCode();
            var rng = new Random(seed);
            var indices = Enumerable.Range(0, grouped.Count)
                .OrderBy(_ => rng.Next())
                .Take(Math.Min(request.DailyTotal, grouped.Count))
                .ToList();
            dailyList = indices.Select(i => grouped[i][0]).ToList();
            var newJson = JsonSerializer.Serialize(dailyList, AppJson.Options);
            var idx = request.DailyQuestionIndex;
            if (idx >= dailyList.Count)
                return (grouped[0], newJson);

            var questionFile = dailyList[idx];
            var group = grouped.FirstOrDefault(g =>
                g.Count > 0 && string.Equals(g[0], questionFile, StringComparison.OrdinalIgnoreCase));
            return (group ?? grouped[RandomNumberGenerator.GetInt32(grouped.Count)], newJson);
        }

        dailyList = JsonSerializer.Deserialize<List<string>>(request.DailyQuestionsJson, AppJson.Options)
                    ?? new List<string>();
        var dailyIdx = request.DailyQuestionIndex;
        if (dailyIdx >= dailyList.Count)
            return (grouped[0], null);

        var file = dailyList[dailyIdx];
        var picked = grouped.FirstOrDefault(g =>
            g.Count > 0 && string.Equals(g[0], file, StringComparison.OrdinalIgnoreCase));
        return (picked ?? grouped[RandomNumberGenerator.GetInt32(grouped.Count)], null);
    }

    private async Task<List<string>?> PickFromPoolAsync(List<List<string>> pool, PracticePickRequest request, bool useSpaced)
    {
        if (pool.Count == 0) return null;

        var recent = request.RecentQuestions ?? Array.Empty<string>();
        var lastSubmitted = request.LastSubmittedQuestion ?? "";

        if (useSpaced && _userProgress != null && !string.IsNullOrEmpty(request.Username))
        {
            var progress = await _userProgress.LoadAsync(request.Username);
            var withPriority = pool
                .Where(g => g.Count > 0)
                .Select(g => (g, priority: _userProgress.GetSpacedPriority(progress, g[0])))
                .GroupBy(x => x.priority)
                .OrderByDescending(g => g.Key)
                .First();

            pool = withPriority.Select(x => x.g).ToList();
        }

        var eligible = pool.Where(g => g.Count > 0
            && !IsQuestionThrottled(g[0])
            && !recent.Contains(g[0])
            && !string.Equals(g[0], lastSubmitted, StringComparison.OrdinalIgnoreCase)).ToList();

        if (eligible.Count == 0)
        {
            eligible = pool.Where(g => g.Count > 0
                && !string.Equals(g[0], lastSubmitted, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (eligible.Count == 0)
            eligible = pool.Where(g => g.Count > 0).ToList();

        int chosenIdx;
        lock (BagLock)
        {
            var now = DateTime.UtcNow;
            var needRebuild = BagOrder == null
                              || BagSourceCount != eligible.Count
                              || BagIndex >= (BagOrder?.Count ?? 0)
                              || (now - BagBuiltAt) > BagTtl;
            if (needRebuild)
            {
                var order = Enumerable.Range(0, eligible.Count).ToList();
                ListShuffle.FisherYates(order);
                BagOrder = order;
                BagIndex = 0;
                BagSourceCount = eligible.Count;
                BagBuiltAt = now;
            }

            chosenIdx = BagOrder![BagIndex % BagOrder.Count];
            BagIndex++;
        }

        return eligible[chosenIdx % eligible.Count];
    }

    private static bool IsQuestionThrottled(string questionImage)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddHours(-1);
        lock (QuestionRateLock)
        {
            if (!QuestionShownTimes.TryGetValue(questionImage, out var times))
                return false;
            times.RemoveAll(t => t < cutoff);
            return times.Count >= 3;
        }
    }
}
