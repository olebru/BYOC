using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
namespace BYOCCore
{
    // A microcoded machine built from a MachineDefinition: buses, devices, a decoder ROM and a program.
    public class Machine
    {
        public MachineDefinition Definition { get; }
        public IReadOnlyDictionary<string, Bus> Buses { get; }
        public IReadOnlyList<IBusDevice> Devices { get; }
        public DecoderRom DecoderRom { get; }
        public Assembler Assembler { get; }
        // The assembled program, one value per memory cell.
        public int[] ProgramByteCode { get; }
        public List<MicroInstruction> CurrentMicroCode { get; private set; }
        public int Cycles { get; private set; }
        private readonly Dictionary<string, IBusDevice> devicesByID;
        private readonly InstructionRegister instructionRegister;
        private readonly Register statusRegister;
        private readonly Clock halt;
        private readonly DeviceRegistry registry;
        private readonly MemoryModule programMemory;
        private readonly RingBuffer<TickRecord> history = new RingBuffer<TickRecord>(HistoryLimit);
        public const int HistoryLimit = 500;

        public Machine(MachineDefinition definition, string microcode, string source, DeviceRegistry registry = null)
            : this(definition, MicrocodeDefinition.Parse(microcode), source, registry)
        {
        }

        // Builds the machine with the microcode stored in its definition (decoder.microcode).
        public Machine(MachineDefinition definition, string source, DeviceRegistry registry = null)
            : this(definition, (MicrocodeDefinition)null, source, registry)
        {
        }

        // microcode overrides the definition's own decoder.microcode when given.
        public Machine(MachineDefinition definition, MicrocodeDefinition microcode, string source, DeviceRegistry registry = null)
        {
            registry ??= DeviceRegistry.CreateDefault();
            this.registry = registry;
            Definition = definition;
            Validate(definition, registry);

            Buses = definition.Buses.ToDictionary(b => b.Id, b => new Bus(b.Id));
            devicesByID = BuildDevices(definition, registry, Buses);
            Devices = definition.Devices.Select(d => devicesByID[d.Id]).ToList();
            foreach (var deviceDefinition in definition.Devices)
            {
                foreach (var busId in deviceDefinition.Ports().Select(p => p.Value).Distinct())
                {
                    Buses[busId].devices.Add(devicesByID[deviceDefinition.Id]);
                }
            }

            statusRegister = Device<Register>(definition.Decoder.Status, "decoder.status");
            instructionRegister = Device<InstructionRegister>(definition.Decoder.InstructionRegister, "decoder.instructionRegister");
            if (definition.Halt != null) halt = Device<Clock>(definition.Halt, "halt");
            if (definition.ProgramMemory != null) programMemory = Device<MemoryModule>(definition.ProgramMemory, "programMemory");

            microcode ??= definition.Decoder.Microcode
                ?? throw new MachineDefinitionException("\"decoder.microcode\" is required: the fetch routine and instructions for this machine.");
            var diagnostics = MicrocodeValidator.Validate(microcode, definition, registry, this);
            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToList();
            if (errors.Count > 0) throw new MachineDefinitionException(errors);
            MicrocodeWarnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();

            DecoderRom = new DecoderRom(microcode);
            Assembler = new Assembler(DecoderRom, programMemory?.Size ?? MemoryModule.DefaultSize);
            ProgramByteCode = Assembler.Assemble(source ?? string.Empty);
            if (ProgramByteCode.Length > 0)
            {
                if (definition.ProgramMemory == null)
                {
                    throw new MachineDefinitionException("\"programMemory\" must be set to load a program.");
                }
                Device<MemoryModule>(definition.ProgramMemory).LoadProgram(ProgramByteCode);
            }
            CurrentMicroCode = DecoderRom.FetchInstruction(statusRegister.Data, instructionRegister.Data);
        }

        public static Machine FromJson(string definitionJson, string microcode, string source, DeviceRegistry registry = null)
        {
            return new Machine(MachineDefinition.FromJson(definitionJson), microcode, source, registry);
        }
        public static Machine FromJson(string definitionJson, string source, DeviceRegistry registry = null)
        {
            return new Machine(MachineDefinition.FromJson(definitionJson), source, registry);
        }
        public static Machine CreateDefault()
        {
            return FromJson(ExampleData.MACHINE, ExampleData.SRC);
        }

        // Checks a definition on its own, without microcode or a program. Returns the problems found.
        public static IReadOnlyList<string> ValidateDefinition(MachineDefinition definition, DeviceRegistry registry = null)
        {
            try
            {
                new Machine(definition, new MicrocodeDefinition { Fetch = new InstructionDefinition { Mnemonic = "FTC", Steps = { new MicroStep() } } }, "", registry);
                return Array.Empty<string>();
            }
            catch (MachineDefinitionException e)
            {
                return e.Errors.Where(error => !error.StartsWith("Microcode")).ToList();
            }
        }

        // Microcode problems that do not stop the machine from running.
        public IReadOnlyList<MicrocodeDiagnostic> MicrocodeWarnings { get; }

        public bool IsHalted { get { return halt != null && halt.IsHalted(); } }
        public IBusDevice Device(string id)
        {
            return devicesByID.TryGetValue(id, out var device) ? device : null;
        }
        public T Device<T>(string id) where T : class, IBusDevice
        {
            return Device(id) as T;
        }

        public void SingleStep()
        {
            if (!IsHalted)
            {
                Step();
            }
        }
        public IEnumerable<int> Run()
        {
            while (!IsHalted)
            {
                Step();
                yield return Cycles;
            }
        }
        // Runs ticks until the next instruction has been fetched into the micro step register, or the machine
        // halts. Returns the number of ticks run.
        public int StepInstruction(int maxTicks = 10000)
        {
            int ticks = 0;
            while (!IsHalted && ticks < maxTicks)
            {
                Step();
                ticks++;
                if (LastTick.FetchedFromAddress != null) break;
            }
            return ticks;
        }

        // The last ticks, oldest first, at most HistoryLimit.
        public IReadOnlyList<TickRecord> History { get { return history; } }
        public TickRecord LastTick { get; private set; }
        // Program memory address of the opcode last fetched into the micro step register.
        public int? CurrentInstructionAddress { get; private set; }
        public int Status { get { return statusRegister.Data; } }
        public int MicroStepRegister { get { return instructionRegister.Data; } }
        // The instruction and micro step the next tick will run.
        public (InstructionDefinition Instruction, MicroStep Step, int Offset)? NextStep
        {
            get { return DecoderRom.Locate(statusRegister.Data, instructionRegister.Data); }
        }

        // Everything a tick at one decoder ROM address needs, worked out the first time the address is used.
        private class TickPlan
        {
            public List<MicroInstruction> MicroCode;
            public IBusDevice[] Devices;
            public string[] Functions;
            public string[] Signals;
            public Dictionary<string, List<string>> ReadersByBus;
            public bool LoadsInstruction;
            public string Instruction;
            public int? StepIndex;
        }
        private readonly Dictionary<int, TickPlan> plans = new Dictionary<int, TickPlan>();
        private Bus[] busArray;
        private IBusDevice[] deviceArray;

        // When false, ticks skip the detail kept for display (bus transfers, value changes, memory writes and the
        // history), which makes running much faster. LastTick, breakpoints and CurrentInstructionAddress still work.
        public bool RecordHistory { get; set; } = true;

        private TickPlan PlanFor(int status, int step)
        {
            int address = DecoderRom.RomAddress(status, step);
            if (plans.TryGetValue(address, out var plan)) return plan;
            var microCode = DecoderRom.FetchInstruction(status, step);
            plan = new TickPlan
            {
                MicroCode = microCode,
                Devices = microCode.Select(m => devicesByID[m.DeviceID]).ToArray(),
                Functions = microCode.Select(m => m.Function).ToArray(),
                Signals = microCode.Select(m => $"{m.DeviceID}.{m.Function}").ToArray(),
                LoadsInstruction = microCode.Any(m => m.DeviceID == Definition.Decoder.InstructionRegister && m.Function == "load"),
            };
            plan.ReadersByBus = Buses.Keys.ToDictionary(id => id, id => ReadersOf(id, plan.Signals));
            var located = DecoderRom.Locate(status, step);
            if (located != null)
            {
                plan.Instruction = located.Value.Instruction.Mnemonic;
                if (located.Value.Step != null) plan.StepIndex = located.Value.Instruction.Steps.IndexOf(located.Value.Step);
            }
            plans[address] = plan;
            return plan;
        }

        private void Step()
        {
            busArray ??= Buses.Values.ToArray();
            deviceArray ??= Devices.ToArray();
            int status = statusRegister.Data, step = instructionRegister.Data;
            var plan = PlanFor(status, step);
            var record = new TickRecord
            {
                Cycle = Cycles + 1,
                Status = status,
                MicroStep = step,
                RomAddress = DecoderRom.RomAddress(status, step),
                Instruction = plan.Instruction,
                StepIndex = plan.StepIndex,
            };
            bool detailed = RecordHistory;
            var valuesBefore = detailed ? SnapshotValues() : null;
            var writesBefore = detailed ? SnapshotWrites() : null;

            CurrentMicroCode = plan.MicroCode;
            for (int i = 0; i < plan.Devices.Length; i++)
            {
                plan.Devices[i].Enable(plan.Functions[i]);
            }
            Clocking.Tick(busArray, deviceArray);
            Cycles++;

            if (plan.LoadsInstruction && programMemory != null)
            {
                record.FetchedFromAddress = programMemory.memoryAddress;
                CurrentInstructionAddress = programMemory.memoryAddress;
            }
            if (detailed)
            {
                record.Signals = plan.Signals.ToList();
                foreach (var bus in busArray)
                {
                    record.Transfers.Add(new BusTransfer { Bus = bus.ID, Driver = bus.Writer?.ID(), Value = bus.Data, Readers = plan.ReadersByBus[bus.ID] });
                }
                var valuesAfter = SnapshotValues();
                foreach (var value in valuesAfter)
                {
                    if (valuesBefore.TryGetValue(value.Key, out var before) && before != value.Value)
                    {
                        record.Changes.Add(new ValueChange { Device = value.Key, Before = before, After = value.Value });
                    }
                }
                foreach (var (deviceId, bank, module, count) in writesBefore)
                {
                    if (module.WriteCount != count)
                    {
                        record.Writes.Add(new MemoryWrite { Device = deviceId, Bank = bank, Address = module.LastWriteAddress, Value = module.ValueAt(module.LastWriteAddress) });
                    }
                }
                history.Add(record);
            }
            LastTick = record;
        }
        private List<string> ReadersOf(string busId, IEnumerable<string> signals)
        {
            var readers = new List<string>();
            foreach (var text in signals)
            {
                if (!Signal.TryParse(text, out var signal)) continue;
                var device = Definition.FindDevice(signal.Device);
                var line = device == null ? null : registry.Info(device.Type)?.ControlLines.FirstOrDefault(l => l.Name == signal.Line);
                if (line?.Reads != null && device.GetPortBus(line.Reads) == busId && !readers.Contains(signal.Device)) readers.Add(signal.Device);
            }
            return readers;
        }
        private Dictionary<string, int> SnapshotValues()
        {
            var values = new Dictionary<string, int>();
            foreach (var device in Devices)
            {
                switch (device)
                {
                    case Register register: values[device.ID()] = register.Data; break;
                    case DualPortRegister dualPort: values[device.ID()] = dualPort.Data; break;
                    case MemoryModule memory: values[device.ID() + ".mar"] = memory.memoryAddress; break;
                    case CharacterDisplay display: values[device.ID() + ".cursor"] = display.Cursor; break;
                    case Keypad keypad: values[device.ID()] = keypad.Data; break;
                    case InstructionRegister counter: values[device.ID()] = counter.Data; break;
                    case Framebuffer framebuffer:
                        values[device.ID() + ".x"] = framebuffer.X;
                        values[device.ID() + ".y"] = framebuffer.Y;
                        break;
                    case MMU mmu:
                        values[device.ID() + ".cs"] = mmu.ChipSelectRegister.Data;
                        values[device.ID() + ".mar"] = mmu.SelectedBank.memoryAddress;
                        break;
                }
            }
            return values;
        }
        private List<(string Device, int Bank, IWriteTracked Module, long Count)> SnapshotWrites()
        {
            var writes = new List<(string, int, IWriteTracked, long)>();
            foreach (var device in Devices)
            {
                if (device is IWriteTracked tracked) writes.Add((device.ID(), -1, tracked, tracked.WriteCount));
                if (device is MMU mmu)
                {
                    for (int bank = 0; bank < mmu.RamBanks.Length; bank++) writes.Add((device.ID(), bank, mmu.RamBanks[bank], mmu.RamBanks[bank].WriteCount));
                }
            }
            return writes;
        }
        private T Device<T>(string id, string setting) where T : class, IBusDevice
        {
            var device = Device(id);
            string Article(string name) => "AEIOU".Contains(name[0]) ? "an" : "a";
            return device as T ?? throw new MachineDefinitionException(
                $"\"{setting}\" must name {Article(typeof(T).Name)} {typeof(T).Name} device, but '{id}' is {Article(device.GetType().Name)} {device.GetType().Name}.");
        }

        private static void Validate(MachineDefinition definition, DeviceRegistry registry)
        {
            var errors = new List<string>();
            var busIds = new HashSet<string>();
            foreach (var bus in definition.Buses)
            {
                if (string.IsNullOrWhiteSpace(bus.Id)) errors.Add("Every bus needs an \"id\".");
                else if (!busIds.Add(bus.Id)) errors.Add($"Bus '{bus.Id}' is defined more than once.");
            }
            var deviceIds = new HashSet<string>();
            foreach (var device in definition.Devices)
            {
                if (string.IsNullOrWhiteSpace(device.Id)) { errors.Add("Every device needs an \"id\"."); continue; }
                if (!deviceIds.Add(device.Id)) errors.Add($"Device '{device.Id}' is defined more than once.");
                if (!registry.IsRegistered(device.Type))
                {
                    errors.Add($"Device '{device.Id}': unknown type '{device.Type}', known types are {string.Join(", ", registry.Types)}.");
                }
                if (device.Bus != null && device.Buses.ContainsKey(DeviceBuildContext.DefaultPort))
                {
                    errors.Add($"Device '{device.Id}': port '{DeviceBuildContext.DefaultPort}' is set by both \"bus\" and \"buses\".");
                }
                foreach (var port in device.Ports().Where(p => !busIds.Contains(p.Value)))
                {
                    errors.Add($"Device '{device.Id}': port '{port.Key}' connects to unknown bus '{port.Value}'.");
                }
            }
            foreach (var device in definition.Devices.Where(d => d.Id != null))
            {
                foreach (var connection in device.Connections.Where(c => !deviceIds.Contains(c.Value)))
                {
                    errors.Add($"Device '{device.Id}': connection '{connection.Key}' refers to unknown device '{connection.Value}'.");
                }
            }
            if (definition.Decoder == null)
            {
                errors.Add("\"decoder\" with \"status\" and \"instructionRegister\" is required.");
            }
            else
            {
                CheckReference(errors, deviceIds, definition.Decoder.Status, "decoder.status", required: true);
                CheckReference(errors, deviceIds, definition.Decoder.InstructionRegister, "decoder.instructionRegister", required: true);
            }
            CheckReference(errors, deviceIds, definition.Halt, "halt", required: false);
            CheckReference(errors, deviceIds, definition.ProgramMemory, "programMemory", required: false);
            if (errors.Count > 0) throw new MachineDefinitionException(errors);
        }
        private static void CheckReference(List<string> errors, HashSet<string> deviceIds, string id, string setting, bool required)
        {
            if (id == null)
            {
                if (required) errors.Add($"\"{setting}\" is required.");
            }
            else if (!deviceIds.Contains(id))
            {
                errors.Add($"\"{setting}\" refers to unknown device '{id}'.");
            }
        }

        // Builds devices on demand so connections may refer to devices defined later in the list.
        private static Dictionary<string, IBusDevice> BuildDevices(MachineDefinition definition, DeviceRegistry registry, IReadOnlyDictionary<string, Bus> buses)
        {
            var definitions = definition.Devices.ToDictionary(d => d.Id);
            var built = new Dictionary<string, IBusDevice>();
            var building = new Stack<string>();
            IBusDevice Build(string id)
            {
                if (built.TryGetValue(id, out var existing)) return existing;
                if (building.Contains(id))
                {
                    throw new MachineDefinitionException($"Device connections form a cycle: {string.Join(" -> ", building.Reverse().Append(id))}.");
                }
                building.Push(id);
                var deviceDefinition = definitions[id];
                var device = registry.Factory(deviceDefinition.Type)(new DeviceBuildContext(deviceDefinition, buses, Build));
                building.Pop();
                if (device.ID() != id)
                {
                    throw new MachineDefinitionException($"Device '{id}': factory for type '{deviceDefinition.Type}' returned a device with ID '{device.ID()}'.");
                }
                built[id] = device;
                return device;
            }
            foreach (var deviceDefinition in definition.Devices) Build(deviceDefinition.Id);
            return built;
        }

    }
}
