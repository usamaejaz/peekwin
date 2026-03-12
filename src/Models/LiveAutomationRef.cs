namespace PeekWin.Models;

public sealed record LiveAutomationRef(
    string SnapshotId,
    string Ref,
    string TargetLabel,
    string? AppName,
    nint WindowHandle,
    string WindowTitle,
    string WindowClassName,
    int ProcessId,
    RectDto Bounds,
    string Name,
    string AutomationId,
    string ControlType,
    string? Path,
    bool IsKeyboardFocusable,
    bool IsEnabled,
    bool IsOffscreen,
    bool HasKeyboardFocus);
