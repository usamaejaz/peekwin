using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using PeekWin.Models;

namespace PeekWin.Services;

public sealed class AutomationSnapshotService
{
    private const string SnapshotSchemaVersion = "3";
    private const string PointerSchemaVersion = "1";
    private const int SnapshotRetentionCount = 8;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _storageRoot;
    private readonly string _snapshotDirectory;
    private readonly string _currentPointerPath;

    public AutomationSnapshotService(string? storageRoot = null)
    {
        _storageRoot = storageRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "peekwin",
            "automation");
        _snapshotDirectory = Path.Combine(_storageRoot, "snapshots");
        _currentPointerPath = Path.Combine(_storageRoot, "current.json");
    }

    public AutomationSnapshot SaveSnapshot(WindowInspection window, int maxDepth, IReadOnlyList<AutomationTreeNode> elements)
    {
        var capturedAt = DateTimeOffset.UtcNow;
        var snapshotId = $"{capturedAt:yyyyMMddTHHmmssfffffffZ}-{Environment.ProcessId}-{Guid.NewGuid():N}";
        var snapshot = new AutomationSnapshot(
            SnapshotSchemaVersion,
            snapshotId,
            capturedAt,
            Environment.ProcessId,
            Process.GetCurrentProcess().SessionId,
            window.Title,
            window.ProcessName,
            window.Handle,
            window.Title,
            window.ClassName,
            window.ProcessId,
            window.Bounds,
            maxDepth,
            elements);

        Directory.CreateDirectory(_snapshotDirectory);

        var snapshotFileName = $"{snapshotId}.json";
        var snapshotPath = Path.Combine(_snapshotDirectory, snapshotFileName);

        // Persist immutable snapshot payloads first, then atomically swap the current pointer.
        WriteJsonAtomically(snapshotPath, snapshot);
        WriteJsonAtomically(
            _currentPointerPath,
            new SnapshotPointer(
                PointerSchemaVersion,
                snapshot.SnapshotId,
                snapshotFileName,
                snapshot.CapturedAt,
                snapshot.CapturedBySessionId));

        CleanupSnapshotHistory(snapshotFileName);
        return snapshot;
    }

    public AutomationSnapshot LoadLatestSnapshot()
    {
        if (!File.Exists(_currentPointerPath))
        {
            throw new InvalidOperationException("No saved UI snapshot found. Run `peekwin see` first.");
        }

        var pointer = ReadJsonFile<SnapshotPointer>(_currentPointerPath, "Saved UI snapshot pointer is invalid. Run `peekwin see` again.");
        if (pointer.Version != PointerSchemaVersion)
        {
            throw new InvalidOperationException("Saved UI snapshot pointer is outdated. Run `peekwin see` again.");
        }

        var currentSessionId = Process.GetCurrentProcess().SessionId;
        if (pointer.CapturedBySessionId != currentSessionId)
        {
            throw new InvalidOperationException(
                $"Saved UI snapshot was captured in Windows session {pointer.CapturedBySessionId} and cannot be reused from session {currentSessionId}. Run `peekwin see` again.");
        }

        var snapshotPath = Path.Combine(_snapshotDirectory, pointer.SnapshotFileName);
        if (!File.Exists(snapshotPath))
        {
            throw new InvalidOperationException("Saved UI snapshot payload is missing. Run `peekwin see` again.");
        }

        var snapshot = ReadJsonFile<AutomationSnapshot>(snapshotPath, "Saved UI snapshot is invalid. Run `peekwin see` again.");
        if (snapshot.Version != SnapshotSchemaVersion)
        {
            throw new InvalidOperationException("Saved UI snapshot is outdated. Run `peekwin see` again.");
        }

        if (!string.Equals(snapshot.SnapshotId, pointer.SnapshotId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Saved UI snapshot pointer is inconsistent. Run `peekwin see` again.");
        }

        if (snapshot.CapturedBySessionId != pointer.CapturedBySessionId)
        {
            throw new InvalidOperationException("Saved UI snapshot metadata is inconsistent. Run `peekwin see` again.");
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
            snapshot.SnapshotId,
            element.Ref,
            snapshot.TargetLabel,
            snapshot.AppName,
            (nint)handleValue,
            snapshot.WindowTitle,
            snapshot.WindowClassName,
            snapshot.ProcessId,
            element.Bounds,
            element.Name,
            element.AutomationId,
            element.ControlType,
            element.Path);
    }

    private void CleanupSnapshotHistory(string currentSnapshotFileName)
    {
        try
        {
            if (!Directory.Exists(_snapshotDirectory))
            {
                return;
            }

            var filesToKeep = Directory
                .EnumerateFiles(_snapshotDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(static fileName => !string.IsNullOrWhiteSpace(fileName))
                .OrderByDescending(static fileName => fileName, StringComparer.Ordinal)
                .Take(SnapshotRetentionCount)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            filesToKeep.Add(currentSnapshotFileName);

            foreach (var snapshotPath in Directory.EnumerateFiles(_snapshotDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(snapshotPath);
                if (filesToKeep.Contains(fileName))
                {
                    continue;
                }

                try
                {
                    File.Delete(snapshotPath);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    private static T ReadJsonFile<T>(string path, string invalidMessage)
    {
        try
        {
            var value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
            return value is null ? throw new InvalidOperationException(invalidMessage) : value;
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(invalidMessage);
        }
        catch (IOException)
        {
            throw new InvalidOperationException(invalidMessage);
        }
    }

    private static void WriteJsonAtomically<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException($"Cannot determine storage directory for {path}.");
        }

        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, value, JsonOptions);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch
            {
            }
        }
    }

    private sealed record SnapshotPointer(
        string Version,
        string SnapshotId,
        string SnapshotFileName,
        DateTimeOffset CapturedAt,
        int CapturedBySessionId);
}
