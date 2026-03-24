using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PeekWin.Cli;
using PeekWin.Infrastructure;

namespace PeekWin.Mcp;

[McpServerToolType]
[Description("Windows desktop automation tools for discovering windows and UI, focusing targets, interacting with mouse or keyboard, waiting for state changes, and capturing visual context.")]
public sealed class PeekWinMcpTools
{
    private readonly ICommandRunner _commandRunner;

    public PeekWinMcpTools(ICommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    [McpServerTool(Name = "window_list", ReadOnly = true)]
    [Description("List candidate top-level windows before focusing or inspecting one. Use this when you do not already know the exact target window.")]
    public Task<object?> WindowList(bool all, string? app, string? title, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "window", "list", Flag("all", all), Opt("app", app), Opt("title", title));

    [McpServerTool(Name = "window_focus")]
    [Description("Bring a window to the foreground before typing or pointer actions. Use this when the target window may not already be active.")]
    public Task<object?> WindowFocus(string? app, string? title, string? handle, string? window, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "window", "focus", WindowTarget(app, title, handle, window));

    [McpServerTool(Name = "window_inspect", ReadOnly = true)]
    [Description("Inspect top-level window metadata and bounds. Use `see_ui` instead when you need controls or visible UI structure.")]
    public Task<object?> WindowInspect(string? app, string? title, string? handle, string? window, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "window", "inspect", WindowTarget(app, title, handle, window));

    [McpServerTool(Name = "window_move")]
    [Description("Move a window to an absolute screen position.")]
    public Task<object?> WindowMove(int x, int y, string? app, string? title, string? handle, string? window, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "window", "move", WindowTarget(app, title, handle, window), Opt("x", x), Opt("y", y));

    [McpServerTool(Name = "window_resize")]
    [Description("Resize a window to the requested width and height.")]
    public Task<object?> WindowResize(int width, int height, string? app, string? title, string? handle, string? window, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "window", "resize", WindowTarget(app, title, handle, window), Opt("width", width), Opt("height", height));

    [McpServerTool(Name = "window_close")]
    [Description("Close a target window.")]
    public Task<object?> WindowClose(string? app, string? title, string? handle, string? window, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "window", "close", WindowTarget(app, title, handle, window));

    [McpServerTool(Name = "window_minimize")]
    [Description("Minimize a target window.")]
    public Task<object?> WindowMinimize(string? app, string? title, string? handle, string? window, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "window", "minimize", WindowTarget(app, title, handle, window));

    [McpServerTool(Name = "window_maximize")]
    [Description("Maximize a target window.")]
    public Task<object?> WindowMaximize(string? app, string? title, string? handle, string? window, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "window", "maximize", WindowTarget(app, title, handle, window));

    [McpServerTool(Name = "window_restore")]
    [Description("Restore a minimized or maximized window.")]
    public Task<object?> WindowRestore(string? app, string? title, string? handle, string? window, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "window", "restore", WindowTarget(app, title, handle, window));

    [McpServerTool(Name = "desktop_list", ReadOnly = true)]
    [Description("List virtual desktops.")]
    public Task<object?> DesktopList(CancellationToken cancellationToken)
        => RunJson(cancellationToken, "desktop", "list");

    [McpServerTool(Name = "desktop_current", ReadOnly = true)]
    [Description("Return the current virtual desktop.")]
    public Task<object?> DesktopCurrent(CancellationToken cancellationToken)
        => RunJson(cancellationToken, "desktop", "current");

    [McpServerTool(Name = "desktop_switch")]
    [Description("Switch to a virtual desktop by index.")]
    public Task<object?> DesktopSwitch(int index, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "desktop", "switch", Opt("index", index));

    [McpServerTool(Name = "screen_layout", ReadOnly = true)]
    [Description("Return monitor layout and bounds for coordinate planning only. Do not use this for window discovery or UI inspection.")]
    public Task<object?> Screens(CancellationToken cancellationToken)
        => RunJson(cancellationToken, "screens");

    [McpServerTool(Name = "pointer_move")]
    [Description("Move the pointer without clicking. Prefer `click`, `drag`, or `scroll` unless hover or pointer positioning specifically matters.")]
    public Task<object?> PointerMove(int? x, int? y, int? screen, string? app, string? title, string? handle, string? window, string? reference, bool focus, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "move", Point(x, y), PointerTarget(screen, app, title, handle, window, reference), Flag("focus", focus));

    [McpServerTool(Name = "click")]
    [Description("Primary pointer action. Use this for most button, link, and control activation once the target window or area is known.")]
    public Task<object?> Click(int? x, int? y, int? screen, string? app, string? title, string? handle, string? window, string? reference, bool focus, string? button, bool @double, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "click", Point(x, y), PointerTarget(screen, app, title, handle, window, reference), Flag("focus", focus), Opt("button", button), Flag("double", @double));

    [McpServerTool(Name = "drag")]
    [Description("Drag from one point to another for drag-and-drop or slider movement. Do not use this for page scrolling; use `scroll` instead.")]
    public Task<object?> Drag(int? x, int? y, int toX, int toY, int? screen, string? app, string? title, string? handle, string? window, string? reference, bool focus, string? button, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "drag", Point(x, y), PointerTarget(screen, app, title, handle, window, reference), Opt("to-x", toX), Opt("to-y", toY), Flag("focus", focus), Opt("button", button));

    [McpServerTool(Name = "scroll")]
    [Description("Scroll by wheel ticks. Positive `ticks` scroll down, negative `ticks` scroll up. Use this for page or pane scrolling.")]
    public Task<object?> Scroll(int? ticks, int? ticksX, int? x, int? y, int? screen, string? app, string? title, string? handle, string? window, string? reference, bool focus, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "scroll", Point(x, y), PointerTarget(screen, app, title, handle, window, reference), Opt("delta", ScaleWheelTicks(ticks)), Opt("delta-x", ScaleWheelTicks(ticksX)), Flag("focus", focus));

    [McpServerTool(Name = "mouse_down")]
    [Description("Low-level mouse press tool. Prefer `click` or `drag` unless you specifically need split press and release control.")]
    public Task<object?> MouseDown(string? button, int? x, int? y, int? screen, string? app, string? title, string? handle, string? window, string? reference, bool focus, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "mouse", "down", Opt("button", button), Point(x, y), PointerTarget(screen, app, title, handle, window, reference), Flag("focus", focus));

    [McpServerTool(Name = "mouse_up")]
    [Description("Low-level mouse release tool. Prefer `click` or `drag` unless you specifically need split press and release control.")]
    public Task<object?> MouseUp(string? button, int? x, int? y, int? screen, string? app, string? title, string? handle, string? window, string? reference, bool focus, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "mouse", "up", Opt("button", button), Point(x, y), PointerTarget(screen, app, title, handle, window, reference), Flag("focus", focus));

    [McpServerTool(Name = "type_text")]
    [Description("Default text-entry tool. Use this for most text input after focusing the right window or target ref.")]
    public Task<object?> TypeText(string text, string? app, string? title, string? handle, string? window, string? reference, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "type", Opt("text", text), KeyboardTarget(app, title, handle, window, reference));

    [McpServerTool(Name = "paste_text")]
    [Description("Paste text instead of typing it key by key. Prefer this for long text or when exact literal content matters.")]
    public Task<object?> PasteText(string text, string? app, string? title, string? handle, string? window, string? reference, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "paste", Opt("text", text), KeyboardTarget(app, title, handle, window, reference));

    [McpServerTool(Name = "press_key")]
    [Description("Press one key, optionally repeated. Use `send_hotkey` for shortcut chords like Ctrl+S.")]
    public Task<object?> PressKey(string key, int? repeat, string? app, string? title, string? handle, string? window, string? reference, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "press", Opt("key", key), Opt("repeat", repeat), KeyboardTarget(app, title, handle, window, reference));

    [McpServerTool(Name = "send_hotkey")]
    [Description("Send a single shortcut chord such as Ctrl+S or Alt+Tab. Use `send_keys` only for scripted multi-step sequences.")]
    public Task<object?> SendHotkey(string[] keys, string? app, string? title, string? handle, string? window, string? reference, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "hotkey", Opt("keys", JoinCsv(keys)), KeyboardTarget(app, title, handle, window, reference));

    [McpServerTool(Name = "send_keys")]
    [Description("Advanced scripted key-sequence tool for taps, holds, and sleeps. Prefer `press_key` or `send_hotkey` unless you specifically need a multi-step sequence.")]
    public Task<object?> SendKeys(string[] steps, string? app, string? title, string? handle, string? window, string? reference, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "keys", Opt("steps", JoinCsv(steps)), KeyboardTarget(app, title, handle, window, reference));

    [McpServerTool(Name = "see_ui", ReadOnly = true)]
    [Description("Inspect the UI Automation tree for the foreground or a targeted window. Use this first to understand visible UI and find controls.")]
    public Task<object?> SeeUi(string? app, string? title, string? handle, string? window, string? role, string? name, bool all, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "see", WindowTarget(app, title, handle, window), Flag("deep", true), Opt("role", role), Opt("name", name), Flag("all", all));

    [McpServerTool(Name = "hold_input")]
    [Description("Advanced tool to hold keys or a mouse button for a duration. Prefer `click`, `drag`, `press_key`, or `send_hotkey` unless a sustained hold is required.")]
    public Task<object?> HoldInput(string[]? keys, string? button, int? durationMs, int? x, int? y, int? screen, string? app, string? title, string? handle, string? window, string? reference, bool focus, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "hold",
            Opt("keys", JoinCsv(keys)),
            Opt("button", button),
            Opt("duration-ms", durationMs),
            Point(x, y),
            (button is not null ? PointerTarget(screen, app, title, handle, window, reference) : KeyboardTarget(app, title, handle, window, reference)),
            Flag("focus", focus));

    [McpServerTool(Name = "capture_image")]
    [Description("Capture an image of a screen, window, or saved UI ref. Use this only when pixels or visual verification are needed.")]
    public Task<object?> CaptureImage(int? screen, string? app, string? title, string? handle, string? window, string? reference, bool focus, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "image", PointerTarget(screen, app, title, handle, window, reference), Flag("focus", focus));

    [McpServerTool(Name = "wait_window")]
    [Description("Synchronization tool. Use this after actions that may open, focus, hide, or close windows.")]
    public Task<object?> WaitWindow(string state, string? app, string? title, string? handle, string? window, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "wait", "window", WindowTarget(app, title, handle, window), Opt("state", state));

    [McpServerTool(Name = "wait_ref")]
    [Description("Wait for a saved UI ref from `see_ui` to reach a state such as visible, focused, enabled, or gone.")]
    public Task<object?> WaitRef(string reference, string state, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "wait", "ref", Opt("ref", reference), Opt("state", state));

    [McpServerTool(Name = "wait_text")]
    [Description("Wait until a window title or saved ref name contains text. Use this for lightweight text-based synchronization.")]
    public Task<object?> WaitText(string contains, string? app, string? title, string? handle, string? window, string? reference, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "wait", "text", KeyboardTarget(app, title, handle, window, reference), Opt("contains", contains));

    [McpServerTool(Name = "clipboard_get", ReadOnly = true)]
    [Description("Get the current global clipboard text. Use carefully because clipboard state is shared with the user.")]
    public Task<object?> ClipboardGet(CancellationToken cancellationToken)
        => RunJson(cancellationToken, "clipboard", "get");

    [McpServerTool(Name = "clipboard_set")]
    [Description("Set the current global clipboard text. Use carefully because clipboard state is shared with the user.")]
    public Task<object?> ClipboardSet(string text, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "clipboard", "set", Opt("text", text));

    [McpServerTool(Name = "sleep")]
    [Description("Delay for a fixed number of milliseconds. Prefer `wait_window`, `wait_ref`, or `wait_text` over blind sleeps when possible.")]
    public Task<object?> Sleep(int durationMs, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "sleep", Opt("duration-ms", durationMs));

    private async Task<object?> RunJson(CancellationToken cancellationToken, params object?[] parts)
    {
        var args = Flatten(parts);
        args.Add("--json");
        var result = await _commandRunner.RunAsync(args, cancellationToken).ConfigureAwait(false);
        return Simplify(result);
    }

    private static object Simplify(CommandRunResult result)
    {
        if (result.Json is not null)
        {
            return result.Json.Deserialize<JsonElement>();
        }

        var stdout = result.Stdout.Trim();
        if (!string.IsNullOrWhiteSpace(stdout))
        {
            return stdout;
        }

        var stderr = result.Stderr.Trim();
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            return result.Success
                ? stderr
                : new { success = false, error = stderr };
        }

        return result.Success
            ? "ok"
            : new { success = false, error = "Command failed." };
    }

    private static List<string> Flatten(params object?[] parts)
    {
        var args = new List<string>();
        foreach (var part in parts)
        {
            switch (part)
            {
                case null:
                    break;
                case string text:
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        args.Add(text);
                    }

                    break;
                case IEnumerable<string?> values:
                    foreach (var item in values)
                    {
                        if (!string.IsNullOrWhiteSpace(item))
                        {
                            args.Add(item);
                        }
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Unsupported MCP command part type: {part.GetType().FullName}");
            }
        }

        return args;
    }

    private static IEnumerable<string?> WindowTarget(string? app, string? title, string? handle, string? window)
        => new[] { Opt("app", app), Opt("title", title), Opt("handle", handle), Opt("window", window) }.SelectMany(x => x);

    private static IEnumerable<string?> KeyboardTarget(string? app, string? title, string? handle, string? window, string? reference)
        => new[]
        {
            Opt("app", app),
            Opt("title", title),
            Opt("handle", handle),
            Opt("window", window),
            Opt("ref", reference)
        }.SelectMany(x => x);

    private static IEnumerable<string?> PointerTarget(int? screen, string? app, string? title, string? handle, string? window, string? reference)
        => new[]
        {
            Opt("screen", screen),
            Opt("app", app),
            Opt("title", title),
            Opt("handle", handle),
            Opt("window", window),
            Opt("ref", reference)
        }.SelectMany(x => x);

    private static int? ScaleWheelTicks(int? ticks) => ticks is null ? null : checked(-ticks.Value * NativeMethods.WHEEL_DELTA);

    private static IEnumerable<string?> Point(int? x, int? y)
        => new[] { Opt("x", x), Opt("y", y) }.SelectMany(x => x);

    private static IEnumerable<string?> WaitTiming(int? timeoutMs, int? intervalMs)
        => new[] { Opt("timeout-ms", timeoutMs), Opt("interval-ms", intervalMs) }.SelectMany(x => x);

    private static IEnumerable<string?> Opt(string name, string? value)
        => string.IsNullOrWhiteSpace(value) ? [] : [$"--{name}", value];

    private static IEnumerable<string?> Opt(string name, int? value)
        => value is null ? [] : [$"--{name}", value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)];

    private static IEnumerable<string?> Flag(string name, bool enabled)
        => enabled ? [$"--{name}"] : [];

    private static string? JoinCsv(string[]? values)
        => values is { Length: > 0 } ? string.Join(",", values) : null;
}
