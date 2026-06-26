using System;

namespace NoodlesSimulator.Models;

public class QuestionDifficulty
{
    public string QuestionFile { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "medium";
    public decimal SuccessRate { get; set; }
    public int TotalAttempts { get; set; }
    public int CorrectAttempts { get; set; }
    public DateTime LastUpdated { get; set; }
    public bool ManualOverride { get; set; }
    public DateTime CreatedAt { get; set; }
}
