using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BYOCCore;

namespace BYOC.WebUI.Pages
{
    public partial class ComputerSIM
    {

        public string TextTest 
        { get; set; }

        public void Run()
        {

            try
            {
                var c = new BYOCCore.LowLevelPileOfPartsActingAsAMCU(BYOCCore.ExampleData.ROMDATA,BYOCCore.ExampleData.SRC);
                // Console.WriteLine($"Percent of decoderrom used: {(c.decoderRom.OpCodeAddressSpaceUsedInPercent())}");
                //   Console.ReadLine();
                var mem = (RamModule)c.bus.devices.Single(d => d.ID() == "mem");
                var mmu = (MMU)c.bus.devices.Single(d => d.ID() == "mmu");
                foreach (var device in c.bus.devices)
                {
                    //     Console.WriteLine(device.ID());
                }
                foreach (int cyclenum in c.RunClk())
                {
                    foreach (var mi in c.currentMicroCode)
                    {
                        //       Console.WriteLine($"{cyclenum}\t {mi.ToSingleLineString()}");
                        TextTest = c.currentMicroCode.ToString();
                    }
                    //Console.WriteLine("---");
                    if (cyclenum == 40) break;
                    //Console.WriteLine(mmu.RamBanks[0].ToString()); }

                }
            }
            catch (Exception e)
            {

                throw e;
            }

        }
    }
}
