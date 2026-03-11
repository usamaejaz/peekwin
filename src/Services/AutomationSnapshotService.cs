using System.Globalization;
using System.Text.Json;
using PeekWin.Models;

namespace PeekWin.Services;

public sealed class AutomationSnapshotService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _snapshotPath = Path.Combine(Path.GetTempPath(), "peekwin", "latest-see.json");

    public void SaveSnapshot(string targetLabel, string? appName, nint windowHandle, RectDto bounds, int maxDepth, IReadOnlyList<AutomationTreeNode> elements)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_snapshotPath)!);
        var snapshot = new AutomationSnapshot(
            "1",
            DateTimeOffset.UtcNow,
            targetLabel,
            appName,
            $"0x{windowHandle.ToInt64():X}",
            bounds,
            maxDepth,
            elements);
        File.WriteAllText(_snapshotPath, JsonSerializer.Serialize(snapshot, JsonOptions));
    }

    public AutomationSnapshot LoadLatestSnapshot()
    {
        if (!File.Exists(_snapshotPath))
        {
            throw new InvalidOperationException("No saved UI snapshot found. Run `peekwin see` first.");
        }

        var snapshot = JsonSerializer.Deserialize<AutomationSnapshot>(File.ReadAllText(_snapshotPath), JsonOptions);
        if (snapshot is null)
        {
            throw new InvalidOperationException("Saved UI snapshot is invalid. Run `peekwin see` again.");
        }

        return snapshot;
    }

    public AutomationRefTarget ResolveRef(string refId)
    {
        var snapshot = LoadLatestSnapshot();
        var element = snapshot.Elements.FirstOrDefault(item => item.Ref.Equals(refId, StringComparison.OrdinalIgnoreCase));
        if (element is null)
        {
            throw new InvalidOperationException($"No UI element ref matched: {refId}. Run `peekwin see` again.");
        }

        var handleValue = snapshot.WindowHandle.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToInt64(snapshot.WindowHandle[2..], 16)
            : Convert.ToInt64(snapshot.WindowHandle, CultureInfo.InvariantCulture);

        return new AutomationRefTarget(
            element.Ref,
            snapshot.TargetLabel,
            snapshot.AppName,
            (nint)handleValue,
            element.Bounds,
            element.Name,
            element.AutomationId,
            element.ControlType,
            element.Path);
    }
}
