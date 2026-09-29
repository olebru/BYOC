using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace BYOCCore
{
    // Memory with its own address register: loadmar takes an address from the bus, output puts the cell there on
    // the bus. It can not be written from the bus; RamModule adds load for that. The program is loaded into the
    // machine's program memory with LoadProgram.
    public class MemoryModule : IBusDevice
    {
        protected Bus connectedBus;
        // One 16 bit word per address. The cells are allocated on first write, so a large memory that is never
        // used costs nothing; until then every cell reads as 0.
        private ushort[] cells;
        private readonly int size;
        public int memoryAddress = 0;
        private string deviceID;
        private string deviceName = "";
        private bool loadMAR = false;
        private bool output = false;
        private bool outputMAR = false;
        public const int DefaultSize = 4096;
        public MemoryModule(string DeviceName, string DeviceID, Bus ConnectedBus, int size = DefaultSize)
        {
            if (size < 1 || size > 65536) throw new ArgumentException($"Memory size must be between 1 and 65536 cells, not {size}.");
            deviceName = DeviceName;
            deviceID = DeviceID;
            connectedBus = ConnectedBus;
            this.size = size;
        }
        public int Size { get { return size; } }
        public bool IsAllocated { get { return cells != null; } }
        // The cells for reading and writing directly; allocates them if needed. Use ValueAt to read without allocating.
        public ushort[] memory { get { return cells ??= new ushort[size]; } }
        public int ValueAt(int address) { return cells == null ? 0 : cells[address]; }
        protected void Store(int address, int value)
        {
            memory[address] = (ushort)(value & Bus.Mask);
        }
        public virtual void Drive()
        {
            if (output)
            {
                connectedBus.Data = ValueAt(memoryAddress);
                output = false;
            }
            if (outputMAR)
            {
                connectedBus.Data = memoryAddress;
                outputMAR = false;
            }
        }
        public virtual void Latch()
        {
            if (loadMAR)
            {
                memoryAddress = connectedBus.Data % size;
                loadMAR = false;
            }
        }
        public string DisplayName() { return deviceName; }
        public virtual void Enable(string function)
        {
            switch (function)
            {
                case "loadmar":
                    loadMAR = true;
                    break;
                case "outputmar":
                    outputMAR = true;
                    break;
                case "output":
                    output = true;
                    break;
                default:
                    throw new Exception("Unable to enable the unknown function: " + function);
            }
        }
        public string ID() { return deviceID; }
        public bool IsOutputEnabled()
        {
            return output || outputMAR;
        }
        public void LoadBytes(Byte[] bytes)
        {
            LoadProgram(bytes.Select(b => (int)b).ToArray());
        }
        public void LoadProgram(IReadOnlyList<int> cells)
        {
            if (cells.Count > size)
            {
                throw new ArgumentException($"Program is {cells.Count} cells, but {deviceName} only holds {size}.");
            }
            for (int i = 0; i < cells.Count; i++)
            {
                Store(i, cells[i]);
            }
        }
        public string OperationsOnNextClockMAR()
        {
            string next = "";
            if (loadMAR) next = $"{next}load";
            if (outputMAR) next = $"{next}output";
            return next;
        }
        public virtual string OperationsOnNextClockRAM()
        {
            string next = "";
            if (output) next = $"{next}output";
            return next;
        }
        public virtual List<String> SignalLines()
        {
            var lines = new List<String>();
            lines.Add("loadmar");
            lines.Add("outputmar");
            lines.Add("output");
            return lines;
        }
        public override string ToString()
        {
            var output = new StringBuilder();
            output.Append(deviceName);
            output.Append(Environment.NewLine);
            output.Append("MAR:");
            output.Append(memoryAddress.ToString(connectedBus.NumberFormat));
            output.Append(Environment.NewLine);
            output.Append("Values:");
            output.Append(Environment.NewLine);
            for (int i = 0; i < size; i += 16)
            {
                for (int n = i; n < Math.Min(i + 16, size); n++)
                {
                    output.Append(ValueAt(n).ToString(connectedBus.NumberFormat));
                    output.Append(" ");
                }
                output.Append(Environment.NewLine);
            }
            return output.ToString();
        }
    }
}
