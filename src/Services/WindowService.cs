using System.Text;
using PeekWin.Infrastructure;
using PeekWin.Models;

namespace PeekWin.Services;

public sealed class WindowService
{
    public IReadOnlyList<WindowInfo> ListWindows()
    {
        var windows = new List<WindowInfo>();

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            var title = GetWindowText(hwnd);
            if (string.IsNullOrWhiteSpace(title))
            {
                return true;
            }

            NativeMethods.GetWindowRect(hwnd, out var rect);
            NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);

            windows.Add(new WindowInfo(
                hwnd.ToInt64(),
                title,
                GetClassName(hwnd),
                unchecked((int)processId),
                NativeMethods.IsWindowVisible(hwnd),
                NativeMethods.IsIconic(hwnd),
                NativeMethods.IsZoomed(hwnd),
                VirtualDesktopHelper.GetDesktopLabel(hwnd),
                ToRectDto(rect)));

            return true;
        }, 0);

        return windows.OrderBy(w => w.Title, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public CommandResult FocusWindow(nint hwnd)
    {
        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd))
        {
            return CommandResult.Error($"Invalid or destroyed window handle: 0x{hwnd.ToInt64():X}.");
        }

        var wasMinimized = NativeMethods.IsIconic(hwnd);
        if (wasMinimized)
        {
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        }

        var success = NativeMethods.BringWindowToTop(hwnd) && NativeMethods.SetForegroundWindow(hwnd);
        return success
            ? CommandResult.Ok($"Focused window 0x{hwnd.ToInt64():X}.")
            : CommandResult.Error($"Failed to focus window 0x{hwnd.ToInt64():X} ({DescribeWindowState(hwnd)}). Windows may block foreground activation.");
    }

    public CommandResult FocusWindowByTitle(string title)
    {
        var match = ListWindows().FirstOrDefault(w => w.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
        return match is null
            ? CommandResult.Error($"No window matched title: {title}")
            : FocusWindow((nint)match.Handle);
    }

    public WindowInspection InspectWindowByTitle(string title)
    {
        var match = ListWindows().FirstOrDefault(w => w.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
        return match is null
            ? throw new InvalidOperationException($"No window matched title: {title}")
            : InspectWindow((nint)match.Handle);
    }

    public WindowInspection InspectWindow(nint hwnd)
    {
        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd) || !NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            throw new InvalidOperationException($"Could not inspect window 0x{hwnd.ToInt64():X}.");
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);

        var elements = UiAutomationHelper.GetTopLevelChildren(hwnd);

        return new WindowInspection(
            hwnd.ToInt64(),
            GetWindowText(hwnd),
            GetClassName(hwnd),
            unchecked((int)processId),
            NativeMethods.IsWindowVisible(hwnd),
            NativeMethods.IsIconic(hwnd),
            NativeMethods.IsZoomed(hwnd),
            VirtualDesktopHelper.GetDesktopLabel(hwnd),
            ToRectDto(rect),
            elements);
    }

    private static string GetWindowText(nint hwnd)
    {
        var length = NativeMethods.GetWindowTextLengthW(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        NativeMethods.GetWindowTextW(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string GetClassName(nint hwnd)
    {
        var builder = new StringBuilder(256);
        NativeMethods.GetClassNameW(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static RectDto ToRectDto(NativeMethods.RECT rect)
        => new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

    private static string DescribeWindowState(nint hwnd)
    {
        var visibility = NativeMethods.IsWindowVisible(hwnd) ? "visible" : "hidden";
        var sizeState = NativeMethods.IsIconic(hwnd)
            ? "minimized"
            : NativeMethods.IsZoomed(hwnd)
                ? "maximized"
                : "normal";

        return $"{visibility}, {sizeState}";
    }
}
