using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PeekWin.Infrastructure;

[SupportedOSPlatform("windows")]
internal static class VirtualDesktopHelper
{
    private static readonly Guid ClsidVirtualDesktopManager = new("AA509086-5CA9-4C25-8F95-589D3C07B48A");

    internal static string GetDesktopLabel(nint hwnd)
    {
        object? manager = null;
        try
        {
            var type = Type.GetTypeFromCLSID(ClsidVirtualDesktopManager);
            if (type is null)
            {
                return "unknown";
            }

            manager = Activator.CreateInstance(type);
            if (manager is not IVirtualDesktopManager desktopManager)
            {
                return "unknown";
            }

            var result = desktopManager.IsWindowOnCurrentVirtualDesktop(hwnd, out var isCurrent);
            if (result != 0)
            {
                return "unknown";
            }

            return isCurrent ? "current" : "other";
        }
        catch
        {
            return "unknown";
        }
        finally
        {
            if (manager is not null && Marshal.IsComObject(manager))
            {
                Marshal.FinalReleaseComObject(manager);
            }
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
