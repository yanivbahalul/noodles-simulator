using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using NoodlesSimulator.Models;
using NoodlesSimulator.Services;

namespace NoodlesSimulator.Pages;

public class SystemCheckModel : PageModel
{
    private readonly SystemVerificationService _verification;
    private readonly IHostEnvironment _env;
    private readonly IConfiguration _configuration;

    public SystemCheckModel(SystemVerificationService verification, IHostEnvironment env, IConfiguration configuration)
    {
        _verification = verification;
        _env = env;
        _configuration = configuration;
    }

    public string EnvironmentName { get; private set; } = "";
    public string PlanJson { get; private set; } = "[]";

    public IActionResult OnGet()
    {
        if (!IsAdmin())
            return RedirectToPage("/Login");

        EnvironmentName = _env.EnvironmentName;
        PlanJson = JsonSerializer.Serialize(_verification.GetPlan(), AppJson.Web);
        return Page();
    }

    public async Task OnGetStreamAsync(CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            Response.ContentType = "text/plain; charset=utf-8";
            await Response.WriteAsync("Unauthorized", cancellationToken);
            return;
        }

        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache, no-store";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        var probeBaseUrl = ResolveProbeBaseUrl();
        var cookieHeader = BuildForwardedCookieHeader();
        var username = HttpContext.Session.GetString("Username") ?? "";

        try
        {
            await foreach (var evt in _verification.RunAsync(probeBaseUrl, cookieHeader, username, cancellationToken))
            {
                await WriteEventAsync(evt, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected.
        }
        catch (Exception ex)
        {
            await WriteEventAsync(new SystemVerificationEvent
            {
                Phase = "error",
                Detail = ex.Message
            }, CancellationToken.None);
        }
    }

    private async Task WriteEventAsync(SystemVerificationEvent evt, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(evt, AppJson.Web);
        await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private string ResolveProbeBaseUrl()
    {
        var port = Environment.GetEnvironmentVariable("PORT");
        if (!string.IsNullOrWhiteSpace(port))
            return $"http://127.0.0.1:{port}";

        return $"{Request.Scheme}://{Request.Host}";
    }

    private string BuildForwardedCookieHeader()
    {
        var parts = new List<string>();
        foreach (var name in new[]
        {
            AuthCookieNames.SessionV3,
            AuthCookieNames.SessionV2,
            AuthCookieNames.Remember,
            AuthCookieNames.LegacyUsername
        })
        {
            if (Request.Cookies.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value))
                parts.Add($"{name}={value}");
        }

        return string.Join("; ", parts);
    }

    private bool IsAdmin() =>
        AdminConfiguration.IsAdminSession(
            _configuration,
            HttpContext.Session.GetString("Username"),
            HttpContext.Session.GetString("IsAdmin"));
}
