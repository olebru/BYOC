using System;
using System.Collections.Generic;
namespace Exuarch.Core
{
    // 16 bit ALU working on two registers (a and b) and writing its flags to a status register. Every operation
    // sets Z when the result is 0 and N from its top bit, the sign in two's complement.
    //   add: a + b. C when the sum carries out, V on signed overflow.
    //   sub: a - b; cmp sets the same flags without driving the bus. C when a < b unsigned (a borrow), V on signed
    //        overflow. So after cmp: Z equal, C below (unsigned), N != V less than (signed).
    //   and, orr, eor: bitwise.
    //   lsl, lsr: a shifted left or right by b (0-15). C the last bit shifted out.
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
                    if (x < y) status |= StatusRegister.CarryFlag;
                    if (((x ^ y) & (x ^ result) & SignBit) != 0) status |= StatusRegister.OverflowFlag;
                    break;
                case "and": result = x & y; break;
                case "orr": result = x | y; break;
                case "eor": result = x ^ y; break;
                case "lsl":
                    int left = y & 15;
                    result = (x << left) & Mask;
                    if (left > 0 && ((x >> (16 - left)) & 1) != 0) status |= StatusRegister.CarryFlag;
                    break;
                case "lsr":
                    int right = y & 15;
                    result = x >> right;
                    if (right > 0 && ((x >> (right - 1)) & 1) != 0) status |= StatusRegister.CarryFlag;
                    break;
                default:
                    throw new InvalidOperationException(pending);
            }
            if (result == 0) status |= StatusRegister.ZeroFlag;
            status |= Sign(result);
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
        public List<String> SignalLines()
        {
            return new List<string>(Operations);
        }
        // 16 bit arithmetic; the top bit is the sign bit for overflow.
        private const int Mask = Bus.Mask;
        private const int SignBit = Bus.SignBit;
    }
}
