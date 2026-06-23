using System.Reflection;
using System.Runtime.InteropServices;
using sirNugg3ts.HtmlToOmt.Contracts;

namespace sirNugg3ts.HtmlToOmt.Services;

internal sealed class OmtOutputService : IDisposable
{
    private readonly object _sender;
    private readonly MethodInfo _sendMethod;
    private readonly object _frame;
    private readonly FieldInfo _timestampField;
    private readonly long _frameDurationTicks;
    private readonly PropertyInfo? _connectionsProperty;
    private readonly IntPtr _frameBuffer;
    private readonly int _frameBufferLength;
    private readonly object _syncRoot = new();
    private long _nextTimestampTicks;
    private bool _disposed;

    public OmtOutputService(string sourceName, int width, int height, int fps, string quality = "Low")
    {
        var assembly = Assembly.LoadFrom(ResolveOmtAssemblyPath());
        var senderType = RequireType(assembly, "libomtnet.OMTSend");
        var qualityType = RequireType(assembly, "libomtnet.OMTQuality");
        var frameType = RequireType(assembly, "libomtnet.OMTMediaFrame");
        var frameKindType = RequireType(assembly, "libomtnet.OMTFrameType");
        var codecType = RequireType(assembly, "libomtnet.OMTCodec");
        var flagsType = RequireType(assembly, "libomtnet.OMTVideoFlags");
        var colorSpaceType = RequireType(assembly, "libomtnet.OMTColorSpace");

        var senderCtor = senderType.GetConstructor(new[] { typeof(string), qualityType })
            ?? throw new InvalidOperationException("libomtnet.OMTSend(string, OMTQuality) constructor was not found.");
        var lowQuality = Enum.Parse(qualityType, quality, ignoreCase: true);
        _sender = senderCtor.Invoke(new[] { sourceName, lowQuality });
        _connectionsProperty = senderType.GetProperty("Connections");

        _sendMethod = senderType.GetMethod("Send", new[] { frameType })
            ?? throw new InvalidOperationException("libomtnet.OMTSend.Send(OMTMediaFrame) was not found.");

        _frame = Activator.CreateInstance(frameType)
            ?? throw new InvalidOperationException("Failed to create libomtnet.OMTMediaFrame.");

        _frameBufferLength = checked(width * height * 4);
        _frameBuffer = Marshal.AllocHGlobal(_frameBufferLength);
        _frameDurationTicks = Math.Max(1, 10_000_000L / Math.Max(1, fps));
        _nextTimestampTicks = 0;

        SetField(frameType, _frame, "Width", width);
        SetField(frameType, _frame, "Height", height);
        SetField(frameType, _frame, "Stride", checked(width * 4));
        SetField(frameType, _frame, "FrameRateN", fps);
        SetField(frameType, _frame, "FrameRateD", 1);
        SetField(frameType, _frame, "AspectRatio", (float)width / height);
        SetField(frameType, _frame, "Type", Enum.Parse(frameKindType, "Video", ignoreCase: true));
        SetField(frameType, _frame, "Codec", Convert.ToInt32(Enum.Parse(codecType, "BGRA", ignoreCase: true)));
        SetField(frameType, _frame, "ColorSpace", Enum.Parse(colorSpaceType, "BT709", ignoreCase: true));

        var alphaFlag = Convert.ToUInt32(Enum.Parse(flagsType, "Alpha", ignoreCase: true));
        var premultipliedFlag = Convert.ToUInt32(Enum.Parse(flagsType, "PreMultiplied", ignoreCase: true));
        SetField(frameType, _frame, "Flags", Enum.ToObject(flagsType, alphaFlag | premultipliedFlag));

        SetField(frameType, _frame, "Data", _frameBuffer);
        SetField(frameType, _frame, "DataLength", _frameBufferLength);
        SetField(frameType, _frame, "CompressedData", IntPtr.Zero);
        SetField(frameType, _frame, "CompressedLength", 0);
        SetField(frameType, _frame, "FrameMetadata", IntPtr.Zero);
        SetField(frameType, _frame, "FrameMetadataLength", 0);

        _timestampField = frameType.GetField("Timestamp")
            ?? throw new InvalidOperationException("libomtnet.OMTMediaFrame.Timestamp was not found.");
    }

    public void SendFrame(RenderedFrame frame)
    {
        if (_disposed)
        {
            return;
        }

        if (frame.Pixels.Length > _frameBufferLength)
        {
            throw new InvalidOperationException("Rendered frame exceeds configured OMT frame buffer.");
        }

        lock (_syncRoot)
        {
            Marshal.Copy(frame.Pixels, 0, _frameBuffer, frame.Pixels.Length);
            _timestampField.SetValue(_frame, _nextTimestampTicks);
            _nextTimestampTicks += _frameDurationTicks;

            var sendResultObject = _sendMethod.Invoke(_sender, new[] { _frame });
            var sendResult = sendResultObject is int value ? value : Convert.ToInt32(sendResultObject);

            if (sendResult <= 0 && GetConnectionCount() > 0)
            {
                throw new InvalidOperationException($"OMT send failed with code {sendResult}.");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_sender is IDisposable disposable)
        {
            disposable.Dispose();
        }

        Marshal.FreeHGlobal(_frameBuffer);
    }

    private static string ResolveOmtAssemblyPath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "libomtnet.dll");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("libomtnet.dll was not found next to the application executable.", path);
        }

        return path;
    }

    private static Type RequireType(Assembly assembly, string fullName)
    {
        return assembly.GetType(fullName)
            ?? throw new InvalidOperationException($"libomtnet type '{fullName}' was not found.");
    }

    private static void SetField(Type type, object target, string fieldName, object value)
    {
        var field = type.GetField(fieldName)
            ?? throw new InvalidOperationException($"libomtnet.OMTMediaFrame.{fieldName} was not found.");
        field.SetValue(target, value);
    }

    private int GetConnectionCount()
    {
        if (_connectionsProperty is null)
        {
            return 0;
        }

        var value = _connectionsProperty.GetValue(_sender);
        return value is int count ? count : 0;
    }
}
