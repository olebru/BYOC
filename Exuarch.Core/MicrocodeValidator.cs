using System;
using System.Collections.Generic;
using System.Linq;
namespace Exuarch.Core
{
    public enum DiagnosticSeverity { Error, Warning }

    public class MicrocodeDiagnostic
    {
        public DiagnosticSeverity Severity { get; set; }
        // Mnemonic the problem is in, or null for the microcode as a whole.
        public string Instruction { get; set; }
        // Index into the instruction's Steps list, or null.
        public int? Step { get; set; }
        public string Signal { get; set; }
        public string Message { get; set; }

        public override string ToString()
        {
            var where = Instruction == null ? "Microcode" : Step == null ? $"Microcode {Instruction}" : $"Microcode {Instruction} step {Step + 1}";
            return $"{where}: {Message}";
        }
    }

    // Checks microcode against a machine definition: every signal must name a device and one of its
    // control lines, and no two signals in a step may drive the same bus.
    public static class MicrocodeValidator
    {
        public static List<MicrocodeDiagnostic> Validate(MicrocodeDefinition microcode, MachineDefinition machine, DeviceRegistry registry = null, Machine built = null)
        {
            registry ??= DeviceRegistry.CreateDefault();
            var diagnostics = new List<MicrocodeDiagnostic>();
            void Add(DiagnosticSeverity severity, InstructionDefinition instruction, int? step, string signal, string message)
            {
                diagnostics.Add(new MicrocodeDiagnostic { Severity = severity, Instruction = instruction?.Mnemonic, Step = step, Signal = signal, Message = message });
            }

            if (microcode.Fetch == null)
            {
                Add(DiagnosticSeverity.Error, null, null, null, "a fetch routine is required, it runs at opcode 0 to load the next instruction.");
            }
            var seen = new HashSet<string>();
            int opCodes = 0;
            foreach (var instruction in microcode.AllInstructions)
            {
                if (string.IsNullOrWhiteSpace(instruction.Mnemonic) || instruction.Mnemonic.Any(char.IsWhiteSpace))
                {
                    Add(DiagnosticSeverity.Error, instruction, null, null, "mnemonic must be a single word.");
                }
                else if (!seen.Add(instruction.Mnemonic))
                {
                    Add(DiagnosticSeverity.Error, instruction, null, null, $"'{instruction.Mnemonic}' is defined more than once.");
                }
                if (instruction.Steps.Count == 0)
                {
                    Add(DiagnosticSeverity.Error, instruction, null, null, "has no steps.");
                }
                if (instruction.Operands < 0)
                {
                    Add(DiagnosticSeverity.Error, instruction, null, null, "operands can not be negative.");
                }
                if (instruction.Operands != null && instruction.OperandTypes != null && instruction.OperandTypes.Count != instruction.Operands)
                {
                    Add(DiagnosticSeverity.Error, instruction, null, null,
                        $"declares {instruction.Operands} operand{(instruction.Operands == 1 ? "" : "s")} but {instruction.OperandTypes.Count} operand type{(instruction.OperandTypes.Count == 1 ? "" : "s")}.");
                }
                int longestVariant = Enumerable.Range(0, DecoderRom.StatusVariants).Max(s => instruction.StepsFor(s).Count);
                opCodes += Math.Max(1, longestVariant);

                for (int step = 0; step < instruction.Steps.Count; step++)
                {
                    ValidateStep(instruction, step, machine, registry, built, Add);
                    ValidateFlagWrites(instruction, step, machine, Add);
                }
                ValidateFlow(instruction, machine, Add);
            }
            if (opCodes > DecoderRom.AddressSpace)
            {
                Add(DiagnosticSeverity.Error, null, null, null, $"needs {opCodes} opcodes but only {DecoderRom.AddressSpace} are available.");
            }
            return diagnostics;
        }

        private static void ValidateStep(InstructionDefinition instruction, int step, MachineDefinition machine, DeviceRegistry registry, Machine built,
            Action<DiagnosticSeverity, InstructionDefinition, int?, string, string> add)
        {
            var drivers = new Dictionary<string, List<string>>();
            var readers = new Dictionary<string, List<string>>();
            var signals = instruction.Steps[step].Signals;
            foreach (var duplicate in signals.GroupBy(s => s).Where(g => g.Count() > 1))
            {
                add(DiagnosticSeverity.Warning, instruction, step, duplicate.Key, $"'{duplicate.Key}' is listed more than once.");
            }
            foreach (var text in signals.Distinct())
            {
                if (!Signal.TryParse(text, out var signal))
                {
                    add(DiagnosticSeverity.Error, instruction, step, text, $"'{text}' is not a signal, write it as device.line.");
                    continue;
                }
                var device = machine.FindDevice(signal.Device);
                if (device == null)
                {
                    add(DiagnosticSeverity.Error, instruction, step, text, $"unknown device '{signal.Device}'.");
                    continue;
                }
                var info = registry.Info(device.Type);
                var lines = info?.ControlLines.Count > 0
                    ? info.ControlLines.Select(l => l.Name).ToList()
                    : built?.Device(device.Id)?.SignalLines();
                if (lines == null) continue;
                if (!lines.Contains(signal.Line))
                {
                    add(DiagnosticSeverity.Error, instruction, step, text, $"device '{device.Id}' has no control line '{signal.Line}', it has {string.Join(", ", lines)}.");
                    continue;
                }
                var line = info?.ControlLines.FirstOrDefault(l => l.Name == signal.Line);
                if (line?.Drives != null && device.GetPortBus(line.Drives) is string drivenBus)
                {
                    if (!drivers.TryGetValue(drivenBus, out var list)) drivers[drivenBus] = list = new List<string>();
                    list.Add(text);
                }
                if (line?.Reads != null && device.GetPortBus(line.Reads) is string readBus)
                {
                    if (!readers.TryGetValue(readBus, out var list)) readers[readBus] = list = new List<string>();
                    list.Add(text);
                }
            }
            foreach (var conflict in drivers.Where(d => d.Value.Count > 1))
            {
                add(DiagnosticSeverity.Error, instruction, step, conflict.Value[1],
                    $"{string.Join(" and ", conflict.Value)} all drive bus '{conflict.Key}' in the same tick.");
            }
            foreach (var read in readers.Where(r => !drivers.ContainsKey(r.Key)))
            {
                add(DiagnosticSeverity.Warning, instruction, step, read.Value[0],
                    $"{string.Join(" and ", read.Value)} read bus '{read.Key}', but nothing drives it in this step (reads 0).");
            }
        }

        private static readonly string[] AluOperations = { "add", "sub", "cmp", "and", "orr", "eor", "lsl", "lsr" };
        private static readonly string[] RegisterWrites = { "load", "reset", "inc", "dec" };

        // An ALU operation writes its flags into the connected status register at the end of the tick. If the same
        // step also loads, resets or counts that register, which write wins depends on the order of the devices.
        private static void ValidateFlagWrites(InstructionDefinition instruction, int step, MachineDefinition machine,
            Action<DiagnosticSeverity, InstructionDefinition, int?, string, string> add)
        {
            var signals = new List<Signal>();
            foreach (var text in instruction.Steps[step].Signals)
            {
                if (Signal.TryParse(text, out var parsed)) signals.Add(parsed);
            }
            foreach (var alu in machine.Devices.Where(d => d.Type == "alu" && d.Connections.ContainsKey("status")))
            {
                var status = alu.Connections["status"];
                var operation = signals.Where(s => s.Device == alu.Id && AluOperations.Contains(s.Line)).Select(s => $"{s.Device}.{s.Line}").FirstOrDefault();
                var write = signals.Where(s => s.Device == status && RegisterWrites.Contains(s.Line)).Select(s => $"{s.Device}.{s.Line}").FirstOrDefault();
                if (operation != null && write != null)
                {
                    add(DiagnosticSeverity.Error, instruction, step, write,
                        $"{operation} writes the flags into {status}, and {write} writes {status} too in the same step; only one can win. Move one of them to another step.");
                }
            }
        }

        // Every flag variant should end by returning to fetch (reset or load the micro step register) or
        // by halting, otherwise the micro step counter runs on into the next instruction's microcode.
        private static void ValidateFlow(InstructionDefinition instruction, MachineDefinition machine,
            Action<DiagnosticSeverity, InstructionDefinition, int?, string, string> add)
        {
            var stepRegister = machine.Decoder?.InstructionRegister;
            if (stepRegister == null || instruction.Steps.Count == 0) return;
            // Only the machine's halt clock stops it; without one named, any clock's disable is taken as the end.
            var clocks = new HashSet<string>(machine.Halt != null ? new[] { machine.Halt } : machine.Devices.Where(d => d.Type == "clock").Select(d => d.Id));
            bool Ends(MicroStep step) => step.Signals.Any(s => Signal.TryParse(s, out var signal)
                && ((signal.Device == stepRegister && (signal.Line == "reset" || signal.Line == "load"))
                    || (clocks.Contains(signal.Device) && signal.Line == "disable")));

            var reported = new HashSet<string>();
            for (int status = 0; status < DecoderRom.StatusVariants; status++)
            {
                var variant = instruction.StepsFor(status);
                var key = string.Join(",", variant.Select(s => instruction.Steps.IndexOf(s)));
                if (variant.Count == 0 || !reported.Add(key)) continue;
                var flags = VariantLabel(instruction, status);
                int end = variant.FindIndex(Ends);
                if (end < 0)
                {
                    add(DiagnosticSeverity.Warning, instruction, instruction.Steps.IndexOf(variant.Last()), null,
                        $"{flags}never resets or loads '{stepRegister}', so it runs on into the next instruction's microcode.");
                }
                else if (end < variant.Count - 1)
                {
                    add(DiagnosticSeverity.Warning, instruction, instruction.Steps.IndexOf(variant[end + 1]), null,
                        $"{flags}step {instruction.Steps.IndexOf(variant[end]) + 1} returns to fetch, so the steps after it never run.");
                }
            }
        }
        private static string VariantLabel(InstructionDefinition instruction, int status)
        {
            if (instruction.Steps.All(s => s.When == null)) return "";
            var used = new[] { ("N", StatusRegister.NegativeFlag, instruction.Steps.Any(s => s.When?.N != null)),
                               ("V", StatusRegister.OverflowFlag, instruction.Steps.Any(s => s.When?.V != null)),
                               ("C", StatusRegister.CarryFlag, instruction.Steps.Any(s => s.When?.C != null)),
                               ("Z", StatusRegister.ZeroFlag, instruction.Steps.Any(s => s.When?.Z != null)),
                               ("I", FlagCondition.InterruptBit, instruction.Steps.Any(s => s.When?.I != null)) };
            return "when " + string.Join(" ", used.Where(u => u.Item3).Select(u => $"{u.Item1}={((status & u.Item2) != 0 ? 1 : 0)}")) + ": ";
        }
    }
}
