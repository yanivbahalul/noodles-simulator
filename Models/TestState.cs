using System;
using System.Collections.Generic;

namespace NoodlesSimulator.Models;

public class TestState
{
    public DateTime StartedUtc { get; set; }
    public List<TestQuestion> Questions { get; set; } = new();
    public List<TestAnswer> Answers { get; set; } = new();
    public int CurrentIndex { get; set; }
}
