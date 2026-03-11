namespace PeekWin.Models;

public sealed record WindowInfo(
    string Handle,
    string Title,
    string ClassName,
    int ProcessId,
    string ProcessName,
    bool IsVisible,
    bool IsMinimized,
    bool IsMaximized,
    string DesktopLabel,
    RectDto Bounds);

public sealed record AutomationElementInfo(
    string Name,
    string AutomationId,
    string ControlType,
    RectDto Bounds);

public sealed record WindowInspection(
    string Handle,
    string Title,
    string ClassName,
    int ProcessId,
    string ProcessName,
    bool IsVisible,
    bool IsMinimized,
    bool IsMaximized,
    string DesktopLabel,
    RectDto Bounds,
    IReadOnlyList<AutomationElementInfo> Elements);

public sealed record AppInfo(
    string Name,
    IReadOnlyList<int> ProcessIds,
    int WindowCount,
    int VisibleWindowCount,
    IReadOnlyList<string> WindowTitles);

public enum MouseButton
{
    Left,
    Right
}
