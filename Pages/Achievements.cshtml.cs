using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Models;
using NoodlesSimulator.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NoodlesSimulator.Pages;

public class AchievementsModel : PageModel
{
    private readonly UserProgressService _progress;
    private readonly AchievementService _achievements;
    private readonly AuthService _auth;

    public AchievementsModel(UserProgressService progress, AchievementService achievements, AuthService auth)
    {
        _progress = progress;
        _achievements = achievements;
        _auth = auth;
    }

    public string Username { get; set; } = "";
    public int UnlockedCount { get; set; }
    public int TotalCount { get; set; }
    public int DailyStreakDays { get; set; }
    public int DailyChallengesCompleted { get; set; }
    public int DailyPerfectCount { get; set; }
    public List<AchievementGroup> Groups { get; set; } = new();

    public class AchievementGroup
    {
        public string Title { get; set; } = "";
        public List<(AchievementDefinition Def, bool Unlocked)> Items { get; set; } = new();
    }

    public async Task OnGetAsync()
    {
        Username = HttpContext.Session.GetString("Username") ?? "";
        if (string.IsNullOrEmpty(Username))
            return;

        var user = await _auth.GetUserAsync(Username);
        if (user != null)
            await _achievements.CheckAllAchievementsAsync(Username, user);

        var data = await _progress.LoadAsync(Username);
        DailyStreakDays = data.DailyStreakDays;
        DailyChallengesCompleted = data.DailyChallengesCompleted;
        DailyPerfectCount = data.DailyPerfectCount;
        var unlocked = new HashSet<string>(data.Achievements, System.StringComparer.OrdinalIgnoreCase);
        var items = AchievementCatalog.All
            .Select(d => (Def: d, Unlocked: unlocked.Contains(d.Key)))
            .ToList();

        UnlockedCount = items.Count(i => i.Unlocked);
        TotalCount = items.Count;

        Groups = AchievementCatalog.CategoryTitles
            .Select(kv => new AchievementGroup
            {
                Title = kv.Value,
                Items = items.Where(i => i.Def.Category == kv.Key).ToList()
            })
            .Where(g => g.Items.Count > 0)
            .ToList();
    }
}
