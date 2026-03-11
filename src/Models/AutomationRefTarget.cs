namespace PeekWin.Models;

public sealed record AutomationRefTarget(
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
    string? Path);
