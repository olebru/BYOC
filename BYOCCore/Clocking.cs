using System;
using System.Collections.Generic;
using System.Linq;
namespace BYOCCore
{
    public static class Clocking
    {
        // The allocation free form used by Machine for every tick.
        public static void Tick(Bus[] buses, IBusDevice[] devices)
        {
            foreach (var bus in buses) bus.BeginTick();
            foreach (var device in devices)
            {
                foreach (var bus in buses) bus.ActiveDevice = device;
                device.Drive();
            }
            foreach (var bus in buses) bus.ActiveDevice = null;
            foreach (var device in devices) device.Latch();
            foreach (var bus in buses) bus.EndTick();
        }

        public static void Tick(IReadOnlyCollection<Bus> buses, IEnumerable<IBusDevice> devices)
        {
            var deviceList = devices.ToList();
            foreach (var bus in buses) bus.BeginTick();
            foreach (var device in deviceList)
            {
                foreach (var bus in buses) bus.ActiveDevice = device;
                device.Drive();
            }
            foreach (var bus in buses) bus.ActiveDevice = null;
            foreach (var device in deviceList)
            {
                device.Latch();
            }
            foreach (var bus in buses) bus.EndTick();
        }
    }
}
