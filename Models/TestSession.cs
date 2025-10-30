using System;
using System.Collections.Generic;

namespace NoodlesSimulator.Models
{
    public class TestSession
    {
        public string Token { get; set; }
        public string Username { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime? CompletedUtc { get; set; }
        public string Status { get; set; } // "active", "completed", "expired"
        public string QuestionsJson { get; set; }
        public string AnswersJson { get; set; }
        public int CurrentIndex { get; set; }
        public int Score { get; set; }
        public int MaxScore { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

