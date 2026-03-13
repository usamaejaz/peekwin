using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using PeekWin;
using PeekWin.Cli;
using PeekWin.Models;
using PeekWin.Services;

var checks = new DevChecks();
return checks.Run();

internal sealed class DevChecks
{
    public int Run()
    {
        try
        {
            var repoRoot = FindRepoRoot();
            VerifyVersionMetadata(repoRoot);
            VerifySnapshotStore(repoRoot);
            VerifyCommandRunner();
            Console.WriteLine("PeekWin dev checks passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void VerifyVersionMetadata(string repoRoot)
    {
        var propsPath = Path.Combine(repoRoot, "Directory.Build.props");
        var doc = XDocument.Load(propsPath);
        var expectedVersion = doc.Root?
            .Descendants("PeekWinVersion")
            .SingleOrDefault()?
            .Value;

        Assert(!string.IsNullOrWhiteSpace(expectedVersion), $"Could not read <PeekWinVersion> from {propsPath}.");
        Assert(CommandShell.GetVersionText() == expectedVersion, $"CommandShell.GetVersionText() returned '{CommandShell.GetVersionText()}', expected '{expectedVersion}'.");
    }

    private static void VerifySnapshotStore(string repoRoot)
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "peekwin-dev-checks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storageRoot);

        try
        {
            var service = new AutomationSnapshotService(storageRoot);
            var firstSnapshot = service.SaveSnapshot(CreateWindowInspection("0x42", "First"), maxDepth: 1, CreateElements("e1", "0.0"));
            var firstResolved = service.ResolveRef("e1");

            Assert(firstResolved.SnapshotId == firstSnapshot.SnapshotId, "ResolveRef should return the active snapshot identifier.");
            Assert(firstResolved.WindowHandle == (nint)0x42, "ResolveRef should preserve the saved window handle.");

            var secondSnapshot = service.SaveSnapshot(CreateWindowInspection("0x43", "Second"), maxDepth: 2, CreateElements("e2", "0.1"));
            var currentSnapshot = service.LoadLatestSnapshot();
            var secondResolved = service.ResolveRef("e2");

            Assert(currentSnapshot.SnapshotId == secondSnapshot.SnapshotId, "LoadLatestSnapshot should follow the current pointer.");
            Assert(secondResolved.SnapshotId == secondSnapshot.SnapshotId, "ResolveRef should use the latest saved snapshot.");
            Assert(secondResolved.WindowHandle == (nint)0x43, "ResolveRef should use the latest saved window handle.");

            var snapshotDirectory = Path.Combine(storageRoot, "snapshots");
            Assert(Directory.GetFiles(snapshotDirectory, "*.json").Length >= 2, "Snapshot history should retain recent immutable payloads.");

            var pointerPath = Path.Combine(storageRoot, "current.json");
            var pointerJson = JsonNode.Parse(File.ReadAllText(pointerPath))!.AsObject();
            var snapshotFileName = pointerJson["snapshotFileName"]?.GetValue<string>();
            Assert(!string.IsNullOrWhiteSpace(snapshotFileName), "Current snapshot pointer should include the snapshot file name.");
            pointerJson["capturedBySessionId"] = Process.GetCurrentProcess().SessionId + 1000;
            File.WriteAllText(pointerPath, pointerJson.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            var ex = AssertThrows<InvalidOperationException>(() => service.ResolveRef("e2"));
            Assert(ex.Message.Contains("Windows session", StringComparison.OrdinalIgnoreCase), "Cross-session snapshot reuse should be rejected with a session-specific message.");
        }
        finally
        {
            try
            {
                Directory.Delete(storageRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static void VerifyCommandRunner()
    {
        var runner = PeekWinRuntimeFactory.CreateCommandRunner();
        var versionResult = runner.RunAsync(["version"]).GetAwaiter().GetResult();
        Assert(versionResult.Success, "CommandRunner should report success for version.");
        Assert(string.IsNullOrWhiteSpace(versionResult.Stderr), "CommandRunner version should not write to stderr.");
        Assert(!string.IsNullOrWhiteSpace(versionResult.Stdout), "CommandRunner version should capture stdout.");

        var helpResult = runner.RunAsync(["wait", "ref", "--help"]).GetAwaiter().GetResult();
        Assert(helpResult.Success, "CommandRunner should report success for help.");
        Assert(helpResult.Stdout.Contains("peekwin wait ref --ref <id>", StringComparison.Ordinal), "CommandRunner should capture command help text.");
    }

    private static WindowInspection CreateWindowInspection(string handle, string title)
        => new(
            handle,
            title,
            "TestClass",
            4242,
            "test-app",
            true,
            false,
            false,
            "current",
            new RectDto(10, 20, 300, 200));

    private static IReadOnlyList<AutomationTreeNode> CreateElements(string elementRef, string path)
        => new[]
        {
            new AutomationTreeNode("e0", null, "0", 0, "Root", "root", "ControlType.Window", "window", new RectDto(10, 20, 300, 200), true, true, false),
            new AutomationTreeNode(elementRef, "e0", path, 1, "Save", "save-button", "ControlType.Button", "button", new RectDto(20, 40, 80, 30), true, true, false)
        };

    private static T AssertThrows<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T ex)
        {
            return ex;
        }

        throw new InvalidOperationException($"Expected exception of type {typeof(T).Name}.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static string FindRepoRoot()
    {
        var candidates = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var candidate in candidates)
        {
            var directory = new DirectoryInfo(candidate);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
