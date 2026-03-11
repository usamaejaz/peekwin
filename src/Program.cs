using System.Runtime.Versioning;
using PeekWin.Cli;
using PeekWin.Services;

[assembly: SupportedOSPlatform("windows")]

var inputService = new InputService();
var shell = new CommandShell(
    new WindowService(),
    inputService,
    new ScreenshotService(),
    new VirtualDesktopService(inputService));

if (!OperatingSystem.IsWindows() && !AllowsNonWindowsExecution(args))
{
    Console.Error.WriteLine("peekwin currently runs on Windows only.");
    return 1;
}

return await shell.RunAsync(args);

static bool AllowsNonWindowsExecution(IReadOnlyList<string> args)
    => args.Count == 0
        || args.Any(static arg => arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase))
        || args[0].Equals("help", StringComparison.OrdinalIgnoreCase)
        || args[0].Equals("version", StringComparison.OrdinalIgnoreCase);
