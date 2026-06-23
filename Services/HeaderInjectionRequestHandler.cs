using CefSharp;
using CefSharp.Handler;
using sirNugg3ts.HtmlToOmt.Contracts;

namespace sirNugg3ts.HtmlToOmt.Services;

internal sealed class HeaderInjectionRequestHandler : RequestHandler
{
    private readonly IReadOnlyList<HeaderInjectionRule> _rules;
    private readonly Action<string>? _log;

    public HeaderInjectionRequestHandler(IReadOnlyList<HeaderInjectionRule> rules, Action<string>? log = null)
    {
        _rules = rules;
        _log = log;
    }

    protected override IResourceRequestHandler? GetResourceRequestHandler(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        IFrame frame,
        IRequest request,
        bool isNavigation,
        bool isDownload,
        string requestInitiator,
        ref bool disableDefaultHandling)
    {
        var applicableRules = _rules
            .Where((rule) =>
                !string.IsNullOrWhiteSpace(rule.UrlPrefix) &&
                request.Url.StartsWith(rule.UrlPrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return new HeaderInjectionResourceRequestHandler(
            applicableRules);
    }

    protected override void OnRenderProcessTerminated(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        CefTerminationStatus status,
        int errorCode,
        string errorString)
    {
        _log?.Invoke($"Render process terminated status={status} code={errorCode} error={errorString}");
        base.OnRenderProcessTerminated(chromiumWebBrowser, browser, status, errorCode, errorString);
    }
}

internal sealed class HeaderInjectionResourceRequestHandler : ResourceRequestHandler
{
    private readonly IReadOnlyList<HeaderInjectionRule> _rules;

    public HeaderInjectionResourceRequestHandler(
        IReadOnlyList<HeaderInjectionRule> rules)
    {
        _rules = rules;
    }

    protected override CefReturnValue OnBeforeResourceLoad(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        IFrame frame,
        IRequest request,
        IRequestCallback callback)
    {
        foreach (var rule in _rules)
        {
            request.SetHeaderByName(rule.HeaderName, rule.ValueProvider(), overwrite: true);
        }

        return CefReturnValue.Continue;
    }
}
