using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BYOCCore
{
   public  class InstructionRegister : Register, IBusDevice
    {
        public string Instruction = "N/A";
        public InstructionRegister(string DeviceName, string DeviceID, Bus bus) : base(DeviceName, DeviceID, bus)
        {
        }
        public new void Clk()
        {
            increment();
            base.Clk();
        }
        public new string OperationsOnNextClock()
        {
            string next = "";
            if (loadEnabled) next = $"{next}load";
            if (outputEnabled) next = $"{next}output";
            if (reset) next = $"{next}reset";
            if (inc) next = $"{next}inc";
            if (dec) next = $"{next}dec";
            return $"{next}";
        }
        public new string ToString(int firstColumnPaddedWidth)
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
