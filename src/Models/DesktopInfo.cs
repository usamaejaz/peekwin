namespace PeekWin.Models;

public sealed record VirtualDesktopInfo(
    int Index,
    string Id,
    bool IsCurrent);
