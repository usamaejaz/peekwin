namespace PeekWin.Models;

public sealed record CommandResult(bool Success, string Message, string? OutputPath = null, object? Details = null)
{
    public static CommandResult Ok(string message, string? outputPath = null, object? details = null)
        => new(true, message, outputPath, details);

    public static CommandResult Error(string message, object? details = null)
        => new(false, message, null, details);
}
