using System;
using System.Collections.Generic;
namespace BYOCCore
{
    public class ALU : IBusDevice
    {
        private Register a;
        private bool add;
        private Register b;
        private Bus bus;
        private bool cmp;
        private string deviceID;
        private string deviceName;
        private Register sta;
        private bool sub;
        private int? pendingStatus;
        public ALU(string DeviceName, string DeviceID, Register rega, Register regb, Register regsta, Bus Bus)
        {
            deviceID = DeviceID;
            a = rega;
            b = regb;
            sta = regsta;
            bus = Bus;
            deviceName = DeviceName;
        }
        // Operands are read in the drive phase, before any register latches a new value this tick.
        public void Drive()
        {
            if (add)
            {
                int x = a.Data & Mask, y = b.Data & Mask;
                int sum = x + y;
                int result = sum & Mask;
                int status = 0;
                if (result == 0) status |= StatusRegister.ZeroFlag;
                if (sum > Mask) status |= StatusRegister.CarryFlag;
                if (((x ^ result) & (y ^ result) & SignBit) != 0) status |= StatusRegister.OverflowFlag;
                bus.Data = result;
                pendingStatus = status;
                add = false;
            }
            if (sub)
            {
                bus.Data = subtract();
                sub = false;
            }
            if (cmp)
            {
                subtract();
                cmp = false;
            }
        }
        public void Latch()
        {
            if (pendingStatus.HasValue)
            {
                sta.Data = pendingStatus.Value;
                pendingStatus = null;
            }
        }
        public string DisplayName() { return deviceName; }
        public void Enable(string function)
        {
            switch (function)
            {
                case "add":
                    add = true;
                    break;
                case "sub":
                    sub = true;
                    break;
                case "cmp":
                    cmp = true;
                    break;
                default:
                    throw new Exception("Unable to enable the unknown function: " + function);
            }
        }
        public string ID() { return deviceID; }
        public bool IsOutputEnabled()
        {
            return add || sub ;
        }
        public string OperationsOnNextClock()
        {
            string next = "";
            if (add) next = $"{next}add";
            if (sub) next = $"{next}sub";
            if (cmp) next = $"{next}cmp";
            return $"{next}";
        }
        public List<String> SignalLines()
        {
            var lines = new List<String>();
            lines.Add("add");
            lines.Add("sub");
            lines.Add("cmp");
            return lines;
        }
        public new string ToString()
        {
            return deviceName;
        }
        // Computes a - b and the resulting status. Negative and carry (borrow) are set when a < b unsigned.
        private int subtract()
        {
            int x = a.Data & Mask, y = b.Data & Mask;
            int result = (x - y) & Mask;
            int status = 0;
            if (result == 0) status |= StatusRegister.ZeroFlag;
            if (x < y) status |= StatusRegister.NegativeFlag | StatusRegister.CarryFlag;
            if (((x ^ y) & (x ^ result) & SignBit) != 0) status |= StatusRegister.OverflowFlag;
            pendingStatus = status;
            return result;
        }
        // 16 bit arithmetic; the top bit is the sign bit for overflow.
        private const int Mask = Bus.Mask;
        private const int SignBit = Bus.SignBit;
    }
}
