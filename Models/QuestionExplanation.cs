using System;

namespace NoodlesSimulator.Models;

public class QuestionExplanation
{
    public string QuestionFile { get; set; } = string.Empty;
    public string VideoPath { get; set; } = string.Empty;
    public string ScriptJson { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime? GeneratedAt { get; set; }
}

public static class QuestionExplanationStatus
{
    public const string Pending = "pending";
    public const string Ready = "ready";
    public const string Failed = "failed";
    public const string NeedsReview = "needs_review";
}
