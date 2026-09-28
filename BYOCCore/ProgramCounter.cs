using System;
using System.Collections.Generic;
namespace BYOCCore
{
    public class ProgramCounter : Register
    {
        private bool countEnabled = false;
        public ProgramCounter(string DeviceName, string DeviceID, Bus bus) : base(DeviceName,DeviceID,bus)
        {
        }
        public override void Latch()
        {
            if (countEnabled)
            {
                increment();
                countEnabled = false;
            }
            base.Latch();
        }
        public override void Enable(string function)
        {
            switch (function)
            {
                case "count":
                    countEnabled = true;
                    break;
                default:
                    base.Enable(function);
                    break;
            }
        }
        public override string OperationsOnNextClock()
        {
            string next = base.OperationsOnNextClock();
            if (countEnabled) next = $"{next}count";
            return next;
        }
        public override List<String> SignalLines()
        {
            var lines = base.SignalLines();
            lines.Add("count");
            return lines;
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
