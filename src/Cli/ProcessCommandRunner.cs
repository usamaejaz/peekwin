using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;

namespace PeekWin.Cli;

public sealed class ProcessCommandRunner : ICommandRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TimeoutGrace = TimeSpan.FromSeconds(5);

    private readonly string _fileName;
    private readonly IReadOnlyList<string> _prefixArguments;
    private readonly TimeSpan _defaultTimeout;
    private readonly TimeSpan _timeoutGrace;

    public ProcessCommandRunner(string fileName, IReadOnlyList<string>? prefixArguments = null, TimeSpan? defaultTimeout = null, TimeSpan? timeoutGrace = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        _fileName = fileName;
        _prefixArguments = prefixArguments ?? [];
        _defaultTimeout = defaultTimeout ?? DefaultTimeout;
        _timeoutGrace = timeoutGrace ?? TimeoutGrace;

        if (_defaultTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultTimeout), "defaultTimeout must be positive.");
        }

        if (_timeoutGrace <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutGrace), "timeoutGrace must be positive.");
        }
    }

    public static ProcessCommandRunner CreateForCurrentProcess()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("Could not determine the current process path for MCP command execution.");
        }

        var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;
        if (LooksLikeDotnetHost(processPath)
            && !string.IsNullOrWhiteSpace(entryAssemblyPath)
            && entryAssemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessCommandRunner(processPath, [entryAssemblyPath]);
        }

        return new ProcessCommandRunner(processPath);
    }

    public async Task<CommandRunResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var args = arguments.ToArray();
        var timeout = ResolveTimeout(args);
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        var startInfo = new ProcessStartInfo(_fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var prefix in _prefixArguments)
        {
            startInfo.ArgumentList.Add(prefix);
        }

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start the peekwin command process.");
        }

        using var _ = linkedSource.Token.Register(() => TryKill(process));
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(linkedSource.Token).ConfigureAwait(false);
            var stdoutText = await stdoutTask.ConfigureAwait(false);
            var stderrText = await stderrTask.ConfigureAwait(false);

            return new CommandRunResult(
                args,
                process.ExitCode,
                process.ExitCode == 0,
                stdoutText,
                stderrText,
                TryParseJson(stdoutText));
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            await WaitForExitOrGracePeriodAsync(process).ConfigureAwait(false);

            var capturedOutput = await TryCaptureOutputWithinGraceAsync(stdoutTask, stderrTask).ConfigureAwait(false);
            var timeoutMessage = $"MCP command timed out after {(int)timeout.TotalMilliseconds}ms.";
            var stderrText = string.IsNullOrWhiteSpace(capturedOutput.Stderr)
                ? timeoutMessage
                : $"{capturedOutput.Stderr.Trim()} {timeoutMessage}";

            if (!capturedOutput.Completed)
            {
                stderrText = $"{stderrText} Output capture did not complete before the timeout grace period elapsed.";
            }

            return new CommandRunResult(
                args,
                -1,
                false,
                capturedOutput.Stdout,
                stderrText,
                TryParseJson(capturedOutput.Stdout));
        }
    }

    private static bool LooksLikeDotnetHost(string processPath)
    {
        var fileName = Path.GetFileName(processPath);
        return fileName.Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase);
    }

    private TimeSpan ResolveTimeout(IReadOnlyList<string> args)
    {
        var timeout = _defaultTimeout;

        foreach (var optionName in new[] { "--timeout-ms", "--timeout", "--duration-ms" })
        {
            var value = TryReadIntOption(args, optionName);
            if (value is > 0)
            {
                timeout = TimeSpan.FromMilliseconds(Math.Max(timeout.TotalMilliseconds, value.Value + _timeoutGrace.TotalMilliseconds));
            }
        }

        return timeout;
    }

    private async Task WaitForExitOrGracePeriodAsync(Process process)
    {
        try
        {
            await Task.WhenAny(process.WaitForExitAsync(), Task.Delay(_timeoutGrace)).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private async Task<(bool Completed, string Stdout, string Stderr)> TryCaptureOutputWithinGraceAsync(Task<string> stdoutTask, Task<string> stderrTask)
    {
        var combinedTask = Task.WhenAll(stdoutTask, stderrTask);
        var completedTask = await Task.WhenAny(combinedTask, Task.Delay(_timeoutGrace)).ConfigureAwait(false);
        if (completedTask == combinedTask)
        {
            await combinedTask.ConfigureAwait(false);
            return (true, stdoutTask.Result, stderrTask.Result);
        }

        return (false, string.Empty, string.Empty);
    }

    private static int? TryReadIntOption(IReadOnlyList<string> args, string optionName)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (!string.Equals(args[i], optionName, StringComparison.Ordinal))
            {
                continue;
            }

            if (int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
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
