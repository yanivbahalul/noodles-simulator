using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using NoodlesSimulator.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NoodlesSimulator.Pages
{
    public class QuestionViewModel : PageModel
    {
        private readonly SupabaseStorageService _storage;

        public QuestionViewModel(SupabaseStorageService storage = null)
        {
            _storage = storage;
        }

        public string QuestionImageUrl { get; set; }
        public Dictionary<string, string> AnswerImageUrls { get; set; } = new Dictionary<string, string>();

        public async Task OnGet()
        {
            var questionId = Request.Query["id"].ToString();
            if (string.IsNullOrWhiteSpace(questionId)) return;

            // Build answer guesses from naming convention: [q, correct, a, b, c] are contiguous in sorted list
            // We attempt to find corresponding answers by scanning storage list and matching group containing the question id
            List<string> group = null;
            if (_storage != null)
            {
                var all = await _storage.ListFilesAsync("");
                var filtered = all.Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg") || f.EndsWith(".webp")).OrderBy(n => n).ToList();
                for (int i = 0; i + 4 < filtered.Count; i += 5)
                {
                    var g = filtered.GetRange(i, 5);
                    if (string.Equals(g[0], questionId, StringComparison.OrdinalIgnoreCase)) { group = g; break; }
                }
                if (group != null)
                {
                    var signed = await _storage.GetSignedUrlsAsync(group);
                    QuestionImageUrl = signed.TryGetValue(group[0], out var qu) ? qu : string.Empty;
                    // answers order: 1=correct, 2..4 = distractors
                    var keys = new[]{"correct","a","b","c"};
                    for (int k=1;k<group.Count && k-1<keys.Length;k++)
                    {
                        var key = keys[k-1];
                        var val = group[k];
                        if (!string.IsNullOrWhiteSpace(val) && signed.TryGetValue(val, out var au))
                            AnswerImageUrls[key] = au;
                    }
                }
            }
            else
            {
                // local filesystem
                var imagesDir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", "images");
                if (!System.IO.Directory.Exists(imagesDir)) return;
                var filtered = System.IO.Directory.GetFiles(imagesDir)
                    .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg") || f.EndsWith(".webp"))
                    .Select(System.IO.Path.GetFileName)
                    .OrderBy(n => n).ToList();
                for (int i = 0; i + 4 < filtered.Count; i += 5)
                {
                    var g = filtered.GetRange(i, 5);
                    if (string.Equals(g[0], questionId, StringComparison.OrdinalIgnoreCase)) { group = g; break; }
                }
                if (group != null)
                {
                    QuestionImageUrl = $"/images/{group[0]}";
                    var keys = new[]{"correct","a","b","c"};
                    for (int k=1;k<group.Count && k-1<keys.Length;k++)
                    {
                        var key = keys[k-1];
                        var val = group[k];
                        if (!string.IsNullOrWhiteSpace(val))
                            AnswerImageUrls[key] = $"/images/{val}";
                    }
                }
            }
        }
    }
}


