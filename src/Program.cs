using System.Runtime.Versioning;
using PeekWin.Cli;
using PeekWin.Services;

[assembly: SupportedOSPlatform("windows")]

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
    var inputService = new InputService();
    return new CommandShell(
        new WindowService(),
        inputService,
        new ScreenshotService(),
        new VirtualDesktopService(inputService));
}

static bool AllowsNonWindowsExecution(IReadOnlyList<string> args)
    => args.Count == 0
        || args.Any(static arg => arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase))
        || args[0].Equals("help", StringComparison.OrdinalIgnoreCase)
        || args[0].Equals("version", StringComparison.OrdinalIgnoreCase);

static bool IsVersionRequest(IReadOnlyList<string> args)
    => args.Count > 0 && args[0].Equals("version", StringComparison.OrdinalIgnoreCase);
