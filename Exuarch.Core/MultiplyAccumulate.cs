using System;
using System.Collections.Generic;
namespace Exuarch.Core
{
    // A multiply-accumulate unit for fixed point maths: the part of a 3D pipeline the ALU can not do. It takes two
    // signed 16 bit operands from the bus (loada, loadb) and keeps a 32 bit accumulator:
    //   mul  acc = a * b
    //   mac  acc = acc + a * b
    //   div  acc = (acc << shift) / b, so a quotient keeps the same fixed point scale (b = 0 gives the largest value
    //        of acc's sign)
    // output puts acc >> shift on the bus, clamped to -32768..32767. With the default shift of 8 the numbers are
    // 8.8 fixed point: 256 is 1.0, so multiplying a coordinate by a cosine of 256 * cos(angle) and reading the output
    // gives the rotated coordinate. The operations happen at the end of the tick, after loads in the same tick.
    public class MultiplyAccumulate : IBusDevice
    {
        public int A { get; private set; }
        public int B { get; private set; }
        public long Accumulator { get; private set; }
        public int Shift { get; }
        // The value output would put on the bus.
        public int Result { get { return (int)Math.Clamp(Accumulator >> Shift, short.MinValue, short.MaxValue); } }

        private readonly Bus bus;
        private readonly string deviceID;
        private readonly string deviceName;
        private bool loadA, loadB, mul, mac, div, output;

        public MultiplyAccumulate(string DeviceName, string DeviceID, Bus bus, int shift = 8)
        {
            deviceName = DeviceName;
            deviceID = DeviceID;
            this.bus = bus;
            Shift = shift;
        }

        private static int Signed(int word) { return (short)(word & Bus.Mask); }

        public void Drive()
        {
            if (output)
            {
                bus.Data = Result;
                output = false;
            }
        }
        public void Latch()
        {
            if (loadA) A = Signed(bus.Data);
            if (loadB) B = Signed(bus.Data);
            if (mul) Accumulator = (long)A * B;
            if (mac) Accumulator += (long)A * B;
            if (div) Accumulator = B == 0 ? (Accumulator < 0 ? int.MinValue : int.MaxValue) : (Accumulator << Shift) / B;
            Accumulator = Math.Clamp(Accumulator, int.MinValue, int.MaxValue);
            loadA = loadB = mul = mac = div = false;
        }

        public string DisplayName() { return deviceName; }
        public void Enable(string function)
        {
            switch (function)
            {
                case "loada": loadA = true; break;
                case "loadb": loadB = true; break;
                case "mul": mul = true; break;
                case "mac": mac = true; break;
                case "div": div = true; break;
                case "output": output = true; break;
                default:
                    throw new Exception("Unable to enable the unknown function: " + function);
            }
            if ((mul ? 1 : 0) + (mac ? 1 : 0) + (div ? 1 : 0) > 1) throw new Exception($"{deviceID}: only one of mul, mac and div can run in a tick.");
        }
        public string ID() { return deviceID; }
        public bool IsOutputEnabled() { return output; }
        public List<string> SignalLines()
        {
            return new List<string> { "loada", "loadb", "mul", "mac", "div", "output" };
        }
    }
}
