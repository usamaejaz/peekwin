namespace PeekWin.Models;

public sealed record AutomationRefTarget(
    string Ref,
    string TargetLabel,
    string? AppName,
    nint WindowHandle,
    RectDto Bounds,
    string Name,
    string AutomationId,
    string ControlType,
    string? Path);
