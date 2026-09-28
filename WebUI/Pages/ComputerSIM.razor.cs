using System;
using System.Collections.Generic;
using System.Linq;
using BYOCCore;

namespace WebUI.Pages
{
    public partial class ComputerSIM
    {
        private static readonly string[] Tabs = { "Design", "Microcode", "Program", "JSON", "Run" };
        private static readonly DeviceRegistry Registry = DeviceRegistry.CreateDefault();

        private string ActiveTab = "Design";
        private Machine C;
        private MachineDefinition Definition;
        private string DefinitionJson;
        private MicrocodeDefinition Microcode;
        private string MicrocodeJson;
        private string Program;
        private IReadOnlyList<string> DefinitionErrors = Array.Empty<string>();
        private IReadOnlyList<MicrocodeDiagnostic> MicrocodeDiagnostics = Array.Empty<MicrocodeDiagnostic>();
        private List<string> ParseErrors = new List<string>();
        private List<string> ProgramErrors = new List<string>();

        public ComputerSIM()
        {
            ResetToDefault();
        }

        private int MicrocodeErrorCount { get { return MicrocodeDiagnostics.Count(d => d.Severity == DiagnosticSeverity.Error); } }
        private int MicrocodeWarningCount { get { return MicrocodeDiagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning); } }
        private IEnumerable<string> AllErrors
        {
            get
            {
                return ParseErrors.Concat(DefinitionErrors)
                    .Concat(MicrocodeDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))
                    .Concat(ProgramErrors);
            }
        }

        private void ResetToDefault()
        {
            Definition = MachineDefinition.FromJson(ExampleData.MACHINE);
            Definition.EnsureLayout();
            DefinitionJson = Definition.ToJson();
            Microcode = MicrocodeDefinition.FromJson(ExampleData.MICROCODE);
            MicrocodeJson = Microcode.ToJson();
            Program = ExampleData.SRC;
            ParseErrors.Clear();
            Rebuild();
        }

        private void OnDesignChanged(MachineDefinition definition)
        {
            Definition = definition;
            DefinitionJson = definition.ToJson();
            Rebuild();
        }

        private void OnMicrocodeChanged(MicrocodeDefinition microcode)
        {
            Microcode = microcode;
            MicrocodeJson = microcode.ToJson();
            Rebuild();
        }

        private void OnJsonChanged(string json)
        {
            DefinitionJson = json;
            ParseText();
        }

        private void OnMicrocodeJsonChanged(string json)
        {
            MicrocodeJson = json;
            ParseText();
        }

        // Parses both JSON editors; a file that does not parse keeps the last good model in the visual editors.
        private void ParseText()
        {
            ParseErrors = new List<string>();
            try { Definition = MachineDefinition.FromJson(DefinitionJson); Definition.EnsureLayout(); }
            catch (MachineDefinitionException e) { ParseErrors.AddRange(e.Errors); }
            try { Microcode = MicrocodeDefinition.Parse(MicrocodeJson); }
            catch (Exception e) when (e is MachineDefinitionException || e is FormatException) { ParseErrors.Add(e.Message); }
            Rebuild();
        }

        private void OnProgramChanged(string program)
        {
            Program = program;
            Rebuild();
        }

        // Validates the definition, then the microcode against it, then builds a machine to assemble the program.
        private void Rebuild()
        {
            DefinitionErrors = Machine.ValidateDefinition(Definition, Registry);
            MicrocodeDiagnostics = MicrocodeValidator.Validate(Microcode, Definition, Registry);
            ProgramErrors = new List<string>();
            C = null;
            if (ParseErrors.Count > 0 || DefinitionErrors.Count > 0 || MicrocodeErrorCount > 0) return;
            try
            {
                C = new Machine(Definition.Clone(), Microcode.Clone(), Program, Registry);
            }
            catch (MachineDefinitionException e)
            {
                ProgramErrors.AddRange(e.Errors);
            }
            catch (Exception e)
            {
                ProgramErrors.Add(e.Message);
            }
        }
    }
}
