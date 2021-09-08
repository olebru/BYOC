using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace BYOCCore
{
    public class LowLevelPileOfPartsActingAsAMCU
    {
        public Bus bus;
        public DecoderRom decoderRom;
        public Assembler assembler;
        public byte[] programByteCode;
        public List<MicroInstruction> currentMicroCode;
        

        public LowLevelPileOfPartsActingAsAMCU(string romPath,string srcPath)
        {

            decoderRom = new DecoderRom(romPath);
            assembler = new Assembler(decoderRom);
            bus = new Bus();
            bus.NumberFormat = "X2";
            bus.DecoderROM = decoderRom;
          
            var rega = new Register("REGA", "rega", bus);
            var regb = new Register("REGB", "regb", bus);
            var regc = new Register("REGC", "regc", bus);
            var regs = new Register("REGSWAP", "regs", bus);
            var regsp = new Register("REGSP", "regsp", bus, 255);
            var regsta = new StatusRegister("STATUS", "regsta", bus);
            var regi = new InstructionRegister("REGINSTR", "regi", bus, regsta);
            var pc = new ProgramCounter("PCOUNT", "pc", bus);
            var mem = new RamModule("BASEMEM", "mem", bus, regsp, pc);
            var mmu = new MMU("MMU", "mmu", bus);
            var clk = new Clock("clk", "Clock");
            var alu = new ALU("ALU ", "alu", rega, regb, regsta, bus);


            bus.devices.Add(regi);
            bus.devices.Add(pc);
            bus.devices.Add(regsp);
            bus.devices.Add(rega);
            bus.devices.Add(regb);
            bus.devices.Add(regc);
            bus.devices.Add(regs);
            bus.devices.Add(alu);
            bus.devices.Add(regsta);
            bus.devices.Add(mem);
            bus.devices.Add(mmu);
            bus.devices.Add(clk);

          
            programByteCode = assembler.Assemble(srcPath);
            mem.LoadBytes(programByteCode);

               
        }  
         
            public IEnumerable<List<MicroInstruction>> RunClk()
            {
                var clk = (Clock)bus.devices.Single(c => c.ID() == "clk");

                while(!clk.IsHalted())
                {
                currentMicroCode = decoderRom.FetchInstruction(((Register)bus.devices.Single(d => d.ID() == "regsta")).Data,((Register)bus.devices.Single(d => d.ID() == "regi")).Data);
                foreach (var microCode in currentMicroCode)
                {
                    var dev = bus.devices.Single(d => d.ID() == microCode.DeviceID);
                    dev.Enable(microCode.Function);
                }
                
                bus.Clk();
                  
                yield return currentMicroCode;
}

            }
                  
    }
}
