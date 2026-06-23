# sirNugg3ts.HtmlToOmt

sirNugg3ts.HtmlToOmt is a .NET 8 library that renders an HTML page offscreen via Chromium (CefSharp) and pushes each frame as a live video source over [OMT](https://www.openmediatransport.org/) through the [libomtnet](https://github.com/openmediatransport/libomtnet) library.

## Requirements

- Windows x64
- [.NET 8 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [Visual C++ Redistributable 2019+](https://aka.ms/vs/17/release/vc_redist.x64.exe) (required by Chromium)

The OMT binaries (`libomtnet.dll`, `libomt.dll`, `libvmx.dll`) and all CefSharp native files are already bundled with the library.

## Installation

```
dotnet add package sirNugg3ts.HtmlToOmt
```

## Usage

```csharp
using sirNugg3ts.HtmlToOmt.Contracts;
using sirNugg3ts.HtmlToOmt.Services;

var renderOptions = new HtmlRenderHostOptions
{
    InitialUrl = "http://localhost:9090/my-graphic",
};

var renderHost = new HtmlRenderHost(renderOptions);

renderHost.Diagnostic += (_, msg) => Console.WriteLine(msg);

await renderHost.StartAsync();

// Keep running until you're done
await Task.Delay(Timeout.Infinite, cancellationToken);

// Disposes all instances and shuts down Chromium
HtmlRenderHost.Shutdown();
```

### HtmlRenderHostOptions

| Property               | Required | Default         | Description                                                                         |
| ---------------------- | -------- | --------------- | ----------------------------------------------------------------------------------- |
| `InitialUrl`           | ✅       |                 | URL to load in the offscreen browser                                                |
| `Width` / `Height`     |          | `1920` / `1080` | Frame dimensions in pixels                                                          |
| `OmtSourceName`        |          | URL             | OMT source name — defaults to the URL if not set                                    |
| `Fps`                  |          | `30`            | Frame rate for both the browser and the OMT stream                                  |
| `PaintOnChangeOnly`    |          | `false`         | When `true`, frames are only sent when the page repaints instead of at a fixed rate |
| `OmtQuality`           |          | `"Low"`         | OMT encode quality — `Low`, `Medium`, or `High`                                     |
| `HeaderInjectionRules` |          |                 | HTTP headers to inject per URL prefix (e.g. auth tokens)                            |
| `CefCommandLineArgs`   |          |                 | Extra Chromium command-line flags (e.g. `["disable-web-security"]`)                 |

### Header injection

```csharp
var renderOptions = new HtmlRenderHostOptions
{
    InitialUrl = "http://localhost:9090/my-graphic",
    Width = 1920,
    Height = 1080,
    HeaderInjectionRules = new[]
    {
        new HeaderInjectionRule
        {
            UrlPrefix = "http://localhost:9090/",
            HeaderName = "Authorization",
            ValueProvider = () => $"Bearer {GetToken()}",
        }
    }
};
```

## Multiple sources

Each `HtmlRenderHost` is an independent browser instance with its own OMT source. You can run as many as you need simultaneously, and start or stop each one individually:

```csharp
var scoreboardHost = new HtmlRenderHost(new HtmlRenderHostOptions
{
    InitialUrl = "http://localhost:9090/scoreboard",
    OmtSourceName = "Scoreboard",
});

var lowerThirdHost = new HtmlRenderHost(new HtmlRenderHostOptions
{
    InitialUrl = "http://localhost:9090/lower-third",
    OmtSourceName = "Lower Third",
});

await scoreboardHost.StartAsync();
await lowerThirdHost.StartAsync();

// Stop one source without affecting the other
lowerThirdHost.Dispose();

// Shut everything down at process exit
HtmlRenderHost.Shutdown();
```

## Shutdown

Call `HtmlRenderHost.Shutdown()` once at process exit. It disposes all remaining instances automatically before shutting down Chromium:

```csharp
HtmlRenderHost.Shutdown();
```

## Initialization

Chromium is initialized automatically on first use. If you need to control timing (e.g. initialize at startup before creating any instances), call `HtmlRenderHost.Initialize()` explicitly — it is safe to call multiple times.

## License

This library is licensed under the [MIT License](LICENSE).

This package bundles binaries from the [Open Media Transport](https://www.openmediatransport.org/) project, also licensed under the MIT License. Copyright (c) 2025 Open Media Transport Contributors.
