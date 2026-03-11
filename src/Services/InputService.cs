using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using PeekWin.Infrastructure;
using PeekWin.Models;

namespace PeekWin.Services;

[SupportedOSPlatform("windows")]
public sealed class InputService
{
    public void Click(int x, int y, MouseButton button, bool isDouble)
    {
        MoveMouse(x, y);
        var count = isDouble ? 2 : 1;
        for (var i = 0; i < count; i++)
        {
            MouseDown(button);
            MouseUp(button);
        }
    }

    public async Task ClickAsync(int x, int y, MouseButton button, bool isDouble, int delayMs)
    {
        Click(x, y, button, isDouble);
        if (delayMs > 0 && isDouble)
        {
            await Task.Delay(delayMs).ConfigureAwait(false);
        }
    }

    public void MoveMouse(int x, int y)
    {
        if (!NativeMethods.SetCursorPos(x, y))
        {
            throw new InvalidOperationException($"Failed to move cursor to {x},{y}.");
        }
    }

    public async Task MoveMouseAsync(int x, int y, int durationMs, int steps)
    {
        if (durationMs <= 0)
        {
            MoveMouse(x, y);
            return;
        }

        var start = GetCursorPosition();
        steps = Math.Max(1, steps);
        var stepDelay = Math.Max(1, durationMs / steps);

        for (var step = 1; step <= steps; step++)
        {
            var nextX = start.X + (int)Math.Round((x - start.X) * (step / (double)steps), MidpointRounding.AwayFromZero);
            var nextY = start.Y + (int)Math.Round((y - start.Y) * (step / (double)steps), MidpointRounding.AwayFromZero);
            MoveMouse(nextX, nextY);

            if (step < steps)
            {
                await Task.Delay(stepDelay).ConfigureAwait(false);
            }
        }
    }

    public (int X, int Y) GetCursorPosition()
    {
        if (!NativeMethods.GetCursorPos(out var point))
        {
            throw new InvalidOperationException("Failed to read cursor position.");
        }

        return (point.X, point.Y);
    }

    public async Task DragAsync(int startX, int startY, int endX, int endY, MouseButton button, int durationMs, int steps)
    {
        steps = Math.Max(1, steps);
        await MoveMouseAsync(startX, startY, 0, 1).ConfigureAwait(false);
        MouseDown(button);

        try
        {
            var stepDelay = steps > 0 ? Math.Max(1, durationMs / steps) : 0;
            for (var step = 1; step <= steps; step++)
            {
                var x = startX + (int)Math.Round((endX - startX) * (step / (double)steps), MidpointRounding.AwayFromZero);
                var y = startY + (int)Math.Round((endY - startY) * (step / (double)steps), MidpointRounding.AwayFromZero);
                MoveMouse(x, y);

                if (step < steps && stepDelay > 0)
                {
                    await Task.Delay(stepDelay).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            MouseUp(button);
        }
    }

    public async Task TypeTextAsync(string text, int delayMs)
    {
        foreach (var ch in text)
        {
            var inputs = new[]
            {
                new NativeMethods.INPUT
                {
                    type = NativeMethods.INPUT_KEYBOARD,
                    U = new NativeMethods.InputUnion
                    {
                        ki = new NativeMethods.KEYBDINPUT
                        {
                            wScan = ch,
                            dwFlags = NativeMethods.KEYEVENTF_UNICODE
                        }
                    }
                },
                new NativeMethods.INPUT
                {
                    type = NativeMethods.INPUT_KEYBOARD,
                    U = new NativeMethods.InputUnion
                    {
                        ki = new NativeMethods.KEYBDINPUT
                        {
                            wScan = ch,
                            dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP
                        }
                    }
                }
            };

            Send(inputs);
            if (delayMs > 0)
            {
                await Task.Delay(delayMs).ConfigureAwait(false);
            }
        }
    }

    public Task PasteTextAsync(string text, int restoreDelayMs)
    {
        RunSta(() =>
        {
            var snapshot = RetryClipboard(CaptureClipboardSnapshot);
            try
            {
                RetryClipboard(() =>
                {
                    SetClipboardText(text);
                    return 0;
                });

                Hotkey(["ctrl", "v"]);
                if (restoreDelayMs > 0)
                {
                    Thread.Sleep(restoreDelayMs);
                }
            }
            finally
            {
                RetryClipboard(() =>
                {
                    RestoreClipboardSnapshot(snapshot);
                    return 0;
                });
            }

            return 0;
        });

        return Task.CompletedTask;
    }

    public async Task PressKeyAsync(string key, int repeat, int delayMs)
    {
        var binding = VirtualKeyParser.Parse(key);
        for (var i = 0; i < repeat; i++)
        {
            PressBinding(binding);

            if (i + 1 < repeat && delayMs > 0)
            {
                await Task.Delay(delayMs).ConfigureAwait(false);
            }
        }
    }

    public void Hotkey(IReadOnlyList<string> keys)
    {
        var sequence = ExpandKeySequence(keys);
        foreach (var key in sequence)
        {
            KeyDown(key);
        }

        for (var i = sequence.Count - 1; i >= 0; i--)
        {
            KeyUp(sequence[i]);
        }
    }

    public async Task SendKeySequenceAsync(IReadOnlyList<KeySequenceStep> steps, int defaultDelayMs)
    {
        var heldKeys = new List<ushort>();
        try
        {
            for (var index = 0; index < steps.Count; index++)
            {
                var step = steps[index];
                switch (step.Action)
                {
                    case "tap":
                        PressBinding(VirtualKeyParser.Parse(step.Key!));
                        break;
                    case "down":
                        foreach (var vk in ExpandBinding(VirtualKeyParser.Parse(step.Key!)))
                        {
                            if (!heldKeys.Contains(vk))
                            {
                                KeyDown(vk);
                                heldKeys.Add(vk);
                            }
                        }
                        break;
                    case "up":
                        ReleaseBinding(VirtualKeyParser.Parse(step.Key!), heldKeys);
                        break;
                    case "sleep":
                        await Task.Delay(step.DelayMs ?? 0).ConfigureAwait(false);
                        continue;
                    default:
                        throw new InvalidOperationException($"Unsupported key sequence action: {step.Action}.");
                }

                if (defaultDelayMs > 0 && index + 1 < steps.Count)
                {
                    await Task.Delay(defaultDelayMs).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            for (var i = heldKeys.Count - 1; i >= 0; i--)
            {
                KeyUp(heldKeys[i]);
            }
        }
    }

    public async Task HoldKeysAsync(IReadOnlyList<string> keys, int durationMs)
    {
        var sequence = ExpandKeySequence(keys);
        foreach (var key in sequence)
        {
            KeyDown(key);
        }

        try
        {
            await Task.Delay(durationMs).ConfigureAwait(false);
        }
        finally
        {
            for (var i = sequence.Count - 1; i >= 0; i--)
            {
                KeyUp(sequence[i]);
            }
        }
    }

    public async Task HoldMouseAsync(MouseButton button, int durationMs, int? x = null, int? y = null)
    {
        MouseDown(button, x, y);
        await Task.Delay(durationMs).ConfigureAwait(false);
        MouseUp(button);
    }

    public void MouseDown(MouseButton button, int? x = null, int? y = null)
    {
        if (x is not null && y is not null)
        {
            MoveMouse(x.Value, y.Value);
        }

        SendMouse(button, true);
    }

    public void MouseUp(MouseButton button, int? x = null, int? y = null)
    {
        if (x is not null && y is not null)
        {
            MoveMouse(x.Value, y.Value);
        }

        SendMouse(button, false);
    }

    public void Scroll(int verticalDelta, int horizontalDelta = 0)
    {
        var inputs = new List<NativeMethods.INPUT>();
        if (verticalDelta != 0)
        {
            inputs.Add(CreateMouseInput(NativeMethods.MOUSEEVENTF_WHEEL, unchecked((uint)verticalDelta)));
        }

        if (horizontalDelta != 0)
        {
            inputs.Add(CreateMouseInput(NativeMethods.MOUSEEVENTF_HWHEEL, unchecked((uint)horizontalDelta)));
        }

        if (inputs.Count == 0)
        {
            return;
        }

        Send(inputs.ToArray());
    }

    private static NativeMethods.INPUT CreateMouseInput(uint flags, uint mouseData)
        => new()
        {
            type = NativeMethods.INPUT_MOUSE,
            U = new NativeMethods.InputUnion
            {
                mi = new NativeMethods.MOUSEINPUT
                {
                    dwFlags = flags,
                    mouseData = mouseData
                }
            }
        };

    private static void SendMouse(MouseButton button, bool down)
    {
        uint flag = (button, down) switch
        {
            (MouseButton.Left, true) => NativeMethods.MOUSEEVENTF_LEFTDOWN,
            (MouseButton.Left, false) => NativeMethods.MOUSEEVENTF_LEFTUP,
            (MouseButton.Right, true) => NativeMethods.MOUSEEVENTF_RIGHTDOWN,
            (MouseButton.Right, false) => NativeMethods.MOUSEEVENTF_RIGHTUP,
            _ => throw new ArgumentOutOfRangeException(nameof(button))
        };

        Send([CreateMouseInput(flag, 0)]);
    }

    private static void KeyDown(ushort vk) => SendKey(vk, false);

    private static void KeyUp(ushort vk) => SendKey(vk, true);

    private static void SendKey(ushort vk, bool keyUp)
    {
        var scanCode = NativeMethods.MapVirtualKey(vk, NativeMethods.MAPVK_VK_TO_VSC);
        Send([
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = (ushort)scanCode,
                        dwFlags = keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0
                    }
                }
            }
        ]);
    }

    private static void Send(NativeMethods.INPUT[] inputs)
    {
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException("Windows rejected the injected input.");
        }
    }

    private void PressBinding(VirtualKeyBinding binding)
    {
        foreach (var modifier in binding.Modifiers)
        {
            KeyDown(modifier);
        }

        try
        {
            KeyDown(binding.VirtualKey);
            KeyUp(binding.VirtualKey);
        }
        finally
        {
            for (var i = binding.Modifiers.Count - 1; i >= 0; i--)
            {
                KeyUp(binding.Modifiers[i]);
            }
        }
    }

    private static IReadOnlyList<ushort> ExpandBinding(VirtualKeyBinding binding)
    {
        var sequence = new List<ushort>();
        foreach (var modifier in binding.Modifiers)
        {
            if (!sequence.Contains(modifier))
            {
                sequence.Add(modifier);
            }
        }

        if (!sequence.Contains(binding.VirtualKey))
        {
            sequence.Add(binding.VirtualKey);
        }

        return sequence;
    }

    private static void ReleaseBinding(VirtualKeyBinding binding, List<ushort> heldKeys)
    {
        var expanded = ExpandBinding(binding);
        for (var i = expanded.Count - 1; i >= 0; i--)
        {
            var vk = expanded[i];
            var index = heldKeys.LastIndexOf(vk);
            if (index >= 0)
            {
                KeyUp(vk);
                heldKeys.RemoveAt(index);
            }
        }
    }

    private static IReadOnlyList<ushort> ExpandKeySequence(IReadOnlyList<string> keys)
    {
        var sequence = new List<ushort>();
        foreach (var binding in keys.Select(VirtualKeyParser.Parse))
        {
            foreach (var vk in ExpandBinding(binding))
            {
                if (!sequence.Contains(vk))
                {
                    sequence.Add(vk);
                }
            }
        }

        return sequence;
    }

    private static ClipboardSnapshot CaptureClipboardSnapshot()
    {
        var hadClipboardData = ClipboardHasData();
        if (!hadClipboardData)
        {
            return new ClipboardSnapshot(false, null);
        }

        var result = NativeMethods.OleGetClipboard(out var dataObject);
        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }

        return new ClipboardSnapshot(true, dataObject);
    }

    private static void RestoreClipboardSnapshot(ClipboardSnapshot snapshot)
    {
        if (!snapshot.HadClipboardData)
        {
            if (!NativeMethods.OpenClipboard(nint.Zero))
            {
                throw new ExternalException("Failed to open clipboard.");
            }

            try
            {
                NativeMethods.EmptyClipboard();
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }

            return;
        }

        var result = NativeMethods.OleSetClipboard(snapshot.DataObject);
        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }

        result = NativeMethods.OleFlushClipboard();
        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }
    }

    private static void SetClipboardText(string text)
    {
        if (!NativeMethods.OpenClipboard(nint.Zero))
        {
            throw new ExternalException("Failed to open clipboard.");
        }

        nint memory = nint.Zero;
        nint locked = nint.Zero;
        try
        {
            if (!NativeMethods.EmptyClipboard())
            {
                throw new ExternalException("Failed to clear clipboard.");
            }

            var bytes = (text.Length + 1) * sizeof(char);
            memory = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE | NativeMethods.GMEM_ZEROINIT, (nuint)bytes);
            if (memory == nint.Zero)
            {
                throw new InvalidOperationException("Failed to allocate clipboard memory.");
            }

            locked = NativeMethods.GlobalLock(memory);
            if (locked == nint.Zero)
            {
                throw new InvalidOperationException("Failed to lock clipboard memory.");
            }

            Marshal.Copy(text.ToCharArray(), 0, locked, text.Length);
            Marshal.WriteInt16(locked, text.Length * sizeof(char), 0);
            NativeMethods.GlobalUnlock(memory);
            locked = nint.Zero;

            if (NativeMethods.SetClipboardData(NativeMethods.CF_UNICODETEXT, memory) == nint.Zero)
            {
                throw new ExternalException("Failed to publish clipboard text.");
            }

            memory = nint.Zero;
        }
        finally
        {
            if (locked != nint.Zero)
            {
                NativeMethods.GlobalUnlock(memory);
            }

            if (memory != nint.Zero)
            {
                NativeMethods.GlobalFree(memory);
            }

            NativeMethods.CloseClipboard();
        }
    }

    private static void WithClipboardRetry(Action action)
        => WithClipboardRetry<object?>(() =>
        {
            action();
            return null;
        });

    private static T WithClipboardRetry<T>(Func<T> action)
        => RunSta(() => RetryClipboard(action));

    private static T RetryClipboard<T>(Func<T> action)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                return action();
            }
            catch (Exception ex) when (ex is ExternalException or COMException)
            {
                lastError = ex;
                Thread.Sleep(25);
            }
        }

        throw new InvalidOperationException("Clipboard is busy and could not be accessed.", lastError);
    }

    private static T RunSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? error = null;

        var thread = new Thread(() =>
        {
            var oleInitialized = false;
            try
            {
                var oleResult = NativeMethods.OleInitialize(nint.Zero);
                if (oleResult < 0)
                {
                    Marshal.ThrowExceptionForHR(oleResult);
                }

                oleInitialized = true;
                result = action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                if (oleInitialized)
                {
                    NativeMethods.OleUninitialize();
                }
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
        {
            throw error;
        }

        return result!;
    }

    private static bool ClipboardHasData()
    {
        if (!NativeMethods.OpenClipboard(nint.Zero))
        {
            throw new ExternalException("Failed to open clipboard.");
        }

        try
        {
            return NativeMethods.EnumClipboardFormats(0) != 0;
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    private sealed record ClipboardSnapshot(bool HadClipboardData, System.Runtime.InteropServices.ComTypes.IDataObject? DataObject);
}

internal sealed record VirtualKeyBinding(ushort VirtualKey, IReadOnlyList<ushort> Modifiers);

internal static class VirtualKeyParser
{
    private static readonly Dictionary<string, ushort> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["backspace"] = 0x08,
        ["tab"] = 0x09,
        ["enter"] = 0x0D,
        ["shift"] = 0x10,
        ["lshift"] = 0xA0,
        ["rshift"] = 0xA1,
        ["ctrl"] = 0x11,
        ["control"] = 0x11,
        ["lctrl"] = 0xA2,
        ["rctrl"] = 0xA3,
        ["alt"] = 0x12,
        ["lalt"] = 0xA4,
        ["ralt"] = 0xA5,
        ["pause"] = 0x13,
        ["capslock"] = 0x14,
        ["esc"] = 0x1B,
        ["escape"] = 0x1B,
        ["space"] = 0x20,
        ["printscreen"] = 0x2C,
        ["prtsc"] = 0x2C,
        ["pageup"] = 0x21,
        ["pagedown"] = 0x22,
        ["end"] = 0x23,
        ["home"] = 0x24,
        ["left"] = 0x25,
        ["up"] = 0x26,
        ["right"] = 0x27,
        ["down"] = 0x28,
        ["insert"] = 0x2D,
        ["delete"] = 0x2E,
        ["numlock"] = 0x90,
        ["scrolllock"] = 0x91,
        ["numpad0"] = 0x60,
        ["numpad1"] = 0x61,
        ["numpad2"] = 0x62,
        ["numpad3"] = 0x63,
        ["numpad4"] = 0x64,
        ["numpad5"] = 0x65,
        ["numpad6"] = 0x66,
        ["numpad7"] = 0x67,
        ["numpad8"] = 0x68,
        ["numpad9"] = 0x69,
        ["multiply"] = 0x6A,
        ["add"] = 0x6B,
        ["separator"] = 0x6C,
        ["subtract"] = 0x6D,
        ["decimal"] = 0x6E,
        ["divide"] = 0x6F,
        ["lwin"] = 0x5B,
        ["win"] = 0x5B,
        ["start"] = 0x5B,
        ["rwin"] = 0x5C,
        ["apps"] = 0x5D,
        ["f1"] = 0x70,
        ["f2"] = 0x71,
        ["f3"] = 0x72,
        ["f4"] = 0x73,
        ["f5"] = 0x74,
        ["f6"] = 0x75,
        ["f7"] = 0x76,
        ["f8"] = 0x77,
        ["f9"] = 0x78,
        ["f10"] = 0x79,
        ["f11"] = 0x7A,
        ["f12"] = 0x7B,
        ["f13"] = 0x7C,
        ["f14"] = 0x7D,
        ["f15"] = 0x7E,
        ["f16"] = 0x7F,
        ["f17"] = 0x80,
        ["f18"] = 0x81,
        ["f19"] = 0x82,
        ["f20"] = 0x83,
        ["f21"] = 0x84,
        ["f22"] = 0x85,
        ["f23"] = 0x86,
        ["f24"] = 0x87,
        ["browserback"] = 0xA6,
        ["browserforward"] = 0xA7,
        ["browserrefresh"] = 0xA8,
        ["browserstop"] = 0xA9,
        ["browsersearch"] = 0xAA,
        ["browserfavorites"] = 0xAB,
        ["browserhome"] = 0xAC,
        ["volumemute"] = 0xAD,
        ["volumedown"] = 0xAE,
        ["volumeup"] = 0xAF,
        ["medianext"] = 0xB0,
        ["mediaprev"] = 0xB1,
        ["mediastop"] = 0xB2,
        ["mediaplaypause"] = 0xB3,
        ["launchmail"] = 0xB4,
        ["launchmedia"] = 0xB5,
        ["launchapp1"] = 0xB6,
        ["launchapp2"] = 0xB7
    };

    public static VirtualKeyBinding Parse(string value)
    {
        if (NamedKeys.TryGetValue(value, out var vk))
        {
            return new VirtualKeyBinding(vk, Array.Empty<ushort>());
        }

        if (value.Length == 1)
        {
            var mapped = NativeMethods.VkKeyScan(value[0]);
            if (mapped != -1)
            {
                var modifiers = new List<ushort>(3);
                var modifierBits = (mapped >> 8) & 0xFF;
                if ((modifierBits & 1) != 0)
                {
                    modifiers.Add(NamedKeys["shift"]);
                }

                if ((modifierBits & 2) != 0)
                {
                    modifiers.Add(NamedKeys["ctrl"]);
                }

                if ((modifierBits & 4) != 0)
                {
                    modifiers.Add(NamedKeys["alt"]);
                }

                return new VirtualKeyBinding((ushort)(mapped & 0xFF), modifiers);
            }
        }

        throw new InvalidOperationException($"Unsupported key: {value}");
    }
}
