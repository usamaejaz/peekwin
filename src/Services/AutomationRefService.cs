using PeekWin.Infrastructure;
using PeekWin.Models;

namespace PeekWin.Services;

public sealed class AutomationRefService
{
    private readonly AutomationSnapshotService _automationSnapshotService;
    private readonly WindowService _windowService;

    public AutomationRefService(AutomationSnapshotService automationSnapshotService, WindowService windowService)
    {
        _automationSnapshotService = automationSnapshotService;
        _windowService = windowService;
    }

    public LiveAutomationRef ResolveLiveRef(string reference)
    {
        if (!TryResolveLiveRef(reference, out var liveRef, out var staleReason))
        {
            throw new InvalidOperationException($"UI ref {reference} is stale: {staleReason ?? "element no longer exists at the saved path"}. Run `peekwin see` again.");
        }

        return liveRef;
    }

    public bool TryResolveLiveRef(string reference, out LiveAutomationRef liveRef, out string? staleReason)
    {
        var target = _automationSnapshotService.ResolveRef(reference);

        WindowInspection window;
        try
        {
            window = _windowService.InspectWindowHandle(target.WindowHandle);
        }
        catch (InvalidOperationException)
        {
            liveRef = default!;
            staleReason = $"source window 0x{target.WindowHandle.ToInt64():X} no longer exists";
            return false;
        }

        if (!string.Equals(window.ProcessName, target.AppName, StringComparison.OrdinalIgnoreCase)
            || window.ProcessId != target.ProcessId
            || !string.Equals(window.ClassName, target.WindowClassName, StringComparison.Ordinal))
        {
            liveRef = default!;
            staleReason = "source window changed since the last `peekwin see`";
            return false;
        }

        string? lookupError = null;
        if (string.IsNullOrWhiteSpace(target.Path)
            || !UiAutomationHelper.TryGetElementStateByPath(target.WindowHandle, target.Path, out var liveState, out lookupError))
        {
            liveRef = default!;
            staleReason = lookupError ?? "element no longer exists at the saved path";
            return false;
        }

        if (!string.Equals(liveState.ControlType, target.ControlType, StringComparison.Ordinal)
            || (!string.IsNullOrWhiteSpace(target.AutomationId) && !string.Equals(liveState.AutomationId, target.AutomationId, StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(target.Name) && !string.Equals(liveState.Name, target.Name, StringComparison.Ordinal)))
        {
            liveRef = default!;
            staleReason = "element identity changed since the last `peekwin see`";
            return false;
        }

        liveRef = new LiveAutomationRef(
            target.SnapshotId,
            target.Ref,
            target.TargetLabel,
            target.AppName,
            target.WindowHandle,
            target.WindowTitle,
            target.WindowClassName,
            target.ProcessId,
            liveState.Bounds,
            liveState.Name,
            liveState.AutomationId,
            liveState.ControlType,
            target.Path,
            liveState.IsKeyboardFocusable,
            liveState.IsEnabled,
            liveState.IsOffscreen,
            liveState.HasKeyboardFocus);

        staleReason = null;
        return true;
    }
}
