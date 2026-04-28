using System;
using System.Collections.Generic;

namespace NoodlesSimulator.Models
{
    public class TestQuestion
    {
        public string Question { get; set; }
        public Dictionary<string, string> Answers { get; set; } = new();
    }

    public class TestAnswer
    {
        public string SelectedKey { get; set; }
        public bool IsCorrect { get; set; }
    }

    public class TestState
    {
        public DateTime StartedUtc { get; set; }
        public List<TestQuestion> Questions { get; set; } = new();
        public List<TestAnswer> Answers { get; set; } = new();
        public int CurrentIndex { get; set; }
    }

    public class DifficultyConfig
    {
        public string Difficulty { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public List<string> Questions { get; set; } = new();
    }
}
