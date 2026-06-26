using System;
using System.Collections.Generic;

namespace NoodlesSimulator.Models;

public class User
{
    public string Username { get; set; }
    public string Password { get; set; }
    public bool IsAdmin { get; set; }
    public int CorrectAnswers { get; set; }
    public int TotalAnswered { get; set; }
    public bool IsCheater { get; set; }
    public bool IsBanned { get; set; }
    public DateTime? LastSeen { get; set; }
    public List<string> DismissedNotices { get; set; }
}
