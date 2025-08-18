using System;

namespace NoodlesSimulator.Models
{
    public class User
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public int CorrectAnswers { get; set; }
        public int TotalAnswered { get; set; }
        public bool IsCheater { get; set; } = false;
        public bool IsBanned { get; set; } = false;
        public DateTime? LastSeen { get; set; }
    }
}
