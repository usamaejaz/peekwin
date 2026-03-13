using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using PeekWin;
using PeekWin.Cli;
using PeekWin.Mcp;

[assembly: SupportedOSPlatform("windows")]

if (IsVersionRequest(args))
{
    Console.WriteLine(CommandShell.GetVersionText());
    return 0;
}

if (IsHelpRequest(args))
{
    PrintHelp();
    return 0;
}

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("peekwin-mcp currently runs on Windows only.");
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Services.AddSingleton(_ => PeekWinRuntimeFactory.CreateCommandRunner());
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<PeekWinMcpTools>();

await builder.Build().RunAsync().ConfigureAwait(false);
return 0;

static bool IsHelpRequest(IReadOnlyList<string> args)
    => args.Count > 0 && args.Any(static arg =>
        arg.Equals("--help", StringComparison.OrdinalIgnoreCase)
        || arg.Equals("-h", StringComparison.OrdinalIgnoreCase)
        || arg.Equals("help", StringComparison.OrdinalIgnoreCase));

static bool IsVersionRequest(IReadOnlyList<string> args)
    => args.Count > 0 && args.Any(static arg => arg.Equals("version", StringComparison.OrdinalIgnoreCase));

static void PrintHelp()
{
    Console.WriteLine("peekwin-mcp - MCP server for peekwin");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  peekwin-mcp");
    Console.WriteLine("  peekwin-mcp --help");
    Console.WriteLine("  peekwin-mcp version");
    Console.WriteLine();
    Console.WriteLine("Transport:");
    Console.WriteLine("  stdio only");
    Console.WriteLine();
    Console.WriteLine("Tools:");
    Console.WriteLine("  run_command   run any peekwin command by passing the CLI tokens");
    Console.WriteLine("  get_help      return top-level or command-specific peekwin help text");
}
