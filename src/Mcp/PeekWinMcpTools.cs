using System.ComponentModel;
using ModelContextProtocol.Server;
using PeekWin.Cli;

namespace PeekWin.Mcp;

[McpServerToolType]
[Description("MCP tools for running peekwin commands over stdio or HTTP.")]
public sealed class PeekWinMcpTools
{
    private readonly CommandRunner _commandRunner;

    public PeekWinMcpTools(CommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    [McpServerTool(Name = "run_command")]
    [Description("Run any peekwin command. Pass the exact tokens you would place after the `peekwin` executable name.")]
    public Task<CommandRunResult> RunCommand(
        [Description("Command arguments to pass after `peekwin`. Example: [\"window\", \"list\", \"--json\"]")]
        string[] arguments,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0)
        {
            arguments = ["--help"];
        }

        return _commandRunner.RunAsync(arguments, cancellationToken);
    }

    [McpServerTool(Name = "get_help", ReadOnly = true)]
    [Description("Return top-level or command-specific peekwin help text.")]
    public Task<CommandRunResult> GetHelp(
        [Description("Optional command path. Example: [\"wait\", \"ref\"] to run `peekwin wait ref --help`.")]
        string[]? commandPath,
        CancellationToken cancellationToken)
    {
        string[] arguments;
        if (commandPath is { Length: > 0 })
        {
            arguments = [.. commandPath, "--help"];
        }
        else
        {
            arguments = ["--help"];
        }

        return _commandRunner.RunAsync(arguments, cancellationToken);
    }
}
