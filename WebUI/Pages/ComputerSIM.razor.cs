using System;
using System.Collections.Generic;
using System.Linq;
using BYOCCore;

namespace WebUI.Pages
{
    public partial class ComputerSIM
    {
        private const int MaxRunCycles = 10000;

        private Machine C;
        private string DefinitionJson;
        private string Microcode;
        private string Program;
        private List<string> Errors = new List<string>();

        public ComputerSIM()
        {
            ResetToDefault();
        }

        private RomModule ProgramMemory
        {
            get
            {
                return C.Definition.ProgramMemory == null ? null : C.Device<RomModule>(C.Definition.ProgramMemory);
            }
        }

        private void ResetToDefault()
        {
            DefinitionJson = ExampleData.MACHINE;
            Microcode = ExampleData.ROMDATA;
            Program = ExampleData.SRC;
            Build();
        }

        private void Build()
        {
            Errors = new List<string>();
            try
            {
                C = Machine.FromJson(DefinitionJson, Microcode, Program);
            }
            catch (MachineDefinitionException e)
            {
                C = null;
                Errors.AddRange(e.Errors);
            }
            catch (Exception e)
            {
                C = null;
                Errors.Add(e.Message);
            }
        }

        private void Step()
        {
            StepMany(1);
        }

        private void StepMany(int cycles)
        {
            try
            {
                for (int i = 0; i < cycles && !C.IsHalted; i++)
                {
                    C.SingleStep();
                }
            }
            catch (Exception e)
            {
                Errors = new List<string> { $"Cycle {C.Cycles}: {e.Message}" };
            }
        }
    }
}
