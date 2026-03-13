using System.Runtime.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;
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

var options = ParseOptions(args);
if (options is null)
{
    return 1;
}

return options.Transport.Equals("http", StringComparison.OrdinalIgnoreCase)
    ? await RunHttpServerAsync(options).ConfigureAwait(false)
    : await RunStdioServerAsync(args).ConfigureAwait(false);

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
    Console.WriteLine("  peekwin-mcp --transport stdio");
    Console.WriteLine("  peekwin-mcp --transport http [--urls <url-list>] [--path <route>] [--stateless]");
    Console.WriteLine("  peekwin-mcp --help");
    Console.WriteLine("  peekwin-mcp version");
    Console.WriteLine();
    Console.WriteLine("Transports:");
    Console.WriteLine("  stdio                          default");
    Console.WriteLine("  http                           streamable HTTP + legacy SSE compatibility");
    Console.WriteLine();
    Console.WriteLine("HTTP options:");
    Console.WriteLine("  --urls <url-list>              default: http://127.0.0.1:3000");
    Console.WriteLine("  --path <route>                 default: /mcp");
    Console.WriteLine("  --stateless                    disable MCP session state across requests");
    Console.WriteLine();
    Console.WriteLine("Tools:");
    Console.WriteLine("  run_command   run any peekwin command by passing the CLI tokens");
    Console.WriteLine("  get_help      return top-level or command-specific peekwin help text");
}

static async Task<int> RunStdioServerAsync(string[] args)
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Logging.ClearProviders();
    builder.Services.AddSingleton(_ => PeekWinRuntimeFactory.CreateCommandRunner());
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<PeekWinMcpTools>();

    await builder.Build().RunAsync().ConfigureAwait(false);
    return 0;
}

static async Task<int> RunHttpServerAsync(McpHostOptions options)
{
    var builder = WebApplication.CreateBuilder();
    builder.Logging.ClearProviders();
    builder.WebHost.UseUrls(options.Urls);
    builder.Services.AddSingleton(_ => PeekWinRuntimeFactory.CreateCommandRunner());
    builder.Services
        .AddMcpServer()
        .WithHttpTransport(transportOptions =>
        {
            transportOptions.Stateless = options.Stateless;
        })
        .WithTools<PeekWinMcpTools>();

    var app = builder.Build();
    app.MapMcp(options.Path);
    Console.Error.WriteLine($"peekwin-mcp listening on {options.Urls}{options.Path}");
    await app.RunAsync().ConfigureAwait(false);
    return 0;
}

static McpHostOptions? ParseOptions(IReadOnlyList<string> args)
{
    var transport = "stdio";
    var urls = "http://127.0.0.1:3000";
    var path = "/mcp";
    var stateless = false;

    for (int i = 0; i < args.Count; i++)
    {
        var arg = args[i];
        switch (arg.ToLowerInvariant())
        {
            case "--transport":
                if (i + 1 >= args.Count)
                {
                    Console.Error.WriteLine("Missing value for --transport.");
                    return null;
                }

                transport = args[++i];
                if (!transport.Equals("stdio", StringComparison.OrdinalIgnoreCase)
                    && !transport.Equals("http", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine("Unsupported transport. Use 'stdio' or 'http'.");
                    return null;
                }

                break;
            case "--urls":
                if (i + 1 >= args.Count)
                {
                    Console.Error.WriteLine("Missing value for --urls.");
                    return null;
                }

                urls = args[++i];
                break;
            case "--path":
                if (i + 1 >= args.Count)
                {
                    Console.Error.WriteLine("Missing value for --path.");
                    return null;
                }

                path = args[++i];
                break;
            case "--stateless":
                stateless = true;
                break;
            case "--help":
            case "-h":
            case "help":
            case "version":
                break;
            default:
                if (arg.StartsWith("--", StringComparison.Ordinal))
                {
                    Console.Error.WriteLine($"Unknown option: {arg}");
                    return null;
                }

                break;
        }
    }

    path = NormalizePath(path);
    return new McpHostOptions(transport, urls, path, stateless);
}

static string NormalizePath(string path)
{
    var trimmed = string.IsNullOrWhiteSpace(path) ? "/mcp" : path.Trim();
    if (trimmed == "/")
    {
        return string.Empty;
    }

    return trimmed.StartsWith("/", StringComparison.Ordinal) ? trimmed : "/" + trimmed;
}

internal sealed record McpHostOptions(string Transport, string Urls, string Path, bool Stateless);
