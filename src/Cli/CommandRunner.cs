using System.Globalization;
using System.Text.Json.Nodes;

namespace PeekWin.Cli;

public sealed class CommandRunner : ICommandRunner
{
    private readonly Func<CommandShell> _shellFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public CommandRunner(Func<CommandShell> shellFactory)
    {
        _shellFactory = shellFactory;
    }

    public async Task<CommandRunResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var args = arguments.ToArray();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var stdout = new StringWriter(CultureInfo.InvariantCulture);
            using var stderr = new StringWriter(CultureInfo.InvariantCulture);
            using var _ = Console.Push(stdout, stderr);

            var exitCode = await _shellFactory().RunAsync(args).ConfigureAwait(false);
            var stdoutText = stdout.ToString();
            var stderrText = stderr.ToString();

            return new CommandRunResult(
                args,
                exitCode,
                exitCode == 0,
                stdoutText,
                stderrText,
                TryParseJson(stdoutText));
        }
        finally
        {
            _gate.Release();
        }
    }

    private static JsonNode? TryParseJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(text.Trim());
        }
        catch
        {
            return null;
        }
    }
}
