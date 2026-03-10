namespace PeekWin.Models;

public sealed record WindowInfo(
    long Handle,
    string Title,
    string ClassName,
    int ProcessId,
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
    long Handle,
    string Title,
    string ClassName,
    int ProcessId,
    bool IsVisible,
    bool IsMinimized,
    bool IsMaximized,
    string DesktopLabel,
    RectDto Bounds,
    IReadOnlyList<AutomationElementInfo> Elements);

public enum MouseButton
{
    Left,
    Right
}
