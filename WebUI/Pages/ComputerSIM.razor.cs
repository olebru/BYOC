using System;
using System.Collections.Generic;
using System.Linq;
using BYOCCore;

namespace WebUI.Pages
{
    public partial class ComputerSIM
    {
        private const int MaxRunCycles = 10000;
        private static readonly string[] Tabs = { "Design", "Microcode", "Program", "JSON", "Run" };

        private string ActiveTab = "Design";
        private Machine C;
        private MachineDefinition Definition;
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
            Definition = MachineDefinition.FromJson(ExampleData.MACHINE);
            DefinitionJson = Definition.ToJson();
            Microcode = ExampleData.ROMDATA;
            Program = ExampleData.SRC;
            Rebuild();
        }

        private void OnDesignChanged(MachineDefinition definition)
        {
            Definition = definition;
            DefinitionJson = definition.ToJson();
            Rebuild();
        }

        private void OnJsonChanged(string json)
        {
            DefinitionJson = json;
            try
            {
                Definition = MachineDefinition.FromJson(json);
                Rebuild();
            }
            catch (MachineDefinitionException e)
            {
                C = null;
                Errors = e.Errors.ToList();
            }
        }

        private void OnMicrocodeChanged(string microcode)
        {
            Microcode = microcode;
            Rebuild();
        }

        private void OnProgramChanged(string program)
        {
            Program = program;
            Rebuild();
        }

        // Builds a fresh machine from a copy of the definition, so the designer can keep editing its own.
        private void Rebuild()
        {
            Errors = new List<string>();
            try
            {
                C = new Machine(Definition.Clone(), Microcode, Program);
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
