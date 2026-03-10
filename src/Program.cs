using PeekWin.Cli;
using PeekWin.Infrastructure;
using PeekWin.Services;

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
