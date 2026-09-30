using System;
using System.Collections.Generic;
namespace Exuarch.Core
{
    // A register connected to two buses, "a" and "b". It can latch from either bus and drive either bus,
    // so a value crosses between buses in two ticks (load on one side, output on the other).
    public class DualPortRegister : IBusDevice
    {
        public int Data;
        public readonly Bus BusA;
        public readonly Bus BusB;
        private string deviceID;
        private string deviceName;
        private bool loadA, loadB, outputA, outputB, reset, inc, dec;
        public DualPortRegister(string DeviceName, string DeviceID, Bus busA, Bus busB)
        {
            deviceName = DeviceName;
            deviceID = DeviceID;
            BusA = busA;
            BusB = busB;
        }
        public void Drive()
        {
            if (outputA)
            {
                BusA.Data = Data;
                outputA = false;
            }
            if (outputB)
            {
                BusB.Data = Data;
                outputB = false;
            }
        }
        public void Latch()
        {
            if (loadA && loadB)
            {
                throw new Exception($"{deviceID}: loada and loadb can not be enabled in the same tick.");
            }
            if (loadA) Data = BusA.Data;
            if (loadB) Data = BusB.Data;
            if (reset) Data = 0;
            if (inc) Data = (Data + 1) & Bus.Mask;
            if (dec) Data = (Data - 1) & Bus.Mask;
            loadA = loadB = reset = inc = dec = false;
        }
        public string DisplayName() { return deviceName; }
        public void Enable(string function)
        {
            switch (function)
            {
                case "loada": loadA = true; break;
                case "loadb": loadB = true; break;
                case "outputa": outputA = true; break;
                case "outputb": outputB = true; break;
                case "reset": reset = true; break;
                case "inc": inc = true; break;
                case "dec": dec = true; break;
                default:
                    throw new Exception("Unable to enable the unknown function: " + function);
            }
        }
        public string ID() { return deviceID; }
        public bool IsOutputEnabled() { return outputA || outputB; }
        public List<string> SignalLines()
        {
            return new List<string> { "loada", "loadb", "outputa", "outputb", "reset", "inc", "dec" };
        }
    }
}
