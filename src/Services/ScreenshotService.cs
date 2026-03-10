using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using PeekWin.Infrastructure;
using PeekWin.Models;

namespace PeekWin.Services;

public sealed class ScreenshotService
{
    private const int MaxCaptureDimension = 16384;
    private const long MaxCapturePixels = 268_435_456;

    public ScreenshotInfo GetScreenshotInfo()
    {
        var screens = GetScreens()
            .Select((bounds, index) => new ScreenInfo(index, bounds))
            .ToList();

        return new ScreenshotInfo(
            "virtual-desktop",
            GetVirtualScreenBounds(),
            screens);
    }

    public CommandResult Capture(string outputPath, int? screenIndex, nint windowHandle)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory);

        if (windowHandle != 0)
        {
            if (!NativeMethods.IsWindow(windowHandle))
            {
                return CommandResult.Error($"Invalid or destroyed window handle: 0x{windowHandle.ToInt64():X}.");
            }

            if (!NativeMethods.GetWindowRect(windowHandle, out var rect))
            {
                return CommandResult.Error($"Could not read bounds for 0x{windowHandle.ToInt64():X}.");
            }

            CaptureArea(outputPath, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            return CommandResult.Ok($"Saved screenshot to {outputPath}.", outputPath);
        }

        if (screenIndex is not null)
        {
            var screens = GetScreens();
            if (screenIndex < 0 || screenIndex >= screens.Count)
            {
                return CommandResult.Error($"Screen index {screenIndex} is out of range. Found {screens.Count} screens.");
            }

            var bounds = screens[screenIndex.Value];
            CaptureArea(outputPath, bounds.Left, bounds.Top, bounds.Width, bounds.Height);
            return CommandResult.Ok($"Saved screenshot to {outputPath}.", outputPath);
        }

        var virtualBounds = GetVirtualScreenBounds();
        CaptureArea(outputPath, virtualBounds.Left, virtualBounds.Top, virtualBounds.Width, virtualBounds.Height);
        return CommandResult.Ok($"Saved screenshot to {outputPath}.", outputPath);
    }

    private static void CaptureArea(string outputPath, int left, int top, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException($"Cannot capture an empty area ({width}x{height}).");
        }

        if (width > MaxCaptureDimension || height > MaxCaptureDimension)
        {
            throw new InvalidOperationException($"Cannot capture an area larger than {MaxCaptureDimension} pixels on either side ({width}x{height}).");
        }

        if ((long)width * height > MaxCapturePixels)
        {
            throw new InvalidOperationException($"Cannot capture an area larger than {MaxCapturePixels:N0} pixels ({width}x{height}).");
        }

        using var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(left, top, 0, 0, new Size(width, height));
        bitmap.Save(outputPath, ImageFormat.Png);
    }

    private static RectDto GetVirtualScreenBounds()
        => new(
            NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN),
            NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN),
            NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN),
            NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN));

    private static IReadOnlyList<RectDto> GetScreens()
    {
        var screens = new List<RectDto>();
        var handle = GCHandle.Alloc(screens);
        try
        {
            NativeMethods.EnumDisplayMonitors(nint.Zero, nint.Zero, static (nint monitor, nint hdcMonitor, ref NativeMethods.RECT monitorRect, nint state) =>
            {
                var listHandle = GCHandle.FromIntPtr(state);
                var list = (List<RectDto>)listHandle.Target!;

                var info = new NativeMethods.MONITORINFOEX();
                info.cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>();
                if (NativeMethods.GetMonitorInfo(monitor, ref info))
                {
                    list.Add(new RectDto(
                        info.rcMonitor.Left,
                        info.rcMonitor.Top,
                        info.rcMonitor.Right - info.rcMonitor.Left,
                        info.rcMonitor.Bottom - info.rcMonitor.Top));
                }

                return true;
            }, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            if (handle.IsAllocated)
            {
                handle.Free();
            }
        }

        return screens;
    }
}
