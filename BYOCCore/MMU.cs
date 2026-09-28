using System;
using System.Collections.Generic;
using System.Linq;
namespace BYOCCore
{
    public class MMU : IBusDevice
    {
        public Register ChipSelectRegister;
        public RamModule[] RamBanks;
        private bool asciiMode;
        private Bus bus;
        private string deviceName;
        private string id;
        private List<string> pendingBankFunctions = new List<string>();
        private bool select0Stack;
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
        // select0stack applies before anything else. Bank outputs use the bank selected at the start of the
        // tick; bank inputs use the bank selected after the chip select register latched this tick.
        public void Drive()
        {
            if (select0Stack)
            {
                ChipSelectRegister.Data = 0;
                select0Stack = false;
            }
            ChipSelectRegister.Drive();
            var bank = this.RamBanks[ChipSelectRegister.Data];
            foreach (var function in pendingBankFunctions.Where(IsDriveFunction))
            {
                bank.Enable(function);
            }
            bank.Drive();
        }
        public void Latch()
        {
            ChipSelectRegister.Latch();
            var bank = this.RamBanks[ChipSelectRegister.Data];
            foreach (var function in pendingBankFunctions.Where(f => !IsDriveFunction(f)))
            {
                bank.Enable(function);
            }
            pendingBankFunctions.Clear();
            bank.Latch();
        }
        private static bool IsDriveFunction(string function)
        {
            return function == "output" || function == "outputmar";
        }
        public string DisplayName() { return deviceName; }
        public void Enable(string function)
        {
            switch (function)
            {
                case "select0stack":
                    select0Stack = true;
                    break;
                case "loadcs":
                    ChipSelectRegister.Enable("load");
                    break;
                case "outputcs":
                    ChipSelectRegister.Enable("output");
                    break;
                default:
                    if (!this.RamBanks[0].SignalLines().Contains(function))
                    {
                        throw new Exception("Unable to enable the unknown function: " + function);
                    }
                    pendingBankFunctions.Add(function);
                    break;
            }
        }
        public string ID()
        {
            return id;
        }
        public bool IsOutputEnabled()
        {
            return ChipSelectRegister.IsOutputEnabled()
                || pendingBankFunctions.Any(IsDriveFunction);
        }
        public List<String> SignalLines()
        {
            var lines = this.RamBanks.FirstOrDefault().SignalLines();
            lines.Add("loadcs");
            lines.Add("outputcs");
            lines.Add("select0stack");
            return lines;
        }
    }
}
