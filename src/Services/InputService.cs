using System.Runtime.InteropServices;
using PeekWin.Infrastructure;
using PeekWin.Models;

namespace PeekWin.Services;

public sealed class InputService
{
    public async Task ClickAsync(int x, int y, MouseButton button, bool isDouble)
    {
        MoveMouse(x, y);
        var count = isDouble ? 2 : 1;
        for (var i = 0; i < count; i++)
        {
            MouseDown(button);
            MouseUp(button);
            if (isDouble && i == 0)
            {
                await Task.Delay(60).ConfigureAwait(false);
            }
        }
    }

    public void MoveMouse(int x, int y)
    {
        if (!NativeMethods.SetCursorPos(x, y))
        {
            throw new InvalidOperationException($"Failed to move cursor to {x},{y}.");
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

    public void PressKey(string key, int repeat)
    {
        var vk = VirtualKeyParser.Parse(key);
        for (var i = 0; i < repeat; i++)
        {
            KeyDown(vk);
            KeyUp(vk);
        }
    }

    public void Hotkey(IReadOnlyList<string> keys)
    {
        var parsed = keys.Select(VirtualKeyParser.Parse).ToArray();
        foreach (var key in parsed)
        {
            KeyDown(key);
        }

        for (var i = parsed.Length - 1; i >= 0; i--)
        {
            KeyUp(parsed[i]);
        }
    }

    public async Task HoldKeyAsync(string key, int durationMs)
    {
        var vk = VirtualKeyParser.Parse(key);
        KeyDown(vk);
        await Task.Delay(durationMs).ConfigureAwait(false);
        KeyUp(vk);
    }

    public async Task HoldMouseAsync(MouseButton button, int durationMs)
    {
        MouseDown(button);
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

        Send(new[]
        {
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_MOUSE,
                U = new NativeMethods.InputUnion
                {
                    mi = new NativeMethods.MOUSEINPUT { dwFlags = flag }
                }
            }
        });
    }

    private static void KeyDown(ushort vk) => SendKey(vk, false);

    private static void KeyUp(ushort vk) => SendKey(vk, true);

    private static void SendKey(ushort vk, bool keyUp)
    {
        var scanCode = NativeMethods.MapVirtualKey(vk, NativeMethods.MAPVK_VK_TO_VSC);
        Send(new[]
        {
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
        });
    }

    private static void Send(NativeMethods.INPUT[] inputs)
    {
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException("Windows rejected the injected input.");
        }
    }
}

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
        ["volumemute"] = 0xAD,
        ["mute"] = 0xAD,
        ["volumedown"] = 0xAE,
        ["volumeup"] = 0xAF,
        ["medianext"] = 0xB0,
        ["mediaprev"] = 0xB1,
        ["mediaprevious"] = 0xB1,
        ["mediastop"] = 0xB2,
        ["mediaplaypause"] = 0xB3,
        ["playpause"] = 0xB3,
        ["semicolon"] = 0xBA,
        ["plus"] = 0xBB,
        ["comma"] = 0xBC,
        ["minus"] = 0xBD,
        ["period"] = 0xBE,
        ["slash"] = 0xBF,
        ["backtick"] = 0xC0,
        ["lbracket"] = 0xDB,
        ["backslash"] = 0xDC,
        ["rbracket"] = 0xDD,
        ["quote"] = 0xDE
    };

    public static ushort Parse(string key)
    {
        if (NamedKeys.TryGetValue(key, out var named))
        {
            return named;
        }

        if (key.Length == 1)
        {
            char ch = char.ToUpperInvariant(key[0]);
            if (ch is >= 'A' and <= 'Z' || ch is >= '0' and <= '9')
            {
                return ch;
            }
        }

        throw new InvalidOperationException($"Unsupported key: {key}");
    }
}
