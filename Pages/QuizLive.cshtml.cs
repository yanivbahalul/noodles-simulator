using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NoodlesSimulator.Services;

namespace NoodlesSimulator.Pages;

public class QuizLiveModel : PageModel
{
    public IActionResult OnGet()
    {
        HttpContext.Session.Remove(PracticeQuizService.DemoExplanationSessionKey);
        return RedirectToPage("/Index");
    }
}
