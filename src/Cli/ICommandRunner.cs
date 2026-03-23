namespace PeekWin.Cli;

public interface ICommandRunner
{
    Task<CommandRunResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}
