using System.Runtime.InteropServices;
using CefSharp;
using CefSharp.OffScreen;
using sirNugg3ts.HtmlToOmt.Contracts;

namespace sirNugg3ts.HtmlToOmt.Services;

public sealed class HtmlRenderHost : IDisposable
{
    #region Static members
    private static readonly List<HtmlRenderHost> _instances = [];
    private static readonly object _instancesLock = new();

    private readonly HtmlRenderHostOptions _options;
    private readonly ChromiumWebBrowser _browser;
    private readonly OmtOutputService _omtOutput;

    #endregion

    public HtmlRenderHost(HtmlRenderHostOptions options)
    {
        Initialize(options.CefCommandLineArgs);
        lock (_instancesLock) { _instances.Add(this); }
        _options = options;
        _omtOutput = new OmtOutputService(options.OmtSourceName ?? options.InitialUrl, options.Width, options.Height, options.Fps, options.OmtQuality);
        FrameReady += (_, frame) =>
        {
            try { _omtOutput.SendFrame(frame); }
            catch (Exception ex) { Diagnostic?.Invoke(this, $"SendFrame failed: {ex.Message}"); }
        };
        var browserSettings = new BrowserSettings
        {
            BackgroundColor = 0x00000000,
            WindowlessFrameRate = options.PaintOnChangeOnly ? 1 : options.Fps,
        };

        _browser = new ChromiumWebBrowser(
            options.InitialUrl,
            browserSettings: browserSettings,
            requestContext: null,
            automaticallyCreateBrowser: false);

        _browser.RequestHandler = new HeaderInjectionRequestHandler(
            options.HeaderInjectionRules,
            (message) => Diagnostic?.Invoke(this, message));
        _browser.Size = new System.Drawing.Size(options.Width, options.Height);
        _browser.CreateBrowser();

        _browser.Paint += OnPaint;
        _browser.LoadingStateChanged += OnLoadingStateChanged;
        _browser.ConsoleMessage += OnConsoleMessage;
        _browser.LoadError += OnLoadError;
        _browser.FrameLoadStart += OnFrameLoadStart;
        _browser.FrameLoadEnd += OnFrameLoadEnd;
        _browser.StatusMessage += OnStatusMessage;
        _browser.AddressChanged += OnAddressChanged;
        _browser.TitleChanged += OnTitleChanged;
    }

    #region Public events

    /// <summary>Fired on every browser paint. Pixels are already forwarded to OMT internally; subscribe here for additional processing (e.g. screenshots).</summary>
    public event EventHandler<RenderedFrame>? FrameReady;

    /// <summary>Fired once when the page finishes loading.</summary>
    public event EventHandler? Ready;

    /// <summary>Verbose browser log messages. Wire to your logger for visibility.</summary>
    public event EventHandler<string>? Diagnostic;

    /// <summary>Fired when the main frame fails to load. The string contains the error details.</summary>
    public event EventHandler<string>? FatalLoadError;

    #endregion
    #region Global control of the Chromium engine

    /// <summary>Initializes the Chromium engine. Called automatically on first use, but can be called explicitly at startup to control timing. Extra CEF command-line args can be passed here or via HtmlRenderHostOptions.CefCommandLineArgs.</summary>
    public static void Initialize(IEnumerable<string>? extraArgs = null)
    {
        if (Cef.IsInitialized == true)
        {
            return;
        }

        var baseDir = AppContext.BaseDirectory;
        var settings = new CefSettings
        {
            WindowlessRenderingEnabled = true,
            LogSeverity = LogSeverity.Warning,
            LogFile = Path.Combine(baseDir, "cef.log"),
            RootCachePath = Path.Combine(baseDir, "CefCache"),
        };
        settings.CefCommandLineArgs.Add("no-sandbox");

        if (extraArgs is not null)
        {
            foreach (var arg in extraArgs)
            {
                settings.CefCommandLineArgs.Add(arg);
            }
        }

        CefSharpSettings.SubprocessExitIfParentProcessClosed = true;

        if (!Cef.Initialize(settings))
        {
            throw new InvalidOperationException("Chromium (CEF) failed to initialize.");
        }
    }

    /// <summary>Disposes all active instances and shuts down the Chromium engine. Call once at process exit.</summary>
    public static void Shutdown()
    {
        lock (_instancesLock)
        {
            foreach (var instance in _instances.ToList())
            {
                instance.Dispose();
            }
        }

        if (Cef.IsInitialized == true)
        {
            Cef.Shutdown();
        }
    }

    #endregion
    #region Instance control

    /// <summary>Loads the configured URL and begins rendering. Subscribe to events before calling this.</summary>
    public Task StartAsync()
    {
        _browser.Load(_options.InitialUrl);
        return Task.CompletedTask;
    }

    /// <summary>Stops the browser and releases all resources, including the OMT output.</summary>
    public void Dispose()
    {
        lock (_instancesLock) { _instances.Remove(this); }
        _browser.Paint -= OnPaint;
        _browser.LoadingStateChanged -= OnLoadingStateChanged;
        _browser.ConsoleMessage -= OnConsoleMessage;
        _browser.LoadError -= OnLoadError;
        _browser.FrameLoadStart -= OnFrameLoadStart;
        _browser.FrameLoadEnd -= OnFrameLoadEnd;
        _browser.StatusMessage -= OnStatusMessage;
        _browser.AddressChanged -= OnAddressChanged;
        _browser.TitleChanged -= OnTitleChanged;
        _browser.Dispose();
        _omtOutput.Dispose();
    }
    #endregion
    #region Private event handlers

    private void OnLoadingStateChanged(object? sender, LoadingStateChangedEventArgs eventArgs)
    {
        Diagnostic?.Invoke(
            this,
            $"LoadingState isLoading={eventArgs.IsLoading} canGoBack={eventArgs.CanGoBack} canGoForward={eventArgs.CanGoForward}");

        if (!eventArgs.IsLoading)
        {
            Diagnostic?.Invoke(this, $"Load complete: {_browser.Address}");
            Ready?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnConsoleMessage(object? sender, ConsoleMessageEventArgs eventArgs)
    {
        if (eventArgs.Level != LogSeverity.Warning && eventArgs.Level != LogSeverity.Error)
        {
            return;
        }

        Diagnostic?.Invoke(
            this,
            $"Console[{eventArgs.Level}] {eventArgs.Message} ({eventArgs.Source}:{eventArgs.Line})");
    }

    private void OnLoadError(object? sender, LoadErrorEventArgs eventArgs)
    {
        if (eventArgs.ErrorCode == CefErrorCode.Aborted)
        {
            return;
        }

        Diagnostic?.Invoke(
            this,
            $"Load error {eventArgs.ErrorCode} on {eventArgs.FailedUrl}: {eventArgs.ErrorText}");

        if (eventArgs.Frame?.IsMain ?? false)
        {
            FatalLoadError?.Invoke(
                this,
                $"Main-frame load error {eventArgs.ErrorCode} on {eventArgs.FailedUrl}: {eventArgs.ErrorText}");
        }
    }

    private void OnFrameLoadStart(object? sender, FrameLoadStartEventArgs eventArgs)
    {
        Diagnostic?.Invoke(
            this,
            $"FrameLoadStart frame={(eventArgs.Frame.IsMain ? "main" : "sub")} url={eventArgs.Url}");
    }

    private void OnFrameLoadEnd(object? sender, FrameLoadEndEventArgs eventArgs)
    {
        Diagnostic?.Invoke(
            this,
            $"FrameLoadEnd frame={(eventArgs.Frame.IsMain ? "main" : "sub")} url={eventArgs.Url} httpStatus={eventArgs.HttpStatusCode}");
    }

    private void OnStatusMessage(object? sender, StatusMessageEventArgs eventArgs)
    {
        Diagnostic?.Invoke(this, $"StatusMessage: {eventArgs.Value}");
    }

    private void OnAddressChanged(object? sender, AddressChangedEventArgs eventArgs)
    {
        Diagnostic?.Invoke(this, $"AddressChanged: {eventArgs.Address}");
    }

    private void OnTitleChanged(object? sender, TitleChangedEventArgs eventArgs)
    {
        Diagnostic?.Invoke(this, $"TitleChanged: {eventArgs.Title}");
    }

    private void OnPaint(object? sender, OnPaintEventArgs eventArgs)
    {
        var size = eventArgs.Width * eventArgs.Height * 4;
        if (size <= 0)
        {
            return;
        }

        var pixels = new byte[size];
        Marshal.Copy(eventArgs.BufferHandle, pixels, 0, size);
        FrameReady?.Invoke(this, new RenderedFrame(eventArgs.Width, eventArgs.Height, pixels));
    }
}

    #endregion