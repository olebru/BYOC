using System;
using System.Collections.Generic;
using System.Text;
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
        private string newStatus = "00000000";
        private Register sta;
        private bool sub;
        char bit = '1';
        public ALU(string DeviceName, string DeviceID, Register rega, Register regb, Register regsta, Bus Bus)
        {
            deviceID = DeviceID;
            a = rega;
            b = regb;
            sta = regsta;
            bus = Bus;
            deviceName = DeviceName;
        }
        public void Clk()
        {
            if (add)
            {
                clearNewStatus();
                bus.Data = (byte)(a.Data + b.Data);
                if (a.Data + b.Data == 0)
                {
                    setZero();
                }
                if (a.Data + b.Data > 255)
                {
                    setCarry();
                }
                add = false;
                pushNewStatusToRegister();
            }
            if (sub)
            {
                clearNewStatus();
                var res = a.Data - b.Data;
                if (res == 0)
                {
                    setZero();
                }
                bus.Data = (byte)res;
                sub = false;
                pushNewStatusToRegister();
            }
            if (cmp)
            {
                clearNewStatus();
                if (a.Data - b.Data == 0)
                {
                    setZero();
                }
                if (a.Data - b.Data < 0)
                {
                    setNegative();
                }
                cmp = false;
                pushNewStatusToRegister();
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
        private void clearNewStatus()
        {
            newStatus = "00000000";
        }
        private void pushNewStatusToRegister()
        {
            this.sta.Data = Convert.ToByte(newStatus, 2);
        }
        private void setCarry()
        {
            StringBuilder sb = new StringBuilder(newStatus);
            sb[6] = bit;
            newStatus = sb.ToString();
        }
        private void setNegative()
        {
            StringBuilder sb = new StringBuilder(newStatus);
            sb[4] = bit;
            newStatus = sb.ToString();
        }
        private void setOverFlow()
        {
            StringBuilder sb = new StringBuilder(newStatus);
            sb[5] = bit;
            newStatus = sb.ToString();
        }
        private void setZero()
        {
            StringBuilder sb = new StringBuilder(newStatus);
            sb[7] = bit;
            newStatus = sb.ToString();
        }
    }
}
