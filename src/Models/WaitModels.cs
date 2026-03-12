namespace PeekWin.Models;

public enum WindowWaitState
{
    Exists,
    Visible,
    Focused,
    Gone,
    Minimized,
    Maximized,
    Restored
}

public enum RefWaitState
{
    Exists,
    Visible,
    Focused,
    Gone,
    Enabled,
    Disabled
}

public sealed record WindowWaitRequest(
    nint Handle,
    string? Title,
    string? App,
    WindowWaitState State,
    int TimeoutMs,
    int IntervalMs);

public sealed record RefWaitRequest(
    string Ref,
    RefWaitState State,
    int TimeoutMs,
    int IntervalMs);

public sealed record TextWaitRequest(
    nint Handle,
    string? Title,
    string? App,
    string? Ref,
    string ContainsText,
    int TimeoutMs,
    int IntervalMs);

public sealed record WaitOutcome(
    string TargetKind,
    string TargetLabel,
    string State,
    bool TimedOut,
    int TimeoutMs,
    int IntervalMs,
    int ElapsedMs,
    int PollCount,
    string? Handle = null,
    string? Ref = null,
    string? AppName = null,
    RectDto? Bounds = null,
    string? Diagnostic = null);

public sealed record TextWaitOutcome(
    string TargetKind,
    string TargetLabel,
    string ContainsText,
    bool TimedOut,
    int TimeoutMs,
    int IntervalMs,
    int ElapsedMs,
    int PollCount,
    string? Handle = null,
    string? Ref = null,
    string? AppName = null,
    RectDto? Bounds = null,
    string? ActualText = null,
    string? Diagnostic = null);
