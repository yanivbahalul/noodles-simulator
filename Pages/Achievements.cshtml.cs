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

    public AchievementsModel(UserProgressService progress)
    {
        _progress = progress;
    }

    public string Username { get; set; } = "";
    public List<(AchievementDefinition Def, bool Unlocked)> Items { get; set; } = new();

    public Task OnGetAsync()
    {
        Username = HttpContext.Session.GetString("Username") ?? "";
        if (string.IsNullOrEmpty(Username))
            return Task.CompletedTask;

        var data = _progress.Load(Username);
        var unlocked = new HashSet<string>(data.Achievements, System.StringComparer.OrdinalIgnoreCase);
        Items = AchievementCatalog.All
            .Select(d => (d, unlocked.Contains(d.Key)))
            .ToList();
        return Task.CompletedTask;
    }
}
