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

    public ProcessCommandRunner(string fileName, IReadOnlyList<string>? prefixArguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        _fileName = fileName;
        _prefixArguments = prefixArguments ?? [];
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
        using var timeoutSource = new CancellationTokenSource(ResolveTimeout(args));
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
            var stdoutText = await stdoutTask.ConfigureAwait(false);
            var stderrText = await stderrTask.ConfigureAwait(false);
            var timeoutMessage = $"MCP command timed out after {(int)ResolveTimeout(args).TotalMilliseconds}ms.";
            stderrText = string.IsNullOrWhiteSpace(stderrText)
                ? timeoutMessage
                : $"{stderrText.Trim()} {timeoutMessage}";

            return new CommandRunResult(
                args,
                -1,
                false,
                stdoutText,
                stderrText,
                TryParseJson(stdoutText));
        }
    }

    private static bool LooksLikeDotnetHost(string processPath)
    {
        var fileName = Path.GetFileName(processPath);
        return fileName.Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static TimeSpan ResolveTimeout(IReadOnlyList<string> args)
    {
        var timeout = DefaultTimeout;

        foreach (var optionName in new[] { "--timeout-ms", "--timeout", "--duration-ms" })
        {
            var value = TryReadIntOption(args, optionName);
            if (value is > 0)
            {
                timeout = TimeSpan.FromMilliseconds(Math.Max(timeout.TotalMilliseconds, value.Value + TimeoutGrace.TotalMilliseconds));
            }
        }

        return timeout;
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
