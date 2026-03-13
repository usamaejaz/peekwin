using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;
using PeekWin.Cli;

namespace PeekWin.Mcp;

public static class McpHost
{
    public static bool IsMcpCommand(IReadOnlyList<string> args)
        => args.Count > 0 && args[0].Equals("mcp", StringComparison.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(IReadOnlyList<string> args)
    {
        if (IsVersionRequest(args))
        {
            System.Console.WriteLine(CommandShell.GetVersionText());
            return 0;
        }

        if (IsHelpRequest(args))
        {
            PrintHelp();
            return 0;
        }

        if (!OperatingSystem.IsWindows())
        {
            System.Console.Error.WriteLine("peekwin mcp currently runs on Windows only.");
            return 1;
        }

        var options = ParseOptions(args);
        if (options is null)
        {
            return 1;
        }

        return options.Transport.Equals("http", StringComparison.OrdinalIgnoreCase)
            ? await RunHttpServerAsync(options).ConfigureAwait(false)
            : await RunStdioServerAsync(args.ToArray()).ConfigureAwait(false);
    }

    public static void PrintHelp()
    {
        System.Console.WriteLine(GetHelpText());
    }

    public static string GetHelpText()
        => string.Join(Environment.NewLine, new[]
        {
            "peekwin mcp - MCP server for peekwin",
            string.Empty,
            "Usage:",
            "  peekwin mcp",
            "  peekwin mcp --transport stdio",
            "  peekwin mcp --transport http [--urls <url-list>] [--path <route>] [--stateless]",
            "  peekwin mcp --help",
            "  peekwin mcp version",
            string.Empty,
            "Transports:",
            "  stdio                          default",
            "  http                           streamable HTTP + legacy SSE compatibility",
            string.Empty,
            "HTTP options:",
            "  --urls <url-list>              default: http://127.0.0.1:3000",
            "  --path <route>                 default: /mcp",
            "  --stateless                    disable MCP session state across requests",
            string.Empty,
            "Tools:",
            "  run_command   run any peekwin command by passing the CLI tokens",
            "  get_help      return top-level or command-specific peekwin help text"
        });

    private static bool IsHelpRequest(IReadOnlyList<string> args)
        => args.Count > 0 && args.Any(static arg =>
            arg.Equals("--help", StringComparison.OrdinalIgnoreCase)
            || arg.Equals("-h", StringComparison.OrdinalIgnoreCase)
            || arg.Equals("help", StringComparison.OrdinalIgnoreCase));

    private static bool IsVersionRequest(IReadOnlyList<string> args)
        => args.Count > 0 && args.Any(static arg => arg.Equals("version", StringComparison.OrdinalIgnoreCase));

    private static async Task<int> RunStdioServerAsync(string[] args)
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

    private static async Task<int> RunHttpServerAsync(McpHostOptions options)
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
        System.Console.Error.WriteLine($"peekwin mcp listening on {options.Urls}{options.Path}");
        await app.RunAsync().ConfigureAwait(false);
        return 0;
    }

    private static McpHostOptions? ParseOptions(IReadOnlyList<string> args)
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
                        System.Console.Error.WriteLine("Missing value for --transport.");
                        return null;
                    }

                    transport = args[++i];
                    if (!transport.Equals("stdio", StringComparison.OrdinalIgnoreCase)
                        && !transport.Equals("http", StringComparison.OrdinalIgnoreCase))
                    {
                        System.Console.Error.WriteLine("Unsupported transport. Use 'stdio' or 'http'.");
                        return null;
                    }

                    break;
                case "--urls":
                    if (i + 1 >= args.Count)
                    {
                        System.Console.Error.WriteLine("Missing value for --urls.");
                        return null;
                    }

                    urls = args[++i];
                    break;
                case "--path":
                    if (i + 1 >= args.Count)
                    {
                        System.Console.Error.WriteLine("Missing value for --path.");
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
                        System.Console.Error.WriteLine($"Unknown option: {arg}");
                        return null;
                    }

                    break;
            }
        }

        path = NormalizePath(path);
        return new McpHostOptions(transport, urls, path, stateless);
    }

    private static string NormalizePath(string path)
    {
        var trimmed = string.IsNullOrWhiteSpace(path) ? "/mcp" : path.Trim();
        if (trimmed == "/")
        {
            return string.Empty;
        }

        return trimmed.StartsWith("/", StringComparison.Ordinal) ? trimmed : "/" + trimmed;
    }

    private sealed record McpHostOptions(string Transport, string Urls, string Path, bool Stateless);
}
