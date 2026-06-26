using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using NoodlesSimulator.Services;
using System;

namespace NoodlesSimulator.Pages
{
    public class LogoutModel : PageModel
    {
        public IActionResult OnGet()
        {
            return RedirectToPage("/Login");
        }

        public IActionResult OnPost()
        {
            try
            {
                HttpContext.Session.Clear();
                RememberMeService.Clear(Response);
                return RedirectToPage("/Login");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Logout OnPost Error] {ex}");
                return StatusCode(500, "Server error");
            }
        }
    }
}