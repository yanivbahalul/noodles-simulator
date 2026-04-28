using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NoodlesSimulator.Services
{
    public static class ImageUrlResolver
    {
        public static async Task<(string QuestionUrl, Dictionary<string, string> AnswerUrls)> ResolveQuestionAndAnswersAsync(
            SupabaseStorageService storage,
            string questionImage,
            Dictionary<string, string> answers)
        {
            var safeAnswers = answers ?? new Dictionary<string, string>();

            if (storage != null)
            {
                var paths = new List<string>();
                if (!string.IsNullOrWhiteSpace(questionImage))
                {
                    paths.Add(questionImage);
                }

                paths.AddRange(safeAnswers.Values.Where(v => !string.IsNullOrWhiteSpace(v)));
                var signed = await storage.GetSignedUrlsAsync(paths);

                var questionUrl = (!string.IsNullOrWhiteSpace(questionImage) && signed.TryGetValue(questionImage, out var qu))
                    ? qu
                    : string.Empty;

                var answerUrls = new Dictionary<string, string>();
                foreach (var kv in safeAnswers)
                {
                    if (string.IsNullOrWhiteSpace(kv.Value))
                    {
                        continue;
                    }

                    answerUrls[kv.Key] = signed.TryGetValue(kv.Value, out var au) ? au : string.Empty;
                }

                return (questionUrl, answerUrls);
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

            return (localQuestionUrl, localAnswerUrls);
        }
    }
}
