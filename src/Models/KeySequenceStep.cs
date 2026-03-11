namespace PeekWin.Models;

public sealed record KeySequenceStep(string Action, string? Key = null, int? DelayMs = null);
