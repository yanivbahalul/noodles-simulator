using System;

namespace NoodlesSimulator.Models;

public class QuestionExplanationRating
{
    public Guid Id { get; set; }
    public string QuestionFile { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int Stars { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class QuestionExplanationRatingSummary
{
    public string QuestionFile { get; init; } = string.Empty;
    public double AvgStars { get; init; }
    public int RatingCount { get; init; }
    public int LowCount { get; init; }
    public string[] RecentFeedback { get; init; } = Array.Empty<string>();
    public double UrgencyScore { get; init; }
}
