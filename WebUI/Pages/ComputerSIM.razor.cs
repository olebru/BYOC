using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Linq;
using BYOCCore;

namespace WebUI.Pages
{
    public partial class ComputerSIM
    {


    BYOCCore.LowLevelPileOfPartsActingAsAMCU C;

    
    public ComputerSIM()
    {
        C = new BYOCCore.LowLevelPileOfPartsActingAsAMCU(BYOCCore.ExampleData.ROMDATA,BYOCCore.ExampleData.SRC);
    }

    public IEnumerable<Register> RegistersFromBus 
    {
        get 
        {
            return C.bus.devices.OfType<Register>();
        }
    }

    
        public MMU MMUFromBus 
    {
        get 
        {
            return C.bus.devices.OfType<MMU>().FirstOrDefault();
        }
    }    
    public RamModule RamModuleFromBus 
    {
        get 
        {
            return C.bus.devices.OfType<RamModule>().FirstOrDefault();
        }
    }

    public  void Run()
    {
        C.SingleStepClk();
    }
}
}