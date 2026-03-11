using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using PeekWin.Models;
using PeekWin.Services;

namespace PeekWin.Cli;

public sealed class CommandShell
{
    private readonly WindowService _windowService;
    private readonly InputService _inputService;
    private readonly ScreenshotService _screenshotService;
    private readonly VirtualDesktopService _virtualDesktopService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CommandShell(WindowService windowService, InputService inputService, ScreenshotService screenshotService, VirtualDesktopService virtualDesktopService)
    {
        _windowService = windowService;
        _inputService = inputService;
        _screenshotService = screenshotService;
        _virtualDesktopService = virtualDesktopService;
    }

    public async Task<int> RunAsync(string[] args)
    {
        var (filteredArgs, verbose) = ExtractGlobalFlags(args);
        args = filteredArgs;

        if (args.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "help" or "--help" or "-h" => HelpAndSuccess(),
                "version" => VersionAndSuccess(),
                "window" => await HandleWindowAsync(args[1..]),
                "app" => HandleApp(args[1..]),
                "desktop" => await HandleDesktopAsync(args[1..]),
                "screens" => HandleScreens(args[1..]),
                "click" => await HandleClickAsync(args[1..]),
                "move" => await HandleMoveAsync(args[1..]),
                "drag" => await HandleDragAsync(args[1..]),
                "scroll" => HandleScroll(args[1..]),
                "mouse" => HandleMouse(args[1..]),
                "type" => await HandleTypeAsync(args[1..]),
                "paste" => await HandlePasteAsync(args[1..]),
                "press" => await HandlePressAsync(args[1..]),
                "hotkey" => HandleHotkey(args[1..]),
                "hold" => await HandleHoldAsync(args[1..]),
                "image" => HandleImageCommand(args[1..], "image"),
                "screenshot" => HandleImageCommand(args[1..], "screenshot"),
                "sleep" => await HandleSleepAsync(args[1..]),
                _ => Fail(BuildCommandName(args), $"Unknown command: {args[0]}", RequestedJson(args))
            };
        }
        catch (Exception ex)
        {
            if (RequestedJson(args))
            {
                WriteJsonEnvelope(false, BuildCommandName(args), null, ex.Message);
            }
            else
            {
                Console.Error.WriteLine(ex.Message);
                if (verbose)
                {
                    Console.Error.WriteLine(ex);
                }
            }

            return 1;
        }
    }

    private int HelpAndSuccess()
    {
        PrintHelp();
        return 0;
    }

    private static int VersionAndSuccess()
    {
        Console.WriteLine(GetVersionText());
        return 0;
    }

    public static string GetVersionText()
    {
        var assembly = typeof(CommandShell).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        var assemblyVersion = assembly.GetName().Version?.ToString();
        return string.IsNullOrWhiteSpace(assemblyVersion) ? "unknown" : assemblyVersion;
    }

    public static void PrintRequestedHelp(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            PrintHelp();
            return;
        }

        var first = args[0].ToLowerInvariant();
        switch (first)
        {
            case "help":
            case "--help":
            case "-h":
                PrintHelp();
                return;
            case "window":
                PrintWindowHelp();
                return;
            case "app":
                PrintAppHelp();
                return;
            case "desktop":
                PrintDesktopHelp();
                return;
            case "mouse":
                PrintMouseHelp();
                return;
            case "hold":
                PrintHoldHelp();
                return;
            case "image":
            case "screenshot":
                PrintImageHelp();
                return;
            default:
                PrintHelp();
                return;
        }
    }

    private Task<int> HandleWindowAsync(string[] args)
    {
        if (IsHelpRequest(args))
        {
            PrintWindowHelp();
            return Task.FromResult(0);
        }

        if (args.Length == 0)
        {
            return Task.FromResult(Fail("window", "Missing window subcommand.", false));
        }

        var result = args[0].ToLowerInvariant() switch
        {
            "list" => HandleWindowList(args[1..]),
            "focus" => HandleWindowAction(args[1..], "window focus", _windowService.FocusWindow),
            "inspect" => HandleWindowInspect(args[1..]),
            "close" => HandleWindowAction(args[1..], "window close", _windowService.CloseWindow),
            "minimize" => HandleWindowAction(args[1..], "window minimize", _windowService.MinimizeWindow),
            "maximize" => HandleWindowAction(args[1..], "window maximize", _windowService.MaximizeWindow),
            "restore" => HandleWindowAction(args[1..], "window restore", _windowService.RestoreWindow),
            _ => Fail("window", $"Unknown window subcommand: {args[0]}", RequestedJson(args))
        };

        return Task.FromResult(result);
    }

    private int HandleWindowList(string[] args)
    {
        if (IsHelpRequest(args))
        {
            Console.WriteLine("Usage: peekwin window list [--all] [--app <name>] [--title <text>] [--json]");
            return 0;
        }

        const string command = "window list";
        if (!TryParseOptions(command, args, out var options))
        {
            return 1;
        }

        EnsureNoPositionals(command, options);
        var windows = _windowService.ListWindows(
            includeHidden: options.HasFlag("all"),
            appFilter: options.GetValueOrDefault("app"),
            titleFilter: options.GetValueOrDefault("title"));

        if (options.HasFlag("json"))
        {
            WriteJsonEnvelope(true, command, new { windows });
            return 0;
        }

        foreach (var window in windows)
        {
            Console.WriteLine($"{window.Handle} [{window.ProcessName}] [{window.DesktopLabel}] {(window.IsVisible ? "visible" : "hidden")} - {window.Title} ({FormatRect(window.Bounds)})");
        }

        return 0;
    }

    private int HandleWindowAction(
        string[] args,
        string command,
        Func<nint, CommandResult> handleAction)
    {
        if (IsHelpRequest(args))
        {
            Console.WriteLine($"Usage: peekwin {command} (--app <name> | --title <text> | --handle <HWND> | --window <HWND>) [--json]");
            return 0;
        }

        if (!TryParseOptions(command, args, out var options))
        {
            return 1;
        }

        EnsureNoPositionals(command, options);
        var target = ParseTarget(command, options, allowScreen: false, allowWindow: true, requireTarget: true);
        var resolvedTarget = ResolveWindowTarget(command, target)!;
        var result = handleAction(resolvedTarget.WindowHandle!.Value);

        WriteResult(command, result, options.HasFlag("json"));
        return result.Success ? 0 : 1;
    }

    private int HandleWindowInspect(string[] args)
    {
        const string command = "window inspect";
        if (IsHelpRequest(args))
        {
            Console.WriteLine("Usage: peekwin window inspect (--app <name> | --title <text> | --handle <HWND> | --window <HWND>) [--json]");
            return 0;
        }

        if (!TryParseOptions(command, args, out var options))
        {
            return 1;
        }

        EnsureNoPositionals(command, options);
        var target = ParseTarget(command, options, allowScreen: false, allowWindow: true, requireTarget: true);
        var resolvedTarget = ResolveWindowTarget(command, target)!;
        var inspection = _windowService.InspectWindow(resolvedTarget.WindowHandle!.Value);

        if (options.HasFlag("json"))
        {
            WriteJsonEnvelope(true, command, new { window = inspection });
            return 0;
        }

        Console.WriteLine($"Handle: {inspection.Handle}");
        Console.WriteLine($"Title: {inspection.Title}");
        Console.WriteLine($"Class: {inspection.ClassName}");
        Console.WriteLine($"PID: {inspection.ProcessId}");
        Console.WriteLine($"Process: {inspection.ProcessName}");
        Console.WriteLine($"Bounds: {FormatRect(inspection.Bounds)}");
        Console.WriteLine($"Desktop: {inspection.DesktopLabel}");
        Console.WriteLine($"Visible: {inspection.IsVisible}");
        Console.WriteLine($"Minimized: {inspection.IsMinimized}");
        Console.WriteLine($"Maximized: {inspection.IsMaximized}");
        Console.WriteLine("Top-level automation children:");
        foreach (var element in inspection.Elements)
        {
            Console.WriteLine($"- {element.ControlType} | {element.Name} | AutomationId={element.AutomationId} | Bounds={FormatRect(element.Bounds)}");
        }

        return 0;
    }

    private int HandleApp(string[] args)
    {
        if (IsHelpRequest(args))
        {
            PrintAppHelp();
            return 0;
        }

        if (args.Length == 0)
        {
            return Fail("app", "Missing app subcommand.", false);
        }

        return args[0].ToLowerInvariant() switch
        {
            "list" => HandleAppList(args[1..]),
            _ => Fail("app", $"Unknown app subcommand: {args[0]}", RequestedJson(args))
        };
    }

    private Task<int> HandleDesktopAsync(string[] args)
    {
        if (IsHelpRequest(args))
        {
            PrintDesktopHelp();
            return Task.FromResult(0);
        }

        if (args.Length == 0)
        {
            return Task.FromResult(Fail("desktop", "Missing desktop subcommand.", false));
        }

        var result = args[0].ToLowerInvariant() switch
        {
            "list" => Task.FromResult(HandleDesktopList(args[1..])),
            "current" => Task.FromResult(HandleDesktopCurrent(args[1..])),
            "switch" => HandleDesktopSwitchAsync(args[1..]),
            _ => Task.FromResult(Fail("desktop", $"Unknown desktop subcommand: {args[0]}", RequestedJson(args)))
        };

        return result;
    }

    private int HandleDesktopList(string[] args)
    {
        const string command = "desktop list";
        if (IsHelpRequest(args))
        {
            Console.WriteLine("Usage: peekwin desktop list [--json]");
            return 0;
        }

        if (!TryParseOptions(command, args, out var options))
        {
            return 1;
        }

        EnsureNoPositionals(command, options);
        var desktops = _virtualDesktopService.ListDesktops();

        if (options.HasFlag("json"))
        {
            WriteJsonEnvelope(true, command, new { desktops });
            return 0;
        }

        foreach (var desktop in desktops)
        {
            Console.WriteLine($"Desktop {desktop.Index}{(desktop.IsCurrent ? " [current]" : string.Empty)} id={desktop.Id}");
        }

        return 0;
    }

    private int HandleDesktopCurrent(string[] args)
    {
        const string command = "desktop current";
        if (IsHelpRequest(args))
        {
            Console.WriteLine("Usage: peekwin desktop current [--json]");
            return 0;
        }

        if (!TryParseOptions(command, args, out var options))
        {
            return 1;
        }

        EnsureNoPositionals(command, options);
        var desktop = _virtualDesktopService.GetCurrentDesktop();

        if (options.HasFlag("json"))
        {
            WriteJsonEnvelope(true, command, new { desktop });
            return 0;
        }

        Console.WriteLine($"Desktop {desktop.Index} [current] id={desktop.Id}");
        return 0;
    }

    private async Task<int> HandleDesktopSwitchAsync(string[] args)
    {
        const string command = "desktop switch";
        if (IsHelpRequest(args))
        {
            Console.WriteLine("Usage: peekwin desktop switch <index> [--delay-ms <n>] [--json]");
            Console.WriteLine("       peekwin desktop switch --index <n> [--delay-ms <n>] [--json]");
            return 0;
        }

        if (!TryParseOptions(command, args, out var options))
        {
            return 1;
        }

        var targetIndex = ResolveDesktopIndex(command, options);
        var delayMs = ReadNonNegativeInt(options, "delay-ms") ?? 125;
        var result = await _virtualDesktopService.SwitchDesktopAsync(targetIndex, delayMs).ConfigureAwait(false);
        WriteResult(command, result, options.HasFlag("json"));
        return result.Success ? 0 : 1;
    }

    private int HandleAppList(string[] args)
    {
        const string command = "app list";
        if (IsHelpRequest(args))
        {
            Console.WriteLine("Usage: peekwin app list [--all] [--name <text>] [--json]");
            return 0;
        }

        if (!TryParseOptions(command, args, out var options))
        {
            return 1;
        }

        EnsureNoPositionals(command, options);
        var apps = _windowService.ListApps(includeHidden: options.HasFlag("all"));
        var nameFilter = options.GetValueOrDefault("name");
        if (!string.IsNullOrWhiteSpace(nameFilter))
        {
            apps = apps
                .Where(app => app.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (options.HasFlag("json"))
        {
            WriteJsonEnvelope(true, command, new { apps });
            return 0;
        }

        foreach (var app in apps)
        {
            Console.WriteLine($"{app.Name} | windows={app.WindowCount} visible={app.VisibleWindowCount} pids={string.Join(",", app.ProcessIds)}");
        }

        return 0;
    }

    private int HandleScreens(string[] args)
    {
        const string command = "screens";
        if (IsHelpRequest(args))
        {
            Console.WriteLine("Usage: peekwin screens [--json]");
            return 0;
        }

        if (!TryParseOptions(command, args, out var options))
        {
            return 1;
        }

        EnsureNoPositionals(command, options);
        var layout = _screenshotService.GetScreenLayout();

        if (options.HasFlag("json"))
        {
            WriteJsonEnvelope(true, command, layout);
            return 0;
        }

        Console.WriteLine($"Virtual desktop: {FormatRect(layout.VirtualBounds)}");
        foreach (var screen in layout.Screens)
        {
            Console.WriteLine($"Screen {screen.Index}: {screen.DeviceName} {(screen.IsPrimary ? "[primary]" : string.Empty)} bounds={FormatRect(screen.Bounds)} work={FormatRect(screen.WorkArea)}".Trim());
        }

        return 0;
    }

    private async Task<int> HandleClickAsync(string[] args)
    {
        const string command = "click";
        if (IsHelpRequest(args))
        {
            Console.WriteLine("Usage: peekwin click [--x <n> --y <n>] [--screen <n> | --app <name> | --title <text> | --handle <HWND> | --window <HWND>] [--button left|right] [--double] [--delay-ms <n>] [--json]");
            return 0;
        }

        if (!TryParseOptions(command, args, out var options))
        {
            return 1;
        }

        EnsureNoPositionals(command, options);
        var target = ParseTarget(command, options, allowScreen: true, allowWindow: true);
        var resolvedTarget = ResolveBoundsTarget(command, target);
        var point = ResolvePoint(command, options, resolvedTarget, requirePointIfNoTarget: true, defaultToCenterWhenTargeted: true);
        var button = ParseMouseButton(options.GetValueOrDefault("button") ?? "left");
        var isDouble = options.HasFlag("double");
        var delayMs = ReadNonNegativeInt(options, "delay-ms") ?? (isDouble ? 60 : 0);

        await _inputService.ClickAsync(point.X, point.Y, button, isDouble, delayMs);
        WriteResult(
            command,
            CommandResult.Ok(
                $"Clicked {button.ToString().ToLowerInvariant()} at {FormatPointForMessage(resolvedTarget, point.X, point.Y)}.",
                details: new { button, point = ToPointData(point.X, point.Y), relativePoint = ToRelativePointData(resolvedTarget, point.X, point.Y), target = ToTargetData(resolvedTarget) }),
            options.HasFlag("json"));
        return 0;
    }

    private async Task<int> HandleMoveAsync(string[] args)
    {
        const string command = "move";
        if (IsHelpRequest(args))
        {
            Console.WriteLine("Usage: peekwin move --x <n> --y <n> [--screen <n> | --app <name> | --title <text> | --handle <HWND> | --window <HWND>] [--duration-ms <n>] [--steps <n>] [--json]");
            return 0;
        }

        if (!TryParseOptions(command, args, out var options))
        {
            return 1;
        }

        EnsureNoPositionals(command, options);
        var target = ParseTarget(command, options, allowScreen: true, allowWindow: true);
        var resolvedTarget = ResolveBoundsTarget(command, target);
        var point = ResolvePoint(command, options, resolvedTarget, requirePointIfNoTarget: true, defaultToCenterWhenTargeted: false);
        var durationMs = ReadNonNegativeInt(options, "duration-ms") ?? 0;
        var steps = ReadPositiveInt(options, "steps") ?? 12;

        await _inputService.MoveMouseAsync(point.X, point.Y, durationMs, steps).ConfigureAwait(false);
        WriteResult(
            command,
            CommandResult.Ok(
                $"Moved cursor to {FormatPointForMessage(resolvedTarget, point.X, point.Y)}.",
                details: new { point = ToPointData(point.X, point.Y), relativePoint = ToRelativePointData(resolvedTarget, point.X, point.Y), durationMs, steps, target = ToTargetData(resolvedTarget) }),
            options.HasFlag("json"));
        return 0;
    }

    private async Task<int> HandleDragAsync(string[] args)
    {
        const string command = "drag";
        if (IsHelpRequest(args))
        {
            Console.WriteLine("Usage: peekwin drag --x <n> --y <n> --to-x <n> --to-y <n> [--screen <n> | --app <name> | --title <text> | --handle <HWND> | --window <HWND>] [--button left|right] [--duration-ms <n>] [--steps <n>] [--json]");
            return 0;
        }

        if (!TryParseOptions(command, args, out var options))
        {
            return 1;
        }

        EnsureNoPositionals(command, options);
        var target = ParseTarget(command, options, allowScreen: true, allowWindow: true);
        var resolvedTarget = ResolveBoundsTarget(command, target);
        var start = ResolvePoint(command, options, resolvedTarget, requirePointIfNoTarget: true, defaultToCenterWhenTargeted: false);
        var end = ResolvePoint(command, options, resolvedTarget, "to-x", "to-y", requirePointIfNoTarget: true, defaultToCenterWhenTargeted: false);
        var button = ParseMouseButton(options.GetValueOrDefault("button") ?? "left");
        var durationMs = ReadNonNegativeInt(options, "duration-ms") ?? 150;
        var steps = ReadPositiveInt(options, "steps") ?? 12;

        await _inputService.DragAsync(start.X, start.Y, end.X, end.Y, button, durationMs, steps);
        WriteResult(
            command,
            CommandResult.Ok(
                $"Dragged {button.ToString().ToLowerInvariant()} from {FormatPointForMessage(resolvedTarget, start.X, start.Y)} to {FormatPointForMessage(resolvedTarget, end.X, end.Y)}.",
                details: new
                {
                    button,
                    start = ToPointData(start.X, start.Y),
                    relativeStart = ToRelativePointData(resolvedTarget, start.X, start.Y),
                    end = ToPointData(end.X, end.Y),
                    relativeEnd = ToRelativePointData(resolvedTarget, end.X, end.Y),
                    durationMs,
                    steps,
                    target = ToTargetData(resolvedTarget)
                }),
            options.HasFlag("json"));
        return 0;
    }

    private int HandleScroll(string[] args)
    {
        const string command = "scroll";
        if (IsHelpRequest(args))
        {
            Console.WriteLine("Usage: peekwin scroll [--delta <n>] [--delta-x <n>] [--x <n> --y <n>] [--screen <n> | --app <name> | --title <text> | --handle <HWND> | --window <HWND>] [--json]");
            return 0;
        }

        if (!TryParseOptions(command, args, out var options))
        {
            return 1;
        }

        EnsureNoPositionals(command, options);
        var verticalDelta = ReadInt(options, "delta") ?? 0;
        var horizontalDelta = ReadInt(options, "delta-x") ?? 0;
        if (verticalDelta == 0 && horizontalDelta == 0)
        {
            return Fail(command, "scroll requires --delta or --delta-x.", options.HasFlag("json"));
        }

        var target = ParseTarget(command, options, allowScreen: true, allowWindow: true);
        var resolvedTarget = ResolveBoundsTarget(command, target);
        var point = ResolvePoint(
            command,
            options,
            resolvedTarget,
            requirePointIfNoTarget: false,
            defaultToCenterWhenTargeted: true,
            defaultToCursorWhenUnspecified: true);

        _inputService.MoveMouse(point.X, point.Y);
        _inputService.Scroll(verticalDelta, horizontalDelta);
        WriteResult(
            command,
            CommandResult.Ok(
                $"Scrolled at {FormatPointForMessage(resolvedTarget, point.X, point.Y)}.",
                details: new
                {
                    point = ToPointData(point.X, point.Y),
                    relativePoint = ToRelativePointData(resolvedTarget, point.X, point.Y),
                    delta = verticalDelta,
                    deltaX = horizontalDelta,
                    target = ToTargetData(resolvedTarget)
                }),
            options.HasFlag("json"));
        return 0;
    }

    private int HandleMouse(string[] args)
    {
        if (IsHelpRequest(args) || args.Length == 0)
        {
            PrintMouseHelp();
            return args.Length == 0 ? 1 : 0;
        }

        return args[0].ToLowerInvariant() switch
        {
            "down" => HandleMouseButton(args[1..], "mouse down", isDown: true),
            "up" => HandleMouseButton(args[1..], "mouse up", isDown: false),
            _ => Fail("mouse", $"Unknown mouse subcommand: {args[0]}", RequestedJson(args))
        };
    }

    private int HandleMouseButton(string[] args, string command, bool isDown)
    {
        if (IsHelpRequest(args))
        {
            Console.WriteLine($"Usage: peekwin {command} [--button left|right] [--x <n> --y <n>] [--screen <n> | --app <name> | --title <text> | --handle <HWND> | --window <HWND>] [--json]");
            return 0;
        }

        if (!TryParseOptions(command, args, out var options))
        {
            return 1;
        }

        EnsureNoPositionals(command, options);
        var button = ParseMouseButton(options.GetValueOrDefault("button") ?? "left");
        var target = ParseTarget(command, options, allowScreen: true, allowWindow: true);
        var resolvedTarget = ResolveBoundsTarget(command, target);
        var point = TryResolvePoint(command, options, resolvedTarget, defaultToCenterWhenTargeted: true);

        if (isDown)
        {
            _inputService.MouseDown(button, point?.X, point?.Y);
        }
        else
        {
            _inputService.MouseUp(button, point?.X, point?.Y);
        }

        WriteResult(
            command,
            CommandResult.Ok(
                point is null
                    ? $"{(isDown ? "Pressed" : "Released")} {button.ToString().ToLowerInvariant()} mouse button."
                    : $"{(isDown ? "Pressed" : "Released")} {button.ToString().ToLowerInvariant()} mouse button at {FormatPointForMessage(resolvedTarget, point.Value.X, point.Value.Y)}.",
                details: new { button, point = point is null ? null : ToPointData(point.Value.X, point.Value.Y), relativePoint = point is null ? null : ToRelativePointData(resolvedTarget, point.Value.X, point.Value.Y), target = ToTargetData(resolvedTarget) }),
            options.HasFlag("json"));
        return 0;
    }

    private async Task<int> HandleTypeAsync(string[] args)
    {
        const string command = "type";
        if (IsHelpRequest(args))
        {
            Console.WriteLine("Usage: peekwin type [text] [--text <value>] [--delay-ms <n>] [--method type|paste] [--app <name> | --title <text> | --handle <HWND> | --window <HWND>] [--json]");
            return 0;
        }

        if (!TryParseOptions(command, args, out var options))
        {
            return 1;
        }

        var text = ResolveText(command, options);
        var method = (options.GetValueOrDefault("method") ?? "type").ToLowerInvariant();
        var delayMs = ReadNonNegativeInt(options, "delay-ms") ?? 0;
        var target = ParseTarget(command, options, allowScreen: false, allowWindow: true);
        if (!FocusTargetIfNeeded(command, target, options.HasFlag("json")))
        {
            return 1;
        }

        switch (method)
        {
            case "type":
                await _inputService.TypeTextAsync(text, delayMs);
                break;
            case "paste":
                await _inputService.PasteTextAsync(text, delayMs);
                break;
            default:
                return Fail(command, $"Unsupported --method value: {method}", options.HasFlag("json"));
        }

        WriteResult(
            command,
            CommandResult.Ok(
                $"Entered {text.Length} characters using {method}.",
                details: new { method, textLength = text.Length, target = ToTargetData(ResolveWindowTarget(command, target)) }),
            options.HasFlag("json"));
        return 0;
    }

    private async Task<int> HandlePasteAsync(string[] args)
    {
        const string command = "paste";
        if (IsHelpRequest(args))
        {
            Console.WriteLine("Usage: peekwin paste [text] [--text <value>] [--delay-ms <n>] [--app <name> | --title <text> | --handle <HWND> | --window <HWND>] [--json]");
            return 0;
        }

        if (!TryParseOptions(command, args, out var options))
        {
            return 1;
        }

        var text = ResolveText(command, options);
        var delayMs = ReadNonNegativeInt(options, "delay-ms") ?? 75;
        var target = ParseTarget(command, options, allowScreen: false, allowWindow: true);
        if (!FocusTargetIfNeeded(command, target, options.HasFlag("json")))
        {
            return 1;
        }

        await _inputService.PasteTextAsync(text, delayMs);
        WriteResult(
            command,
            CommandResult.Ok(
                $"Pasted {text.Length} characters.",
                details: new { textLength = text.Length, target = ToTargetData(ResolveWindowTarget(command, target)) }),
            options.HasFlag("json"));
        return 0;
    }

    private async Task<int> HandlePressAsync(string[] args)
    {
        const string command = "press";
        if (IsHelpRequest(args))
        {
            Console.WriteLine("Usage: peekwin press [key] [--key <name>] [--repeat <n>] [--delay-ms <n>] [--app <name> | --title <text> | --handle <HWND> | --window <HWND>] [--json]");
            return 0;
        }

        if (!TryParseOptions(command, args, out var options))
        {
            return 1;
        }

        var key = ResolveSingleKey(command, options);
        var repeat = ReadPositiveInt(options, "repeat") ?? 1;
        var delayMs = ReadNonNegativeInt(options, "delay-ms") ?? 0;
        var target = ParseTarget(command, options, allowScreen: false, allowWindow: true);
        if (!FocusTargetIfNeeded(command, target, options.HasFlag("json")))
        {
            return 1;
        }

        await _inputService.PressKeyAsync(key, repeat, delayMs);
        WriteResult(
            command,
            CommandResult.Ok(
                $"Pressed {key} x{repeat}.",
                details: new { key, repeat, delayMs, target = ToTargetData(ResolveWindowTarget(command, target)) }),
            options.HasFlag("json"));
        return 0;
    }

    private int HandleHotkey(string[] args)
    {
        const string command = "hotkey";
        if (IsHelpRequest(args))
        {
            Console.WriteLine("Usage: peekwin hotkey [keys...] [--keys ctrl,s] [--app <name> | --title <text> | --handle <HWND> | --window <HWND>] [--json]");
            return 0;
        }

        if (!TryParseOptions(command, args, out var options))
        {
            return 1;
        }

        var keys = ResolveKeys(command, options);
        var target = ParseTarget(command, options, allowScreen: false, allowWindow: true);
        if (!FocusTargetIfNeeded(command, target, options.HasFlag("json")))
        {
            return 1;
        }

        _inputService.Hotkey(keys);
        WriteResult(
            command,
            CommandResult.Ok(
                $"Sent hotkey {string.Join("+", keys)}.",
                details: new { keys, target = ToTargetData(ResolveWindowTarget(command, target)) }),
            options.HasFlag("json"));
        return 0;
    }

    private async Task<int> HandleHoldAsync(string[] args)
    {
        const string command = "hold";
        if (IsHelpRequest(args))
        {
            PrintHoldHelp();
            return 0;
        }

        if (!TryParseOptions(command, args, out var options))
        {
            return 1;
        }

        var durationMs = ReadPositiveInt(options, "duration-ms") ?? 1000;
        var buttonText = options.GetValueOrDefault("button");
        var hasButton = !string.IsNullOrWhiteSpace(buttonText);
        var hasKeys = options.HasValue("keys") || options.Positionals.Count > 0;
        if (hasButton == hasKeys)
        {
            return Fail(command, "hold requires either keys or --button, but not both.", options.HasFlag("json"));
        }

        if (hasButton)
        {
            var target = ParseTarget(command, options, allowScreen: true, allowWindow: true);
            var resolvedTarget = ResolveBoundsTarget(command, target);
            var point = TryResolvePoint(command, options, resolvedTarget, defaultToCenterWhenTargeted: true);
            var button = ParseMouseButton(buttonText!);
            await _inputService.HoldMouseAsync(button, durationMs, point?.X, point?.Y);
            WriteResult(
                command,
                CommandResult.Ok(
                    $"Held {button.ToString().ToLowerInvariant()} mouse button for {durationMs}ms.",
                    details: new { button, durationMs, point = point is null ? null : new { x = point.Value.X, y = point.Value.Y }, target = ToTargetData(resolvedTarget) }),
                options.HasFlag("json"));
            return 0;
        }

        var windowTarget = ParseTarget(command, options, allowScreen: false, allowWindow: true);
        if (!FocusTargetIfNeeded(command, windowTarget, options.HasFlag("json")))
        {
            return 1;
        }

        var keys = ResolveKeys(command, options);
        await _inputService.HoldKeysAsync(keys, durationMs);
        WriteResult(
            command,
            CommandResult.Ok(
                $"Held {string.Join("+", keys)} for {durationMs}ms.",
                details: new { keys, durationMs, target = ToTargetData(ResolveWindowTarget(command, windowTarget)) }),
            options.HasFlag("json"));
        return 0;
    }

    private int HandleImageCommand(string[] args, string command)
    {
        if (IsHelpRequest(args))
        {
            PrintImageHelp();
            return 0;
        }

        if (args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal))
        {
            return args[0].ToLowerInvariant() switch
            {
                "info" => HandleScreens(args[1..]),
                _ => Fail(command, $"Unknown {command} subcommand: {args[0]}", RequestedJson(args))
            };
        }

        return HandleImage(args, command);
    }

    private int HandleImage(string[] args, string command)
    {
        if (IsHelpRequest(args))
        {
            Console.WriteLine($"Usage: peekwin {command} (--screen <n> | --app <name> | --title <text> | --handle <HWND> | --window <HWND>) [--output <path>] [--json]");
            return 0;
        }

        if (!TryParseOptions(command, args, out var options))
        {
            return 1;
        }

        EnsureNoPositionals(command, options);
        var output = options.GetValueOrDefault("output")
            ?? Path.Combine(Environment.CurrentDirectory, $"peekwin-{DateTime.UtcNow:yyyyMMddHHmmss}.png");
        var target = ParseTarget(command, options, allowScreen: true, allowWindow: true, requireTarget: true);
        var resolvedTarget = ResolveBoundsTarget(command, target)!;
        var result = _screenshotService.Capture(output, resolvedTarget.Bounds, new { target = ToTargetData(resolvedTarget), bounds = resolvedTarget.Bounds });

        WriteResult(command, result, options.HasFlag("json"));
        return result.Success ? 0 : 1;
    }

    private async Task<int> HandleSleepAsync(string[] args)
    {
        const string command = "sleep";
        if (IsHelpRequest(args))
        {
            Console.WriteLine("Usage: peekwin sleep <milliseconds> [--json]");
            Console.WriteLine("       peekwin sleep --duration-ms <milliseconds> [--json]");
            return 0;
        }

        if (!TryParseOptions(command, args, out var options))
        {
            return 1;
        }

        var durationMs = ResolveSleepDuration(command, options);
        await Task.Delay(durationMs).ConfigureAwait(false);

        WriteResult(
            command,
            CommandResult.Ok($"Slept for {durationMs}ms.", details: new { durationMs }),
            options.HasFlag("json"));
        return 0;
    }

    private TargetSelector ParseTarget(string command, OptionSet options, bool allowScreen, bool allowWindow, bool requireTarget = false)
    {
        var screen = ReadInt(options, "screen");
        var handle = options.TryGetHandle("handle");
        var windowHandle = options.TryGetHandle("window");
        var title = options.GetValueOrDefault("title");
        var app = options.GetValueOrDefault("app");

        if (handle != 0 && windowHandle != 0 && handle != windowHandle)
        {
            throw new InvalidOperationException($"{command} received different values for --handle and --window.");
        }

        if (windowHandle != 0)
        {
            handle = windowHandle;
        }

        var targetCount = (screen is not null ? 1 : 0)
            + (handle != 0 ? 1 : 0)
            + (!string.IsNullOrWhiteSpace(title) ? 1 : 0)
            + (!string.IsNullOrWhiteSpace(app) ? 1 : 0);

        if (targetCount > 1)
        {
            throw new InvalidOperationException($"{command} accepts only one of --screen, --app, --title, --handle, or --window.");
        }

        if (!allowScreen && screen is not null)
        {
            throw new InvalidOperationException($"{command} does not support --screen.");
        }

        if (!allowWindow && (handle != 0 || !string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(app)))
        {
            throw new InvalidOperationException($"{command} does not support --app, --title, --handle, or --window.");
        }

        if (requireTarget && targetCount == 0)
        {
            throw new InvalidOperationException($"{command} requires a target.");
        }

        return new TargetSelector(screen, handle, title, app);
    }

    private ResolvedTarget? ResolveBoundsTarget(string command, TargetSelector target)
    {
        if (target.Screen is not null)
        {
            var layout = _screenshotService.GetScreenLayout();
            if (target.Screen < 0 || target.Screen >= layout.Screens.Count)
            {
                throw new InvalidOperationException($"Screen index {target.Screen} is out of range. Found {layout.Screens.Count} screens.");
            }

            var screen = layout.Screens[target.Screen.Value];
            return new ResolvedTarget("screen", $"screen {screen.Index}", screen.Bounds, WindowHandle: null, ScreenIndex: screen.Index);
        }

        return ResolveWindowTarget(command, target);
    }

    private ResolvedTarget? ResolveWindowTarget(string command, TargetSelector target)
    {
        WindowInfo? window = null;
        if (target.Handle != 0)
        {
            window = _windowService.FindWindow(target.Handle);
            if (window is null)
            {
                throw new InvalidOperationException($"Invalid or destroyed window handle: 0x{target.Handle.ToInt64():X}.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(target.Title))
        {
            window = _windowService.FindWindowByTitle(target.Title);
            if (window is null)
            {
                throw new InvalidOperationException($"No window matched title: {target.Title}");
            }
        }
        else if (!string.IsNullOrWhiteSpace(target.App))
        {
            window = _windowService.FindWindowByApp(target.App);
            if (window is null)
            {
                throw new InvalidOperationException($"No window matched app: {target.App}");
            }
        }

        return window is null
            ? null
            : new ResolvedTarget("window", window.Title, window.Bounds, ParseHandle(window.Handle), AppName: window.ProcessName);
    }

    private bool FocusTargetIfNeeded(string command, TargetSelector target, bool asJson)
    {
        var resolved = ResolveWindowTarget(command, target);
        if (resolved?.WindowHandle is null)
        {
            return true;
        }

        var result = _windowService.FocusWindow(resolved.WindowHandle.Value);
        if (result.Success)
        {
            return true;
        }

        WriteResult(command, result, asJson);
        return false;
    }

    private (int X, int Y)? TryResolvePoint(string command, OptionSet options, ResolvedTarget? target, bool defaultToCenterWhenTargeted)
    {
        var x = ReadInt(options, "x");
        var y = ReadInt(options, "y");
        if ((x is null) != (y is null))
        {
            throw new InvalidOperationException($"{command} requires both --x and --y when either is provided.");
        }

        if (x is null)
        {
            if (target is not null && defaultToCenterWhenTargeted)
            {
                return (target.Bounds.Left + target.Bounds.Width / 2, target.Bounds.Top + target.Bounds.Height / 2);
            }

            return null;
        }

        return OffsetPoint(target, x.Value, y!.Value);
    }

    private (int X, int Y) ResolvePoint(
        string command,
        OptionSet options,
        ResolvedTarget? target,
        bool requirePointIfNoTarget,
        bool defaultToCenterWhenTargeted,
        bool defaultToCursorWhenUnspecified = false)
        => ResolvePoint(command, options, target, "x", "y", requirePointIfNoTarget, defaultToCenterWhenTargeted, defaultToCursorWhenUnspecified);

    private (int X, int Y) ResolvePoint(
        string command,
        OptionSet options,
        ResolvedTarget? target,
        string xKey,
        string yKey,
        bool requirePointIfNoTarget,
        bool defaultToCenterWhenTargeted,
        bool defaultToCursorWhenUnspecified = false)
    {
        var x = ReadInt(options, xKey);
        var y = ReadInt(options, yKey);
        if ((x is null) != (y is null))
        {
            throw new InvalidOperationException($"{command} requires both --{xKey} and --{yKey} when either is provided.");
        }

        if (x is not null)
        {
            return OffsetPoint(target, x.Value, y!.Value);
        }

        if (target is not null && defaultToCenterWhenTargeted)
        {
            return (target.Bounds.Left + target.Bounds.Width / 2, target.Bounds.Top + target.Bounds.Height / 2);
        }

        if (defaultToCursorWhenUnspecified)
        {
            return _inputService.GetCursorPosition();
        }

        if (requirePointIfNoTarget || target is not null)
        {
            throw new InvalidOperationException($"{command} requires --{xKey} and --{yKey}.");
        }

        return _inputService.GetCursorPosition();
    }

    private static (int X, int Y) OffsetPoint(ResolvedTarget? target, int x, int y)
        => target is null
            ? (x, y)
            : (target.Bounds.Left + x, target.Bounds.Top + y);

    private string ResolveText(string command, OptionSet options)
    {
        var text = options.GetValueOrDefault("text");
        if (!string.IsNullOrWhiteSpace(text))
        {
            EnsureNoPositionals(command, options);
            return text;
        }

        if (options.Positionals.Count == 1)
        {
            return options.Positionals[0];
        }

        if (options.Positionals.Count > 1)
        {
            throw new InvalidOperationException($"{command} accepts a single positional text argument.");
        }

        throw new InvalidOperationException($"{command} requires text.");
    }

    private static string ResolveSingleKey(string command, OptionSet options)
    {
        var key = options.GetValueOrDefault("key");
        if (!string.IsNullOrWhiteSpace(key))
        {
            EnsureNoPositionals(command, options);
            return key;
        }

        if (options.Positionals.Count == 1)
        {
            return options.Positionals[0];
        }

        if (options.Positionals.Count > 1)
        {
            throw new InvalidOperationException($"{command} accepts a single key.");
        }

        throw new InvalidOperationException($"{command} requires a key.");
    }

    private static IReadOnlyList<string> ResolveKeys(string command, OptionSet options)
    {
        if (options.HasValue("keys"))
        {
            EnsureNoPositionals(command, options);
            return SplitKeys(options.GetValueOrDefault("keys")!);
        }

        if (options.Positionals.Count == 0)
        {
            throw new InvalidOperationException($"{command} requires keys.");
        }

        return options.Positionals;
    }

    private static int ResolveDesktopIndex(string command, OptionSet options)
    {
        var index = ReadNonNegativeInt(options, "index");
        if (index is not null)
        {
            EnsureNoPositionals(command, options);
            return index.Value;
        }

        if (options.Positionals.Count == 1 && int.TryParse(options.Positionals[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var positionalIndex) && positionalIndex >= 0)
        {
            return positionalIndex;
        }

        if (options.Positionals.Count > 1)
        {
            throw new InvalidOperationException($"{command} accepts a single desktop index.");
        }

        throw new InvalidOperationException($"{command} requires a desktop index.");
    }

    private static int ResolveSleepDuration(string command, OptionSet options)
    {
        var durationMs = ReadNonNegativeInt(options, "duration-ms");
        if (durationMs is not null)
        {
            EnsureNoPositionals(command, options);
            return durationMs.Value;
        }

        if (options.Positionals.Count == 1 && int.TryParse(options.Positionals[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var positionalDuration) && positionalDuration >= 0)
        {
            return positionalDuration;
        }

        if (options.Positionals.Count > 1)
        {
            throw new InvalidOperationException($"{command} accepts a single positional duration.");
        }

        throw new InvalidOperationException($"{command} requires milliseconds.");
    }

    private static IReadOnlyList<string> SplitKeys(string value)
        => value
            .Split(new[] { ',', '+' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    private static bool IsHelpRequest(IReadOnlyList<string> args)
        => args.Count == 1 && (args[0].Equals("--help", StringComparison.OrdinalIgnoreCase)
            || args[0].Equals("-h", StringComparison.OrdinalIgnoreCase)
            || args[0].Equals("help", StringComparison.OrdinalIgnoreCase));

    private static MouseButton ParseMouseButton(string value) => value.ToLowerInvariant() switch
    {
        "left" => MouseButton.Left,
        "right" => MouseButton.Right,
        _ => throw new InvalidOperationException($"Unsupported mouse button: {value}")
    };

    private static string FormatRect(RectDto rect)
        => $"{rect.Left},{rect.Top} {rect.Width}x{rect.Height}";

    private static (string[] Args, bool Verbose) ExtractGlobalFlags(string[] args)
    {
        if (args.Length == 0)
        {
            return (args, false);
        }

        var verbose = false;
        var filtered = new List<string>(args.Length);

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (IsVerboseFlag(arg) && !LooksLikeOptionValue(args, index))
            {
                verbose = true;
                continue;
            }

            filtered.Add(arg);
        }

        return (filtered.ToArray(), verbose);
    }

    private static bool IsVerboseFlag(string arg)
        => arg.Equals("--verbose", StringComparison.OrdinalIgnoreCase)
            || arg.Equals("-v", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeOptionValue(IReadOnlyList<string> args, int index)
    {
        if (index == 0)
        {
            return false;
        }

        var previous = args[index - 1];
        return previous.StartsWith("--", StringComparison.Ordinal)
            && !previous.Contains('=', StringComparison.Ordinal)
            && !IsVerboseFlag(previous)
            && !previous.Equals("--json", StringComparison.OrdinalIgnoreCase)
            && !previous.Equals("--all", StringComparison.OrdinalIgnoreCase)
            && !previous.Equals("--double", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryParseOptions(string command, IReadOnlyList<string> args, out OptionSet options)
    {
        try
        {
            options = OptionSet.Parse(args);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            options = null!;
            Fail(command, $"Invalid arguments: {ex.Message}", RequestedJson(args));
            return false;
        }
    }

    private static int? ReadInt(OptionSet options, string key)
    {
        var value = options.GetValueOrDefault(key);
        if (value is null)
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Invalid integer for --{key}: {value}");
    }

    private static int? ReadPositiveInt(OptionSet options, string key)
    {
        var value = ReadInt(options, key);
        if (value is not null && value <= 0)
        {
            throw new InvalidOperationException($"--{key} must be greater than 0.");
        }

        return value;
    }

    private static int? ReadNonNegativeInt(OptionSet options, string key)
    {
        var value = ReadInt(options, key);
        if (value is not null && value < 0)
        {
            throw new InvalidOperationException($"--{key} must be greater than or equal to 0.");
        }

        return value;
    }

    private static void WriteResult(string command, CommandResult result, bool asJson)
    {
        if (asJson)
        {
            WriteJsonEnvelope(
                result.Success,
                command,
                result.Success ? new ActionResultData(result.Message, result.OutputPath, result.Details) : result.Details,
                result.Success ? null : result.Message);
            return;
        }

        if (result.Success)
        {
            Console.WriteLine(result.Message);
        }
        else
        {
            Console.Error.WriteLine(result.Message);
        }
    }

    private static int Fail(string command, string message, bool asJson)
    {
        if (asJson)
        {
            WriteJsonEnvelope(false, command, null, message);
        }
        else
        {
            Console.Error.WriteLine(message);
        }

        return 1;
    }

    private static void WriteJsonEnvelope(bool success, string command, object? data, string? error = null)
        => Console.WriteLine(JsonSerializer.Serialize(new JsonEnvelope(success, command, data, error), JsonOptions));

    private static object ToPointData(int x, int y)
        => new { x, y };

    private static object? ToRelativePointData(ResolvedTarget? target, int x, int y)
        => target is null
            ? null
            : new { x = x - target.Bounds.Left, y = y - target.Bounds.Top };

    private static string FormatPointForMessage(ResolvedTarget? target, int x, int y)
    {
        if (target is null)
        {
            return $"{x},{y}";
        }

        var relativeX = x - target.Bounds.Left;
        var relativeY = y - target.Bounds.Top;
        return $"{relativeX},{relativeY} relative to {target.Label} ({x},{y} absolute)";
    }

    private static object? ToTargetData(ResolvedTarget? target)
        => target is null
            ? null
            : new
            {
                kind = target.Kind,
                label = target.Label,
                app = target.AppName,
                screen = target.ScreenIndex,
                handle = target.WindowHandle is null ? null : $"0x{target.WindowHandle.Value.ToInt64():X}",
                bounds = target.Bounds
            };

    private static void EnsureNoPositionals(string command, OptionSet options)
    {
        if (options.Positionals.Count > 0)
        {
            throw new InvalidOperationException($"{command} does not accept positional arguments: {string.Join(" ", options.Positionals)}");
        }
    }

    private static nint ParseHandle(string value)
        => value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? (nint)Convert.ToInt64(value[2..], 16)
            : (nint)Convert.ToInt64(value);

    private static bool RequestedJson(IReadOnlyList<string> args)
        => args.Any(arg => arg.Equals("--json", StringComparison.OrdinalIgnoreCase));

    private static string BuildCommandName(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return "peekwin";
        }

        var first = args[0].ToLowerInvariant();
        if ((first is "window" or "app" or "mouse" or "desktop" or "image" or "screenshot") && args.Count > 1 && !args[1].StartsWith("--", StringComparison.Ordinal))
        {
            return $"{first} {args[1].ToLowerInvariant()}";
        }

        return first;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("peekwin - Windows-native automation CLI");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  peekwin <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Core commands:");
        Console.WriteLine("  peekwin window list [--all] [--app <name>] [--title <text>] [--json]");
        Console.WriteLine("  peekwin window focus|inspect|close|minimize|maximize|restore (--app <name> | --title <text> | --handle <HWND> | --window <HWND>) [--json]");
        Console.WriteLine("  peekwin app list [--all] [--name <text>] [--json]");
        Console.WriteLine("  peekwin desktop list|current [--json]");
        Console.WriteLine("  peekwin desktop switch <index> [--delay-ms <n>] [--json]");
        Console.WriteLine("  peekwin screens [--json]");
        Console.WriteLine("  peekwin image (--screen <n> | --app <name> | --title <text> | --handle <HWND> | --window <HWND>) [--output <path>] [--json]");
        Console.WriteLine("  peekwin screenshot ...   alias for 'peekwin image'");
        Console.WriteLine();
        Console.WriteLine("Pointer commands:");
        Console.WriteLine("  peekwin move --x <n> --y <n> [target] [--duration-ms <n>] [--steps <n>] [--json]");
        Console.WriteLine("  peekwin click [--x <n> --y <n>] [target] [--button left|right] [--double] [--delay-ms <n>] [--json]");
        Console.WriteLine("  peekwin drag --x <n> --y <n> --to-x <n> --to-y <n> [target] [--button left|right] [--duration-ms <n>] [--steps <n>] [--json]");
        Console.WriteLine("  peekwin scroll [--delta <n>] [--delta-x <n>] [--x <n> --y <n>] [target] [--json]");
        Console.WriteLine("  peekwin mouse down|up [--button left|right] [--x <n> --y <n>] [target] [--json]");
        Console.WriteLine();
        Console.WriteLine("Keyboard/text commands:");
        Console.WriteLine("  peekwin type [text] [--delay-ms <n>] [--method type|paste] [window-target] [--json]");
        Console.WriteLine("  peekwin paste [text] [--delay-ms <n>] [window-target] [--json]");
        Console.WriteLine("  peekwin press [key] [--repeat <n>] [--delay-ms <n>] [window-target] [--json]");
        Console.WriteLine("  peekwin hotkey [keys...] [--keys ctrl,s] [window-target] [--json]");
        Console.WriteLine("  peekwin hold [keys...] [--keys ctrl,shift | --button left|right] [--duration-ms <n>] [target] [--json]");
        Console.WriteLine("  peekwin sleep <milliseconds> [--json]");
        Console.WriteLine();
        Console.WriteLine("Target flags:");
        Console.WriteLine("  pointer/image:      --screen <n> | --app <name> | --title <text> | --handle <HWND> | --window <HWND>");
        Console.WriteLine("  keyboard/text:      --app <name> | --title <text> | --handle <HWND> | --window <HWND>");
        Console.WriteLine("  relative coordinates are offset from the selected screen or window");
        Console.WriteLine();
        Console.WriteLine("Timing flags:");
        Console.WriteLine("  --delay-ms <n>      inter-action delay where applicable");
        Console.WriteLine("  --duration-ms <n>   total move/hold/drag duration");
        Console.WriteLine();
        Console.WriteLine("Global flags:");
        Console.WriteLine("  --verbose, -v       print exception details for troubleshooting");
        Console.WriteLine();
        Console.WriteLine("Use 'peekwin <command> --help' for command-specific help.");
    }

    private static void PrintWindowHelp()
    {
        Console.WriteLine("Window commands:");
        Console.WriteLine("  peekwin window list [--all] [--app <name>] [--title <text>] [--json]");
        Console.WriteLine("  peekwin window focus (--app <name> | --title <text> | --handle <HWND> | --window <HWND>) [--json]");
        Console.WriteLine("  peekwin window inspect (--app <name> | --title <text> | --handle <HWND> | --window <HWND>) [--json]");
        Console.WriteLine("  peekwin window close (--app <name> | --title <text> | --handle <HWND> | --window <HWND>) [--json]");
        Console.WriteLine("  peekwin window minimize (--app <name> | --title <text> | --handle <HWND> | --window <HWND>) [--json]");
        Console.WriteLine("  peekwin window maximize (--app <name> | --title <text> | --handle <HWND> | --window <HWND>) [--json]");
        Console.WriteLine("  peekwin window restore (--app <name> | --title <text> | --handle <HWND> | --window <HWND>) [--json]");
    }

    private static void PrintAppHelp()
    {
        Console.WriteLine("App commands:");
        Console.WriteLine("  peekwin app list [--all] [--name <text>] [--json]");
    }

    private static void PrintDesktopHelp()
    {
        Console.WriteLine("Desktop commands:");
        Console.WriteLine("  peekwin desktop list [--json]");
        Console.WriteLine("  peekwin desktop current [--json]");
        Console.WriteLine("  peekwin desktop switch <index> [--delay-ms <n>] [--json]");
        Console.WriteLine("  peekwin desktop switch --index <n> [--delay-ms <n>] [--json]");
    }

    private static void PrintMouseHelp()
    {
        Console.WriteLine("Mouse commands:");
        Console.WriteLine("  peekwin move --x <n> --y <n> [target] [--duration-ms <n>] [--steps <n>] [--json]");
        Console.WriteLine("  peekwin click [--x <n> --y <n>] [target] [--button left|right] [--double] [--delay-ms <n>] [--json]");
        Console.WriteLine("  peekwin drag --x <n> --y <n> --to-x <n> --to-y <n> [target] [--button left|right] [--duration-ms <n>] [--steps <n>] [--json]");
        Console.WriteLine("  peekwin scroll [--delta <n>] [--delta-x <n>] [--x <n> --y <n>] [target] [--json]");
        Console.WriteLine("  peekwin mouse down [--button left|right] [--x <n> --y <n>] [target] [--json]");
        Console.WriteLine("  peekwin mouse up [--button left|right] [--x <n> --y <n>] [target] [--json]");
        Console.WriteLine("  target = --screen <n> | --app <name> | --title <text> | --handle <HWND> | --window <HWND>");
    }

    private static void PrintHoldHelp()
    {
        Console.WriteLine("Hold commands:");
        Console.WriteLine("  peekwin hold ctrl shift [--duration-ms <n>] [--app <name> | --title <text> | --handle <HWND> | --window <HWND>] [--json]");
        Console.WriteLine("  peekwin hold --keys ctrl,shift [--duration-ms <n>] [--app <name> | --title <text> | --handle <HWND> | --window <HWND>] [--json]");
        Console.WriteLine("  peekwin hold --button left|right [--duration-ms <n>] [--x <n> --y <n>] [--screen <n> | --app <name> | --title <text> | --handle <HWND> | --window <HWND>] [--json]");
    }

    private static void PrintImageHelp()
    {
        Console.WriteLine("Image commands:");
        Console.WriteLine("  peekwin image (--screen <n> | --app <name> | --title <text> | --handle <HWND> | --window <HWND>) [--output <path>] [--json]");
        Console.WriteLine("  peekwin screenshot ...             alias for 'peekwin image'");
        Console.WriteLine("  peekwin image info [--json]        alias for 'peekwin screens'");
    }

    private sealed record JsonEnvelope(bool Success, string Command, object? Data, string? Error);

    private sealed record ActionResultData(string Message, string? OutputPath, object? Details);

    private sealed record TargetSelector(int? Screen, nint Handle, string? Title, string? App);

    private sealed record ResolvedTarget(string Kind, string Label, RectDto Bounds, nint? WindowHandle = null, int? ScreenIndex = null, string? AppName = null);
}

internal sealed class OptionSet
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _flags = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _positionals = [];

    private OptionSet() { }

    public IReadOnlyList<string> Positionals => _positionals;

    public static OptionSet Parse(IReadOnlyList<string> args)
    {
        var set = new OptionSet();
        for (int i = 0; i < args.Count; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                set._positionals.Add(token);
                continue;
            }

            var span = token[2..];
            var separatorIndex = span.IndexOf('=');
            if (separatorIndex >= 0)
            {
                var inlineKey = span[..separatorIndex];
                var value = span[(separatorIndex + 1)..];
                if (value.Length == 0)
                {
                    throw new InvalidOperationException($"Missing value for --{inlineKey}.");
                }

                set._values[inlineKey] = value;
                continue;
            }

            var key = span;
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

    public bool HasValue(string key) => _values.ContainsKey(key);

    public string? GetValueOrDefault(string key) => _values.GetValueOrDefault(key);

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
