using System.ComponentModel;
using ModelContextProtocol.Server;
using PeekWin.Cli;

namespace PeekWin.Mcp;

[McpServerToolType]
[Description("Named MCP tools for the peekwin command surface.")]
public sealed class PeekWinMcpTools
{
    private readonly CommandRunner _commandRunner;

    public PeekWinMcpTools(CommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    [McpServerTool(Name = "version", ReadOnly = true)]
    [Description("Return the peekwin version.")]
    public Task<CommandRunResult> Version(CancellationToken cancellationToken)
        => Run(cancellationToken, "version");

    [McpServerTool(Name = "get_help", ReadOnly = true)]
    [Description("Return top-level or command-specific peekwin help text.")]
    public Task<CommandRunResult> GetHelp(
        [Description("Optional command path. Example: [\"wait\", \"ref\"] to run `peekwin wait ref --help`.")]
        string[]? commandPath,
        CancellationToken cancellationToken)
    {
        string[] args;
        if (commandPath is { Length: > 0 })
        {
            args = [.. commandPath, "--help"];
        }
        else
        {
            args = ["--help"];
        }

        return Run(cancellationToken, args);
    }

    [McpServerTool(Name = "window_list", ReadOnly = true)]
    [Description("List windows, optionally including hidden ones and filtering by app or title.")]
    public Task<CommandRunResult> WindowList(bool all, string? app, string? title, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "window", "list", Flag("all", all), Opt("app", app), Opt("title", title));

    [McpServerTool(Name = "window_focus")]
    [Description("Focus a window by app, title, handle, or window alias.")]
    public Task<CommandRunResult> WindowFocus(string? app, string? title, string? handle, string? window, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "window", "focus", WindowTarget(app, title, handle, window));

    [McpServerTool(Name = "window_inspect", ReadOnly = true)]
    [Description("Inspect a window by app, title, handle, or window alias.")]
    public Task<CommandRunResult> WindowInspect(string? app, string? title, string? handle, string? window, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "window", "inspect", WindowTarget(app, title, handle, window));

    [McpServerTool(Name = "window_move")]
    [Description("Move a window to an absolute screen position.")]
    public Task<CommandRunResult> WindowMove(int x, int y, string? app, string? title, string? handle, string? window, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "window", "move", WindowTarget(app, title, handle, window), Opt("x", x), Opt("y", y));

    [McpServerTool(Name = "window_resize")]
    [Description("Resize a window to the requested width and height.")]
    public Task<CommandRunResult> WindowResize(int width, int height, string? app, string? title, string? handle, string? window, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "window", "resize", WindowTarget(app, title, handle, window), Opt("width", width), Opt("height", height));

    [McpServerTool(Name = "window_close")]
    [Description("Close a target window.")]
    public Task<CommandRunResult> WindowClose(string? app, string? title, string? handle, string? window, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "window", "close", WindowTarget(app, title, handle, window));

    [McpServerTool(Name = "window_minimize")]
    [Description("Minimize a target window.")]
    public Task<CommandRunResult> WindowMinimize(string? app, string? title, string? handle, string? window, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "window", "minimize", WindowTarget(app, title, handle, window));

    [McpServerTool(Name = "window_maximize")]
    [Description("Maximize a target window.")]
    public Task<CommandRunResult> WindowMaximize(string? app, string? title, string? handle, string? window, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "window", "maximize", WindowTarget(app, title, handle, window));

    [McpServerTool(Name = "window_restore")]
    [Description("Restore a minimized or maximized window.")]
    public Task<CommandRunResult> WindowRestore(string? app, string? title, string? handle, string? window, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "window", "restore", WindowTarget(app, title, handle, window));

    [McpServerTool(Name = "app_list", ReadOnly = true)]
    [Description("List apps grouped by process name.")]
    public Task<CommandRunResult> AppList(string? name, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "app", "list", Opt("name", name));

    [McpServerTool(Name = "desktop_list", ReadOnly = true)]
    [Description("List virtual desktops.")]
    public Task<CommandRunResult> DesktopList(CancellationToken cancellationToken)
        => RunJson(cancellationToken, "desktop", "list");

    [McpServerTool(Name = "desktop_current", ReadOnly = true)]
    [Description("Return the current virtual desktop.")]
    public Task<CommandRunResult> DesktopCurrent(CancellationToken cancellationToken)
        => RunJson(cancellationToken, "desktop", "current");

    [McpServerTool(Name = "desktop_switch")]
    [Description("Switch to a virtual desktop by index.")]
    public Task<CommandRunResult> DesktopSwitch(int index, int? delayMs, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "desktop", "switch", Opt("index", index), Opt("delay-ms", delayMs));

    [McpServerTool(Name = "screens", ReadOnly = true)]
    [Description("Return monitor layout information.")]
    public Task<CommandRunResult> Screens(CancellationToken cancellationToken)
        => RunJson(cancellationToken, "screens");

    [McpServerTool(Name = "image_info", ReadOnly = true)]
    [Description("Return image capture layout information.")]
    public Task<CommandRunResult> ImageInfo(CancellationToken cancellationToken)
        => RunJson(cancellationToken, "image", "info");

    [McpServerTool(Name = "screenshot_info", ReadOnly = true)]
    [Description("Return screenshot capture layout information.")]
    public Task<CommandRunResult> ScreenshotInfo(CancellationToken cancellationToken)
        => RunJson(cancellationToken, "screenshot", "info");

    [McpServerTool(Name = "pointer_move")]
    [Description("Move the mouse pointer, optionally relative to a target.")]
    public Task<CommandRunResult> PointerMove(int? x, int? y, int? screen, string? app, string? title, string? handle, string? window, string? reference, bool focus, int? durationMs, int? steps, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "move", Point(x, y), PointerTarget(screen, app, title, handle, window, reference), Flag("focus", focus), Opt("duration-ms", durationMs), Opt("steps", steps));

    [McpServerTool(Name = "click")]
    [Description("Click the mouse, optionally at a point relative to a target.")]
    public Task<CommandRunResult> Click(int? x, int? y, int? screen, string? app, string? title, string? handle, string? window, string? reference, bool focus, string? button, bool @double, int? delayMs, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "click", Point(x, y), PointerTarget(screen, app, title, handle, window, reference), Flag("focus", focus), Opt("button", button), Flag("double", @double), Opt("delay-ms", delayMs));

    [McpServerTool(Name = "drag")]
    [Description("Drag from one point to another, optionally relative to a target.")]
    public Task<CommandRunResult> Drag(int? x, int? y, int toX, int toY, int? screen, string? app, string? title, string? handle, string? window, string? reference, bool focus, string? button, int? durationMs, int? steps, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "drag", Point(x, y), PointerTarget(screen, app, title, handle, window, reference), Opt("to-x", toX), Opt("to-y", toY), Flag("focus", focus), Opt("button", button), Opt("duration-ms", durationMs), Opt("steps", steps));

    [McpServerTool(Name = "scroll")]
    [Description("Scroll vertically and/or horizontally, optionally relative to a target.")]
    public Task<CommandRunResult> Scroll(int? delta, int? deltaX, int? x, int? y, int? screen, string? app, string? title, string? handle, string? window, string? reference, bool focus, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "scroll", Point(x, y), PointerTarget(screen, app, title, handle, window, reference), Opt("delta", delta), Opt("delta-x", deltaX), Flag("focus", focus));

    [McpServerTool(Name = "mouse_down")]
    [Description("Press a mouse button, optionally at a target point.")]
    public Task<CommandRunResult> MouseDown(string? button, int? x, int? y, int? screen, string? app, string? title, string? handle, string? window, string? reference, bool focus, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "mouse", "down", Opt("button", button), Point(x, y), PointerTarget(screen, app, title, handle, window, reference), Flag("focus", focus));

    [McpServerTool(Name = "mouse_up")]
    [Description("Release a mouse button, optionally at a target point.")]
    public Task<CommandRunResult> MouseUp(string? button, int? x, int? y, int? screen, string? app, string? title, string? handle, string? window, string? reference, bool focus, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "mouse", "up", Opt("button", button), Point(x, y), PointerTarget(screen, app, title, handle, window, reference), Flag("focus", focus));

    [McpServerTool(Name = "ref_click")]
    [Description("Activate a saved UI ref, preferring UI Automation invoke.")]
    public Task<CommandRunResult> RefClick(string reference, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "ref", "click", Opt("ref", reference));

    [McpServerTool(Name = "ref_focus")]
    [Description("Focus a saved UI ref.")]
    public Task<CommandRunResult> RefFocus(string reference, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "ref", "focus", Opt("ref", reference));

    [McpServerTool(Name = "type_text")]
    [Description("Type or paste text into a target window or saved ref.")]
    public Task<CommandRunResult> TypeText(string text, string? method, int? delayMs, string? app, string? title, string? handle, string? window, string? reference, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "type", Opt("text", text), Opt("method", method), Opt("delay-ms", delayMs), KeyboardTarget(app, title, handle, window, reference));

    [McpServerTool(Name = "paste_text")]
    [Description("Paste text into a target window or saved ref.")]
    public Task<CommandRunResult> PasteText(string text, int? delayMs, string? app, string? title, string? handle, string? window, string? reference, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "paste", Opt("text", text), Opt("delay-ms", delayMs), KeyboardTarget(app, title, handle, window, reference));

    [McpServerTool(Name = "press_key")]
    [Description("Press a single key one or more times in a target window or saved ref.")]
    public Task<CommandRunResult> PressKey(string key, int? repeat, int? delayMs, string? app, string? title, string? handle, string? window, string? reference, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "press", Opt("key", key), Opt("repeat", repeat), Opt("delay-ms", delayMs), KeyboardTarget(app, title, handle, window, reference));

    [McpServerTool(Name = "send_hotkey")]
    [Description("Send a hotkey chord like ctrl+s to a target window or saved ref.")]
    public Task<CommandRunResult> SendHotkey(string[] keys, string? app, string? title, string? handle, string? window, string? reference, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "hotkey", Opt("keys", JoinCsv(keys)), KeyboardTarget(app, title, handle, window, reference));

    [McpServerTool(Name = "send_keys")]
    [Description("Send a sequence of key steps such as tap:tab or sleep:100.")]
    public Task<CommandRunResult> SendKeys(string[] steps, int? delayMs, string? app, string? title, string? handle, string? window, string? reference, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "keys", Opt("steps", JoinCsv(steps)), Opt("delay-ms", delayMs), KeyboardTarget(app, title, handle, window, reference));

    [McpServerTool(Name = "see_ui", ReadOnly = true)]
    [Description("Inspect the UI Automation tree for the foreground window or a targeted window.")]
    public Task<CommandRunResult> SeeUi(string? app, string? title, string? handle, string? window, bool deep, int? maxDepth, string? role, string? name, bool all, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "see", WindowTarget(app, title, handle, window), Flag("deep", deep), Opt("max-depth", maxDepth), Opt("role", role), Opt("name", name), Flag("all", all));

    [McpServerTool(Name = "hold_input")]
    [Description("Hold keys or a mouse button for a duration.")]
    public Task<CommandRunResult> HoldInput(string[]? keys, string? button, int? durationMs, int? x, int? y, int? screen, string? app, string? title, string? handle, string? window, string? reference, bool focus, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "hold",
            Opt("keys", JoinCsv(keys)),
            Opt("button", button),
            Opt("duration-ms", durationMs),
            Point(x, y),
            (button is not null ? PointerTarget(screen, app, title, handle, window, reference) : KeyboardTarget(app, title, handle, window, reference)),
            Flag("focus", focus));

    [McpServerTool(Name = "capture_image")]
    [Description("Capture an image of a screen, window, or saved ref.")]
    public Task<CommandRunResult> CaptureImage(string? output, int? screen, string? app, string? title, string? handle, string? window, string? reference, bool focus, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "image", Opt("output", output), PointerTarget(screen, app, title, handle, window, reference), Flag("focus", focus));

    [McpServerTool(Name = "capture_screenshot")]
    [Description("Alias for image capture, using the screenshot command name.")]
    public Task<CommandRunResult> CaptureScreenshot(string? output, int? screen, string? app, string? title, string? handle, string? window, string? reference, bool focus, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "screenshot", Opt("output", output), PointerTarget(screen, app, title, handle, window, reference), Flag("focus", focus));

    [McpServerTool(Name = "wait_window")]
    [Description("Wait for a window to reach a state like visible, focused, or gone.")]
    public Task<CommandRunResult> WaitWindow(string state, string? app, string? title, string? handle, string? window, int? timeoutMs, int? intervalMs, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "wait", "window", WindowTarget(app, title, handle, window), Opt("state", state), WaitTiming(timeoutMs, intervalMs));

    [McpServerTool(Name = "wait_ref")]
    [Description("Wait for a saved UI ref to reach a state like visible or enabled.")]
    public Task<CommandRunResult> WaitRef(string reference, string state, int? timeoutMs, int? intervalMs, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "wait", "ref", Opt("ref", reference), Opt("state", state), WaitTiming(timeoutMs, intervalMs));

    [McpServerTool(Name = "wait_text")]
    [Description("Wait until window title text or a saved ref name contains the requested text.")]
    public Task<CommandRunResult> WaitText(string contains, string? app, string? title, string? handle, string? window, string? reference, int? timeoutMs, int? intervalMs, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "wait", "text", KeyboardTarget(app, title, handle, window, reference), Opt("contains", contains), WaitTiming(timeoutMs, intervalMs));

    [McpServerTool(Name = "clipboard_get", ReadOnly = true)]
    [Description("Get current clipboard text.")]
    public Task<CommandRunResult> ClipboardGet(CancellationToken cancellationToken)
        => RunJson(cancellationToken, "clipboard", "get");

    [McpServerTool(Name = "clipboard_set")]
    [Description("Set current clipboard text.")]
    public Task<CommandRunResult> ClipboardSet(string text, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "clipboard", "set", Opt("text", text));

    [McpServerTool(Name = "sleep")]
    [Description("Sleep for the requested duration in milliseconds.")]
    public Task<CommandRunResult> Sleep(int durationMs, CancellationToken cancellationToken)
        => RunJson(cancellationToken, "sleep", Opt("duration-ms", durationMs));

    private Task<CommandRunResult> RunJson(CancellationToken cancellationToken, params object?[] parts)
    {
        var args = Flatten(parts);
        args.Add("--json");
        return _commandRunner.RunAsync(args, cancellationToken);
    }

    private Task<CommandRunResult> Run(CancellationToken cancellationToken, params object?[] parts)
        => _commandRunner.RunAsync(Flatten(parts), cancellationToken);

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
