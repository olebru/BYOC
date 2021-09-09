using System;
using System.Collections.Generic;
namespace BYOCCore
{
    public  class  Clock : IBusDevice
    {
        public int cycle = 0;
        private string deviceID;
        private bool halted = false;
        private string name;
        public Clock(string Name, string DeviceID)
        {
            deviceID = DeviceID;
            name = Name;
        }
        public void Clk()
        {
           cycle++;
        }
        public string DisplayName() { return name; }
        public void Enable(string function)
        {
            if (function == "disable")
            {
                halted = true;
            }
            else
            {
                throw new Exception($"Clock does not have a control line function called {function}");
            }
        }
        public string ID()
        {
            return deviceID;
        }
        public bool IsHalted() { return halted; }
        public bool IsOutputEnabled()
        {
            return false;
        }
        public List<String> SignalLines()
        {
            var lines = new List<String>();
            lines.Add("disable");
            return lines;
        }
    }
}
