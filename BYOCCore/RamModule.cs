using System;
using System.Collections.Generic;
namespace BYOCCore
{
    public class RamModule : MemoryModule, IWriteTracked
    {
        private bool load = false;
        // Counts stores, so observers can tell which modules were written in a tick.
        public long WriteCount { get; private set; }
        public int LastWriteAddress { get; private set; } = -1;
        public RamModule(string DeviceName, string DeviceID, Bus ConnectedBus, int size = DefaultSize) : base(DeviceName, DeviceID, ConnectedBus, size)
        {
        }
        public override void Latch()
        {
            if (load)
            {
                Store(memoryAddress, connectedBus.Data);
                LastWriteAddress = memoryAddress;
                WriteCount++;
                load = false;
            }
            base.Latch();
        }
        public override void Enable(string function)
        {
            switch (function)
            {
                case "load":
                    load = true;
                    break;
                default:
                    base.Enable(function);
                    break;
            }
        }
        public override string OperationsOnNextClockRAM()
        {
            string next = base.OperationsOnNextClockRAM();
            if (load) next = $"{next}load";
            return next;
        }
        public override List<String> SignalLines()
        {
            var baseList = base.SignalLines();
            baseList.Add("load");
            return baseList;
        }
    }
}
