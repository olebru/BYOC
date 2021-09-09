using System;
using System.Collections.Generic;
namespace BYOCCore
{
   public interface IBusDevice
    {
        void Clk();
        string DisplayName();
        void Enable(string function);
        string ID();
        bool IsOutputEnabled();
        List<string> SignalLines();
    }
}
