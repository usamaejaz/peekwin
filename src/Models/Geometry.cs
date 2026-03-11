namespace PeekWin.Models;

public sealed record RectDto(int Left, int Top, int Width, int Height);

public sealed record ScreenInfo(
    int Index,
    string DeviceName,
    bool IsPrimary,
    RectDto Bounds,
    RectDto WorkArea);

public sealed record ScreenLayoutInfo(
    RectDto VirtualBounds,
    int ScreenIndexBase,
    IReadOnlyList<ScreenInfo> Screens);
