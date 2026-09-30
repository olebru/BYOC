using System;
using System.Collections.Generic;
namespace Exuarch.Core
{
    // A device that drives other devices' control lines by itself, without microcode. The machine asks it which
    // devices took a value from which bus in the last tick, so those transfers can be shown like any other.
    public interface IBusMaster
    {
        IEnumerable<(string BusId, string ReaderId)> LastReaders { get; }
    }

    // A graphics coprocessor. The CPU gives it a rectangle on the host bus (loadx, loady, loadw, loadh, loadcolour)
    // and starts it; from then on the blitter fills the rectangle by itself, one bus transfer per tick on its own
    // video bus: the column, then the row, then one pixel per tick along the row, and so on for each row. It does
    // this by putting the value on the video bus and enabling the framebuffer's loadx, loady or plot line, as
    // microcode would. The CPU carries on with its own program and can read status (1 busy, 0 idle) on the host bus.
    // There is no clipping: a rectangle past the right edge wraps like the framebuffer's cursor does.
    public class Blitter : IBusDevice, IBusMaster, IInterruptSource
    {
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Colour { get; private set; }
        public bool Busy { get; private set; }
        // Progress through the current (or last) job.
        public int Row { get; private set; }
        public int Column { get; private set; }
        public long PixelsDrawn { get; private set; }
        public long JobsDone { get; private set; }

        private enum Phase { Column, Row, Pixels }
        private readonly Bus host;
        private readonly Bus video;
        private readonly Framebuffer screen;
        private readonly string deviceID;
        private readonly string deviceName;
        private Phase phase;
        private bool loadX, loadY, loadW, loadH, loadColour, start, status;
        private string lastReader;
        private bool interruptRequest;

        // Asks for an interrupt when a job is finished.
        public bool TakeInterruptRequest()
        {
            var taken = interruptRequest;
            interruptRequest = false;
            return taken;
        }

        public Blitter(string DeviceName, string DeviceID, Bus host, Bus video, Framebuffer screen)
        {
            deviceName = DeviceName;
            deviceID = DeviceID;
            this.host = host;
            this.video = video;
            this.screen = screen ?? throw new ArgumentException("A blitter needs a framebuffer to draw on.");
        }

        public IEnumerable<(string BusId, string ReaderId)> LastReaders
        {
            get { if (lastReader != null) yield return (video.ID, lastReader); }
        }

        public void Drive()
        {
            lastReader = null;
            if (status)
            {
                host.Data = Busy ? 1 : 0;
                status = false;
            }
            if (!Busy) return;
            switch (phase)
            {
                case Phase.Column:
                    video.Data = X;
                    screen.Enable("loadx");
                    phase = Phase.Row;
                    break;
                case Phase.Row:
                    video.Data = Y + Row;
                    screen.Enable("loady");
                    Column = 0;
                    phase = Phase.Pixels;
                    break;
                case Phase.Pixels:
                    video.Data = Colour;
                    screen.Enable("plot");
                    PixelsDrawn++;
                    if (++Column == Width)
                    {
                        phase = Phase.Column;
                        if (++Row == Height)
                        {
                            Busy = false;
                            JobsDone++;
                            interruptRequest = true;
                        }
                    }
                    break;
            }
            lastReader = screen.ID();
        }
        public void Latch()
        {
            int value = host.Data;
            if (loadX) X = value;
            if (loadY) Y = value;
            if (loadW) Width = value;
            if (loadH) Height = value;
            if (loadColour) Colour = value;
            if (start && Width > 0 && Height > 0)
            {
                Busy = true;
                Row = 0;
                Column = 0;
                phase = Phase.Column;
            }
            loadX = loadY = loadW = loadH = loadColour = start = false;
        }

        public string DisplayName() { return deviceName; }
        public void Enable(string function)
        {
            switch (function)
            {
                case "loadx": loadX = true; break;
                case "loady": loadY = true; break;
                case "loadw": loadW = true; break;
                case "loadh": loadH = true; break;
                case "loadcolour": loadColour = true; break;
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
            return new List<string> { "loadx", "loady", "loadw", "loadh", "loadcolour", "start", "status" };
        }
    }
}
