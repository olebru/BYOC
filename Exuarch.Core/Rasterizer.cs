using System;
using System.Collections.Generic;
using System.Linq;
namespace Exuarch.Core
{
    // A triangle rasterizer: the heart of a fixed function GPU. The CPU puts triangles in the rasterizer's memory,
    // gives it their address and how many there are (loadaddr, loadcount) and starts it. From then on it works by
    // itself, one bus transfer per tick, while the CPU carries on:
    //
    //   1. It reads each triangle from its memory over its list bus, 12 words: x, y, z and colour for each of the
    //      three corners. A word takes two ticks, one to put the address in the memory's MAR, one to read the cell.
    //   2. It fills the triangle row by row. A row starts with two ticks that put the column and the row in the
    //      framebuffer's cursor (and the depth buffer's). Then each pixel is one plot: the colour, blended from the
    //      three corner colours by how near the pixel is to each corner (Gouraud shading; three equal colours give a
    //      flat triangle).
    //   3. With a depth buffer connected, each pixel is first read from the depth buffer (one tick). Only if the
    //      triangle is nearer there (a smaller z) is the new depth written and the colour plotted (two more ticks);
    //      otherwise both cursors step past the pixel (one tick). So hidden surfaces cost less than drawn ones, but
    //      the depth test still costs a read for every pixel: memory bandwidth, the thing GPUs are built around.
    //
    // Pixels whose centre is inside the triangle are drawn; a pixel exactly on an edge shared by two triangles is
    // drawn by one of them only (the top-left rule), so a mesh has no gaps and no pixel drawn twice. Pixels off the
    // screen are not drawn. status puts 1 on the host bus while busy; it asks for an interrupt when it is done.
    public class Rasterizer : IBusDevice, IBusMaster, IInterruptSource
    {
        public const int WordsPerTriangle = 12;

        public int ListAddress { get; private set; }
        public int Count { get; private set; }
        public bool Busy { get; private set; }
        // The triangle being read or drawn, counting from 0, and totals since the machine started.
        public int Triangle { get; private set; }
        public string Stage { get; private set; } = "idle";
        public long TrianglesDrawn { get; private set; }
        public long PixelsDrawn { get; private set; }
        public long PixelsHidden { get; private set; }
        public long JobsDone { get; private set; }

        private enum Kind { Write, Read, Lines }
        // One step per kind of transfer, made once and reused with a new value: a step is carried out in the tick
        // after it is chosen, before the next one is chosen, and allocating one per tick is costly where .NET is
        // interpreted (WebAssembly).
        private sealed class Step
        {
            public Kind Kind;
            public Bus Bus;
            public int Value;
            public (IBusDevice Device, string Line)[] Lines;
            public string[] Readers;

            public Step(Kind kind, Bus bus, params (IBusDevice Device, string Line)[] lines)
            {
                Kind = kind;
                Bus = bus;
                Lines = lines;
                var readers = new List<string>();
                foreach (var (device, _) in lines) if (!readers.Contains(device.ID())) readers.Add(device.ID());
                Readers = readers.ToArray();
            }
            public Step With(int value)
            {
                Value = value;
                return this;
            }
        }
        private readonly Step address, word, column, row, plot, depthRead, depthWrite, pass;

        private readonly Bus host, list, video;
        private readonly Framebuffer screen;
        private readonly DepthBuffer depth;
        private readonly MemoryModule memory;
        private readonly string deviceID, deviceName;
        private bool loadAddress, loadCount, start, status;
        private IEnumerator<Step> job;
        private Step pending, executing;
        private int readValue;
        private bool interruptRequest;
        private readonly List<(string BusId, string ReaderId)> lastReaders = new List<(string, string)>();

        public Rasterizer(string DeviceName, string DeviceID, Bus host, Bus list, Bus video, Framebuffer screen, DepthBuffer depth, MemoryModule memory)
        {
            deviceName = DeviceName;
            deviceID = DeviceID;
            this.host = host;
            this.list = list;
            this.video = video;
            this.screen = screen ?? throw new ArgumentException("A rasterizer needs a framebuffer to draw on.");
            this.memory = memory ?? throw new ArgumentException("A rasterizer needs a memory to read its triangles from.");
            this.depth = depth;
            address = new Step(Kind.Write, list, (memory, "loadmar"));
            word = new Step(Kind.Read, list, (memory, "output"));
            plot = new Step(Kind.Write, video, (screen, "plot"));
            if (depth == null)
            {
                column = new Step(Kind.Write, video, (screen, "loadx"));
                row = new Step(Kind.Write, video, (screen, "loady"));
            }
            else
            {
                column = new Step(Kind.Write, video, (screen, "loadx"), (depth, "loadx"));
                row = new Step(Kind.Write, video, (screen, "loady"), (depth, "loady"));
                depthRead = new Step(Kind.Read, video, (depth, "output"));
                depthWrite = new Step(Kind.Write, video, (depth, "load"), (depth, "next"));
                pass = new Step(Kind.Lines, null, (depth, "next"), (screen, "skip"));
            }
        }

        public IEnumerable<(string BusId, string ReaderId)> LastReaders { get { return lastReaders; } }

        public bool TakeInterruptRequest()
        {
            var taken = interruptRequest;
            interruptRequest = false;
            return taken;
        }

        public void Drive()
        {
            lastReaders.Clear();
            if (status)
            {
                host.Data = Busy ? 1 : 0;
                status = false;
            }
            executing = pending;
            pending = null;
            if (executing == null) return;
            switch (executing.Kind)
            {
                case Kind.Write:
                    executing.Bus.Data = executing.Value;
                    Enable(executing.Lines);
                    foreach (var reader in executing.Readers) lastReaders.Add((executing.Bus.ID, reader));
                    break;
                case Kind.Lines:
                    Enable(executing.Lines);
                    break;
                case Kind.Read:
                    // The source was told to drive in the last tick's latch, so it does so now whatever the device order.
                    lastReaders.Add((executing.Bus.ID, deviceID));
                    break;
            }
        }

        public void Latch()
        {
            if (executing?.Kind == Kind.Read) readValue = executing.Bus.Data;
            executing = null;
            int value = host.Data;
            if (loadAddress) ListAddress = value;
            if (loadCount) Count = value;
            if (start && !Busy && Count > 0)
            {
                Busy = true;
                job = Job().GetEnumerator();
            }
            loadAddress = loadCount = start = false;
            if (Busy && pending == null) Advance();
        }

        // The next tick's step, or the end of the job.
        private void Advance()
        {
            if (job.MoveNext())
            {
                pending = job.Current;
                if (pending.Kind == Kind.Read) Enable(pending.Lines);
                return;
            }
            job = null;
            Busy = false;
            Stage = "idle";
            JobsDone++;
            interruptRequest = true;
        }

        private static void Enable((IBusDevice Device, string Line)[] lines)
        {
            foreach (var (device, line) in lines) device.Enable(line);
        }

        private IEnumerable<Step> Job()
        {
            for (Triangle = 0; Triangle < Count; Triangle++)
            {
                Stage = "reading";
                var words = new int[WordsPerTriangle];
                int address = ListAddress + Triangle * WordsPerTriangle;
                for (int i = 0; i < WordsPerTriangle; i++)
                {
                    yield return this.address.With(address + i);
                    yield return word;
                    words[i] = readValue;
                }
                Stage = "drawing";
                foreach (var step in Draw(words)) yield return step;
                TrianglesDrawn++;
            }
        }

        private struct Corner
        {
            public long X, Y;
            public int Z, Colour;
        }

        private IEnumerable<Step> Draw(int[] words)
        {
            var v = new Corner[3];
            for (int i = 0; i < 3; i++)
            {
                // Coordinates are signed, so a corner can be off the screen; doubled so pixel centres are whole numbers.
                v[i] = new Corner { X = 2L * (short)words[i * 4], Y = 2L * (short)words[i * 4 + 1], Z = words[i * 4 + 2], Colour = words[i * 4 + 3] };
            }
            long area = Edge(v[0], v[1], v[2].X, v[2].Y);
            if (area == 0) yield break;
            if (area < 0)
            {
                (v[1], v[2]) = (v[2], v[1]);
                area = -area;
            }
            // Pixels exactly on an edge belong to the triangle only if the edge is a top or a left edge.
            long bias0 = TopLeft(v[1], v[2]) ? 0 : -1, bias1 = TopLeft(v[2], v[0]) ? 0 : -1, bias2 = TopLeft(v[0], v[1]) ? 0 : -1;
            int left = (int)Math.Max(0, Math.Min(v[0].X, Math.Min(v[1].X, v[2].X)) / 2 - 1);
            int right = (int)Math.Min(Framebuffer.Width - 1, Math.Max(v[0].X, Math.Max(v[1].X, v[2].X)) / 2 + 1);
            int top = (int)Math.Max(0, Math.Min(v[0].Y, Math.Min(v[1].Y, v[2].Y)) / 2 - 1);
            int bottom = (int)Math.Min(Framebuffer.Height - 1, Math.Max(v[0].Y, Math.Max(v[1].Y, v[2].Y)) / 2 + 1);
            for (int y = top; y <= bottom; y++)
            {
                // The span of this row: the pixels whose centre is inside.
                int first = -1, last = -1;
                for (int x = left; x <= right; x++)
                {
                    if (Inside(v, 2L * x + 1, 2L * y + 1, bias0, bias1, bias2))
                    {
                        if (first < 0) first = x;
                        last = x;
                    }
                    else if (first >= 0) break;
                }
                if (first < 0) continue;
                yield return column.With(first);
                yield return row.With(y);
                for (int x = first; x <= last; x++)
                {
                    long px = 2L * x + 1, py = 2L * y + 1;
                    long w0 = Edge(v[1], v[2], px, py), w1 = Edge(v[2], v[0], px, py), w2 = area - w0 - w1;
                    int colour = Blend(v, w0, w1, w2, area);
                    int z = (int)Math.Clamp((v[0].Z * w0 + v[1].Z * w1 + v[2].Z * w2 + area / 2) / area, 0, 0xFFFF);
                    if (depth == null)
                    {
                        yield return plot.With(colour);
                        PixelsDrawn++;
                        continue;
                    }
                    yield return depthRead;
                    if (z < readValue)
                    {
                        yield return depthWrite.With(z);
                        yield return plot.With(colour);
                        PixelsDrawn++;
                    }
                    else
                    {
                        yield return pass;
                        PixelsHidden++;
                    }
                }
            }
        }

        // Twice the signed area of the triangle a, b, p: positive when p is on the inside of the edge from a to b.
        private static long Edge(Corner a, Corner b, long px, long py)
        {
            return (b.X - a.X) * (py - a.Y) - (b.Y - a.Y) * (px - a.X);
        }
        private static bool Inside(Corner[] v, long px, long py, long bias0, long bias1, long bias2)
        {
            return Edge(v[1], v[2], px, py) + bias0 >= 0 && Edge(v[2], v[0], px, py) + bias1 >= 0 && Edge(v[0], v[1], px, py) + bias2 >= 0;
        }
        // With the corners in the order that makes the area positive (y growing downwards), the inside is on the
        // right of each edge as it runs from a to b. So a left edge runs upwards, and a top edge runs to the right.
        private static bool TopLeft(Corner a, Corner b)
        {
            long dx = b.X - a.X, dy = b.Y - a.Y;
            return (dy == 0 && dx > 0) || dy < 0;
        }

        // RGB565 corner colours mixed channel by channel by the pixel's weights.
        private static int Blend(Corner[] v, long w0, long w1, long w2, long area)
        {
            int Channel(int shift, int mask)
            {
                long sum = ((v[0].Colour >> shift) & mask) * w0 + ((v[1].Colour >> shift) & mask) * w1 + ((v[2].Colour >> shift) & mask) * w2;
                return (int)Math.Clamp((sum + area / 2) / area, 0, mask);
            }
            return (Channel(11, 0x1F) << 11) | (Channel(5, 0x3F) << 5) | Channel(0, 0x1F);
        }

        public string DisplayName() { return deviceName; }
        public void Enable(string function)
        {
            switch (function)
            {
                case "loadaddr": loadAddress = true; break;
                case "loadcount": loadCount = true; break;
                case "start": start = true; break;
                case "status": status = true; break;
                default:
                    throw new Exception("Unable to enable the unknown function: " + function);
            }
        }
        public string ID() { return deviceID; }
        public bool IsOutputEnabled() { return status; }
        public List<string> SignalLines()
        {
            return new List<string> { "loadaddr", "loadcount", "start", "status" };
        }
    }
}
