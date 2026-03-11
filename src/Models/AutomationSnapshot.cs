namespace PeekWin.Models;

public sealed record AutomationSnapshot(
    string Version,
    DateTimeOffset CapturedAt,
    string TargetLabel,
    string? AppName,
    string WindowHandle,
    RectDto Bounds,
    int MaxDepth,
    IReadOnlyList<AutomationTreeNode> Elements);
