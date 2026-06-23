namespace sirNugg3ts.HtmlToOmt.Contracts;

public sealed class HeaderInjectionRule
{
    /// <summary>Requests whose URL starts with this prefix will have the header injected.</summary>
    public string UrlPrefix { get; init; } = string.Empty;

    /// <summary>Name of the HTTP header to inject.</summary>
    public string HeaderName { get; init; } = string.Empty;

    /// <summary>Factory that returns the header value. Called on every matching request, so it can return fresh values (e.g. rotating tokens).</summary>
    public Func<string> ValueProvider { get; init; } = static () => string.Empty;
}
