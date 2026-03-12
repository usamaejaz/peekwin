using System.Runtime.Versioning;
using PeekWin.Cli;
using PeekWin.Infrastructure;
using PeekWin.Services;

[assembly: SupportedOSPlatform("windows")]

if (OperatingSystem.IsWindows())
{
    TryEnablePerMonitorDpiAwareness();
}

if (!OperatingSystem.IsWindows())
{
    if (!AllowsNonWindowsExecution(args))
    {
        Console.Error.WriteLine("peekwin currently runs on Windows only.");
        return 1;
    }

    if (IsVersionRequest(args))
    {
        Console.WriteLine(CommandShell.GetVersionText());
    }
    else
    {
        CommandShell.PrintRequestedHelp(args);
    }

    return 0;
}

return await CreateShell().RunAsync(args);

static CommandShell CreateShell()
{
    var windowService = new WindowService();
    var inputService = new InputService();
    var automationSnapshotService = new AutomationSnapshotService();
    var automationRefService = new AutomationRefService(automationSnapshotService, windowService);
    return new CommandShell(
        windowService,
        inputService,
        new ScreenshotService(),
        new VirtualDesktopService(inputService),
        automationSnapshotService,
        automationRefService,
        new WaitService(windowService, automationRefService));
}

static bool AllowsNonWindowsExecution(IReadOnlyList<string> args)
    => args.Count == 0
        || args.Any(static arg => arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase))
        || args[0].Equals("help", StringComparison.OrdinalIgnoreCase)
        || args[0].Equals("version", StringComparison.OrdinalIgnoreCase);

static bool IsVersionRequest(IReadOnlyList<string> args)
    => args.Count > 0 && args[0].Equals("version", StringComparison.OrdinalIgnoreCase);


static void TryEnablePerMonitorDpiAwareness()
{
    try
    {
        if (NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
        {
            return;
        }
    }
    catch (EntryPointNotFoundException)
    {
    }
    catch (DllNotFoundException)
    {
    }

    try
    {
        NativeMethods.SetProcessDPIAware();
    }
    catch (EntryPointNotFoundException)
    {
    }
    catch (DllNotFoundException)
    {
    }
}
