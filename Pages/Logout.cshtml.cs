using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Services;
using System;
using System.Threading.Tasks;

namespace NoodlesSimulator.Pages;

public class LogoutModel : PageModel
{
    private readonly AuthService? _auth;

    public LogoutModel(AuthService? auth = null)
    {
        _auth = auth;
    }

    public IActionResult OnGet()
    {
        return RedirectToPage("/Login");
    }

    public async Task<IActionResult> OnPost()
    {
        try
        {
            var username = HttpContext.Session.GetString("Username");
            HttpContext.Session.Clear();
            RememberMeService.Clear(Response);

            if (!string.IsNullOrWhiteSpace(username) && _auth != null)
                await _auth.MarkOfflineAsync(username);

            return RedirectToPage("/Login");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Logout OnPost Error] {ex}");
            return StatusCode(500, "Server error");
        }
    }
}
