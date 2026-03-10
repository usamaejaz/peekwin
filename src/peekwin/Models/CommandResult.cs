namespace PeekWin.Models;

public sealed record CommandResult(bool Success, string Message, string? OutputPath = null)
{
    public static CommandResult Ok(string message, string? outputPath = null) => new(true, message, outputPath);
    public static CommandResult Error(string message) => new(false, message);
}
