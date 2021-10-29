using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace BYOCCore
{
    public class RomModule : IBusDevice
    {
        public bool ASCIIMode = false;
        public bool INSTRMode = false;
        public bool ShowRAMValues = true;
        protected Bus connectedBus;
        public byte[] memory = new byte[256];
        public byte memoryAddress = 0;
        private string deviceID;
        private string deviceName = "";
        private bool loadMAR = false;
        private bool output = false;
        private bool outputMAR = false;
        public RomModule(string DeviceName, string DeviceID, Bus ConnectedBus)
        {
            deviceName = DeviceName;
            deviceID = DeviceID;
            connectedBus = ConnectedBus;
        }
        public void Clk()
        {
            if (output)
            {
                connectedBus.Data = memory[memoryAddress];
                output = false;
            }
            if (loadMAR)
            {
                memoryAddress = connectedBus.Data;
                loadMAR = false;
            }
            if (outputMAR)
            {
                connectedBus.Data = memoryAddress;
                outputMAR = false;
            }
        }
        public string DisplayName() { return deviceName; }
        public void Enable(string function)
        {
            switch (function)
            {
                case "loadmar":
                    loadMAR = true;
                    break;
                case "outputMAR":
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
            return output;
        }
        public void LoadBytes(Byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                memory[i] = bytes[i];
            }
        }
        public string OperationsOnNextClockMAR()
        {
            string next = "";
            if (loadMAR) next = $"{next}load";
            if (outputMAR) next = $"{next}output";
            return next;
        }
        public string OperationsOnNextClockRAM()
        {
            string next = "";
            if (output) next = $"{next}output";
            return next;
        }
        public List<String> SignalLines()
        {
            var lines = new List<String>();
            lines.Add("loadmar");
            lines.Add("outputMAR");
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
            for (int i = 0; i < 16; i++)
            {
                for (int n = 0; n < 16; n++)
                {
                    output.Append(memory[(i * 16) + n].ToString(connectedBus.NumberFormat));
                    output.Append(" ");
                }
                output.Append(Environment.NewLine);
            }
            return output.ToString();
        }
    }
}
