using Microsoft.AspNetCore.Http;
using System;

namespace NoodlesSimulator.Services
{
    /// <summary>
    /// Chooses mobile vs desktop quiz layout from User-Agent and optional cookie/query overrides.
    /// </summary>
    public static class MobileLayoutPreference
    {
        public const string LayoutCookieName = "noodles_layout";
        public const string MobileValue = "mobile";
        public const string DesktopValue = "desktop";

        public static bool IsMobileUserAgent(HttpRequest request)
        {
            var ua = request.Headers.UserAgent.ToString();
            if (string.IsNullOrWhiteSpace(ua))
                return false;

            ua = ua.ToLowerInvariant();
            if (ua.Contains("iphone") || ua.Contains("ipod") || ua.Contains("android"))
                return true;
            if (ua.Contains("ipad") || ua.Contains("tablet"))
                return true;
            if (ua.Contains("mobile"))
                return true;
            return false;
        }

        public static void ApplyLayoutQuery(HttpRequest request, HttpResponse response)
        {
            if (request.Query.ContainsKey("desktop"))
            {
                SetLayoutCookie(response, DesktopValue);
            }
            else if (request.Query.ContainsKey("mobile"))
            {
                SetLayoutCookie(response, MobileValue);
            }
        }

        public static bool ShouldUseMobileQuiz(HttpRequest request)
        {
            if (request.Query.ContainsKey("desktop"))
                return false;
            if (request.Query.ContainsKey("mobile"))
                return true;

            var cookie = request.Cookies[LayoutCookieName];
            if (string.Equals(cookie, DesktopValue, StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.Equals(cookie, MobileValue, StringComparison.OrdinalIgnoreCase))
                return true;

            return IsMobileUserAgent(request);
        }

        public static string QuizPageRoute(HttpRequest request) => "Index";

        private static void SetLayoutCookie(HttpResponse response, string value)
        {
            response.Cookies.Append(LayoutCookieName, value, new CookieOptions
            {
                MaxAge = TimeSpan.FromDays(30),
                SameSite = SameSiteMode.Lax,
                IsEssential = true
            });
        }
    }
}
