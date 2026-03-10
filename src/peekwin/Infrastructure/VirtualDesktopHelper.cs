using System.Runtime.InteropServices;

namespace PeekWin.Infrastructure;

internal static class VirtualDesktopHelper
{
    private static readonly Guid ClsidVirtualDesktopManager = new("AA509086-5CA9-4C25-8F95-589D3C07B48A");

    internal static string GetDesktopLabel(nint hwnd)
    {
        try
        {
            var type = Type.GetTypeFromCLSID(ClsidVirtualDesktopManager);
            if (type is null)
            {
                return "unknown";
            }

            var manager = (IVirtualDesktopManager)Activator.CreateInstance(type)!;
            var onCurrentDesktop = manager.IsWindowOnCurrentVirtualDesktop(hwnd, out var isCurrent) == 0 && isCurrent;
            return onCurrentDesktop ? "current" : "other";
        }
        catch
        {
            return "unknown";
        }
    }

    [ComImport]
    [Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IVirtualDesktopManager
    {
        [PreserveSig]
        int IsWindowOnCurrentVirtualDesktop(nint topLevelWindow, [MarshalAs(UnmanagedType.Bool)] out bool onCurrentDesktop);

        [PreserveSig]
        int GetWindowDesktopId(nint topLevelWindow, out Guid desktopId);

        [PreserveSig]
        int MoveWindowToDesktop(nint topLevelWindow, [MarshalAs(UnmanagedType.LPStruct)] Guid desktopId);
    }

}
