using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using PeekWin.Infrastructure;
using PeekWin.Models;

namespace PeekWin.Services;

public sealed class ScreenshotService
{
    public CommandResult Capture(string outputPath, int? screenIndex, nint windowHandle)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory);

        if (windowHandle != 0)
        {
            if (!NativeMethods.GetWindowRect(windowHandle, out var rect))
            {
                return CommandResult.Error($"Could not read bounds for 0x{windowHandle.ToInt64():X}.");
            }

            CaptureArea(outputPath, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            return CommandResult.Ok($"Saved screenshot to {outputPath}.", outputPath);
        }

        var screens = Screen.AllScreens;
        if (screenIndex is not null)
        {
            if (screenIndex < 0 || screenIndex >= screens.Length)
            {
                return CommandResult.Error($"Screen index {screenIndex} is out of range. Found {screens.Length} screens.");
            }

            var bounds = screens[screenIndex.Value].Bounds;
            CaptureArea(outputPath, bounds.Left, bounds.Top, bounds.Width, bounds.Height);
            return CommandResult.Ok($"Saved screenshot to {outputPath}.", outputPath);
        }

        var virtualBounds = SystemInformation.VirtualScreen;
        CaptureArea(outputPath, virtualBounds.Left, virtualBounds.Top, virtualBounds.Width, virtualBounds.Height);
        return CommandResult.Ok($"Saved screenshot to {outputPath}.", outputPath);
    }

    private static void CaptureArea(string outputPath, int left, int top, int width, int height)
    {
        using var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(left, top, 0, 0, new Size(width, height));
        bitmap.Save(outputPath, ImageFormat.Png);
    }
}
