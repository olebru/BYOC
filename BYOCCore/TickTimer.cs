using System;
using System.Collections.Generic;
namespace BYOCCore
{
    // Counts clock ticks and raises an interrupt request every period ticks while it runs. loadperiod takes the
    // period from the bus, start and stop switch it on and off, output puts the current count on the bus.
    public class TickTimer : IBusDevice, IInterruptSource
    {
        public int Period { get; private set; }
        public int Count { get; private set; }
        public bool Running { get; private set; }
        public long Expired { get; private set; }

        private readonly Bus bus;
        private readonly string deviceID;
        private readonly string deviceName;
        private bool loadPeriod, start, stop, output, request;

        public TickTimer(string DeviceName, string DeviceID, Bus bus, int period)
        {
            deviceName = DeviceName;
            deviceID = DeviceID;
            this.bus = bus;
            Period = period;
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
                bus.Data = Count;
                output = false;
            }
        }
        public void Latch()
        {
            if (loadPeriod) Period = bus.Data;
            if (start) { Running = true; Count = 0; }
            if (stop) Running = false;
            else if (Running && !start && Period > 0 && ++Count >= Period)
            {
                Count = 0;
                Expired++;
                request = true;
            }
            loadPeriod = start = stop = false;
        }

        public string DisplayName() { return deviceName; }
        public void Enable(string function)
        {
            switch (function)
            {
                case "loadperiod": loadPeriod = true; break;
                case "start": start = true; break;
                case "stop": stop = true; break;
                case "output": output = true; break;
                default:
                    throw new Exception("Unable to enable the unknown function: " + function);
            }
        }
        public string ID() { return deviceID; }
        public bool IsOutputEnabled() { return output; }
        public List<string> SignalLines() { return new List<string> { "loadperiod", "start", "stop", "output" }; }
    }
}
