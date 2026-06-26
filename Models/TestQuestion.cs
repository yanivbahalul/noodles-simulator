using System.Collections.Generic;

namespace NoodlesSimulator.Models;

public class TestQuestion
{
    public string Question { get; set; }
    public Dictionary<string, string> Answers { get; set; } = new();
}
