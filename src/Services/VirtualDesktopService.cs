using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;
using PeekWin.Models;

namespace PeekWin.Services;

[SupportedOSPlatform("windows")]
public sealed class VirtualDesktopService(InputService inputService)
{
    private static readonly string[] CandidateCurrentDesktopValuePaths =
    [
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops",
        $@"Software\Microsoft\Windows\CurrentVersion\Explorer\SessionInfo\{Process.GetCurrentProcess().SessionId}\VirtualDesktops"
    ];

    public IReadOnlyList<VirtualDesktopInfo> ListDesktops()
    {
        var desktopIds = TryReadDesktopIds();
        var currentId = TryReadCurrentDesktopId();

        if (desktopIds.Count == 0)
        {
            return
            [
                new VirtualDesktopInfo(0, currentId ?? "unknown", true)
            ];
        }

        var desktops = desktopIds
            .Select((id, index) => new VirtualDesktopInfo(index, id, string.Equals(id, currentId, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (!desktops.Any(desktop => desktop.IsCurrent))
        {
            desktops[0] = desktops[0] with { IsCurrent = true };
        }

        return desktops;
    }

    public VirtualDesktopInfo GetCurrentDesktop()
    {
        var desktops = ListDesktops();
        return desktops.FirstOrDefault(desktop => desktop.IsCurrent)
            ?? desktops[0];
    }

    public async Task<CommandResult> SwitchDesktopAsync(int targetIndex, int delayMs = 125)
    {
        if (targetIndex < 0)
        {
            return CommandResult.Error("Desktop index must be greater than or equal to 0.");
        }

        var desktops = ListDesktops();
        if (targetIndex >= desktops.Count)
        {
            return CommandResult.Error($"Desktop index {targetIndex} is out of range. Found {desktops.Count} desktops.");
        }

        var current = GetCurrentDesktop();
        if (current.Index == targetIndex)
        {
            return CommandResult.Ok($"Already on desktop {targetIndex}.", details: new { current, desktops });
        }

        var stepKey = targetIndex > current.Index ? "right" : "left";
        var steps = Math.Abs(targetIndex - current.Index);

        for (var step = 0; step < steps; step++)
        {
            inputService.Hotkey(["ctrl", "lwin", stepKey]);
            await Task.Delay(delayMs).ConfigureAwait(false);
        }

        var updatedCurrent = GetCurrentDesktop();
        return updatedCurrent.Index == targetIndex
            ? CommandResult.Ok(
                $"Switched to desktop {targetIndex}.",
                details: new { from = current, current = updatedCurrent, steps })
            : CommandResult.Error(
                $"Desktop switch shortcut ran, but the current desktop is {updatedCurrent.Index} instead of {targetIndex}.",
                new { from = current, current = updatedCurrent, targetIndex, steps });
    }

    private static IReadOnlyList<string> TryReadDesktopIds()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops");
            var value = key?.GetValue("VirtualDesktopIDs") as byte[];
            return ParseGuidBuffer(value);
        }
        catch
        {
            return [];
        }
    }

    private static string? TryReadCurrentDesktopId()
    {
        foreach (var path in CandidateCurrentDesktopValuePaths)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(path);
                var value = key?.GetValue("CurrentVirtualDesktop") as byte[];
                var ids = ParseGuidBuffer(value);
                if (ids.Count > 0)
                {
                    return ids[0];
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ParseGuidBuffer(byte[]? bytes)
    {
        if (bytes is null || bytes.Length < 16 || bytes.Length % 16 != 0)
        {
            return [];
        }

        var ids = new List<string>(bytes.Length / 16);
        for (var offset = 0; offset < bytes.Length; offset += 16)
        {
            var slice = new byte[16];
            Buffer.BlockCopy(bytes, offset, slice, 0, 16);
            ids.Add(new Guid(slice).ToString("D"));
        }

        return ids;
    }
}
