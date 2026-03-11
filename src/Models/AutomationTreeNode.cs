namespace PeekWin.Models;

public sealed record AutomationTreeNode(
    string Ref,
    string? ParentRef,
    string Path,
    int Depth,
    string Name,
    string AutomationId,
    string ControlType,
    string Role,
    RectDto Bounds,
    bool IsKeyboardFocusable,
    bool IsEnabled,
    bool IsOffscreen);
