namespace NoodlesSimulator.Services;

public sealed class LoginFlowResult
{
    public bool RedirectToIndex { get; init; }
    public bool ShowPage { get; init; }
    public string? ErrorMessage { get; init; }

    public static LoginFlowResult Page() => new() { ShowPage = true };
    public static LoginFlowResult Index() => new() { RedirectToIndex = true };
    public static LoginFlowResult Error(string message) => new() { ShowPage = true, ErrorMessage = message };
}
