using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NoodlesSimulator.Models;
using Microsoft.AspNetCore.Http;

namespace NoodlesSimulator.Pages
{
    public class StatsModel : PageModel
    {
        private readonly AuthService _authService;

        public StatsModel(AuthService authService)
        {
            _authService = authService;
        }

        public string Username { get; set; } = "";
        public int CorrectAnswers { get; set; }
        public int TotalAnswered { get; set; }
        public int SuccessRate { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
                return new JsonResult(new { error = "No session" });

            var user = await _authService.GetUser(username);
            if (user == null)
                return new JsonResult(new { error = "User not found" });

            Username = user.Username;
            CorrectAnswers = user.CorrectAnswers;
            TotalAnswered = user.TotalAnswered;
            SuccessRate = (TotalAnswered > 0)
                ? (int)((double)CorrectAnswers / TotalAnswered * 100)
                : 0;

            return new JsonResult(new
            {
                username,
                correct = CorrectAnswers,
                total = TotalAnswered,
                successRate = SuccessRate
            });
        }
    }
}
