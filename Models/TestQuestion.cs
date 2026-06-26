using System.Collections.Generic;

namespace NoodlesSimulator.Models;

public class TestQuestion
{
    public string Question { get; set; }
    /// <summary>Opaque token for the correct option — never "correct".</summary>
    public string CorrectKey { get; set; }
    public Dictionary<string, string> Answers { get; set; } = new();
}
