namespace sirNugg3ts.HtmlToOmt.Contracts;

public sealed class HtmlRenderHostOptions
{
    /// <summary>URL to load in the offscreen browser.</summary>
    public string InitialUrl { get; init; } = string.Empty;

    /// <summary>Frame width in pixels. Defaults to 1920.</summary>
    public int Width { get; init; } = 1920;

    /// <summary>Frame height in pixels. Defaults to 1080.</summary>
    public int Height { get; init; } = 1080;

    /// <summary>OMT source name. Defaults to <see cref="InitialUrl"/> if not set.</summary>
    public string? OmtSourceName { get; init; }

    /// <summary>Frame rate for both the browser and the OMT stream. Defaults to 30.</summary>
    public int Fps { get; init; } = 30;

    /// <summary>When true, frames are only sent when the page repaints instead of at a fixed rate. Defaults to false.</summary>
    public bool PaintOnChangeOnly { get; init; } = false;

    /// <summary>OMT encode quality. Valid values are "Low", "Medium", "High". Defaults to "Low".</summary>
    public string OmtQuality { get; init; } = "Low";

    /// <summary>HTTP headers to inject into requests matching a URL prefix. Useful for auth tokens.</summary>
    public IReadOnlyList<HeaderInjectionRule> HeaderInjectionRules { get; init; } = Array.Empty<HeaderInjectionRule>();

    /// <summary>Extra Chromium command-line flags passed during CEF initialization (e.g. "disable-web-security").</summary>
    public IReadOnlyList<string> CefCommandLineArgs { get; init; } = Array.Empty<string>();
}
