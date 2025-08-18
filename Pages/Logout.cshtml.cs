using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using System;

namespace NoodlesSimulator.Pages
{
    public class LogoutModel : PageModel
    {
        public IActionResult OnPost()
        {
            try
            {
                HttpContext.Session.Clear();
                Response.Cookies.Delete("Username");
                return Redirect(Request.Path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Logout OnPost Error] {ex}");
                return StatusCode(500, $"Server error: {ex.Message}");
            }
        }
    }
}