using System.Text.Json.Nodes;

namespace PeekWin.Cli;

public sealed record CommandRunResult(
    string[] Arguments,
    int ExitCode,
    bool Success,
    string Stdout,
    string Stderr,
    JsonNode? Json = null);
