using System;
using System.Collections.Generic;
namespace Exuarch.Core
{
    // A 640 x 480 picture the Run view can show: the framebuffer's colours, or a depth buffer drawn in grey.
    public interface IScreen : IBusDevice
    {
        int X { get; }
        int Y { get; }
        // What the view says about the picture, such as "RGB565".
        string Kind { get; }
        // True when the words are depths, to be drawn in grey, rather than RGB565 colours.
        bool IsDepth { get; }
        (int X, int Y, int Width, int Height)? TakeDirtyRegion();
        // The 16 bit words of a region, two little endian bytes each, row by row: the browser turns them into pixels.
        byte[] WordBytes(int x, int y, int width, int height);
    }

    // A 640 x 480 colour display. Each pixel is a 16 bit RGB565 word: 5 bits red, 6 bits green, 5 bits blue.
    // loadx and loady set the cursor from the bus; plot writes the bus value at the cursor and moves one pixel
    // right, wrapping to the start of the next row (and from the last row to the first); skip moves the same way
    // without writing; clear blanks the screen and homes the cursor.
    public class Framebuffer : IScreen, IWriteTracked
    {
        public const int Width = 640;
        public const int Height = 480;

        // Row by row, top left first.
        public readonly ushort[] Pixels = new ushort[Width * Height];
        public int X { get; private set; }
        public int Y { get; private set; }
        public long WriteCount { get; private set; }
        public int LastWriteAddress { get; private set; } = -1;
        private readonly Bus bus;
        private readonly string deviceID;
        private readonly string deviceName;
        private bool loadX, loadY, plot, skip, clear;
        public string Kind { get { return "RGB565"; } }
        public bool IsDepth { get { return false; } }
        public byte[] WordBytes(int x, int y, int width, int height) { return ToRgb565Bytes(x, y, width, height); }
        private int dirtyLeft = Width, dirtyTop = Height, dirtyRight = -1, dirtyBottom = -1;

        public Framebuffer(string DeviceName, string DeviceID, Bus bus)
        {
            deviceName = DeviceName;
            deviceID = DeviceID;
            this.bus = bus;
        }

        public int ValueAt(int address) { return Pixels[address]; }

        public void Drive()
        {
        }
        public void Latch()
        {
            if (clear)
            {
                Array.Clear(Pixels);
                X = Y = 0;
                MarkDirty(0, 0, Width - 1, Height - 1);
                clear = false;
            }
            if (loadX)
            {
                X = bus.Data % Width;
                loadX = false;
            }
            if (loadY)
            {
                Y = bus.Data % Height;
                loadY = false;
            }
            if (plot)
            {
                int address = Y * Width + X;
                Pixels[address] = (ushort)bus.Data;
                LastWriteAddress = address;
                WriteCount++;
                MarkDirty(X, Y, X, Y);
                Advance();
                plot = false;
            }
            if (skip)
            {
                Advance();
                skip = false;
            }
        }

        private void Advance()
        {
            if (++X == Width)
            {
                X = 0;
                Y = (Y + 1) % Height;
            }
        }

        private void MarkDirty(int left, int top, int right, int bottom)
        {
            dirtyLeft = Math.Min(dirtyLeft, left);
            dirtyTop = Math.Min(dirtyTop, top);
            dirtyRight = Math.Max(dirtyRight, right);
            dirtyBottom = Math.Max(dirtyBottom, bottom);
        }

        // The rectangle changed since the last call (x, y, width, height), or null when nothing changed. For a
        // view that redraws only what changed.
        public (int X, int Y, int Width, int Height)? TakeDirtyRegion()
        {
            if (dirtyRight < 0) return null;
            var region = (dirtyLeft, dirtyTop, dirtyRight - dirtyLeft + 1, dirtyBottom - dirtyTop + 1);
            dirtyLeft = Width; dirtyTop = Height; dirtyRight = -1; dirtyBottom = -1;
            return region;
        }

        // A region as RGBA bytes, 4 per pixel, row by row, ready for a canvas.
        public byte[] ToRgba(int x, int y, int width, int height)
        {
            var bytes = new byte[width * height * 4];
            int i = 0;
            for (int row = y; row < y + height; row++)
            {
                for (int column = x; column < x + width; column++)
                {
                    var (r, g, b) = ToRgb(Pixels[row * Width + column]);
                    bytes[i++] = r;
                    bytes[i++] = g;
                    bytes[i++] = b;
                    bytes[i++] = 255;
                }
            }
            return bytes;
        }

        // The raw RGB565 words of a region, two little endian bytes per pixel, copied row by row. Much cheaper than
        // ToRgba where .NET is interpreted (WebAssembly): the browser expands the colours itself.
        public byte[] ToRgb565Bytes(int x, int y, int width, int height)
        {
            var bytes = new byte[width * height * 2];
            for (int row = 0; row < height; row++)
            {
                Buffer.BlockCopy(Pixels, ((y + row) * Width + x) * 2, bytes, row * width * 2, width * 2);
            }
            return bytes;
        }

        // RGB565 to 8 bit channels, repeating the top bits so full intensity maps to 255.
        public static (byte R, byte G, byte B) ToRgb(int rgb565)
        {
            int r = (rgb565 >> 11) & 0x1F, g = (rgb565 >> 5) & 0x3F, b = rgb565 & 0x1F;
            return ((byte)((r << 3) | (r >> 2)), (byte)((g << 2) | (g >> 4)), (byte)((b << 3) | (b >> 2)));
        }
        public static int FromRgb(int r, int g, int b)
        {
            return ((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3);
        }

        public string DisplayName() { return deviceName; }
        public void Enable(string function)
        {
            switch (function)
            {
                case "loadx": loadX = true; break;
                case "loady": loadY = true; break;
                case "plot": plot = true; break;
                case "skip": skip = true; break;
                case "clear": clear = true; break;
                default:
                    throw new Exception("Unable to enable the unknown function: " + function);
            }
        }
        public string ID() { return deviceID; }
        public bool IsOutputEnabled() { return false; }
        public List<string> SignalLines()
        {
            return new List<string> { "loadx", "loady", "plot", "skip", "clear" };
        }
    }
}
