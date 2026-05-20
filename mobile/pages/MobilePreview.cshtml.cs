using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NoodlesSimulator.Pages
{
    /// <summary>Legacy URL — forwards to the main quiz page.</summary>
    public class MobilePreviewModel : PageModel
    {
        public IActionResult OnGet()
        {
            var query = Request.QueryString.HasValue ? Request.QueryString.Value : string.Empty;
            return Redirect("/Index" + query);
        }
    }
}
