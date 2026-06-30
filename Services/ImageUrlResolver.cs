using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NoodlesSimulator.Services;

public static class ImageUrlResolver
{
    public static Task<(string QuestionUrl, Dictionary<string, string> AnswerUrls)> ResolveQuestionAndAnswersAsync(
        SupabaseStorageService? storage,
        string questionImage,
        Dictionary<string, string> answers)
    {
        var safeAnswers = answers ?? new Dictionary<string, string>();

        if (storage != null)
        {
            var questionUrl = string.IsNullOrWhiteSpace(questionImage)
                ? string.Empty
                : MediaUrl.ForStoragePath(questionImage);

            var answerUrls = new Dictionary<string, string>();
            foreach (var kv in safeAnswers)
            {
                if (string.IsNullOrWhiteSpace(kv.Value))
                    continue;

                answerUrls[kv.Key] = MediaUrl.ForStoragePath(kv.Value);
            }

            return Task.FromResult((questionUrl, answerUrls));
        }

        var localQuestionUrl = string.IsNullOrWhiteSpace(questionImage) ? string.Empty : $"/images/{questionImage}";
        var localAnswerUrls = new Dictionary<string, string>();
        foreach (var kv in safeAnswers)
        {
            if (!string.IsNullOrWhiteSpace(kv.Value))
            {
                localAnswerUrls[kv.Key] = $"/images/{kv.Value}";
            }
        }

        return Task.FromResult((localQuestionUrl, localAnswerUrls));
    }
}
