using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Hosting;
using NoodlesSimulator.Services;

namespace NoodlesSimulator.Pages;

public class QuizDemoModel : PageModel
{
    private const string DefaultDemoQuestion = "Screenshot at Apr 15 20-14-53.png";
    private readonly IWebHostEnvironment _env;

    public QuizDemoModel(IWebHostEnvironment env) => _env = env;

    public IActionResult OnGet()
    {
        if (_env.IsProduction())
            return RedirectToPage("/Index");

        HttpContext.Session.SetString(PracticeQuizService.DemoExplanationSessionKey, DefaultDemoQuestion);
        return RedirectToPage("/Index");
    }
}
