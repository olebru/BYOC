using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BYOCCore;

namespace WebUI.Pages
{
    public partial class ComputerSIM
    {

        public string TextTest 
        { get; set; }

        BYOCCore.LowLevelPileOfPartsActingAsAMCU C;

    
    public ComputerSIM()
    {

        C = new BYOCCore.LowLevelPileOfPartsActingAsAMCU(BYOCCore.ExampleData.ROMDATA,BYOCCore.ExampleData.SRC);
    

    }
        public void Run()
        {
            C.SingleStepClk();

                    foreach (var mi in C.currentMicroCode)
                    {
                        //       Console.WriteLine($"{cyclenum}\t {mi.ToSingleLineString()}");
                        TextTest = mi.ToString();
                    }
                    
            
        }
    }
}
