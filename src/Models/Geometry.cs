namespace PeekWin.Models;

public sealed record RectDto(int Left, int Top, int Width, int Height);

public sealed record ScreenInfo(int Index, RectDto Bounds);

public sealed record ScreenshotInfo(
	string DefaultCaptureTarget,
	RectDto VirtualBounds,
	IReadOnlyList<ScreenInfo> Screens);
