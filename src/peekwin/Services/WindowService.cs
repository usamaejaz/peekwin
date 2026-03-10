using System.Text;
using System.Windows.Automation;
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
        if (hwnd == 0)
        {
            return CommandResult.Error("Invalid window handle.");
        }

        if (NativeMethods.IsIconic(hwnd))
        {
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        }

        var success = NativeMethods.BringWindowToTop(hwnd) && NativeMethods.SetForegroundWindow(hwnd);
        return success
            ? CommandResult.Ok($"Focused window 0x{hwnd.ToInt64():X}.")
            : CommandResult.Error($"Failed to focus window 0x{hwnd.ToInt64():X}.");
    }

    public CommandResult FocusWindowByTitle(string title)
    {
        var match = ListWindows().FirstOrDefault(w => w.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
        return match is null
            ? CommandResult.Error($"No window matched title: {title}")
            : FocusWindow((nint)match.Handle);
    }

    public WindowInspection InspectWindow(nint hwnd)
    {
        NativeMethods.GetWindowRect(hwnd, out var rect);
        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);

        var elements = new List<AutomationElementInfo>();
        try
        {
            var root = AutomationElement.FromHandle(hwnd);
            var children = root.FindAll(TreeScope.Children, Condition.TrueCondition);
            foreach (AutomationElement child in children)
            {
                var bounds = child.Current.BoundingRectangle;
                elements.Add(new AutomationElementInfo(
                    child.Current.Name ?? string.Empty,
                    child.Current.AutomationId ?? string.Empty,
                    child.Current.ControlType?.ProgrammaticName ?? string.Empty,
                    new RectDto((int)bounds.Left, (int)bounds.Top, (int)bounds.Width, (int)bounds.Height)));
            }
        }
        catch
        {
            // Best-effort inspection. Window metadata still returns even if UIA is unavailable.
        }

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
}
