namespace PeekWin.Models;

public sealed record AutomationTreeResult(bool Success, IReadOnlyList<AutomationTreeNode> Nodes, string? Error = null);
