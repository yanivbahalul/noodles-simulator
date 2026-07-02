using System;
using System.Collections.Generic;

namespace NoodlesSimulator.Models;

public static class AppNotices
{
    private static readonly string[] PriorityOrder = Array.Empty<string>();

    private static readonly HashSet<string> ValidIds = new(StringComparer.Ordinal);

    public static bool IsValid(string? noticeId) =>
        !string.IsNullOrWhiteSpace(noticeId) && ValidIds.Contains(noticeId);

    public static string? GetFirstUndismissed(IReadOnlyList<string>? dismissed)
    {
        foreach (var id in PriorityOrder)
        {
            if (dismissed == null || !dismissed.Contains(id, StringComparer.Ordinal))
                return id;
        }

        return null;
    }
}
