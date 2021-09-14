using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BYOCCore;
namespace ConsoleUI
{
    class Program
    {
        static void Main(string[] args)
        {
            var c = new BYOCCore.LowLevelPileOfPartsActingAsAMCU(BYOCCore.ExampleData.ROMDATA,BYOCCore.ExampleData.SRC);
            Console.WriteLine($"Percent of decoderrom used: {(c.decoderRom.OpCodeAddressSpaceUsedInPercent())}");
            Console.ReadLine();
            var mem = (RamModule)c.bus.devices.Single(d => d.ID() == "mem");
            var mmu = (MMU)c.bus.devices.Single(d => d.ID() == "mmu");
            foreach (var device in c.bus.devices)
            {
                Console.WriteLine(device.ID());
            }
            foreach(int cyclenum in c.RunClk())
            {
                foreach( var mi in c.currentMicroCode)
                {
                    Console.WriteLine($"{cyclenum}\t {mi.ToSingleLineString()}");
                }
                Console.WriteLine("---");
            if(cyclenum == 40) break;
            Console.WriteLine(mmu.RamBanks[0].ToString());

            
            }
        }
    }
}