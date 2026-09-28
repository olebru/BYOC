using System;
using System.Collections.Generic;
namespace BYOCCore
{
    public class Bus
    {
        public string ID;
        public string NumberFormat = "X2";
        // Devices attached to this bus, for display and single bus rigs. A machine clocks its own device list.
        public List<IBusDevice> devices;
        public int Cycles = 0;
        internal IBusDevice ActiveDevice;
        private byte data;
        private bool dataWrittenInThisClk = false;
        private IBusDevice writer;
        public Bus(string id = "bus")
        {
            ID = id;
            devices = new List<IBusDevice>();
        }
        public byte Data
        {
            get
            {
                return data;
            }
            set
            {
                if (dataWrittenInThisClk)
                {
                    throw new Exception($"Puff of blue smoke exception, multiple bus devices has output enabled at the same time on bus '{ID}': {writer?.ID() ?? "unknown"}, {ActiveDevice?.ID() ?? "unknown"}");
                }
                data = value;
                dataWrittenInThisClk = true;
                writer = ActiveDevice;
            }
        }
        // The device that drove the bus in the last tick, or null if the bus floated (reads as 0).
        public IBusDevice Writer { get { return writer; } }
        internal void BeginTick()
        {
            data = 0;
            dataWrittenInThisClk = false;
            writer = null;
            ActiveDevice = null;
        }
        internal void EndTick()
        {
            ActiveDevice = null;
            Cycles++;
        }
        public void Clk()
        {
            Clocking.Tick(new[] { this }, devices);
        }
    }
}
