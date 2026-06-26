using System.Collections.Generic;

namespace NoodlesSimulator.Models;

public class DifficultyConfig
{
    public string Difficulty { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public List<string> Questions { get; set; } = new();
}
