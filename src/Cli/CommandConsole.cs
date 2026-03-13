using System.IO;
using System.Threading;

namespace PeekWin.Cli;

internal static class Console
{
    private static readonly AsyncLocal<TextWriter?> StdOut = new();
    private static readonly AsyncLocal<TextWriter?> StdErr = new();

    public static TextWriter Error => StdErr.Value ?? System.Console.Error;

    public static void WriteLine()
        => (StdOut.Value ?? System.Console.Out).WriteLine();

    public static void WriteLine(string? value)
        => (StdOut.Value ?? System.Console.Out).WriteLine(value);

    public static void WriteLine(object? value)
        => (StdOut.Value ?? System.Console.Out).WriteLine(value);

    internal static IDisposable Push(TextWriter stdout, TextWriter stderr)
        => new Scope(stdout, stderr);

    private sealed class Scope : IDisposable
    {
        private readonly TextWriter? _previousStdOut;
        private readonly TextWriter? _previousStdErr;

        public Scope(TextWriter stdout, TextWriter stderr)
        {
            _previousStdOut = StdOut.Value;
            _previousStdErr = StdErr.Value;
            StdOut.Value = stdout;
            StdErr.Value = stderr;
        }

        public void Dispose()
        {
            StdOut.Value = _previousStdOut;
            StdErr.Value = _previousStdErr;
        }
    }
}
