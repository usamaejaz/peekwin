using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using PeekWin.Infrastructure;
using PeekWin.Models;

namespace PeekWin.Services;

public sealed class WindowService
{
    public IReadOnlyList<WindowInfo> ListWindows(bool includeHidden = true, string? appFilter = null, string? titleFilter = null)
    {
        var windows = EnumerateWindows();

        if (!includeHidden)
        {
            windows = windows.Where(window => window.IsVisible).ToList();
        }

        if (!string.IsNullOrWhiteSpace(appFilter))
        {
            windows = windows
                .Where(window => window.ProcessName.Contains(appFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(titleFilter))
        {
            windows = windows
                .Where(window => window.Title.Contains(titleFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return windows;
    }

    public IReadOnlyList<AppInfo> ListApps()
        => ListWindows(includeHidden: false)
            .GroupBy(window => window.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AppInfo(
                group.First().ProcessName,
                group.Select(window => window.ProcessId).Distinct().OrderBy(id => id).ToList(),
                group.Count(),
                group.Count(window => window.IsVisible),
                group.Select(window => window.Title).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(title => title, StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(app => app.WindowCount)
            .ToList();

    public WindowInfo? FindWindow(nint hwnd)
    {
        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd))
        {
            return null;
        }

        return CreateWindowInfo(hwnd);
    }

    public WindowInfo? FindWindowByTitle(string title)
        => ListWindows()
            .Where(window => window.Title.Contains(title, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(window => window.IsVisible)
            .ThenBy(window => window.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(window => window.Handle, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

    public WindowInfo? FindWindowByApp(string appName)
        => ListWindows()
            .Where(window => window.ProcessName.Contains(appName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(window => window.IsVisible)
            .ThenBy(window => window.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(window => window.Handle, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

    public CommandResult FocusWindow(nint hwnd)
    {
        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd))
        {
            return CommandResult.Error($"Invalid or destroyed window handle: {FormatHandle(hwnd)}.");
        }

        if (NativeMethods.IsIconic(hwnd))
        {
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        }

        var success = NativeMethods.BringWindowToTop(hwnd) && NativeMethods.SetForegroundWindow(hwnd);
        return success
            ? CommandResult.Ok($"Focused window {FormatHandle(hwnd)}.")
            : CommandResult.Error($"Failed to focus window {FormatHandle(hwnd)} ({DescribeWindowState(hwnd)}). Windows may block foreground activation.");
    }

    public CommandResult FocusWindowByTitle(string title)
    {
        var match = FindWindowByTitle(title);
        return match is null
            ? CommandResult.Error($"No window matched title: {title}")
            : FocusWindow(ParseHandle(match.Handle));
    }

    public WindowInspection InspectWindowByTitle(string title)
    {
        var match = FindWindowByTitle(title);
        return match is null
            ? throw new InvalidOperationException($"No window matched title: {title}")
            : InspectWindow(ParseHandle(match.Handle));
    }

    public WindowInspection InspectWindow(nint hwnd)
    {
        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd) || !NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            throw new InvalidOperationException($"Could not inspect window {FormatHandle(hwnd)}.");
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);

        return new WindowInspection(
            FormatHandle(hwnd),
            GetWindowText(hwnd),
            GetClassName(hwnd),
            unchecked((int)processId),
            GetProcessName(unchecked((int)processId)),
            NativeMethods.IsWindowVisible(hwnd),
            NativeMethods.IsIconic(hwnd),
            NativeMethods.IsZoomed(hwnd),
            VirtualDesktopHelper.GetDesktopLabel(hwnd),
            ToRectDto(rect));
    }

    public CommandResult CloseWindow(nint hwnd)
        => ApplyWindowAction(hwnd, "close", static handle => NativeMethods.PostMessage(handle, NativeMethods.WM_CLOSE, nint.Zero, nint.Zero));

    public CommandResult MinimizeWindow(nint hwnd)
        => ApplyWindowAction(hwnd, "minimize", static handle => NativeMethods.ShowWindow(handle, NativeMethods.SW_MINIMIZE));

    public CommandResult MaximizeWindow(nint hwnd)
        => ApplyWindowAction(hwnd, "maximize", static handle => NativeMethods.ShowWindow(handle, NativeMethods.SW_MAXIMIZE));

    public CommandResult RestoreWindow(nint hwnd)
        => ApplyWindowAction(hwnd, "restore", static handle => NativeMethods.ShowWindow(handle, NativeMethods.SW_RESTORE));

    public CommandResult? TryGetCaptureBounds(nint hwnd, out RectDto bounds)
    {
        bounds = default!;

        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd))
        {
            return CommandResult.Error($"Invalid or destroyed window handle: {FormatHandle(hwnd)}.");
        }

        if (NativeMethods.IsIconic(hwnd))
        {
            return CommandResult.Error($"Window {FormatHandle(hwnd)} is minimized and cannot be captured while minimized.");
        }

        if (TryGetExtendedFrameBounds(hwnd, out var frameRect) || NativeMethods.GetWindowRect(hwnd, out frameRect))
        {
            bounds = ToRectDto(frameRect);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return CommandResult.Error($"Window {FormatHandle(hwnd)} has invalid capture bounds {bounds.Width}x{bounds.Height}.");
            }

            return null;
        }

        return CommandResult.Error($"Could not read capture bounds for window {FormatHandle(hwnd)}.");
    }

    public CommandResult CloseWindowByTitle(string title) => ApplyWindowTitleAction(title, CloseWindow);

    public CommandResult MinimizeWindowByTitle(string title) => ApplyWindowTitleAction(title, MinimizeWindow);

    public CommandResult MaximizeWindowByTitle(string title) => ApplyWindowTitleAction(title, MaximizeWindow);

    public CommandResult RestoreWindowByTitle(string title) => ApplyWindowTitleAction(title, RestoreWindow);

    private static List<WindowInfo> EnumerateWindows()
    {
        var windows = new List<WindowInfo>();

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            var info = CreateWindowInfo(hwnd);
            if (info is not null)
            {
                windows.Add(info);
            }

            return true;
        }, 0);

        return windows
            .OrderBy(window => window.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(window => window.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(window => window.Handle, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static WindowInfo? CreateWindowInfo(nint hwnd)
    {
        var title = GetWindowText(hwnd);
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        NativeMethods.GetWindowRect(hwnd, out var rect);
        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);

        var pid = unchecked((int)processId);
        return new WindowInfo(
            FormatHandle(hwnd),
            title,
            GetClassName(hwnd),
            pid,
            GetProcessName(pid),
            NativeMethods.IsWindowVisible(hwnd),
            NativeMethods.IsIconic(hwnd),
            NativeMethods.IsZoomed(hwnd),
            VirtualDesktopHelper.GetDesktopLabel(hwnd),
            ToRectDto(rect));
    }

    private static CommandResult ApplyWindowAction(nint hwnd, string actionName, Func<nint, bool> action)
    {
        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd))
        {
            return CommandResult.Error($"Invalid or destroyed window handle: {FormatHandle(hwnd)}.");
        }

        return action(hwnd)
            ? CommandResult.Ok($"{Capitalize(actionName)}d window {FormatHandle(hwnd)}.")
            : CommandResult.Error($"Failed to {actionName} window {FormatHandle(hwnd)}.");
    }

    private CommandResult ApplyWindowTitleAction(string title, Func<nint, CommandResult> action)
    {
        var match = FindWindowByTitle(title);
        return match is null
            ? CommandResult.Error($"No window matched title: {title}")
            : action(ParseHandle(match.Handle));
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

    private static string GetProcessName(int processId)
    {
        try
        {
            return Process.GetProcessById(processId).ProcessName;
        }
        catch
        {
            return "unknown";
        }
    }

    private static RectDto ToRectDto(NativeMethods.RECT rect)
        => new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

    private static bool TryGetExtendedFrameBounds(nint hwnd, out NativeMethods.RECT rect)
    {
        var result = NativeMethods.DwmGetWindowAttribute(
            hwnd,
            NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
            out rect,
            Marshal.SizeOf<NativeMethods.RECT>());

        return result == 0;
    }

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

    private static string FormatHandle(nint hwnd) => $"0x{hwnd.ToInt64():X}";

    private static nint ParseHandle(string value)
        => value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? (nint)Convert.ToInt64(value[2..], 16)
            : (nint)Convert.ToInt64(value);

    private static string Capitalize(string value)
        => string.IsNullOrEmpty(value)
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..];
}
