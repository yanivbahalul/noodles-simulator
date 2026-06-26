using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using NoodlesSimulator.Models;

#nullable enable

namespace NoodlesSimulator.Services
{
    public static class ErrorReportBuilder
    {
        public sealed class ReportPayload
        {
            public string Username { get; init; } = "Unknown";
            public string QuestionImage { get; set; } = string.Empty;
            public string CorrectAnswer { get; set; } = string.Empty;
            public string Explanation { get; set; } = string.Empty;
            public string SelectedAnswer { get; set; } = string.Empty;
            public Dictionary<string, string> AnswersDict { get; set; } = new();
            public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
        }

        public static ReportPayload? TryParse(JsonDocument doc, string? sessionUsername)
        {
            var root = doc.RootElement;
            var username = WebUtility.HtmlEncode(sessionUsername ?? "Unknown");
            var explanation = WebUtility.HtmlEncode(GetJsonString(root, "explanation") ?? string.Empty);

            var payload = new ReportPayload
            {
                Username = username,
                QuestionImage = NormalizeImageReference(GetJsonString(root, "questionImage")),
                CorrectAnswer = NormalizeImageReference(GetJsonString(root, "correctAnswer")),
                Explanation = explanation,
                SelectedAnswer = GetJsonString(root, "selectedAnswer") ?? string.Empty,
                AnswersDict = ParseAnswersDict(GetJsonString(root, "answers"))
            };

            return payload;
        }

        public static string BuildSubject(string username) =>
            $"דיווח טעות חדשה מהמשתמש {username}";

        public static string BuildHtmlBody(ReportPayload payload, string baseUrl)
        {
            var answersList = BuildAnswersListHtml(payload);
            var questionViewUrl = BuildQuestionViewUrl(baseUrl, payload);

            return $@"
<!DOCTYPE html>
<html dir='rtl' lang='he'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>דיווח שגיאה</title>
</head>
<body style='margin: 0; padding: 0; background-color: #f5f5f5; direction: rtl;'>
    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f9f9f9; direction: rtl;'>
        <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 20px; border-radius: 10px 10px 0 0; text-align: center; direction: rtl;'>
            <h2 style='color: white; margin: 0; direction: rtl; unicode-bidi: embed;'>📩 דיווח חדש התקבל מהמערכת</h2>
        </div>
        <div style='background-color: white; padding: 25px; border-radius: 0 0 10px 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); direction: rtl; text-align: right;'>
            <p style='font-size: 16px; color: #333; line-height: 1.8; direction: rtl; text-align: right; unicode-bidi: embed;'>
                <strong>👤 משתמש:</strong> {payload.Username}<br/>
                <strong>🕓 תאריך:</strong> {payload.TimestampUtc:dd/MM/yyyy HH:mm:ss}<br/>
            </p>
            <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'/>
            <div style='margin: 20px 0; padding: 20px; background-color: #f8f9fa; border-radius: 8px; direction: rtl; text-align: right;'>
                <p style='font-size: 16px; color: #333; margin-bottom: 15px; direction: rtl; unicode-bidi: embed;'>
                    <strong>❓ שם קובץ השאלה:</strong> {payload.QuestionImage}
                </p>
                <p style='font-size: 16px; color: #333; margin-bottom: 15px; direction: rtl; unicode-bidi: embed;'>
                    <strong>📝 תשובות אפשריות:</strong><br/>
                    <span style='font-size: 14px; line-height: 1.8;'>{answersList}</span>
                </p>
            </div>
            <div style='background-color: #fff3cd; border-right: 4px solid #ffc107; padding: 15px; margin: 20px 0; border-radius: 5px; direction: rtl; text-align: right;'>
                <strong style='unicode-bidi: embed;'>💬 סיבה:</strong> <span style='unicode-bidi: embed;'>{payload.Explanation}</span>
            </div>
            <div style='margin: 25px 0; padding: 20px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); border-radius: 8px; text-align: center;'>
                <a href='{questionViewUrl}'
                   target='_blank'
                   style='display: inline-block; padding: 15px 30px; background-color: white; color: #667eea; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 18px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); transition: all 0.3s;'>
                    🔍 להצגת השאלה לחץ כאן
                </a>
                <p style='color: white; margin-top: 15px; font-size: 14px; direction: rtl; unicode-bidi: embed;'>
                    הקישור יפתח את השאלה עם כל התשובות בעמוד נפרד באתר
                </p>
            </div>
            <hr style='border: none; border-top: 1px solid #eee; margin: 25px 0;'/>
            <p style='text-align: center; color: #888; font-size: 14px; direction: rtl; unicode-bidi: embed;'>
                <strong>מערכת: Noodles Simulator</strong><br/>
                🎮 Find your limits. Or crash into them.
            </p>
        </div>
    </div>
</body>
</html>";
        }

        private static string BuildAnswersListHtml(ReportPayload payload)
        {
            var abcd = new[] { "A", "B", "C", "D" };
            var allAnswers = payload.AnswersDict.Values.ToList();
            var correctIdx = allAnswers.IndexOf(payload.CorrectAnswer);

            var selectedAnswerValue = string.Empty;
            if (!string.IsNullOrWhiteSpace(payload.SelectedAnswer)
                && payload.AnswersDict.TryGetValue(payload.SelectedAnswer, out var selectedValue))
            {
                selectedAnswerValue = selectedValue;
            }

            var answersList = new StringBuilder();
            var safeCorrectAnswer = WebUtility.HtmlEncode(payload.CorrectAnswer ?? string.Empty);
            answersList.Append($"<span style='color: #28a745; font-weight: bold;'>A:</span> <span style='color: #28a745; font-weight: bold;'>{safeCorrectAnswer}</span><br/>");

            var distractors = allAnswers.Where((_, i) => i != correctIdx).ToList();
            for (var i = 0; i < Math.Min(3, distractors.Count); i++)
            {
                var letter = abcd[i + 1];
                var distractor = WebUtility.HtmlEncode(distractors[i] ?? string.Empty);
                var isSelected = selectedAnswerValue == distractors[i];
                var style = isSelected ? "font-weight: bold; color: #ffc107;" : "";
                answersList.Append($"<span style='{style}'>{letter}: {distractor}</span><br/>");
            }

            return answersList.ToString();
        }

        private static string BuildQuestionViewUrl(string baseUrl, ReportPayload payload)
        {
            var queryParams = new NameValueCollection { ["id"] = payload.QuestionImage };
            if (!string.IsNullOrWhiteSpace(payload.SelectedAnswer)
                && payload.AnswersDict.ContainsKey(payload.SelectedAnswer))
            {
                queryParams.Add("selected", payload.SelectedAnswer);
            }
            queryParams.Add("correct", "correct");

            var queryString = string.Join("&",
                queryParams.AllKeys.Select(key => $"{Uri.EscapeDataString(key!)}={Uri.EscapeDataString(queryParams[key!])}"));
            return $"{baseUrl}/QuestionView?{queryString}";
        }

        private static Dictionary<string, string> ParseAnswersDict(string? answersJson)
        {
            if (string.IsNullOrWhiteSpace(answersJson))
                return new Dictionary<string, string>();

            try
            {
                var answersDict = JsonSerializer.Deserialize<Dictionary<string, string>>(answersJson, AppJson.Options)
                                  ?? new Dictionary<string, string>();
                var cleaned = new Dictionary<string, string>();
                foreach (var kv in answersDict)
                    cleaned[kv.Key] = NormalizeImageReference(kv.Value);
                return cleaned;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ErrorReportBuilder ParseAnswersDict Error] {ex.Message}");
                return new Dictionary<string, string>();
            }
        }

        private static string NormalizeImageReference(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || !value.Contains("token=", StringComparison.Ordinal))
                return value ?? string.Empty;

            var extractedName = SupabaseStorageService.ExtractFileNameFromSignedUrl(value);
            return string.IsNullOrWhiteSpace(extractedName) ? value : extractedName;
        }

        private static string? GetJsonString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var value))
                return null;
            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        }
    }
}
