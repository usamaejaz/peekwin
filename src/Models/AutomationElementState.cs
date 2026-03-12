namespace PeekWin.Models;

public sealed record AutomationElementState(
    string Name,
    string AutomationId,
    string ControlType,
    RectDto Bounds,
    bool IsKeyboardFocusable,
    bool IsEnabled,
    bool IsOffscreen,
    bool HasKeyboardFocus);
