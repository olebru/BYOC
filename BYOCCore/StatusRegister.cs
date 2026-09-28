using System;
namespace BYOCCore
{
   public  class StatusRegister : Register
    {
        public const byte ZeroFlag = 1;
        public const byte CarryFlag = 2;
        public const byte OverflowFlag = 4;
        public const byte NegativeFlag = 8;
        public StatusRegister(string DeviceName, string DeviceID, Bus ConnectedBus) : base(DeviceName, DeviceID, ConnectedBus)
        {
        }
        public bool Carry2 { get { return (Data & CarryFlag) != 0; } }
        public bool Negative8 { get { return (Data & NegativeFlag) != 0; } }
        public bool Overflow4 { get { return (Data & OverflowFlag) != 0; } }
        public bool Zero1 { get { return (Data & ZeroFlag) != 0; } }
        public override string ToString()
        {
            return deviceName + " Value = " + Data.ToString(connectedBus.NumberFormat) + Environment.NewLine;
        }
    }
}
