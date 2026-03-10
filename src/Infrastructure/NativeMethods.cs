using System.Runtime.InteropServices;
using System.Text;

namespace PeekWin.Infrastructure;

internal static class NativeMethods
{
    internal const int SW_RESTORE = 9;
    internal const uint INPUT_MOUSE = 0;
    internal const uint INPUT_KEYBOARD = 1;
    internal const uint KEYEVENTF_KEYUP = 0x0002;
    internal const uint KEYEVENTF_UNICODE = 0x0004;
    internal const uint MOUSEEVENTF_MOVE = 0x0001;
    internal const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    internal const uint MOUSEEVENTF_LEFTUP = 0x0004;
    internal const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    internal const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    internal const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    internal const int SRCCOPY = 0x00CC0020;
    internal const uint MAPVK_VK_TO_VSC = 0;
    internal const int SM_XVIRTUALSCREEN = 76;
    internal const int SM_YVIRTUALSCREEN = 77;
    internal const int SM_CXVIRTUALSCREEN = 78;
    internal const int SM_CYVIRTUALSCREEN = 79;

    [DllImport("user32.dll")]
    internal static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowTextW(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    internal static extern int GetWindowTextLengthW(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassNameW(nint hWnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    internal static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    internal static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    internal static extern bool IsIconic(nint hWnd);

    [DllImport("user32.dll")]
    internal static extern bool IsZoomed(nint hWnd);

    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    internal static extern bool BringWindowToTop(nint hWnd);

    [DllImport("user32.dll")]
    internal static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    internal static extern bool GetWindowRect(nint hWnd, out RECT rect);

    [DllImport("user32.dll")]
    internal static extern nint GetDesktopWindow();

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll")]
    internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    internal static extern short VkKeyScan(char ch);

    [DllImport("user32.dll")]
    internal static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    internal static extern bool EnumDisplayMonitors(nint hdc, nint lprcClip, MonitorEnumProc callback, nint dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFOEX monitorInfo);

    [DllImport("user32.dll")]
    internal static extern nint GetDC(nint hWnd);

    [DllImport("user32.dll")]
    internal static extern int ReleaseDC(nint hWnd, nint hDc);

    [DllImport("gdi32.dll")]
    internal static extern nint CreateCompatibleDC(nint hdc);

    [DllImport("gdi32.dll")]
    internal static extern nint CreateCompatibleBitmap(nint hdc, int cx, int cy);

    [DllImport("gdi32.dll")]
    internal static extern nint SelectObject(nint hdc, nint h);

    [DllImport("gdi32.dll")]
    internal static extern bool BitBlt(nint hdcDest, int xDest, int yDest, int width, int height, nint hdcSrc, int xSrc, int ySrc, int rop);

    [DllImport("gdi32.dll")]
    internal static extern bool DeleteDC(nint hdc);

    [DllImport("gdi32.dll")]
    internal static extern bool DeleteObject(nint ho);

    internal delegate bool EnumWindowsProc(nint hWnd, nint lParam);
    internal delegate bool MonitorEnumProc(nint hMonitor, nint hdcMonitor, ref RECT monitorRect, nint dwData);

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }
}
