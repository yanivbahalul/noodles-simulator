using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Services;

namespace NoodlesSimulator.Middleware;

public class SessionRestoreMiddleware
{
    private readonly RequestDelegate _next;

    public SessionRestoreMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, RememberMeService rememberMe)
    {
        if (string.IsNullOrEmpty(context.Session.GetString("Username")))
        {
            var restored = rememberMe.TryRead(context.Request);
            if (restored != null)
            {
                context.Session.SetString("Username", restored.Value.Username);
                context.Session.SetString("IsAdmin", restored.Value.IsAdmin ? "1" : "0");
            }
        }

        await _next(context);
    }
}
