using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace NoodlesSimulator.Models;

// ponytail: keep in sync with wwwroot/js/html-utils.js and tools/check_question_label_sync.py
internal static class QuestionLabel
{
    private static readonly Regex ScreenshotName = new(
        @"^Screenshot at (\w{3}) (\d{1,2}) (\d{2})-(\d{2})-(\d{2})$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<string, string> MonthMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Jan"] = "01", ["Feb"] = "02", ["Mar"] = "03", ["Apr"] = "04",
            ["May"] = "05", ["Jun"] = "06", ["Jul"] = "07", ["Aug"] = "08",
            ["Sep"] = "09", ["Oct"] = "10", ["Nov"] = "11", ["Dec"] = "12"
        };

    internal static string Format(string questionId)
    {
        if (string.IsNullOrWhiteSpace(questionId)) return "—";

        var name = Path.GetFileName(questionId);
        name = Regex.Replace(name, @"\.(png|jpg|jpeg|webp)$", "", RegexOptions.IgnoreCase);

        var match = ScreenshotName.Match(name);
        if (match.Success)
        {
            var mon = MonthMap.TryGetValue(match.Groups[1].Value, out var m) ? m : match.Groups[1].Value;
            var day = match.Groups[2].Value.PadLeft(2, '0');
            return $"{day}/{mon} {match.Groups[3].Value}:{match.Groups[4].Value}";
        }

        if (name.Length > 28)
            return name[..25] + "…";

        return name;
    }
}
