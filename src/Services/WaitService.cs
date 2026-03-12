using System.Diagnostics;
using PeekWin.Models;

namespace PeekWin.Services;

public sealed class WaitService
{
    private readonly WindowService _windowService;
    private readonly AutomationRefService _automationRefService;

    public WaitService(WindowService windowService, AutomationRefService automationRefService)
    {
        _windowService = windowService;
        _automationRefService = automationRefService;
    }

    public async Task<WaitOutcome> WaitForWindowAsync(WindowWaitRequest request, CancellationToken cancellationToken = default)
    {
        var targetLabel = BuildWindowTargetLabel(request);
        var stopwatch = Stopwatch.StartNew();
        var pollCount = 0;
        WindowProbeResult? lastProbe = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pollCount++;
            lastProbe = ProbeWindow(request);

            if (lastProbe.IsSatisfied)
            {
                return CreateWindowOutcome(request, targetLabel, timedOut: false, stopwatch.ElapsedMilliseconds, pollCount, lastProbe);
            }

            if (stopwatch.ElapsedMilliseconds >= request.TimeoutMs)
            {
                return CreateWindowOutcome(request, targetLabel, timedOut: true, stopwatch.ElapsedMilliseconds, pollCount, lastProbe);
            }

            var delayMs = Math.Min(request.IntervalMs, Math.Max(0, request.TimeoutMs - (int)stopwatch.ElapsedMilliseconds));
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task<WaitOutcome> WaitForRefAsync(RefWaitRequest request, CancellationToken cancellationToken = default)
    {
        var targetLabel = $"UI ref {request.Ref}";
        var stopwatch = Stopwatch.StartNew();
        var pollCount = 0;
        RefProbeResult? lastProbe = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pollCount++;
            lastProbe = ProbeRef(request);

            if (lastProbe.IsSatisfied)
            {
                return CreateRefOutcome(request, targetLabel, timedOut: false, stopwatch.ElapsedMilliseconds, pollCount, lastProbe);
            }

            if (stopwatch.ElapsedMilliseconds >= request.TimeoutMs)
            {
                return CreateRefOutcome(request, targetLabel, timedOut: true, stopwatch.ElapsedMilliseconds, pollCount, lastProbe);
            }

            var delayMs = Math.Min(request.IntervalMs, Math.Max(0, request.TimeoutMs - (int)stopwatch.ElapsedMilliseconds));
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private WindowProbeResult ProbeWindow(WindowWaitRequest request)
    {
        var window = ResolveWindow(request);
        if (window is null)
        {
            return request.State == WindowWaitState.Gone
                ? new WindowProbeResult(true, null, "window is gone")
                : new WindowProbeResult(false, null, "no matching window");
        }

        var handle = ParseHandle(window.Handle);
        var isFocused = _windowService.GetForegroundWindowHandle() == handle;
        var isVisible = window.IsVisible && !window.IsMinimized && window.Bounds.Width > 0 && window.Bounds.Height > 0;

        var isSatisfied = request.State switch
        {
            WindowWaitState.Exists => true,
            WindowWaitState.Visible => isVisible,
            WindowWaitState.Focused => isFocused,
            WindowWaitState.Gone => false,
            WindowWaitState.Minimized => window.IsMinimized,
            WindowWaitState.Maximized => window.IsMaximized,
            WindowWaitState.Restored => !window.IsMinimized && !window.IsMaximized,
            _ => false
        };

        return new WindowProbeResult(
            isSatisfied,
            window,
            DescribeWindowState(request.State, window, isVisible, isFocused));
    }

    private RefProbeResult ProbeRef(RefWaitRequest request)
    {
        if (_automationRefService.TryResolveLiveRef(request.Ref, out var liveRef, out var staleReason))
        {
            var isVisible = !liveRef.IsOffscreen && liveRef.Bounds.Width > 0 && liveRef.Bounds.Height > 0;
            var isSatisfied = request.State switch
            {
                RefWaitState.Exists => true,
                RefWaitState.Visible => isVisible,
                RefWaitState.Focused => liveRef.HasKeyboardFocus,
                RefWaitState.Gone => false,
                RefWaitState.Enabled => liveRef.IsEnabled,
                RefWaitState.Disabled => !liveRef.IsEnabled,
                _ => false
            };

            return new RefProbeResult(
                isSatisfied,
                liveRef,
                DescribeRefState(request.State, liveRef, isVisible));
        }

        return request.State == RefWaitState.Gone
            ? new RefProbeResult(true, null, staleReason ?? "element is gone")
            : new RefProbeResult(false, null, staleReason ?? "element is not available");
    }

    private WindowInfo? ResolveWindow(WindowWaitRequest request)
    {
        if (request.Handle != 0)
        {
            return _windowService.FindWindow(request.Handle);
        }

        return _windowService.FindWindowMatch(request.Title, request.App);
    }

    private static WaitOutcome CreateWindowOutcome(WindowWaitRequest request, string targetLabel, bool timedOut, long elapsedMs, int pollCount, WindowProbeResult probe)
        => new(
            "window",
            targetLabel,
            request.State.ToString().ToLowerInvariant(),
            timedOut,
            request.TimeoutMs,
            request.IntervalMs,
            ToElapsedMilliseconds(elapsedMs),
            pollCount,
            Handle: probe.Window?.Handle,
            AppName: probe.Window?.ProcessName,
            Bounds: probe.Window?.Bounds,
            Diagnostic: probe.Diagnostic);

    private static WaitOutcome CreateRefOutcome(RefWaitRequest request, string targetLabel, bool timedOut, long elapsedMs, int pollCount, RefProbeResult probe)
        => new(
            "ref",
            targetLabel,
            request.State.ToString().ToLowerInvariant(),
            timedOut,
            request.TimeoutMs,
            request.IntervalMs,
            ToElapsedMilliseconds(elapsedMs),
            pollCount,
            Handle: probe.LiveRef is null ? null : $"0x{probe.LiveRef.WindowHandle.ToInt64():X}",
            Ref: request.Ref,
            AppName: probe.LiveRef?.AppName,
            Bounds: probe.LiveRef?.Bounds,
            Diagnostic: probe.Diagnostic);

    private static string BuildWindowTargetLabel(WindowWaitRequest request)
    {
        if (request.Handle != 0)
        {
            return $"window 0x{request.Handle.ToInt64():X}";
        }

        if (!string.IsNullOrWhiteSpace(request.Title) && !string.IsNullOrWhiteSpace(request.App))
        {
            return $"window title '{request.Title}' in app '{request.App}'";
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            return $"window title '{request.Title}'";
        }

        return $"window app '{request.App}'";
    }

    private static string DescribeWindowState(WindowWaitState state, WindowInfo window, bool isVisible, bool isFocused)
        => state switch
        {
            WindowWaitState.Exists => $"matched {window.Handle}",
            WindowWaitState.Visible => isVisible ? $"window {window.Handle} is visible" : window.IsMinimized ? $"window {window.Handle} is minimized" : window.IsVisible ? $"window {window.Handle} has invalid bounds" : $"window {window.Handle} is hidden",
            WindowWaitState.Focused => isFocused ? $"window {window.Handle} is focused" : $"window {window.Handle} is not focused",
            WindowWaitState.Gone => $"window {window.Handle} still exists",
            WindowWaitState.Minimized => window.IsMinimized ? $"window {window.Handle} is minimized" : $"window {window.Handle} is not minimized",
            WindowWaitState.Maximized => window.IsMaximized ? $"window {window.Handle} is maximized" : $"window {window.Handle} is not maximized",
            WindowWaitState.Restored => !window.IsMinimized && !window.IsMaximized ? $"window {window.Handle} is restored" : window.IsMinimized ? $"window {window.Handle} is minimized" : $"window {window.Handle} is maximized",
            _ => $"window {window.Handle} did not satisfy the requested state"
        };

    private static string DescribeRefState(RefWaitState state, LiveAutomationRef liveRef, bool isVisible)
        => state switch
        {
            RefWaitState.Exists => $"ref {liveRef.Ref} is live",
            RefWaitState.Visible => isVisible ? $"ref {liveRef.Ref} is visible" : liveRef.IsOffscreen ? $"ref {liveRef.Ref} is offscreen" : $"ref {liveRef.Ref} has invalid bounds",
            RefWaitState.Focused => liveRef.HasKeyboardFocus ? $"ref {liveRef.Ref} has keyboard focus" : $"ref {liveRef.Ref} does not have keyboard focus",
            RefWaitState.Gone => $"ref {liveRef.Ref} is still live",
            RefWaitState.Enabled => liveRef.IsEnabled ? $"ref {liveRef.Ref} is enabled" : $"ref {liveRef.Ref} is disabled",
            RefWaitState.Disabled => liveRef.IsEnabled ? $"ref {liveRef.Ref} is enabled" : $"ref {liveRef.Ref} is disabled",
            _ => $"ref {liveRef.Ref} did not satisfy the requested state"
        };

    private static nint ParseHandle(string value)
        => value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? (nint)Convert.ToInt64(value[2..], 16)
            : (nint)Convert.ToInt64(value);

    private static int ToElapsedMilliseconds(long elapsedMs)
        => elapsedMs >= int.MaxValue ? int.MaxValue : (int)elapsedMs;

    private sealed record WindowProbeResult(bool IsSatisfied, WindowInfo? Window, string Diagnostic);

    private sealed record RefProbeResult(bool IsSatisfied, LiveAutomationRef? LiveRef, string Diagnostic);
}
