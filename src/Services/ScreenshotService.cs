using System.Buffers.Binary;
using System.Runtime.InteropServices;
using PeekWin.Infrastructure;
using PeekWin.Models;

namespace PeekWin.Services;

public sealed class ScreenshotService
{
    private const int MaxCaptureDimension = 16384;
    private const long MaxCapturePixels = 268_435_456;

    public ScreenLayoutInfo GetScreenLayout()
    {
        var screens = EnumerateScreens()
            .OrderBy(screen => screen.Bounds.Top)
            .ThenBy(screen => screen.Bounds.Left)
            .Select((screen, index) => screen with { Index = index })
            .ToList();

        return new ScreenLayoutInfo(
            GetVirtualScreenBounds(),
            screens);
    }

    public CommandResult Capture(string outputPath, RectDto bounds, object? details = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory);
        CaptureArea(outputPath, bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        return CommandResult.Ok($"Saved image to {outputPath}.", outputPath, details);
    }

    private static void CaptureArea(string outputPath, int left, int top, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException($"Cannot capture an empty area ({width}x{height}).");
        }

        if (width > MaxCaptureDimension || height > MaxCaptureDimension)
        {
            throw new InvalidOperationException($"Cannot capture an area larger than {MaxCaptureDimension} pixels on either side ({width}x{height}).");
        }

        if ((long)width * height > MaxCapturePixels)
        {
            throw new InvalidOperationException($"Cannot capture an area larger than {MaxCapturePixels:N0} pixels ({width}x{height}).");
        }

        var screenDc = NativeMethods.GetDC(nint.Zero);
        if (screenDc == nint.Zero)
        {
            throw new InvalidOperationException("Failed to access the desktop device context.");
        }

        var memoryDc = nint.Zero;
        var bitmap = nint.Zero;
        var previous = nint.Zero;
        try
        {
            memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
            if (memoryDc == nint.Zero)
            {
                throw new InvalidOperationException("Failed to create a compatible device context.");
            }

            bitmap = NativeMethods.CreateCompatibleBitmap(screenDc, width, height);
            if (bitmap == nint.Zero)
            {
                throw new InvalidOperationException("Failed to create a capture bitmap.");
            }

            previous = NativeMethods.SelectObject(memoryDc, bitmap);
            if (previous == nint.Zero)
            {
                throw new InvalidOperationException("Failed to prepare the capture bitmap.");
            }

            if (!NativeMethods.BitBlt(memoryDc, 0, 0, width, height, screenDc, left, top, NativeMethods.SRCCOPY))
            {
                throw new InvalidOperationException("Failed to copy screen pixels.");
            }

            var pixels = new byte[checked(width * height * 4)];
            var bitmapInfo = new NativeMethods.BITMAPINFO
            {
                bmiHeader = new NativeMethods.BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height,
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = NativeMethods.BI_RGB,
                    biSizeImage = (uint)pixels.Length
                }
            };

            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                var scanLines = NativeMethods.GetDIBits(
                    memoryDc,
                    bitmap,
                    0,
                    (uint)height,
                    handle.AddrOfPinnedObject(),
                    ref bitmapInfo,
                    NativeMethods.DIB_RGB_COLORS);

                if (scanLines == 0)
                {
                    throw new InvalidOperationException("Failed to read captured pixels.");
                }
            }
            finally
            {
                handle.Free();
            }

            WritePng(outputPath, width, height, pixels);
        }
        finally
        {
            if (previous != nint.Zero && memoryDc != nint.Zero)
            {
                NativeMethods.SelectObject(memoryDc, previous);
            }

            if (bitmap != nint.Zero)
            {
                NativeMethods.DeleteObject(bitmap);
            }

            if (memoryDc != nint.Zero)
            {
                NativeMethods.DeleteDC(memoryDc);
            }

            NativeMethods.ReleaseDC(nint.Zero, screenDc);
        }
    }

    private static void WritePng(string outputPath, int width, int height, byte[] bgraPixels)
    {
        using var output = File.Create(outputPath);

        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        WriteChunk(output, "IHDR", BuildHeader(width, height));
        WriteChunk(output, "IDAT", BuildZlibStream(BuildImageData(width, height, bgraPixels)));
        WriteChunk(output, "IEND", []);
    }

    private static byte[] BuildHeader(int width, int height)
    {
        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), (uint)height);
        header[8] = 8;
        header[9] = 6;
        header[10] = 0;
        header[11] = 0;
        header[12] = 0;
        return header;
    }

    private static byte[] BuildImageData(int width, int height, byte[] bgraPixels)
    {
        var stride = width * 4;
        var data = new byte[height * (stride + 1)];
        for (var y = 0; y < height; y++)
        {
            var outputOffset = y * (stride + 1);
            data[outputOffset] = 0;

            var inputOffset = y * stride;
            for (var x = 0; x < width; x++)
            {
                var src = inputOffset + (x * 4);
                var dest = outputOffset + 1 + (x * 4);
                data[dest] = bgraPixels[src + 2];
                data[dest + 1] = bgraPixels[src + 1];
                data[dest + 2] = bgraPixels[src];
                data[dest + 3] = bgraPixels[src + 3];
            }
        }

        return data;
    }

    private static byte[] BuildZlibStream(byte[] rawData)
    {
        using var stream = new MemoryStream();
        stream.WriteByte(0x78);
        stream.WriteByte(0x01);

        var offset = 0;
        while (offset < rawData.Length)
        {
            var blockLength = Math.Min(65_535, rawData.Length - offset);
            var isFinal = offset + blockLength >= rawData.Length;

            stream.WriteByte(isFinal ? (byte)0x01 : (byte)0x00);
            WriteLittleEndianUInt16(stream, (ushort)blockLength);
            WriteLittleEndianUInt16(stream, unchecked((ushort)~blockLength));
            stream.Write(rawData, offset, blockLength);
            offset += blockLength;
        }

        var checksum = Adler32(rawData);
        Span<byte> checksumBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(checksumBytes, checksum);
        stream.Write(checksumBytes);
        return stream.ToArray();
    }

    private static void WriteLittleEndianUInt16(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static uint Adler32(byte[] data)
    {
        const uint modulo = 65_521;
        uint a = 1;
        uint b = 0;

        foreach (var value in data)
        {
            a = (a + value) % modulo;
            b = (b + a) % modulo;
        }

        return (b << 16) | a;
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        Span<byte> lengthBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lengthBytes, (uint)data.Length);
        output.Write(lengthBytes);

        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);

        var checksumInput = new byte[typeBytes.Length + data.Length];
        Buffer.BlockCopy(typeBytes, 0, checksumInput, 0, typeBytes.Length);
        Buffer.BlockCopy(data, 0, checksumInput, typeBytes.Length, data.Length);

        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, Crc32(checksumInput));
        output.Write(crcBytes);
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFF_FFFF;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                var mask = (crc & 1) != 0 ? 0xEDB8_8320u : 0u;
                crc = (crc >> 1) ^ mask;
            }
        }

        return ~crc;
    }

    private static RectDto GetVirtualScreenBounds()
        => new(
            NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN),
            NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN),
            NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN),
            NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN));

    private static IReadOnlyList<ScreenInfo> EnumerateScreens()
    {
        var screens = new List<ScreenInfo>();
        var handle = GCHandle.Alloc(screens);
        try
        {
            NativeMethods.EnumDisplayMonitors(nint.Zero, nint.Zero, static (nint monitor, nint hdcMonitor, ref NativeMethods.RECT monitorRect, nint state) =>
            {
                var listHandle = GCHandle.FromIntPtr(state);
                var list = (List<ScreenInfo>)listHandle.Target!;

                var info = new NativeMethods.MONITORINFOEX();
                info.cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>();
                if (NativeMethods.GetMonitorInfo(monitor, ref info))
                {
                    list.Add(new ScreenInfo(
                        -1,
                        info.szDevice,
                        (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0,
                        new RectDto(
                            info.rcMonitor.Left,
                            info.rcMonitor.Top,
                            info.rcMonitor.Right - info.rcMonitor.Left,
                            info.rcMonitor.Bottom - info.rcMonitor.Top),
                        new RectDto(
                            info.rcWork.Left,
                            info.rcWork.Top,
                            info.rcWork.Right - info.rcWork.Left,
                            info.rcWork.Bottom - info.rcWork.Top)));
                }

                return true;
            }, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            if (handle.IsAllocated)
            {
                handle.Free();
            }
        }

        return screens;
    }
}
