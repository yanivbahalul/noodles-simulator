using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NoodlesSimulator.Models;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

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
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
                return new JsonResult(new { status = "unauthenticated" });

            var user = await _authService.GetUser(username);
            if (user == null)
                return new JsonResult(new { status = "not_found" });

            user.CorrectAnswers = 0;
            user.TotalAnswered = 0;
            user.IsCheater = true;
            await _authService.UpdateUser(user);

            return new JsonResult(new { status = "flagged" });
        }
    }
}
