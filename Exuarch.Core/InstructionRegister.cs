using System;
using System.Collections.Generic;
namespace Exuarch.Core
{
    // The decoder's micro step counter, called the instruction register. It counts up by one every tick on its own, so the decoder ROM moves on to
    // the next micro step without the microcode asking. load takes a step address from the bus (an opcode, which is
    // where that instruction's steps start) and reset clears it to 0, where the fetch routine starts.
    public class InstructionRegister : IBusDevice
    {
        public int Data { get; set; }
        private readonly Bus bus;
        private readonly string deviceID;
        private readonly string deviceName;
        private bool load, reset;

        public InstructionRegister(string DeviceName, string DeviceID, Bus bus)
        {
            deviceName = DeviceName;
            deviceID = DeviceID;
            this.bus = bus;
        }

        public void Drive()
        {
        }
        public void Latch()
        {
            if (reset) Data = 0;
            else if (load) Data = bus.Data & Bus.Mask;
            else Data = (Data + 1) & Bus.Mask;
            load = reset = false;
        }

        public string DisplayName() { return deviceName; }
        public void Enable(string function)
        {
            switch (function)
            {
                case "load": load = true; break;
                case "reset": reset = true; break;
                default:
                    throw new Exception("Unable to enable the unknown function: " + function);
            }
        }
        public string ID() { return deviceID; }
        public bool IsOutputEnabled() { return false; }
        public List<string> SignalLines() { return new List<string> { "load", "reset" }; }
    }
}
