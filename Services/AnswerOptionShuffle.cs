using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public static class AnswerOptionShuffle
{
    public sealed class Result
    {
        public Dictionary<string, string> Options { get; init; } = new();
        public string CorrectKey { get; init; } = "";
    }

    public static Result Create(string correctImage, IEnumerable<string> wrongImages)
    {
        var options = new List<(string Key, string Image)>
        {
            (GenerateKey(), correctImage)
        };

        var correctKey = options[0].Key;

        foreach (var wrong in wrongImages.Where(x => !string.IsNullOrWhiteSpace(x)))
            options.Add((GenerateKey(), wrong));

        ListShuffle.FisherYates(options);

        return new Result
        {
            CorrectKey = correctKey,
            Options = options.ToDictionary(x => x.Key, x => x.Image)
        };
    }

    public static string ResolveCorrectKey(TestQuestion question)
    {
        if (!string.IsNullOrEmpty(question.CorrectKey))
            return question.CorrectKey;
        if (question.Answers != null && question.Answers.ContainsKey("correct"))
            return "correct";
        return "";
    }

    public static bool IsSelectedCorrect(TestQuestion question, string selectedKey)
    {
        if (string.IsNullOrEmpty(selectedKey) || question.Answers == null || !question.Answers.ContainsKey(selectedKey))
            return false;

        var correctKey = ResolveCorrectKey(question);
        return !string.IsNullOrEmpty(correctKey) && selectedKey == correctKey;
    }

    private static string GenerateKey() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
}
