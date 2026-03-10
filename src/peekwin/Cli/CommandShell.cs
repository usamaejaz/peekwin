using System.Globalization;
using System.Text.Json;
using PeekWin.Models;
using PeekWin.Services;

namespace PeekWin.Cli;

public sealed class CommandShell
{
    private readonly WindowService _windowService;
    private readonly InputService _inputService;
    private readonly ScreenshotService _screenshotService;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public CommandShell(WindowService windowService, InputService inputService, ScreenshotService screenshotService)
    {
        _windowService = windowService;
        _inputService = inputService;
        _screenshotService = screenshotService;
    }

    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "window" => await HandleWindowAsync(args[1..]),
                "click" => HandleClick(args[1..]),
                "type" => HandleType(args[1..]),
                "press" => HandlePress(args[1..]),
                "hotkey" => HandleHotkey(args[1..]),
                "hold" => HandleHold(args[1..]),
                "screenshot" => HandleScreenshot(args[1..]),
                "help" or "--help" or "-h" => HelpAndSuccess(),
                _ => Fail($"Unknown command: {args[0]}")
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private int HelpAndSuccess()
    {
        PrintHelp();
        return 0;
    }

    private async Task<int> HandleWindowAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Missing window subcommand.");
            return 1;
        }

        return args[0].ToLowerInvariant() switch
        {
            "list" => HandleWindowList(args[1..]),
            "focus" => HandleWindowFocus(args[1..]),
            "inspect" => HandleWindowInspect(args[1..]),
            _ => Fail($"Unknown window subcommand: {args[0]}")
        };
    }

    private int HandleWindowList(string[] args)
    {
        var options = OptionSet.Parse(args);
        var windows = _windowService.ListWindows();

        if (options.HasFlag("json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(windows, JsonOptions));
            return 0;
        }

        foreach (var window in windows)
        {
            Console.WriteLine($"0x{window.Handle:X} [{window.DesktopLabel}] {(window.IsVisible ? "visible" : "hidden")} - {window.Title}");
        }

        return 0;
    }

    private int HandleWindowFocus(string[] args)
    {
        var options = OptionSet.Parse(args);
        nint handle = options.TryGetHandle("handle");
        string? title = options.GetValueOrDefault("title");

        if (handle == 0 && string.IsNullOrWhiteSpace(title))
        {
            return Fail("window focus requires --handle or --title.");
        }

        var result = handle != 0
            ? _windowService.FocusWindow(handle)
            : _windowService.FocusWindowByTitle(title!);

        WriteResult(result, options.HasFlag("json"));
        return result.Success ? 0 : 1;
    }

    private int HandleWindowInspect(string[] args)
    {
        var options = OptionSet.Parse(args);
        var handle = options.TryGetHandle("handle");
        if (handle == 0)
        {
            return Fail("window inspect requires --handle.");
        }

        var inspection = _windowService.InspectWindow(handle);
        if (options.HasFlag("json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(inspection, JsonOptions));
            return 0;
        }

        Console.WriteLine($"Handle: 0x{inspection.Handle:X}");
        Console.WriteLine($"Title: {inspection.Title}");
        Console.WriteLine($"Class: {inspection.ClassName}");
        Console.WriteLine($"PID: {inspection.ProcessId}");
        Console.WriteLine($"Bounds: {inspection.Bounds.Left},{inspection.Bounds.Top} {inspection.Bounds.Width}x{inspection.Bounds.Height}");
        Console.WriteLine($"Desktop: {inspection.DesktopLabel}");
        Console.WriteLine($"Visible: {inspection.IsVisible}");
        Console.WriteLine($"Minimized: {inspection.IsMinimized}");
        Console.WriteLine($"Maximized: {inspection.IsMaximized}");
        Console.WriteLine("Top-level automation children:");
        foreach (var element in inspection.Elements)
        {
            Console.WriteLine($"- {element.ControlType} | {element.Name} | AutomationId={element.AutomationId} | Bounds={element.Bounds.Left},{element.Bounds.Top} {element.Bounds.Width}x{element.Bounds.Height}");
        }

        return 0;
    }

    private int HandleClick(string[] args)
    {
        var options = OptionSet.Parse(args);
        int x = options.GetInt("x") ?? throw new InvalidOperationException("click requires --x.");
        int y = options.GetInt("y") ?? throw new InvalidOperationException("click requires --y.");
        var button = ParseMouseButton(options.GetValueOrDefault("button") ?? "left");
        bool isDouble = options.HasFlag("double");

        _inputService.Click(x, y, button, isDouble);
        WriteResult(CommandResult.Ok($"Clicked {button} at {x},{y}."), options.HasFlag("json"));
        return 0;
    }

    private int HandleType(string[] args)
    {
        var options = OptionSet.Parse(args);
        string text = options.GetValueOrDefault("text") ?? throw new InvalidOperationException("type requires --text.");
        int delayMs = options.GetInt("delay-ms") ?? 0;
        _inputService.TypeText(text, delayMs);
        WriteResult(CommandResult.Ok($"Typed {text.Length} characters."), options.HasFlag("json"));
        return 0;
    }

    private int HandlePress(string[] args)
    {
        var options = OptionSet.Parse(args);
        string key = options.GetValueOrDefault("key") ?? throw new InvalidOperationException("press requires --key.");
        int repeat = options.GetInt("repeat") ?? 1;
        _inputService.PressKey(key, repeat);
        WriteResult(CommandResult.Ok($"Pressed {key} x{repeat}."), options.HasFlag("json"));
        return 0;
    }

    private int HandleHotkey(string[] args)
    {
        var options = OptionSet.Parse(args);
        string keys = options.GetValueOrDefault("keys") ?? throw new InvalidOperationException("hotkey requires --keys.");
        _inputService.Hotkey(keys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        WriteResult(CommandResult.Ok($"Sent hotkey {keys}."), options.HasFlag("json"));
        return 0;
    }

    private int HandleHold(string[] args)
    {
        if (args.Length == 0)
        {
            return Fail("hold requires a target: key or mouse.");
        }

        var options = OptionSet.Parse(args[1..]);
        int durationMs = options.GetInt("duration-ms") ?? 1000;
        bool json = options.HasFlag("json");

        switch (args[0].ToLowerInvariant())
        {
            case "key":
                string key = options.GetValueOrDefault("key") ?? throw new InvalidOperationException("hold key requires --key.");
                _inputService.HoldKey(key, durationMs);
                WriteResult(CommandResult.Ok($"Held {key} for {durationMs}ms."), json);
                return 0;
            case "mouse":
                var button = ParseMouseButton(options.GetValueOrDefault("button") ?? "left");
                _inputService.HoldMouse(button, durationMs);
                WriteResult(CommandResult.Ok($"Held {button} mouse button for {durationMs}ms."), json);
                return 0;
            default:
                return Fail($"Unknown hold target: {args[0]}");
        }
    }

    private int HandleScreenshot(string[] args)
    {
        var options = OptionSet.Parse(args);
        string output = options.GetValueOrDefault("output") ?? Path.Combine(Environment.CurrentDirectory, $"peekwin-{DateTime.UtcNow:yyyyMMddHHmmss}.png");
        int? screenIndex = options.GetInt("screen");
        nint windowHandle = options.TryGetHandle("window");

        var result = _screenshotService.Capture(output, screenIndex, windowHandle);
        WriteResult(result, options.HasFlag("json"));
        return result.Success ? 0 : 1;
    }

    private static MouseButton ParseMouseButton(string value) => value.ToLowerInvariant() switch
    {
        "left" => MouseButton.Left,
        "right" => MouseButton.Right,
        _ => throw new InvalidOperationException($"Unsupported mouse button: {value}")
    };

    private static void WriteResult(CommandResult result, bool asJson)
    {
        if (asJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return;
        }

        Console.WriteLine(result.Message);
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("peekwin - Windows-native automation CLI");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  peekwin window list [--json]");
        Console.WriteLine("  peekwin window focus --handle <HWND>|--title <text> [--json]");
        Console.WriteLine("  peekwin window inspect --handle <HWND> [--json]");
        Console.WriteLine("  peekwin click --x <n> --y <n> [--button left|right] [--double] [--json]");
        Console.WriteLine("  peekwin type --text <value> [--delay-ms <n>] [--json]");
        Console.WriteLine("  peekwin press --key <name> [--repeat <n>] [--json]");
        Console.WriteLine("  peekwin hotkey --keys ctrl,s [--json]");
        Console.WriteLine("  peekwin hold key --key <name> [--duration-ms <n>] [--json]");
        Console.WriteLine("  peekwin hold mouse --button left|right [--duration-ms <n>] [--json]");
        Console.WriteLine("  peekwin screenshot [--screen <n>|--window <HWND>] [--output <path>] [--json]");
    }
}

internal sealed class OptionSet
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _flags = new(StringComparer.OrdinalIgnoreCase);

    private OptionSet() { }

    public static OptionSet Parse(IReadOnlyList<string> args)
    {
        var set = new OptionSet();
        for (int i = 0; i < args.Count; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unexpected token: {token}");
            }

            var key = token[2..];
            if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                set._values[key] = args[++i];
            }
            else
            {
                set._flags.Add(key);
            }
        }

        return set;
    }

    public bool HasFlag(string key) => _flags.Contains(key);

    public string? GetValueOrDefault(string key) => _values.GetValueOrDefault(key);

    public int? GetInt(string key)
        => int.TryParse(GetValueOrDefault(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    public nint TryGetHandle(string key)
    {
        var value = GetValueOrDefault(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && long.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
        {
            return (nint)hex;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dec))
        {
            return (nint)dec;
        }

        throw new InvalidOperationException($"Invalid handle: {value}");
    }
}
