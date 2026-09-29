using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BYOCCore;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace WebUI.Components
{
    public partial class MicrocodeEditor
    {
        private static readonly (string Name, int Flag)[] Flags =
        {
            ("N", StatusRegister.NegativeFlag), ("V", StatusRegister.OverflowFlag), ("C", StatusRegister.CarryFlag), ("Z", StatusRegister.ZeroFlag),
        };

        [Inject] private IJSRuntime JS { get; set; }

        [Parameter] public MicrocodeDefinition Microcode { get; set; }
        [Parameter] public EventCallback<MicrocodeDefinition> MicrocodeChanged { get; set; }
        [Parameter] public MachineDefinition Machine { get; set; }
        [Parameter] public IReadOnlyList<MicrocodeDiagnostic> Diagnostics { get; set; } = Array.Empty<MicrocodeDiagnostic>();
        [Parameter] public DeviceRegistry Registry { get; set; } = DeviceRegistry.CreateDefault();
        // Undo history shared with the machine editor.
        [Parameter] public EditHistory History { get; set; }
        // Asks the page to show a device in the machine editor.
        [Parameter] public EventCallback<string> OnOpenDevice { get; set; }
        // Selects this instruction and step when FocusVersion changes.
        [Parameter] public string FocusMnemonic { get; set; }
        [Parameter] public int FocusStep { get; set; }
        [Parameter] public int FocusVersion { get; set; }

        private int focusVersionSeen;
        private string selectedMnemonic;
        private int activeStep;
        private string filter = "";
        private string paletteFilter = "";
        private string renameError;
        private string importError;
        private bool showProblems;
        private int? previewStatus;
        private string newSignal = "";
        private DragItem drag;

        private enum DragKind { Instruction, Step, Signal, PaletteSignal }

        private class DragItem
        {
            public DragKind Kind;
            public int Index;
            public int Step;
            public string Signal;
        }

        protected override void OnParametersSet()
        {
            if (Microcode == null) return;
            if (FocusVersion != focusVersionSeen)
            {
                focusVersionSeen = FocusVersion;
                if (FocusMnemonic != null && Microcode.FindInstruction(FocusMnemonic) != null)
                {
                    selectedMnemonic = FocusMnemonic;
                    activeStep = FocusStep;
                    filter = "";
                    return;
                }
            }
            if (selectedMnemonic == null || Microcode.FindInstruction(selectedMnemonic) == null)
            {
                selectedMnemonic = Microcode.Fetch?.Mnemonic ?? Microcode.Instructions.FirstOrDefault()?.Mnemonic;
                activeStep = 0;
            }
        }

        private InstructionDefinition Selected { get { return selectedMnemonic == null ? null : Microcode.FindInstruction(selectedMnemonic); } }
        private bool SelectedIsFetch { get { return Selected != null && Selected == Microcode.Fetch; } }

        // ---- Machine knowledge ----

        private DeviceTypeInfo InfoFor(string deviceId)
        {
            var device = Machine?.FindDevice(deviceId);
            return device == null ? null : Registry.Info(device.Type);
        }
        private ControlLineInfo LineFor(string signalText)
        {
            if (!Signal.TryParse(signalText, out var signal)) return null;
            return InfoFor(signal.Device)?.ControlLines.FirstOrDefault(l => l.Name == signal.Line);
        }
        private string SignalColor(string signalText)
        {
            if (!Signal.TryParse(signalText, out var signal)) return Palette.Error;
            var info = InfoFor(signal.Device);
            return Palette.Category(info);
        }
        private IEnumerable<string> AllSignals()
        {
            if (Machine == null) yield break;
            foreach (var device in Machine.Devices)
            {
                var info = Registry.Info(device.Type);
                if (info == null) continue;
                foreach (var line in info.ControlLines) yield return $"{device.Id}.{line.Name}";
            }
        }
        private string SignalTitle(string signalText)
        {
            var line = LineFor(signalText);
            if (line == null) return signalText;
            return SignalDescription(signalText, line) + " (double-click to show the device)";
        }
        private string SignalDescription(string signalText, ControlLineInfo line)
        {
            var bus = "";
            if (Signal.TryParse(signalText, out var signal))
            {
                var device = Machine.FindDevice(signal.Device);
                if (line.Drives != null) bus = $" · drives bus {device.GetPortBus(line.Drives) ?? "(not connected)"}";
                if (line.Reads != null) bus = $" · reads bus {device.GetPortBus(line.Reads) ?? "(not connected)"}";
            }
            return $"{signalText}: {line.Description}{bus}";
        }
        private static string DirectionMark(ControlLineInfo line)
        {
            return line?.Drives != null ? "▲" : line?.Reads != null ? "▼" : "";
        }

        // For each bus touched in the step: the drivers and the readers.
        private List<(string Bus, List<string> Drivers, List<string> Readers)> BusActivity(MicroStep step)
        {
            var activity = new Dictionary<string, (List<string> Drivers, List<string> Readers)>();
            foreach (var text in step.Signals)
            {
                var line = LineFor(text);
                if (line == null || !Signal.TryParse(text, out var signal)) continue;
                var device = Machine.FindDevice(signal.Device);
                void Note(string port, bool drives)
                {
                    var busId = port == null ? null : device.GetPortBus(port);
                    if (busId == null) return;
                    if (!activity.TryGetValue(busId, out var entry)) activity[busId] = entry = (new List<string>(), new List<string>());
                    (drives ? entry.Drivers : entry.Readers).Add(signal.Device);
                }
                Note(line.Drives, true);
                Note(line.Reads, false);
            }
            return activity.Select(a => (a.Key, a.Value.Drivers, a.Value.Readers)).ToList();
        }

        private const int AddressSpace = DecoderRom.AddressSpace;

        private int OpCodesUsed
        {
            get
            {
                return Microcode.AllInstructions.Sum(BlockSize);
            }
        }
        // Micro step addresses an instruction takes: its longest flag variant, at least one.
        private static int BlockSize(InstructionDefinition instruction)
        {
            return Math.Max(1, Enumerable.Range(0, DecoderRom.StatusVariants).Max(s => instruction.StepsFor(s).Count));
        }
        // The opcode the decoder ROM gives each instruction: blocks are laid out in order from 0.
        private Dictionary<InstructionDefinition, string> OpcodeMap()
        {
            var opcodes = new Dictionary<InstructionDefinition, string>();
            int address = 0;
            foreach (var instruction in Microcode.AllInstructions)
            {
                opcodes[instruction] = address.ToString("X4");
                address += BlockSize(instruction);
            }
            return opcodes;
        }

        // ---- Diagnostics ----

        private IEnumerable<MicrocodeDiagnostic> DiagnosticsFor(InstructionDefinition instruction, int? step = null)
        {
            return Diagnostics.Where(d => d.Instruction == instruction.Mnemonic && (step == null || d.Step == step));
        }
        private string Marker(InstructionDefinition instruction)
        {
            var diagnostics = DiagnosticsFor(instruction).ToList();
            if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)) return "error";
            return diagnostics.Count > 0 ? "warning" : "";
        }
        private bool SignalHasProblem(InstructionDefinition instruction, int step, string signal)
        {
            return Diagnostics.Any(d => d.Instruction == instruction.Mnemonic && d.Step == step && d.Signal == signal);
        }
        private int ErrorCount { get { return Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error); } }
        private int WarningCount { get { return Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning); } }

        // ---- Editing ----

        private async Task Mutate(Action change)
        {
            History?.Record();
            change();
            await MicrocodeChanged.InvokeAsync(Microcode);
        }
        private Task Undo() { return History?.Undo() ?? Task.CompletedTask; }
        private Task Redo() { return History?.Redo() ?? Task.CompletedTask; }
        private Task OpenDevice(string signalText)
        {
            return Signal.TryParse(signalText, out var signal) && Machine?.FindDevice(signal.Device) != null
                ? OnOpenDevice.InvokeAsync(signal.Device)
                : Task.CompletedTask;
        }
        private async Task Replace(MicrocodeDefinition microcode)
        {
            Microcode = microcode;
            if (microcode.FindInstruction(selectedMnemonic) == null) selectedMnemonic = microcode.Fetch?.Mnemonic;
            await MicrocodeChanged.InvokeAsync(microcode);
        }

        private void Select(InstructionDefinition instruction)
        {
            selectedMnemonic = instruction.Mnemonic;
            activeStep = 0;
            renameError = null;
            newSignal = "";
        }

        private async Task AddInstruction()
        {
            var mnemonic = "NEW";
            for (int i = 2; Microcode.FindInstruction(mnemonic) != null; i++) mnemonic = $"NEW{i}";
            var stepRegister = Machine?.Decoder?.InstructionRegister;
            var instruction = new InstructionDefinition { Mnemonic = mnemonic, Operands = 0, Steps = { new MicroStep() } };
            if (stepRegister != null) instruction.Steps[0].Signals.Add($"{stepRegister}.reset");
            await Mutate(() => Microcode.Instructions.Add(instruction));
            Select(instruction);
        }
        private async Task DuplicateInstruction()
        {
            var source = Selected;
            if (source == null || SelectedIsFetch) return;
            var copy = Microcode.Clone().FindInstruction(source.Mnemonic);
            var mnemonic = source.Mnemonic + "2";
            for (int i = 3; Microcode.FindInstruction(mnemonic) != null; i++) mnemonic = source.Mnemonic + i;
            copy.Mnemonic = mnemonic;
            await Mutate(() => Microcode.Instructions.Insert(Microcode.Instructions.IndexOf(source) + 1, copy));
            Select(copy);
        }
        private async Task DeleteInstruction()
        {
            var instruction = Selected;
            if (instruction == null || SelectedIsFetch) return;
            var index = Microcode.Instructions.IndexOf(instruction);
            await Mutate(() => Microcode.Instructions.Remove(instruction));
            var next = Microcode.Instructions.ElementAtOrDefault(Math.Min(index, Microcode.Instructions.Count - 1)) ?? Microcode.Fetch;
            if (next != null) Select(next);
        }
        private async Task Rename(string value)
        {
            var mnemonic = value?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(mnemonic) || mnemonic.Any(char.IsWhiteSpace)) { renameError = "Mnemonic must be a single word."; return; }
            if (mnemonic == selectedMnemonic) { renameError = null; return; }
            if (Microcode.FindInstruction(mnemonic) != null) { renameError = $"'{mnemonic}' is already defined."; return; }
            var instruction = Selected;
            await Mutate(() => instruction.Mnemonic = mnemonic);
            selectedMnemonic = mnemonic;
            renameError = null;
        }
        // Changing the count keeps the operand types in step: extra ones are dropped, new ones start unknown.
        private Task SetOperands(string value)
        {
            var instruction = Selected;
            return Mutate(() =>
            {
                instruction.Operands = int.TryParse(value, out var n) && n >= 0 ? n : null;
                if (instruction.OperandTypes == null) return;
                if (instruction.Operands == null || instruction.Operands == 0) { instruction.OperandTypes = null; return; }
                if (instruction.OperandTypes.Count > instruction.Operands) instruction.OperandTypes.RemoveRange(instruction.Operands.Value, instruction.OperandTypes.Count - instruction.Operands.Value);
                while (instruction.OperandTypes.Count < instruction.Operands) instruction.OperandTypes.Add(OperandType.Value);
            });
        }
        // Sets one operand's type. Clearing every type removes the list, so the instruction simply does not say.
        private Task SetOperandType(int index, string value)
        {
            var instruction = Selected;
            return Mutate(() =>
            {
                int count = instruction.OperandCount ?? 0;
                if (!Enum.TryParse<OperandType>(value, out var type))
                {
                    instruction.OperandTypes = null;
                    return;
                }
                instruction.OperandTypes ??= Enumerable.Repeat(OperandType.Value, count).ToList();
                while (instruction.OperandTypes.Count < count) instruction.OperandTypes.Add(OperandType.Value);
                instruction.OperandTypes[index] = type;
            });
        }
        private Task SetDescription(string value)
        {
            var instruction = Selected;
            return Mutate(() => instruction.Description = string.IsNullOrWhiteSpace(value) ? null : value.Trim());
        }

        private async Task AddStep()
        {
            var instruction = Selected;
            await Mutate(() => instruction.Steps.Add(new MicroStep()));
            activeStep = instruction.Steps.Count - 1;
        }
        private async Task DuplicateStep(int index)
        {
            var instruction = Selected;
            var source = instruction.Steps[index];
            await Mutate(() => instruction.Steps.Insert(index + 1, new MicroStep
            {
                When = source.When == null ? null : FlagCondition.FromPattern(FlagCondition.ToPattern(source.When)),
                Signals = source.Signals.ToList(),
                Comment = source.Comment,
            }));
            activeStep = index + 1;
        }
        private async Task DeleteStep(int index)
        {
            var instruction = Selected;
            await Mutate(() => instruction.Steps.RemoveAt(index));
            activeStep = Math.Max(0, Math.Min(activeStep, instruction.Steps.Count - 1));
        }
        private Task MoveStep(int from, int to)
        {
            var instruction = Selected;
            if (from == to || to < 0 || to >= instruction.Steps.Count) return Task.CompletedTask;
            activeStep = to;
            return Mutate(() =>
            {
                var step = instruction.Steps[from];
                instruction.Steps.RemoveAt(from);
                instruction.Steps.Insert(to, step);
            });
        }
        // Cycles a flag condition: any -> set -> clear -> any.
        private Task CycleFlag(MicroStep step, string flag)
        {
            return Mutate(() =>
            {
                var pattern = FlagCondition.ToPattern(step.When).ToCharArray();
                int index = Array.FindIndex(Flags, f => f.Name == flag);
                pattern[index] = pattern[index] == 'x' ? '1' : pattern[index] == '1' ? '0' : 'x';
                step.When = FlagCondition.FromPattern(new string(pattern));
            });
        }
        private static string FlagState(MicroStep step, string flag)
        {
            var pattern = FlagCondition.ToPattern(step.When);
            return pattern[Array.FindIndex(Flags, f => f.Name == flag)].ToString();
        }
        private Task SetComment(MicroStep step, string value)
        {
            return Mutate(() => step.Comment = string.IsNullOrWhiteSpace(value) ? null : value.Trim());
        }

        private async Task AddSignal(int stepIndex, string signal)
        {
            signal = signal?.Trim();
            var instruction = Selected;
            if (string.IsNullOrEmpty(signal) || instruction == null || stepIndex < 0 || stepIndex >= instruction.Steps.Count) return;
            var step = instruction.Steps[stepIndex];
            if (step.Signals.Contains(signal)) return;
            await Mutate(() => step.Signals.Add(signal));
            activeStep = stepIndex;
        }
        private async Task AddSignalFromInput(int stepIndex)
        {
            var signal = newSignal;
            newSignal = "";
            await AddSignal(stepIndex, signal);
        }
        private async Task OnSignalInputKey(KeyboardEventArgs e, int stepIndex)
        {
            if (e.Key == "Enter") await AddSignalFromInput(stepIndex);
        }
        private Task RemoveSignal(int stepIndex, string signal)
        {
            var step = Selected.Steps[stepIndex];
            return Mutate(() => step.Signals.Remove(signal));
        }
        private async Task PaletteClick(string signal)
        {
            if (Selected == null) return;
            if (Selected.Steps.Count == 0) await AddStep();
            await AddSignal(Math.Min(activeStep, Selected.Steps.Count - 1), signal);
        }

        private void TogglePreview(int flag)
        {
            previewStatus = (previewStatus ?? 0) ^ flag;
        }
        private bool Runs(MicroStep step)
        {
            return previewStatus == null || step.AppliesTo(previewStatus.Value);
        }
        private int? TickOf(InstructionDefinition instruction, MicroStep step)
        {
            if (previewStatus == null) return null;
            var index = instruction.StepsFor(previewStatus.Value).IndexOf(step);
            return index < 0 ? null : index;
        }

        // ---- Drag and drop (HTML5) ----

        private void DragInstruction(int index) { drag = new DragItem { Kind = DragKind.Instruction, Index = index }; }
        private void DragStep(int index) { drag = new DragItem { Kind = DragKind.Step, Step = index }; }
        private void DragSignal(int step, string signal) { drag = new DragItem { Kind = DragKind.Signal, Step = step, Signal = signal }; }
        private void DragPaletteSignal(string signal) { drag = new DragItem { Kind = DragKind.PaletteSignal, Signal = signal }; }

        private async Task DropOnInstruction(int index)
        {
            var d = drag;
            drag = null;
            if (d?.Kind != DragKind.Instruction || d.Index == index) return;
            await Mutate(() =>
            {
                var item = Microcode.Instructions[d.Index];
                Microcode.Instructions.RemoveAt(d.Index);
                Microcode.Instructions.Insert(index, item);
            });
        }
        private async Task DropOnStep(int stepIndex)
        {
            var d = drag;
            drag = null;
            if (d == null) return;
            var instruction = Selected;
            switch (d.Kind)
            {
                case DragKind.Step:
                    await MoveStep(d.Step, stepIndex);
                    break;
                case DragKind.PaletteSignal:
                    await AddSignal(stepIndex, d.Signal);
                    break;
                case DragKind.Signal when d.Step != stepIndex:
                    var target = instruction.Steps[stepIndex];
                    await Mutate(() =>
                    {
                        instruction.Steps[d.Step].Signals.Remove(d.Signal);
                        if (!target.Signals.Contains(d.Signal)) target.Signals.Add(d.Signal);
                    });
                    activeStep = stepIndex;
                    break;
            }
        }
        private async Task DropOnNewStep()
        {
            var d = drag;
            if (d == null || (d.Kind != DragKind.Signal && d.Kind != DragKind.PaletteSignal)) { drag = null; return; }
            await AddStep();
            drag = d;
            await DropOnStep(Selected.Steps.Count - 1);
        }

        // ---- Files ----

        private async Task Download()
        {
            var name = string.IsNullOrWhiteSpace(Microcode.Name) ? "microcode" : Microcode.Name;
            await JS.InvokeVoidAsync("byocEditor.download", $"{name}.json", Microcode.ToJson());
        }
        private async Task Import(InputFileChangeEventArgs e)
        {
            importError = null;
            try
            {
                using var reader = new System.IO.StreamReader(e.File.OpenReadStream(2 * 1024 * 1024));
                var imported = MicrocodeDefinition.Parse(await reader.ReadToEndAsync());
                History?.Record();
                await Replace(imported);
            }
            catch (Exception ex) when (ex is MachineDefinitionException || ex is FormatException)
            {
                importError = ex.Message;
            }
        }
    }
}
