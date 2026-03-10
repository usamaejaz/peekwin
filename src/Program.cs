using PeekWin.Cli;
using PeekWin.Infrastructure;
using PeekWin.Services;

if (args.Length > 0 && args[0].Equals("version", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine(CommandShell.GetVersionText());
    return 0;
}

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("peekwin currently runs on Windows only.");
    return 1;
}

var shell = new CommandShell(
    new WindowService(),
    new InputService(),
    new ScreenshotService());

return await shell.RunAsync(args);
