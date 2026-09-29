using System;
using System.Collections.Generic;
namespace Exuarch.Core
{
    // 16 bit ALU working on two registers (a and b) and writing its flags to a status register.
    //   add: a + b. Z when 0, C when the sum carries out, V on signed overflow.
    //   sub: a - b; cmp sets the same flags without driving the bus. Z when equal, N and C when a < b unsigned
    //        (C is borrow), V on signed overflow.
    //   and, orr, eor: bitwise. Z when 0, N from the top bit.
    //   lsl, lsr: a shifted left or right by b (0-15). Z when 0, N from the top bit, C the last bit shifted out.
    public class ALU : IBusDevice
    {
        private Register a;
        private Register b;
        private Bus bus;
        private string deviceID;
        private string deviceName;
        private Register sta;
        private string pending;
        private int? pendingStatus;
        private static readonly string[] Operations = { "add", "sub", "cmp", "and", "orr", "eor", "lsl", "lsr" };
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
            if (pending == null) return;
            int x = a.Data & Mask, y = b.Data & Mask;
            int result;
            int status = 0;
            switch (pending)
            {
                case "add":
                    int sum = x + y;
                    result = sum & Mask;
                    if (sum > Mask) status |= StatusRegister.CarryFlag;
                    if (((x ^ result) & (y ^ result) & SignBit) != 0) status |= StatusRegister.OverflowFlag;
                    break;
                case "sub":
                case "cmp":
                    result = (x - y) & Mask;
                    if (x < y) status |= StatusRegister.NegativeFlag | StatusRegister.CarryFlag;
                    if (((x ^ y) & (x ^ result) & SignBit) != 0) status |= StatusRegister.OverflowFlag;
                    break;
                case "and": result = x & y; status |= Sign(result); break;
                case "orr": result = x | y; status |= Sign(result); break;
                case "eor": result = x ^ y; status |= Sign(result); break;
                case "lsl":
                    int left = y & 15;
                    result = (x << left) & Mask;
                    if (left > 0 && ((x >> (16 - left)) & 1) != 0) status |= StatusRegister.CarryFlag;
                    status |= Sign(result);
                    break;
                case "lsr":
                    int right = y & 15;
                    result = x >> right;
                    if (right > 0 && ((x >> (right - 1)) & 1) != 0) status |= StatusRegister.CarryFlag;
                    status |= Sign(result);
                    break;
                default:
                    throw new InvalidOperationException(pending);
            }
            if (result == 0) status |= StatusRegister.ZeroFlag;
            if (pending != "cmp") bus.Data = result;
            pendingStatus = status;
            pending = null;
        }
        public void Latch()
        {
            if (pendingStatus.HasValue)
            {
                sta.Data = pendingStatus.Value;
                pendingStatus = null;
            }
        }
        private static int Sign(int result) { return (result & SignBit) != 0 ? StatusRegister.NegativeFlag : 0; }
        public string DisplayName() { return deviceName; }
        public void Enable(string function)
        {
            if (Array.IndexOf(Operations, function) < 0) throw new Exception("Unable to enable the unknown function: " + function);
            if (pending != null && pending != function) throw new Exception($"{deviceID}: '{pending}' and '{function}' can not run in the same tick.");
            pending = function;
        }
        public string ID() { return deviceID; }
        public bool IsOutputEnabled()
        {
            return pending != null && pending != "cmp";
        }
        public string OperationsOnNextClock()
        {
            return pending ?? "";
        }
        public List<String> SignalLines()
        {
            return new List<string>(Operations);
        }
        public new string ToString()
        {
            return deviceName;
        }
        // 16 bit arithmetic; the top bit is the sign bit for overflow.
        private const int Mask = Bus.Mask;
        private const int SignBit = Bus.SignBit;
    }
}
