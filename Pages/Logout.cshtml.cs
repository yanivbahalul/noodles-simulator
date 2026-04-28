using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
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
                Response.Cookies.Delete("Username");
                Response.Cookies.Delete(".Noodles.Session.v2");
                Response.Cookies.Delete(".Noodles.Session.v3");
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