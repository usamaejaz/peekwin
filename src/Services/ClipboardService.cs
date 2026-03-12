using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using PeekWin.Infrastructure;
using PeekWin.Models;

namespace PeekWin.Services;

[SupportedOSPlatform("windows")]
public sealed class ClipboardService
{
    public string GetText()
        => WithClipboardRetry(ReadClipboardText);

    public CommandResult SetText(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        WithClipboardRetry(() =>
        {
            SetClipboardText(text);
            return 0;
        });

        return CommandResult.Ok(
            $"Set clipboard text ({text.Length} chars).",
            details: new { text, length = text.Length });
    }

    public void WithTemporaryText(string text, int restoreDelayMs, Action action)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

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

                action();
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
    }

    private static string ReadClipboardText()
    {
        if (!NativeMethods.OpenClipboard(nint.Zero))
        {
            throw new ExternalException("Failed to open clipboard.");
        }

        nint data = nint.Zero;
        nint locked = nint.Zero;
        try
        {
            if (!NativeMethods.IsClipboardFormatAvailable(NativeMethods.CF_UNICODETEXT))
            {
                throw new InvalidOperationException("Clipboard does not contain text.");
            }

            data = NativeMethods.GetClipboardData(NativeMethods.CF_UNICODETEXT);
            if (data == nint.Zero)
            {
                throw new InvalidOperationException("Clipboard text is unavailable.");
            }

            locked = NativeMethods.GlobalLock(data);
            if (locked == nint.Zero)
            {
                throw new InvalidOperationException("Failed to lock clipboard text.");
            }

            return Marshal.PtrToStringUni(locked) ?? string.Empty;
        }
        finally
        {
            if (locked != nint.Zero)
            {
                NativeMethods.GlobalUnlock(data);
            }

            NativeMethods.CloseClipboard();
        }
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
