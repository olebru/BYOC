using System; 
using System.Collections.Generic;
using BYOCCore;
using System.Linq;

namespace ConsoleUI
{
    class Program
    {
        static void Main(string[] args)
        {
   
      
            var c = new BYOCCore.LowLevelPileOfPartsActingAsAMCU(   "./ExampleFiles/examplerom.csv","./ExampleFiles/examplesrc.asm");

            foreach (var device in c.bus.devices)
            {

                Console.WriteLine(device.ID());
            }
            
            
        }
    }
}
