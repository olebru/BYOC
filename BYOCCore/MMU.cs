using System;
using System.Collections.Generic;
using System.Linq;
namespace BYOCCore
{
    public class MMU : IBusDevice
    {
        private string id;
        private string deviceName;
        public Register ChipSelectRegister;
        private Bus bus;
        public RamModule[] RamBanks;
        private bool asciiMode;
        public bool ASCIIMode
        {
            get
            {
                return asciiMode;
            }
            set
            {
                asciiMode = value;
                foreach (var ramModule in RamBanks)
                {
                    ramModule.ASCIIMode = asciiMode;
                }
            }
        }
        public string DisplayName() { return deviceName; }
        public MMU(string DeviceName, string DeviceID, Bus bus)
        {
            this.bus = bus;
            ChipSelectRegister = new Register("CS  ", "cs", this.bus);
            id = DeviceID;
            deviceName = DeviceName;
            RamBanks = new RamModule[256];
            for (int i = 0; i < 256; i++)
            {
                RamBanks[i] = new RamModule($"Bank {i}", i.ToString(), this.bus);
            }
        }
        public void Clk()
        {
            this.ChipSelectRegister.Clk();
            this.RamBanks[ChipSelectRegister.Data].Clk();
        }
        public List<String> SignalLines()
        {
            var lines = this.RamBanks.FirstOrDefault().SignalLines();
            lines.Add("loadcs");
            lines.Add("outputcs");
            lines.Add("select0stack");
            return lines;
        }
        public void Enable(string function)
        {
            switch (function)
            {
                case "select0stack":
                    ChipSelectRegister.Data = 0;
                    break;
                case "loadcs":
                    ChipSelectRegister.Enable("load");
                    break;
                case "outputcs":
                    ChipSelectRegister.Enable("output");
                    break;
                default:
                    this.RamBanks[ChipSelectRegister.Data].Enable(function);
                    break;
            }
        }
        public string ID()
        {
            return id;
        }
        public bool IsOutputEnabled()
        {
            if (ChipSelectRegister.IsOutputEnabled()) return true;
            foreach (var bank in RamBanks)
            {
                if (bank != null && bank.IsOutputEnabled()) return true;
            }
            return false;
        }
    }
}
