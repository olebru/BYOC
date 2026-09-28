using System;
using System.Collections.Generic;
using System.Linq;
namespace BYOCCore
{
    public class MMU : IBusDevice
    {
        public Register ChipSelectRegister;
        public RamModule[] RamBanks;
        private Bus bus;
        private string deviceName;
        private string id;
        private List<string> pendingBankFunctions = new List<string>();
        private bool select0Stack;
        public const int MaxCells = 1 << 20;
        public const int DefaultBanks = 16;
        public MMU(string DeviceName, string DeviceID, Bus bus, int banks = DefaultBanks, int bankSize = RomModule.DefaultSize)
        {
            if (banks < 1 || banks > 256) throw new ArgumentException($"An MMU has between 1 and 256 banks, not {banks}.");
            if ((long)banks * bankSize > MaxCells) throw new ArgumentException($"{banks} banks of {bankSize} cells is more than {MaxCells} cells.");
            this.bus = bus;
            ChipSelectRegister = new Register("CS  ", "cs", this.bus);
            id = DeviceID;
            deviceName = DeviceName;
            RamBanks = new RamModule[banks];
            for (int i = 0; i < banks; i++)
            {
                RamBanks[i] = new RamModule($"Bank {i}", i.ToString(), this.bus, bankSize);
            }
        }
        // The bank the chip select register points at; bank numbers wrap at the number of banks.
        public int SelectedBankNumber { get { return ChipSelectRegister.Data % RamBanks.Length; } }
        public RamModule SelectedBank { get { return RamBanks[SelectedBankNumber]; } }
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
            var bank = SelectedBank;
            foreach (var function in pendingBankFunctions.Where(IsDriveFunction))
            {
                bank.Enable(function);
            }
            bank.Drive();
        }
        public void Latch()
        {
            ChipSelectRegister.Latch();
            var bank = SelectedBank;
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
