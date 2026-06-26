using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Services;
using System;
using System.Threading.Tasks;

namespace NoodlesSimulator.Pages;

public class SystemCheckModel : PageModel
{
    private readonly SystemHealthService _health;

    public SystemCheckModel(SystemHealthService health)
    {
        _health = health;
    }

    public SystemHealthReport Report { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!string.Equals(HttpContext.Session.GetString("IsAdmin"), "1", StringComparison.Ordinal))
            return RedirectToPage("/Login");

        Report = await _health.RunAsync();
        return Page();
    }
}
