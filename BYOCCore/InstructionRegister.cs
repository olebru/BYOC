using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BYOCCore
{
   public  class InstructionRegister : Register
    {
        public string Instruction = "N/A";
        public InstructionRegister(string DeviceName, string DeviceID, Bus bus) : base(DeviceName, DeviceID, bus)
        {
        }
        // Acts as the micro step counter: advances every tick unless loaded or reset.
        public override void Latch()
        {
            increment();
            base.Latch();
        }
        public override string ToString(int firstColumnPaddedWidth)
        {
            return $"{base.deviceName} Value".PadRight(firstColumnPaddedWidth, ' ') + $"= {Data.ToString(base.connectedBus.NumberFormat)}";
        }
        private void increment()
        {
            if (Data == byte.MaxValue)
            {
                Data = 0;
            }
            else
            {
                Data++;
            }
        }
    }
}
