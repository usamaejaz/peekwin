namespace PeekWin.Models;

public sealed record AutomationSnapshot(
    string Version,
    string SnapshotId,
    DateTimeOffset CapturedAt,
    int CapturedByProcessId,
    int CapturedBySessionId,
    string TargetLabel,
    string? AppName,
    string WindowHandle,
    string WindowTitle,
    string WindowClassName,
    int ProcessId,
    RectDto Bounds,
    int MaxDepth,
    IReadOnlyList<AutomationTreeNode> Elements);
