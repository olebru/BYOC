using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Exuarch.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Exuarch.Web.Components;

namespace Exuarch.Web.Pages
{
    public partial class ComputerSIM
    {
        private static readonly string[] Tabs = { "Design", "Microcode", "Program", "JSON", "Run" };
        private static readonly DeviceRegistry Registry = DeviceRegistry.CreateDefault();

        [Microsoft.AspNetCore.Components.Inject] private IJSRuntime JS { get; set; }

        private string ActiveTab = "Design";
        // The package the machine and the example programs came from, as it was loaded.
        private MachinePackage Package;
        private string PackageName { get { return Package.Name; } }
        private string PackageError;
        private Machine C;
        // The whole machine, microcode included (Definition.Decoder.Microcode).
        private MachineDefinition Definition;
        private string DefinitionJson;
        private string Program;
        private IReadOnlyList<string> DefinitionErrors = Array.Empty<string>();
        private IReadOnlyList<MicrocodeDiagnostic> MicrocodeDiagnostics = Array.Empty<MicrocodeDiagnostic>();
        private List<string> ParseErrors = new List<string>();
        private List<string> ProgramErrors = new List<string>();
        private readonly EditHistory History;
        private string focusDevice;
        private string focusMnemonic;
        private int focusStep;
        private int focusVersion;

        public ComputerSIM()
        {
            History = new EditHistory(() => Definition.ToJson(), json =>
            {
                ApplyDefinition(MachineDefinition.FromJson(json));
                StateHasChanged();
                return System.Threading.Tasks.Task.CompletedTask;
            });
            LoadPackage(BuiltInPackages.Get(BuiltInPackages.Default.Name));
        }

        // Cells in the program memory, from the machine when it builds, otherwise its size parameter.
        private int ProgramMemorySize
        {
            get
            {
                if (C?.Definition.ProgramMemory != null && C.Device<MemoryModule>(C.Definition.ProgramMemory) is MemoryModule memory) return memory.Size;
                var device = Definition.FindDevice(Definition.ProgramMemory ?? "");
                return device != null && device.Parameters.TryGetValue("size", out var size) && size.TryGetInt32(out var cells) ? cells : MemoryModule.DefaultSize;
            }
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

        private void ApplyDefinition(MachineDefinition definition)
        {
            Definition = definition;
            Definition.EnsureLayout();
            DefinitionJson = Definition.ToJson();
            ParseErrors = new List<string>();
            Rebuild();
        }

        private void LoadPackage(MachinePackage package)
        {
            Package = package;
            PackageError = null;
            Program = package.Programs.FirstOrDefault()?.Source ?? "";
            ApplyDefinition(package.Machine.Clone());
        }

        // Switching or resetting the machine is undoable, like any other edit of it.
        private void SelectPackage(Microsoft.AspNetCore.Components.ChangeEventArgs e)
        {
            var name = e.Value?.ToString();
            if (name == PackageName || !BuiltInPackages.All.Any(p => p.Name == name)) return;
            History.Record();
            LoadPackage(BuiltInPackages.Get(name));
        }
        // ---- New machine ----
        private static readonly (string Value, string Title, string Description)[] NewStarts =
        {
            ("minimal", "Minimal CPU", "A bus, program counter, memory, instruction register, status register and clock, with fetch, NOP, JMP and HLT. It runs straight away."),
            ("empty", "Empty", "One bus and nothing else. Add the devices, decoder and microcode yourself."),
            ("copy", "Copy of the current machine", "Everything in the machine you have open now, with its programs, under the new name."),
        };
        private bool newDialog;
        private string newName = "My machine";
        private string newStart = "minimal";
        private Microsoft.AspNetCore.Components.ElementReference newNameInput;
        private bool focusNewName;

        private void OpenNewDialog()
        {
            newDialog = true;
            focusNewName = true;
        }
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (focusNewName && newDialog)
            {
                focusNewName = false;
                await newNameInput.FocusAsync();
            }
        }
        private void NewDialogKey(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
        {
            if (e.Key == "Escape") newDialog = false;
            else if (e.Key == "Enter") CreateMachine();
        }
        // Undoable, like switching packages. A name already used by a built in package gets a number.
        private void CreateMachine()
        {
            var name = string.IsNullOrWhiteSpace(newName) ? "My machine" : newName.Trim();
            var unique = name;
            for (int n = 2; BuiltInPackages.All.Any(p => p.Name == unique); n++) unique = $"{name} {n}";
            var package = newStart switch
            {
                "empty" => MachineTemplates.Empty(unique),
                "copy" => MachineTemplates.CopyOf(new MachinePackage { Name = Package.Name, Description = Package.Description, Machine = Definition.Clone(), Programs = Package.Programs }, unique),
                _ => MachineTemplates.Minimal(unique),
            };
            History.Record();
            LoadPackage(package);
            if (newStart == "minimal") { Program = MachineTemplates.StarterProgram; Rebuild(); }
            newDialog = false;
            ActiveTab = "Design";
        }

        private void ResetPackage()
        {
            History.Record();
            LoadPackage(Package.Clone());
        }

        private async Task OpenPackage(InputFileChangeEventArgs e)
        {
            try
            {
                using var reader = new StreamReader(e.File.OpenReadStream(maxAllowedSize: 16 * 1024 * 1024));
                var package = MachinePackage.FromJson(await reader.ReadToEndAsync());
                if (string.IsNullOrWhiteSpace(package.Name)) package.Name = Path.GetFileNameWithoutExtension(e.File.Name);
                History.Record();
                LoadPackage(package);
            }
            catch (Exception ex) when (ex is MachineDefinitionException || ex is IOException)
            {
                PackageError = $"Could not open {e.File.Name}: {ex.Message}";
            }
        }

        // The machine as it is now, with the package's programs; a program that is not one of them is added.
        private async Task DownloadPackage()
        {
            var package = Package.Clone();
            package.Machine = Definition.Clone();
            if (!string.IsNullOrWhiteSpace(Program) && !package.Programs.Any(p => p.Source == Program))
                package.Programs.Add(new PackageProgram { Name = "My program", Source = Program });
            await JS.InvokeVoidAsync("exuarchEditor.download", $"{package.Name}.json", package.ToJson());
        }

        private void OnDesignChanged(MachineDefinition definition)
        {
            Definition = definition;
            DefinitionJson = definition.ToJson();
            Rebuild();
        }

        private void OnMicrocodeChanged(MicrocodeDefinition microcode)
        {
            Definition.Decoder ??= new DecoderDefinition();
            Definition.Decoder.Microcode = microcode;
            DefinitionJson = Definition.ToJson();
            Rebuild();
        }

        private void StartMicrocode()
        {
            History.Record();
            OnMicrocodeChanged(new MicrocodeDefinition { Name = Definition.Name, Fetch = new InstructionDefinition { Mnemonic = "FTC", Steps = { new MicroStep() } } });
        }

        // A JSON edit that parses replaces the model; one that does not keeps the last good model in the editors.
        private void OnJsonChanged(string json)
        {
            DefinitionJson = json;
            try
            {
                var parsed = MachineDefinition.FromJson(json);
                History.Record();
                Definition = parsed;
                Definition.EnsureLayout();
                ParseErrors = new List<string>();
                Rebuild();
            }
            catch (MachineDefinitionException e)
            {
                C = null;
                ParseErrors = e.Errors.ToList();
            }
        }

        private void LoadExample(string source)
        {
            Program = source;
            Rebuild();
        }

        private void OnProgramChanged(string program)
        {
            Program = program;
            Rebuild();
        }

        private void OpenMicrocode((string Mnemonic, int Step) target)
        {
            focusMnemonic = target.Mnemonic;
            focusStep = target.Step;
            focusVersion++;
            ActiveTab = "Microcode";
        }

        private void OpenDevice(string deviceId)
        {
            focusDevice = deviceId;
            focusVersion++;
            ActiveTab = "Design";
        }

        // Validates the definition, then the microcode against it, then builds a machine to assemble the program.
        private void Rebuild()
        {
            DefinitionErrors = Machine.ValidateDefinition(Definition, Registry);
            var microcode = Definition.Decoder?.Microcode;
            MicrocodeDiagnostics = microcode == null
                ? new[] { new MicrocodeDiagnostic { Severity = DiagnosticSeverity.Error, Message = "the machine has no microcode (decoder.microcode)." } }
                : MicrocodeValidator.Validate(microcode, Definition, Registry);
            ProgramErrors = new List<string>();
            C = null;
            if (ParseErrors.Count > 0 || DefinitionErrors.Count > 0 || MicrocodeErrorCount > 0) return;
            try
            {
                C = new Machine(Definition.Clone(), Program, Registry);
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
