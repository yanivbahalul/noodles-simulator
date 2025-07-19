using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NoodlesSimulator.Models;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System;

namespace NoodlesSimulator.Pages
{
    public class FlagCheaterModel : PageModel
    {
        private readonly AuthService _authService;

        public FlagCheaterModel(AuthService authService)
        {
            _authService = authService;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                var username = HttpContext.Session.GetString("Username");
                if (string.IsNullOrEmpty(username))
                    return new JsonResult(new { status = "unauthenticated" });

                User user = null;
                try { user = await _authService.GetUser(username); } catch (Exception ex) { Console.WriteLine($"[FlagCheater GetUser Error] {ex}"); }
                if (user == null)
                    return new JsonResult(new { status = "not_found" });

                user.CorrectAnswers = 0;
                user.TotalAnswered = 0;
                user.IsCheater = true;
                try { await _authService.UpdateUser(user); } catch (Exception ex) { Console.WriteLine($"[FlagCheater UpdateUser Error] {ex}"); }

                return new JsonResult(new { status = "flagged" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FlagCheater OnPostAsync Error] {ex}");
                return new JsonResult(new { status = "error", error = ex.Message }) { StatusCode = 500 };
            }
        }
    }
}
