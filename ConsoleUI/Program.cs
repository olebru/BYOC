using System;
using System.IO;
using System.Linq;
using BYOCCore;
namespace ConsoleUI
{
    class Program
    {
        // Usage: ConsoleUI [machine.json microcode.tsv program.asm]
        static int Main(string[] args)
        {
            Machine c;
            try
            {
                c = args.Length == 3
                    ? Machine.FromJson(File.ReadAllText(args[0]), File.ReadAllText(args[1]), File.ReadAllText(args[2]))
                    : Machine.CreateDefault();
            }
            catch (Exception e) when (e is MachineDefinitionException || e is FormatException)
            {
                Console.Error.WriteLine(e.Message);
                return 1;
            }
            Console.WriteLine($"Machine: {c.Definition.Name}");
            Console.WriteLine($"Percent of decoderrom used: {(c.DecoderRom.OpCodeAddressSpaceUsedInPercent())}");
            Console.ReadLine();
            var mmu = c.Devices.OfType<MMU>().FirstOrDefault();
            foreach (var bus in c.Buses.Values)
            {
                Console.WriteLine($"{bus.ID}: {string.Join(", ", bus.devices.Select(d => d.ID()))}");
            }
            foreach(int cyclenum in c.Run())
            {
                foreach( var mi in c.CurrentMicroCode)
                {
                    Console.WriteLine($"{cyclenum}\t {mi.ToSingleLineString()}");
                }
                Console.WriteLine("---");
            if(cyclenum == 40) break;
            if (mmu != null) Console.WriteLine(mmu.RamBanks[0].ToString());
            }
            return 0;
        }
    }
}
