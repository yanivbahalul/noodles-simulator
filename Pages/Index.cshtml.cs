using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

public class IndexModel : PageModel
{
    [BindProperty]
    public string Answer { get; set; }

    public bool AnswerChecked { get; set; }
    public bool IsCorrect { get; set; }
    public string SelectedAnswer { get; set; }

    public string QuestionImage { get; set; }
    public Dictionary<string, string> ShuffledAnswers { get; set; }

    public void OnGet()
    {
        LoadRandomQuestion();
    }

    public void OnPost(string answer, string questionImage, string answersJson)
    {
        SelectedAnswer = answer;
        AnswerChecked = true;

        QuestionImage = questionImage;
        ShuffledAnswers = JsonConvert.DeserializeObject<Dictionary<string, string>>(answersJson);

        IsCorrect = answer == "correct";
    }

    private void LoadRandomQuestion()
    {
        var imagesPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");

        var allImages = Directory.GetFiles(imagesPath)
            .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg") || f.EndsWith(".webp"))
            .Select(Path.GetFileName)
            .OrderBy(name => name)
            .ToList();

        var grouped = new List<List<string>>();
        for (int i = 0; i + 4 < allImages.Count; i += 5)
        {
            grouped.Add(allImages.GetRange(i, 5));
        }

        if (grouped.Count == 0)
        {
            QuestionImage = "placeholder.jpg";
            ShuffledAnswers = new Dictionary<string, string>();
            return;
        }

        var random = new Random();
        var chosenGroup = grouped[random.Next(grouped.Count)];

        QuestionImage = chosenGroup[0];
        var correctAnswer = chosenGroup[1];
        var wrongAnswers = chosenGroup.Skip(2).Take(3).ToList();

        var answers = new List<(string key, string file)>
        {
            ("correct", correctAnswer),
            ("a", wrongAnswers[0]),
            ("b", wrongAnswers[1]),
            ("c", wrongAnswers[2])
        };

        ShuffledAnswers = answers
            .OrderBy(x => Guid.NewGuid())
            .ToDictionary(x => x.key, x => x.file);
    }
}