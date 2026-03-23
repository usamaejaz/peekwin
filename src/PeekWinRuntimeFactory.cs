using PeekWin.Cli;
using PeekWin.Services;

namespace PeekWin;

public static class PeekWinRuntimeFactory
{
    public static CommandShell CreateCommandShell()
    {
        var windowService = new WindowService();
        var clipboardService = new ClipboardService();
        var inputService = new InputService();
        var automationSnapshotService = new AutomationSnapshotService();
        var automationRefService = new AutomationRefService(automationSnapshotService, windowService);

        return new CommandShell(
            windowService,
            inputService,
            clipboardService,
            new ScreenshotService(),
            new VirtualDesktopService(inputService),
            automationSnapshotService,
            automationRefService,
            new WaitService(windowService, automationRefService));
    }

    public static CommandRunner CreateCommandRunner()
        => new(CreateCommandShell);

    public static ICommandRunner CreateMcpCommandRunner()
        => ProcessCommandRunner.CreateForCurrentProcess();
}
