using System;
using System.Collections.Generic;
using System.Linq;

namespace NoodlesSimulator.Models;

public static class AppNotices
{
    public const string June2026Update = "update-2026-06";
    public const string ExamFix = "exam-fix-2026-06";

    private static readonly string[] PriorityOrder = { June2026Update, ExamFix };

    private static readonly HashSet<string> ValidIds = new(StringComparer.Ordinal)
    {
        June2026Update,
        ExamFix
    };

    public static bool IsValid(string? noticeId) =>
        !string.IsNullOrWhiteSpace(noticeId) && ValidIds.Contains(noticeId);

    public static string? GetFirstUndismissed(IReadOnlyList<string>? dismissed)
    {
        if (dismissed == null || dismissed.Count == 0)
            return PriorityOrder[0];

        foreach (var id in PriorityOrder)
        {
            if (!dismissed.Contains(id, StringComparer.Ordinal))
                return id;
        }

        return null;
    }
}
