using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using PeekWin.Models;

namespace PeekWin.Infrastructure;

[SupportedOSPlatform("windows")]
internal static class UiAutomationHelper
{
    private static readonly Guid CUIAutomationClsid = new("FF48DBA4-60EF-4201-AA87-54103EEF594E");

    public static IReadOnlyList<AutomationElementInfo> GetTopLevelChildren(nint hwnd)
        => GetTree(hwnd, maxDepth: 1).Nodes
            .Where(node => node.Depth == 1)
            .Select(node => new AutomationElementInfo(node.Name, node.AutomationId, node.ControlType, node.Bounds))
            .ToList();

    public static AutomationTreeResult GetTree(nint hwnd, int maxDepth)
    {
        try
        {
            using var session = UiAutomationSession.Create(hwnd);
            var nodes = new List<AutomationTreeNode>();
            var nextRef = 1;
            Traverse(session.Root, session.Condition, maxDepth, depth: 0, parentRef: null, path: "0", nodes, ref nextRef);
            return new AutomationTreeResult(true, nodes);
        }
        catch (UiAutomationException ex)
        {
            return new AutomationTreeResult(false, Array.Empty<AutomationTreeNode>(), ex.Message);
        }
        catch (COMException ex)
        {
            return new AutomationTreeResult(false, Array.Empty<AutomationTreeNode>(), FormatComFailure("UI Automation traversal failed", ex));
        }
        catch (Exception ex)
        {
            return new AutomationTreeResult(false, Array.Empty<AutomationTreeNode>(), $"UI Automation traversal failed ({ex.GetType().Name}: {ex.Message}).");
        }
    }

    public static bool TryFocusElementByPath(nint hwnd, string path)
        => TryFocusElementByPath(hwnd, path, out _);

    public static bool TryFocusElementByPath(nint hwnd, string path, out string? error)
        => TryWithElementByPath(
            hwnd,
            path,
            static element =>
            {
                if (element.SetFocus() != 0)
                {
                    throw new UiAutomationException("UI Automation could not focus the target element.");
                }

                return true;
            },
            out _,
            out error);

    public static bool TryGetBoundsByPath(nint hwnd, string path, out RectDto bounds)
        => TryGetBoundsByPath(hwnd, path, out bounds, out _);

    public static bool TryGetBoundsByPath(nint hwnd, string path, out RectDto bounds, out string? error)
    {
        if (TryWithElementByPath(
            hwnd,
            path,
            static element =>
            {
                if (element.get_CurrentBoundingRectangle(out var rect) != 0)
                {
                    throw new UiAutomationException("UI Automation could not read the target element bounds.");
                }

                return new RectDto(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            },
            out bounds,
            out error))
        {
            return true;
        }

        bounds = default!;
        return false;
    }

    public static bool TryGetElementStateByPath(nint hwnd, string path, out AutomationElementState state)
        => TryGetElementStateByPath(hwnd, path, out state, out _);

    public static bool TryGetElementStateByPath(nint hwnd, string path, out AutomationElementState state, out string? error)
    {
        if (TryWithElementByPath(
            hwnd,
            path,
            static element => CreateElementState(element),
            out state,
            out error))
        {
            return true;
        }

        state = default!;
        return false;
    }

    public static bool TryGetNodeByPath(nint hwnd, string path, out AutomationTreeNode node)
        => TryGetNodeByPath(hwnd, path, out node, out _);

    public static bool TryGetNodeByPath(nint hwnd, string path, out AutomationTreeNode node, out string? error)
    {
        if (TryWithElementByPath(
            hwnd,
            path,
            element =>
            {
                var segments = ParsePath(path);
                return CreateNode(
                    element,
                    currentRef: string.Empty,
                    parentRef: null,
                    path,
                    depth: Math.Max(segments.Length - 1, 0));
            },
            out node,
            out error))
        {
            return true;
        }

        node = default!;
        return false;
    }

    private static void Traverse(
        IUIAutomationElement element,
        IUIAutomationCondition condition,
        int maxDepth,
        int depth,
        string? parentRef,
        string path,
        List<AutomationTreeNode> nodes,
        ref int nextRef)
    {
        var currentRef = $"e{nextRef++}";
        nodes.Add(CreateNode(element, currentRef, parentRef, path, depth));
        if (depth >= maxDepth)
        {
            return;
        }

        if (element.FindAll(TreeScope.Children, condition, out var children) != 0 || children is null)
        {
            return;
        }

        try
        {
            if (children.get_Length(out var length) != 0 || length <= 0)
            {
                return;
            }

            for (var index = 0; index < length; index++)
            {
                if (children.GetElement(index, out var child) != 0 || child is null)
                {
                    continue;
                }

                try
                {
                    Traverse(child, condition, maxDepth, depth + 1, currentRef, $"{path}.{index}", nodes, ref nextRef);
                }
                finally
                {
                    ReleaseComObject(child);
                }
            }
        }
        finally
        {
            ReleaseComObject(children);
        }
    }

    private static AutomationTreeNode CreateNode(IUIAutomationElement element, string currentRef, string? parentRef, string path, int depth)
    {
        var state = CreateElementState(element);
        return new AutomationTreeNode(
            currentRef,
            parentRef,
            path,
            depth,
            state.Name,
            state.AutomationId,
            state.ControlType,
            NormalizeRole(state.ControlType),
            state.Bounds,
            state.IsKeyboardFocusable,
            state.IsEnabled,
            state.IsOffscreen);
    }

    private static AutomationElementState CreateElementState(IUIAutomationElement element)
    {
        var name = ReadBstr(element.get_CurrentName);
        var automationId = ReadBstr(element.get_CurrentAutomationId);
        var controlType = ReadInt(element.get_CurrentControlType);
        var bounds = ReadRect(element.get_CurrentBoundingRectangle);
        var isKeyboardFocusable = ReadBool(element.get_CurrentIsKeyboardFocusable);
        var isEnabled = ReadBool(element.get_CurrentIsEnabled);
        var isOffscreen = ReadBool(element.get_CurrentIsOffscreen);
        var hasKeyboardFocus = ReadBool(element.get_CurrentHasKeyboardFocus);

        return new AutomationElementState(
            name,
            automationId,
            GetControlTypeName(controlType),
            new RectDto(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top),
            isKeyboardFocusable,
            isEnabled,
            isOffscreen,
            hasKeyboardFocus);
    }

    private static string NormalizeRole(string controlTypeName)
        => controlTypeName.StartsWith("ControlType.", StringComparison.OrdinalIgnoreCase)
            ? controlTypeName["ControlType.".Length..].ToLowerInvariant()
            : controlTypeName.ToLowerInvariant();

    private static bool TryWithElementByPath<T>(
        nint hwnd,
        string path,
        Func<IUIAutomationElement, T> selector,
        out T result,
        out string? error)
    {
        result = default!;
        error = null;

        try
        {
            using var session = UiAutomationSession.Create(hwnd);
            var segments = ParsePath(path);
            var current = session.Root;
            var ownsCurrent = false;

            try
            {
                for (var segmentIndex = 1; segmentIndex < segments.Length; segmentIndex++)
                {
                    var childIndex = segments[segmentIndex];
                    if (current.FindAll(TreeScope.Children, session.Condition, out var children) != 0 || children is null)
                    {
                        throw new UiAutomationException(
                            $"UI Automation could not enumerate children for path segment {segmentIndex} of `{path}`.");
                    }

                    try
                    {
                        if (children.get_Length(out var length) != 0)
                        {
                            throw new UiAutomationException(
                                $"UI Automation could not read child count for path segment {segmentIndex} of `{path}`.");
                        }

                        if (childIndex < 0 || childIndex >= length)
                        {
                            throw new UiAutomationException(
                                $"Saved UI path `{path}` is out of range at segment {segmentIndex} (requested child {childIndex}, found {length}).");
                        }

                        if (children.GetElement(childIndex, out var child) != 0 || child is null)
                        {
                            throw new UiAutomationException(
                                $"UI Automation could not resolve child {childIndex} for path segment {segmentIndex} of `{path}`.");
                        }

                        if (ownsCurrent)
                        {
                            ReleaseComObject(current);
                        }

                        current = child;
                        ownsCurrent = true;
                    }
                    finally
                    {
                        ReleaseComObject(children);
                    }
                }

                result = selector(current);
                return true;
            }
            finally
            {
                if (ownsCurrent)
                {
                    ReleaseComObject(current);
                }
            }
        }
        catch (UiAutomationException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (COMException ex)
        {
            error = FormatComFailure("UI Automation path lookup failed", ex);
            return false;
        }
        catch (Exception ex)
        {
            error = $"UI Automation path lookup failed ({ex.GetType().Name}: {ex.Message}).";
            return false;
        }
    }

    private static int[] ParsePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new UiAutomationException("Saved UI path is missing.");
        }

        var segments = path
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select((segment, index) =>
            {
                if (!int.TryParse(segment, out var parsed) || parsed < 0)
                {
                    throw new UiAutomationException($"Saved UI path `{path}` contains an invalid segment at index {index}.");
                }

                return parsed;
            })
            .ToArray();

        if (segments.Length == 0 || segments[0] != 0)
        {
            throw new UiAutomationException($"Saved UI path `{path}` is invalid.");
        }

        return segments;
    }

    private static string ReadBstr(StringPropertyGetter getter)
    {
        return getter(out var value) == 0 && value is not null
            ? value
            : string.Empty;
    }

    private static int ReadInt(IntPropertyGetter getter)
    {
        return getter(out var value) == 0 ? value : 0;
    }

    private static bool ReadBool(IntPropertyGetter getter)
    {
        return getter(out var value) == 0 && value != 0;
    }

    private static NativeMethods.RECT ReadRect(RectPropertyGetter getter)
    {
        return getter(out var value) == 0 ? value : default;
    }

    private static string FormatComFailure(string prefix, COMException ex)
        => $"{prefix} (HRESULT 0x{unchecked((uint)ex.HResult):X8}: {ex.Message}).";

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

    private static bool TryCreateControlViewCondition(IUIAutomation automation, out IUIAutomationCondition? condition)
    {
        condition = null;
        if (automation.get_ControlViewCondition(out var conditionHandle) != 0 || conditionHandle == 0)
        {
            return false;
        }

        try
        {
            condition = (IUIAutomationCondition)Marshal.GetObjectForIUnknown(conditionHandle);
            return true;
        }
        finally
        {
            Marshal.Release(conditionHandle);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            try
            {
                Marshal.FinalReleaseComObject(value);
            }
            catch
            {
            }
        }
    }

    private sealed class UiAutomationSession : IDisposable
    {
        private object? _automationObject;
        private bool _disposed;

        private UiAutomationSession(object automationObject, IUIAutomation automation, IUIAutomationElement root, IUIAutomationCondition condition)
        {
            _automationObject = automationObject;
            Automation = automation;
            Root = root;
            Condition = condition;
        }

        public IUIAutomation Automation { get; }

        public IUIAutomationElement Root { get; }

        public IUIAutomationCondition Condition { get; }

        public static UiAutomationSession Create(nint hwnd)
        {
            if (hwnd == 0)
            {
                throw new UiAutomationException("UI Automation requires a non-zero window handle.");
            }

            var type = Type.GetTypeFromCLSID(CUIAutomationClsid);
            if (type is null)
            {
                throw new UiAutomationException("UI Automation is unavailable on this system.");
            }

            var automationObject = Activator.CreateInstance(type)
                ?? throw new UiAutomationException("Failed to initialize UI Automation.");

            try
            {
                if (automationObject is not IUIAutomation automation)
                {
                    throw new UiAutomationException("Failed to initialize UI Automation.");
                }

                if (automation.ElementFromHandle(hwnd, out var root) != 0 || root is null)
                {
                    throw new UiAutomationException($"Could not create a UI Automation root for window 0x{hwnd.ToInt64():X}.");
                }

                try
                {
                    if (!TryCreateControlViewCondition(automation, out var condition))
                    {
                        throw new UiAutomationException("Failed to create the UI Automation traversal condition.");
                    }

                    return new UiAutomationSession(automationObject, automation, root, condition);
                }
                catch
                {
                    ReleaseComObject(root);
                    throw;
                }
            }
            catch
            {
                ReleaseComObject(automationObject);
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ReleaseComObject(Condition);
            ReleaseComObject(Root);
            ReleaseComObject(Automation);
            ReleaseComObject(_automationObject);
            _automationObject = null;
        }
    }

    private sealed class UiAutomationException(string message) : Exception(message);

    private delegate int StringPropertyGetter([MarshalAs(UnmanagedType.BStr)] out string value);

    private delegate int IntPropertyGetter(out int value);

    private delegate int RectPropertyGetter(out NativeMethods.RECT value);

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
