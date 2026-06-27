using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Hosting;
using NoodlesSimulator.Models;
using NoodlesSimulator.Services;

namespace NoodlesSimulator.Pages;

public class SystemCheckModel : PageModel
{
    private readonly SystemVerificationService _verification;
    private readonly IHostEnvironment _env;

    public SystemCheckModel(SystemVerificationService verification, IHostEnvironment env)
    {
        _verification = verification;
        _env = env;
    }

    public string EnvironmentName { get; private set; } = "";
    public string PlanJson { get; private set; } = "[]";

    public IActionResult OnGet()
    {
        if (!IsAdmin())
            return RedirectToPage("/Login");

        EnvironmentName = _env.EnvironmentName;
        PlanJson = JsonSerializer.Serialize(_verification.GetPlan(), AppJson.Options);
        return Page();
    }

    public async Task OnGetStreamAsync(CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Response.WriteAsync("Unauthorized");
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache, no-store";
        Response.Headers.Connection = "keep-alive";

        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        await foreach (var evt in _verification.RunAsync(baseUrl, cancellationToken))
        {
            var json = JsonSerializer.Serialize(evt, AppJson.Options);
            await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    private bool IsAdmin() =>
        string.Equals(HttpContext.Session.GetString("IsAdmin"), "1", StringComparison.Ordinal);
}
