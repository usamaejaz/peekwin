using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using PeekWin.Models;

namespace PeekWin.Infrastructure;

[SupportedOSPlatform("windows")]
internal static class UiAutomationHelper
{
    private static readonly Guid CUIAutomationClsid = new("FF48DBA4-60EF-4201-AA87-54103EEF594E");

    public static IReadOnlyList<AutomationElementInfo> GetTopLevelChildren(nint hwnd)
    {
        object? automationObject = null;
        IUIAutomation? automation = null;
        IUIAutomationElement? root = null;
        IUIAutomationCondition? condition = null;
        IUIAutomationElementArray? children = null;

        try
        {
            var type = Type.GetTypeFromCLSID(CUIAutomationClsid);
            if (type is null)
            {
                return Array.Empty<AutomationElementInfo>();
            }

            automationObject = Activator.CreateInstance(type);
            automation = automationObject as IUIAutomation;
            if (automation is null)
            {
                return Array.Empty<AutomationElementInfo>();
            }

            if (automation.ElementFromHandle(hwnd, out root) != 0 || root is null)
            {
                return Array.Empty<AutomationElementInfo>();
            }

            if (automation.CreateTrueCondition(out condition) != 0 || condition is null)
            {
                return Array.Empty<AutomationElementInfo>();
            }

            if (root.FindAll(TreeScope.Children, condition, out children) != 0 || children is null)
            {
                return Array.Empty<AutomationElementInfo>();
            }

            if (children.get_Length(out var length) != 0 || length <= 0)
            {
                return Array.Empty<AutomationElementInfo>();
            }

            var elements = new List<AutomationElementInfo>(length);
            for (var i = 0; i < length; i++)
            {
                if (children.GetElement(i, out var child) != 0 || child is null)
                {
                    continue;
                }

                try
                {
                    child.get_CurrentName(out var name);
                    child.get_CurrentAutomationId(out var automationId);
                    child.get_CurrentControlType(out var controlType);
                    child.get_CurrentBoundingRectangle(out var bounds);

                    elements.Add(new AutomationElementInfo(
                        name ?? string.Empty,
                        automationId ?? string.Empty,
                        GetControlTypeName(controlType),
                        new RectDto(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top)));
                }
                finally
                {
                    ReleaseComObject(child);
                }
            }

            return elements;
        }
        catch
        {
            return Array.Empty<AutomationElementInfo>();
        }
        finally
        {
            ReleaseComObject(children);
            ReleaseComObject(condition);
            ReleaseComObject(root);
            ReleaseComObject(automation);
            ReleaseComObject(automationObject);
        }
    }

    private static string GetControlTypeName(int controlType)
        => controlType switch
        {
            50000 => "ControlType.Button",
            50001 => "ControlType.Calendar",
            50002 => "ControlType.CheckBox",
            50003 => "ControlType.ComboBox",
            50004 => "ControlType.Edit",
            50005 => "ControlType.Hyperlink",
            50006 => "ControlType.Image",
            50007 => "ControlType.ListItem",
            50008 => "ControlType.List",
            50009 => "ControlType.Menu",
            50010 => "ControlType.MenuBar",
            50011 => "ControlType.MenuItem",
            50012 => "ControlType.ProgressBar",
            50013 => "ControlType.RadioButton",
            50014 => "ControlType.ScrollBar",
            50015 => "ControlType.Slider",
            50016 => "ControlType.Spinner",
            50017 => "ControlType.StatusBar",
            50018 => "ControlType.Tab",
            50019 => "ControlType.TabItem",
            50020 => "ControlType.Text",
            50021 => "ControlType.ToolBar",
            50022 => "ControlType.ToolTip",
            50023 => "ControlType.Tree",
            50024 => "ControlType.TreeItem",
            50025 => "ControlType.Custom",
            50026 => "ControlType.Group",
            50027 => "ControlType.Thumb",
            50028 => "ControlType.DataGrid",
            50029 => "ControlType.DataItem",
            50030 => "ControlType.Document",
            50031 => "ControlType.SplitButton",
            50032 => "ControlType.Window",
            50033 => "ControlType.Pane",
            50034 => "ControlType.Header",
            50035 => "ControlType.HeaderItem",
            50036 => "ControlType.Table",
            50037 => "ControlType.TitleBar",
            50038 => "ControlType.Separator",
            50039 => "ControlType.SemanticZoom",
            50040 => "ControlType.AppBar",
            _ => $"ControlType.{controlType}"
        };

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }

    private enum TreeScope
    {
        Children = 0x2
    }

    [ComImport]
    [Guid("30CBE57D-D9D0-452A-AB13-7AC5AC4825EE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomation
    {
        [PreserveSig]
        int CompareElements(nint el1, nint el2, out int areSame);

        [PreserveSig]
        int CompareRuntimeIds(nint runtimeId1, nint runtimeId2, out int areSame);

        [PreserveSig]
        int GetRootElement(out IUIAutomationElement root);

        [PreserveSig]
        int ElementFromHandle(nint hwnd, out IUIAutomationElement element);

        [PreserveSig]
        int ElementFromPoint(POINT point, out IUIAutomationElement element);

        [PreserveSig]
        int GetFocusedElement(out IUIAutomationElement element);

        [PreserveSig]
        int GetRootElementBuildCache(nint cacheRequest, out IUIAutomationElement root);

        [PreserveSig]
        int ElementFromHandleBuildCache(nint hwnd, nint cacheRequest, out IUIAutomationElement element);

        [PreserveSig]
        int ElementFromPointBuildCache(POINT point, nint cacheRequest, out IUIAutomationElement element);

        [PreserveSig]
        int GetFocusedElementBuildCache(nint cacheRequest, out IUIAutomationElement element);

        [PreserveSig]
        int CreateTreeWalker(nint condition, out nint walker);

        [PreserveSig]
        int get_ControlViewWalker(out nint walker);

        [PreserveSig]
        int get_ContentViewWalker(out nint walker);

        [PreserveSig]
        int get_RawViewWalker(out nint walker);

        [PreserveSig]
        int get_RawViewCondition(out nint condition);

        [PreserveSig]
        int get_ControlViewCondition(out nint condition);

        [PreserveSig]
        int get_ContentViewCondition(out nint condition);

        [PreserveSig]
        int CreateCacheRequest(out nint cacheRequest);

        [PreserveSig]
        int CreateTrueCondition(out IUIAutomationCondition condition);
    }

    [ComImport]
    [Guid("352FFBA8-0973-437C-A61F-F64CAFD81DF9")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationCondition
    {
    }

    [ComImport]
    [Guid("14314595-B4BC-4055-95F2-58F2E42C9855")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationElementArray
    {
        [PreserveSig]
        int get_Length(out int length);

        [PreserveSig]
        int GetElement(int index, out IUIAutomationElement element);
    }

    [ComImport]
    [Guid("D22108AA-8AC5-49A5-837B-37BBB3D7591E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationElement
    {
        [PreserveSig]
        int SetFocus();

        [PreserveSig]
        int GetRuntimeId(out nint runtimeId);

        [PreserveSig]
        int FindFirst(TreeScope scope, IUIAutomationCondition condition, out IUIAutomationElement found);

        [PreserveSig]
        int FindAll(TreeScope scope, IUIAutomationCondition condition, out IUIAutomationElementArray found);

        [PreserveSig]
        int FindFirstBuildCache(TreeScope scope, IUIAutomationCondition condition, nint cacheRequest, out IUIAutomationElement found);

        [PreserveSig]
        int FindAllBuildCache(TreeScope scope, IUIAutomationCondition condition, nint cacheRequest, out IUIAutomationElementArray found);

        [PreserveSig]
        int BuildUpdatedCache(nint cacheRequest, out IUIAutomationElement updatedElement);

        [PreserveSig]
        int GetCurrentPropertyValue(int propertyId, [MarshalAs(UnmanagedType.Struct)] out object value);

        [PreserveSig]
        int GetCurrentPropertyValueEx(int propertyId, int ignoreDefaultValue, [MarshalAs(UnmanagedType.Struct)] out object value);

        [PreserveSig]
        int GetCachedPropertyValue(int propertyId, [MarshalAs(UnmanagedType.Struct)] out object value);

        [PreserveSig]
        int GetCachedPropertyValueEx(int propertyId, int ignoreDefaultValue, [MarshalAs(UnmanagedType.Struct)] out object value);

        [PreserveSig]
        int GetCurrentPatternAs(int patternId, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object patternObject);

        [PreserveSig]
        int GetCachedPatternAs(int patternId, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object patternObject);

        [PreserveSig]
        int GetCurrentPattern(int patternId, [MarshalAs(UnmanagedType.IUnknown)] out object patternObject);

        [PreserveSig]
        int GetCachedPattern(int patternId, [MarshalAs(UnmanagedType.IUnknown)] out object patternObject);

        [PreserveSig]
        int GetCachedParent(out IUIAutomationElement parent);

        [PreserveSig]
        int GetCachedChildren(out IUIAutomationElementArray children);

        [PreserveSig]
        int get_CurrentProcessId(out int processId);

        [PreserveSig]
        int get_CurrentControlType(out int controlType);

        [PreserveSig]
        int get_CurrentLocalizedControlType([MarshalAs(UnmanagedType.BStr)] out string controlType);

        [PreserveSig]
        int get_CurrentName([MarshalAs(UnmanagedType.BStr)] out string name);

        [PreserveSig]
        int get_CurrentAcceleratorKey([MarshalAs(UnmanagedType.BStr)] out string acceleratorKey);

        [PreserveSig]
        int get_CurrentAccessKey([MarshalAs(UnmanagedType.BStr)] out string accessKey);

        [PreserveSig]
        int get_CurrentHasKeyboardFocus(out int hasKeyboardFocus);

        [PreserveSig]
        int get_CurrentIsKeyboardFocusable(out int isKeyboardFocusable);

        [PreserveSig]
        int get_CurrentIsEnabled(out int isEnabled);

        [PreserveSig]
        int get_CurrentAutomationId([MarshalAs(UnmanagedType.BStr)] out string automationId);

        [PreserveSig]
        int get_CurrentClassName([MarshalAs(UnmanagedType.BStr)] out string className);

        [PreserveSig]
        int get_CurrentHelpText([MarshalAs(UnmanagedType.BStr)] out string helpText);

        [PreserveSig]
        int get_CurrentCulture(out int culture);

        [PreserveSig]
        int get_CurrentIsControlElement(out int isControlElement);

        [PreserveSig]
        int get_CurrentIsContentElement(out int isContentElement);

        [PreserveSig]
        int get_CurrentIsPassword(out int isPassword);

        [PreserveSig]
        int get_CurrentNativeWindowHandle(out nint hwnd);

        [PreserveSig]
        int get_CurrentItemType([MarshalAs(UnmanagedType.BStr)] out string itemType);

        [PreserveSig]
        int get_CurrentIsOffscreen(out int isOffscreen);

        [PreserveSig]
        int get_CurrentOrientation(out int orientation);

        [PreserveSig]
        int get_CurrentFrameworkId([MarshalAs(UnmanagedType.BStr)] out string frameworkId);

        [PreserveSig]
        int get_CurrentIsRequiredForForm(out int isRequiredForForm);

        [PreserveSig]
        int get_CurrentItemStatus([MarshalAs(UnmanagedType.BStr)] out string itemStatus);

        [PreserveSig]
        int get_CurrentBoundingRectangle(out NativeMethods.RECT boundingRectangle);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
}
