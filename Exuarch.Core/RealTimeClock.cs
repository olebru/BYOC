using System;
using System.Collections.Generic;
namespace Exuarch.Core
{
    // Raises an interrupt request every interval milliseconds of real time while it runs, however fast or slowly the
    // machine ticks. loadinterval takes the interval, in milliseconds, from the bus, output puts it on the bus, and
    // start and stop switch it on and off.
    // The clock only looks at the time in a tick. While the machine is paused, time goes on but nothing can ask: the
    // first tick after the interval has passed raises one request, however many intervals that was, and the next
    // interval is counted from then. When it keeps up, each interval is counted from the end of the one before, so
    // the requests do not drift.
    public class RealTimeClock : IBusDevice, IInterruptSource
    {
        public int Interval { get; private set; }
        public bool Running { get; private set; }
        public long Expired { get; private set; }
        // Milliseconds since the current interval began, 0 when stopped.
        public double Elapsed { get { return Running ? time.GetElapsedTime(since).TotalMilliseconds : 0; } }

        private readonly Bus bus;
        private readonly string deviceID;
        private readonly string deviceName;
        private readonly TimeProvider time;
        private long since;
        private bool loadInterval, start, stop, output, request;

        public RealTimeClock(string DeviceName, string DeviceID, Bus bus, int interval, TimeProvider time = null)
        {
            deviceName = DeviceName;
            deviceID = DeviceID;
            this.bus = bus;
            Interval = interval;
            this.time = time ?? TimeProvider.System;
        }

        public bool TakeInterruptRequest()
        {
            var taken = request;
            request = false;
            return taken;
        }

        public void Drive()
        {
            if (output)
            {
                bus.Data = Interval;
                output = false;
            }
        }
        public void Latch()
        {
            if (loadInterval) Interval = bus.Data;
            if (start) { Running = true; since = time.GetTimestamp(); }
            if (stop) Running = false;
            else if (Running && !start && Interval > 0)
            {
                long now = time.GetTimestamp();
                long interval = Interval * time.TimestampFrequency / 1000;
                if (now - since >= interval)
                {
                    since = now - since >= 2 * interval ? now : since + interval;
                    Expired++;
                    request = true;
                }
            }
            loadInterval = start = stop = false;
        }

        public string DisplayName() { return deviceName; }
        public void Enable(string function)
        {
            switch (function)
            {
                case "loadinterval": loadInterval = true; break;
                case "start": start = true; break;
                case "stop": stop = true; break;
                case "output": output = true; break;
                default:
                    throw new Exception("Unable to enable the unknown function: " + function);
            }
        }
        public string ID() { return deviceID; }
        public bool IsOutputEnabled() { return output; }
        public List<string> SignalLines() { return new List<string> { "loadinterval", "start", "stop", "output" }; }
    }
}
