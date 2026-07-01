using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using NoodlesSimulator.Models;

namespace NoodlesSimulator.Services;

public static class LoginValidation
{
    public static string? CredentialsEmptyError(string? username, string? password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            return "שם המשתמש והסיסמה לא יכולים להיות ריקים.";
        return null;
    }

    public static string? RegistrationError(IConfiguration configuration, string username, string password)
    {
        var empty = CredentialsEmptyError(username, password);
        if (empty != null) return empty;

        if (username.Length < 5 || password.Length < 5)
            return "שם המשתמש והסיסמה חייבים להיות באורך של לפחות 5 תווים.";

        if (AdminConfiguration.IsReservedUsername(configuration, username))
            return "שם המשתמש לא זמין. בחר שם אחר.";

        if (!Regex.IsMatch(username, @"^[a-zA-Z0-9א-ת]+$"))
            return "שם המשתמש יכול להכיל רק אותיות (עברית/אנגלית) ומספרים.";

        return null;
    }
}
