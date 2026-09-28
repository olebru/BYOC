using System;
using System.Collections.Generic;
namespace BYOCCore
{
    public class Bus
    {
        public string ID;
        public string NumberFormat = "X4";
        // Devices attached to this bus, for display and single bus rigs. A machine clocks its own device list.
        public List<IBusDevice> devices;
        public int Cycles = 0;
        // The machine is 16 bits throughout: every bus, register and memory cell holds a 16 bit word.
        public const int Width = 16;
        public const int Mask = 0xFFFF;
        public const int SignBit = 0x8000;
        internal IBusDevice ActiveDevice;
        private int data;
        private bool dataWrittenInThisClk = false;
        private IBusDevice writer;
        public Bus(string id = "bus")
        {
            ID = id;
            devices = new List<IBusDevice>();
        }
        public int Data
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
                data = value & Mask;
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
