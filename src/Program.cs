using System.Runtime.Versioning;
using PeekWin;
using PeekWin.Cli;
using PeekWin.Infrastructure;

[assembly: SupportedOSPlatform("windows")]

if (OperatingSystem.IsWindows())
{
    TryEnablePerMonitorDpiAwareness();
}

if (!OperatingSystem.IsWindows())
{
    if (!AllowsNonWindowsExecution(args))
    {
        System.Console.Error.WriteLine("peekwin currently runs on Windows only.");
        return 1;
    }

    if (IsVersionRequest(args))
    {
        System.Console.WriteLine(CommandShell.GetVersionText());
    }
    else
    {
        CommandShell.PrintRequestedHelp(args);
    }

    return 0;
}

return await PeekWinRuntimeFactory.CreateCommandShell().RunAsync(args);

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
