using System;
using System.Collections.Generic;
namespace BYOCCore
{
   public  class Register : IBusDevice
    {
        // A 16 bit word; inc and dec wrap around.
        public int Data = 0;
        protected Bus connectedBus;
        protected bool dec = false;
        protected string deviceID = "";
        protected string deviceName = "";
        protected bool inc = false;
        protected bool loadEnabled = false;
        protected  bool outputEnabled = false;
        protected bool reset = false;
        public Register(string DeviceName, string DeviceID, Bus ConnectedBus, int InitialValue = 0)
        {
            deviceName = DeviceName;
            deviceID = DeviceID;
            connectedBus = ConnectedBus;
            Data = InitialValue & Bus.Mask;
        }
        public virtual void Drive()
        {
            if (outputEnabled)
            {
                connectedBus.Data = Data;
                outputEnabled = false;
            }
        }
        public virtual void Latch()
        {
            if (loadEnabled)
            {
                Data = connectedBus.Data;
                loadEnabled = false;
            }
            if (reset)
            {
                Data = 0;
                reset = false;
            }
            if (inc)
            {
                Data = (Data + 1) & Bus.Mask;
                inc = false;
            }
            if (dec)
            {
                Data = (Data - 1) & Bus.Mask;
                dec = false;
            }
        }
        public string DisplayName() { return deviceName; }
        public virtual void Enable(string function)
        {
            switch (function)
            {
                case "output":
                    outputEnabled = true;
                    break;
                case "load":
                    loadEnabled = true;
                    break;
                case "reset":
                    reset = true;
                    break;
                case "inc":
                    inc = true;
                    break;
                case "dec":
                    dec = true;
                    break;
                default:
                    throw new Exception("Unable to enable the unknown function: " + function);
            }
        }
        public string ID() { return deviceID; }
        public virtual bool IsOutputEnabled()
        {
            return outputEnabled;
        }
        public virtual string OperationsOnNextClock()
        {
            string next = "";
            if (loadEnabled) next = $"{next}load";
            if (outputEnabled) next = $"{next}output";
            if (reset) next = $"{next}reset";
            if (inc) next = $"{next}inc";
            if (dec) next = $"{next}dec";
            return $"{next}";
        }
        public virtual List<String> SignalLines()
        {
            var lines = new List<String>();
            lines.Add("output");
            lines.Add("load");
            lines.Add("reset");
            lines.Add("inc");
            lines.Add("dec");
            return lines;
        }
        public virtual string ToString(int firstColumnPaddedWidth)
        {
            return $"{deviceName}".PadRight(firstColumnPaddedWidth, ' ') + $"= {Data.ToString("X4")}";
        }
    }
}
