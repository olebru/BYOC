using System;
using System.Collections.Generic;
using System.Linq;
namespace Exuarch.Core
{
    // A bank of numbered registers behind one select input, the way most real CPUs hold their registers: an
    // instruction names a register by number (R0, R1, ...) instead of having one opcode per register. select takes
    // a register number from the bus (modulo the count); output, load, reset, inc and dec then act on the selected
    // register like a register's own lines. select takes effect at the end of the tick, after anything else in the
    // same tick has acted on the register selected before. All registers start at 0 and wrap at 16 bits.
    public class RegisterFile : IBusDevice
    {
        public const int DefaultCount = 8;
        public int Count { get; }
        public int Selected { get; private set; }
        private readonly int[] values;
        public IReadOnlyList<int> Values { get { return values; } }
        public int this[int register] { get { return values[register]; } }

        private readonly Bus bus;
        private readonly string deviceID;
        private readonly string deviceName;
        private bool select, output, load, reset, inc, dec;

        public RegisterFile(string DeviceName, string DeviceID, Bus bus, int count = DefaultCount)
        {
            deviceName = DeviceName;
            deviceID = DeviceID;
            this.bus = bus;
            Count = count;
            values = new int[count];
        }

        public void Drive()
        {
            if (output)
            {
                bus.Data = values[Selected];
                output = false;
            }
        }
        public void Latch()
        {
            if (load) values[Selected] = bus.Data & Bus.Mask;
            if (reset) values[Selected] = 0;
            if (inc) values[Selected] = (values[Selected] + 1) & Bus.Mask;
            if (dec) values[Selected] = (values[Selected] - 1) & Bus.Mask;
            if (select) Selected = bus.Data % Count;
            select = load = reset = inc = dec = false;
        }

        public string DisplayName() { return deviceName; }
        public void Enable(string function)
        {
            switch (function)
            {
                case "select": select = true; break;
                case "output": output = true; break;
                case "load": load = true; break;
                case "reset": reset = true; break;
                case "inc": inc = true; break;
                case "dec": dec = true; break;
                default:
                    throw new Exception("Unable to enable the unknown function: " + function);
            }
        }
        public string ID() { return deviceID; }
        public bool IsOutputEnabled() { return output; }
        public List<string> SignalLines()
        {
            return new List<string> { "select", "output", "load", "reset", "inc", "dec" };
        }

        // The registers a machine's register operands can name: those of its first register file, or none.
        public static int CountIn(MachineDefinition machine)
        {
            var device = machine?.Devices.FirstOrDefault(d => d.Type == "registerFile");
            if (device == null) return 0;
            return device.Parameters.TryGetValue("count", out var count) && count.TryGetInt32(out var n) ? n : DefaultCount;
        }
    }
}
