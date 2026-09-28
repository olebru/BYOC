using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace BYOCCore
{
    public class RomModule : IBusDevice
    {
        protected Bus connectedBus;
        // One 16 bit word per address.
        public readonly int[] memory;
        public int memoryAddress = 0;
        private string deviceID;
        private string deviceName = "";
        private bool loadMAR = false;
        private bool output = false;
        private bool outputMAR = false;
        public const int DefaultSize = 4096;
        public RomModule(string DeviceName, string DeviceID, Bus ConnectedBus, int size = DefaultSize)
        {
            if (size < 1 || size > 65536) throw new ArgumentException($"Memory size must be between 1 and 65536 cells, not {size}.");
            deviceName = DeviceName;
            deviceID = DeviceID;
            connectedBus = ConnectedBus;
            memory = new int[size];
        }
        public int Size { get { return memory.Length; } }
        public virtual void Drive()
        {
            if (output)
            {
                connectedBus.Data = memory[memoryAddress];
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
                memoryAddress = connectedBus.Data % memory.Length;
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
            if (cells.Count > memory.Length)
            {
                throw new ArgumentException($"Program is {cells.Count} cells, but {deviceName} only holds {memory.Length}.");
            }
            for (int i = 0; i < cells.Count; i++)
            {
                memory[i] = cells[i] & Bus.Mask;
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
            for (int i = 0; i < memory.Length; i += 16)
            {
                for (int n = i; n < Math.Min(i + 16, memory.Length); n++)
                {
                    output.Append(memory[n].ToString(connectedBus.NumberFormat));
                    output.Append(" ");
                }
                output.Append(Environment.NewLine);
            }
            return output.ToString();
        }
    }
}
