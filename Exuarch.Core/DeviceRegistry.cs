using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
namespace Exuarch.Core
{
    public delegate IBusDevice DeviceFactory(DeviceBuildContext context);

    // Describes a device type for tools such as the machine editor: what it is, its bus ports,
    // the devices it must be connected to and the parameters it accepts.
    public class DeviceTypeInfo
    {
        public string Type { get; set; }
        public string Category { get; set; } = "Other";
        public string Description { get; set; } = "";
        public List<string> Ports { get; set; } = new List<string> { DeviceBuildContext.DefaultPort };
        public List<ConnectionInfo> Connections { get; set; } = new List<ConnectionInfo>();
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();
        // The control lines microcode can enable. Empty when the type does not describe them.
        public List<ControlLineInfo> ControlLines { get; set; } = new List<ControlLineInfo>();
    }
    public class ControlLineInfo
    {
        public string Name { get; set; }
        public string Description { get; set; } = "";
        // Bus port this line puts a value on during the tick, if any.
        public string Drives { get; set; }
        // Bus port this line takes a value from at the end of the tick, if any.
        public string Reads { get; set; }

        public static ControlLineInfo Output(string name, string description, string port = DeviceBuildContext.DefaultPort)
            => new ControlLineInfo { Name = name, Description = description, Drives = port };
        public static ControlLineInfo Input(string name, string description, string port = DeviceBuildContext.DefaultPort)
            => new ControlLineInfo { Name = name, Description = description, Reads = port };
        public static ControlLineInfo Internal(string name, string description)
            => new ControlLineInfo { Name = name, Description = description };
    }
    public class ConnectionInfo
    {
        public string Name { get; set; }
        public string Description { get; set; } = "";
    }
    public class ParameterInfo
    {
        public string Name { get; set; }
        public string Description { get; set; } = "";
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 255;
        public int Default { get; set; } = 0;
    }

    // Maps the "type" of a device definition to the code that builds it. Register your own device types
    // here to use them from a machine definition.
    public class DeviceRegistry
    {
        private readonly Dictionary<string, DeviceFactory> factories = new Dictionary<string, DeviceFactory>();
        private readonly Dictionary<string, DeviceTypeInfo> infos = new Dictionary<string, DeviceTypeInfo>();

        public static DeviceRegistry CreateDefault()
        {
            var size = new ParameterInfo { Name = "size", Description = "Number of 16 bit cells", Min = 1, Max = 65536, Default = MemoryModule.DefaultSize };
            List<ControlLineInfo> RegisterLines() => new List<ControlLineInfo>
            {
                ControlLineInfo.Output("output", "Put the value on the bus"),
                ControlLineInfo.Input("load", "Take the value from the bus"),
                ControlLineInfo.Internal("reset", "Set to 0"),
                ControlLineInfo.Internal("inc", "Add 1; 65535 wraps to 0"),
                ControlLineInfo.Internal("dec", "Subtract 1; 0 wraps to 65535"),
            };
            List<ControlLineInfo> MemoryLines() => new List<ControlLineInfo>
            {
                ControlLineInfo.Input("loadmar", "Take the address from the bus"),
                ControlLineInfo.Output("outputmar", "Put the address on the bus"),
                ControlLineInfo.Output("output", "Put the cell at the address on the bus"),
            };
            List<ControlLineInfo> RamLines()
            {
                var lines = MemoryLines();
                lines.Add(ControlLineInfo.Input("load", "Store the bus value at the address"));
                return lines;
            }

            var registry = new DeviceRegistry();
            registry.Register("register", c => new Register(c.Name, c.Id, c.Bus()),
                new DeviceTypeInfo { Category = "Registers", Description = "Holds one 16 bit value between ticks. output puts it on the bus and load stores the bus value; reset, inc and dec change it in place, wrapping from 65535 to 0 and back. It starts at 0", ControlLines = RegisterLines() });
            registry.Register("statusRegister", c => new StatusRegister(c.Name, c.Id, c.Bus()),
                new DeviceTypeInfo { Category = "Registers", Description = "Holds the four ALU flags: N negative, V overflow, C carry and Z zero. The decoder reads them to pick which steps of an instruction run", ControlLines = RegisterLines() });
            registry.Register("dualPortRegister", c => new DualPortRegister(c.Name, c.Id, c.Bus("a"), c.Bus("b")),
                new DeviceTypeInfo
                {
                    Category = "Registers",
                    Description = "A register that sits on two buses: load it from one and output it on the other to move a value between them",
                    Ports = new List<string> { "a", "b" },
                    ControlLines =
                    {
                        ControlLineInfo.Input("loada", "Take the value from bus a", "a"),
                        ControlLineInfo.Input("loadb", "Take the value from bus b", "b"),
                        ControlLineInfo.Output("outputa", "Put the value on bus a", "a"),
                        ControlLineInfo.Output("outputb", "Put the value on bus b", "b"),
                        ControlLineInfo.Internal("reset", "Set to 0"),
                        ControlLineInfo.Internal("inc", "Add 1; 65535 wraps to 0"),
                        ControlLineInfo.Internal("dec", "Subtract 1; 0 wraps to 65535"),
                    }
                });
            registry.Register("instructionRegister", c => new InstructionRegister(c.Name, c.Id, c.Bus()),
                new DeviceTypeInfo
                {
                    Category = "Control",
                    Description = "The decoder's micro step counter. It counts up every tick on its own and, with the status flags, addresses the decoder ROM step that runs next. load jumps to the steps of the opcode on the bus, reset clears it to 0 where fetch starts",
                    ControlLines =
                    {
                        ControlLineInfo.Input("load", "Jump to the step address on the bus: the start of an opcode's steps"),
                        ControlLineInfo.Internal("reset", "Clear to 0, the start of the fetch routine"),
                    }
                });
            registry.Register("clock", c => new Clock(c.Name, c.Id),
                new DeviceTypeInfo
                {
                    Category = "Control",
                    Description = "Drives the ticks and counts cycles. Its disable line stops the clock, which halts the machine",
                    Ports = new List<string>(),
                    ControlLines = { ControlLineInfo.Internal("disable", "Halt the machine") }
                });
            registry.Register("alu", c => new ALU(c.Name, c.Id, c.Connection<Register>("a"), c.Connection<Register>("b"), c.Connection<Register>("status"), c.Bus()),
                new DeviceTypeInfo
                {
                    Category = "Arithmetic",
                    Description = "Arithmetic and logic on the registers connected as a and b. add, sub, and, orr, eor, lsl and lsr put the result on the bus, cmp only compares; each sets the flags in the connected status register",
                    Connections =
                    {
                        new ConnectionInfo { Name = "a", Description = "First operand register" },
                        new ConnectionInfo { Name = "b", Description = "Second operand register" },
                        new ConnectionInfo { Name = "status", Description = "Register that receives the flags" },
                    },
                    ControlLines =
                    {
                        ControlLineInfo.Output("add", "Put a + b on the bus and set flags"),
                        ControlLineInfo.Output("sub", "Put a - b on the bus and set flags"),
                        ControlLineInfo.Internal("cmp", "Set flags for a - b"),
                        ControlLineInfo.Output("and", "Put a AND b on the bus and set Z and N"),
                        ControlLineInfo.Output("orr", "Put a OR b on the bus and set Z and N"),
                        ControlLineInfo.Output("eor", "Put a XOR b on the bus and set Z and N"),
                        ControlLineInfo.Output("lsl", "Put a shifted left by b on the bus; C is the last bit out"),
                        ControlLineInfo.Output("lsr", "Put a shifted right by b on the bus; C is the last bit out"),
                    }
                });
            registry.Register("ram", c => new RamModule(c.Name, c.Id, c.Bus(), c.IntParameter("size", MemoryModule.DefaultSize, 1, 65536)),
                new DeviceTypeInfo { Category = "Memory", Description = "Read/write memory with its own address register (MAR): loadmar takes an address from the bus, output reads that cell and load writes the bus value into it", Parameters = { size }, ControlLines = RamLines() });
            var mmuLines = RamLines();
            mmuLines.Add(ControlLineInfo.Input("loadcs", "Select the bank given on the bus"));
            mmuLines.Add(ControlLineInfo.Output("outputcs", "Put the selected bank number on the bus"));
            mmuLines.Add(ControlLineInfo.Internal("select0stack", "Select bank 0, the stack bank"));
            registry.Register("mmu", c =>
                {
                    int banks = c.IntParameter("banks", MMU.DefaultBanks, 1, 256), bankSize = c.IntParameter("bankSize", MemoryModule.DefaultSize, 1, 65536);
                    return new MMU(c.Name, c.Id, c.Bus(), banks, bankSize);
                },
                new DeviceTypeInfo
                {
                    Category = "Memory",
                    Description = "Banks of RAM behind a chip select register: loadcs picks a bank from the bus and the memory lines then work on that bank. select0stack picks bank 0, the stack bank",
                    Parameters =
                    {
                        new ParameterInfo { Name = "banks", Description = "Number of banks", Min = 1, Max = 256, Default = MMU.DefaultBanks },
                        new ParameterInfo { Name = "bankSize", Description = "16 bit cells per bank", Min = 1, Max = 65536, Default = MemoryModule.DefaultSize },
                    },
                    ControlLines = mmuLines
                });
            registry.Register("display", c => new CharacterDisplay(c.Name, c.Id, c.Bus(), c.IntParameter("columns", 16, 1, 64), c.IntParameter("rows", 4, 1, 16)),
                new DeviceTypeInfo
                {
                    Category = "I/O",
                    Description = "A character LCD: load prints the low 8 bits of the bus at the cursor as a Latin-1 character and moves on, clear blanks it. Line feed and carriage return move the cursor",
                    Parameters =
                    {
                        new ParameterInfo { Name = "columns", Description = "Characters per row", Min = 1, Max = 64, Default = 16 },
                        new ParameterInfo { Name = "rows", Description = "Rows of characters", Min = 1, Max = 16, Default = 4 },
                    },
                    ControlLines =
                    {
                        ControlLineInfo.Input("load", "Print the character on the bus at the cursor"),
                        ControlLineInfo.Internal("clear", "Blank the display and move the cursor home"),
                    }
                });
            registry.Register("framebuffer", c => new Framebuffer(c.Name, c.Id, c.Bus()),
                new DeviceTypeInfo
                {
                    Category = "I/O",
                    Description = "A 640 x 480 colour screen, one RGB565 word per pixel. loadx and loady move the cursor, plot writes the bus value there and moves right, clear blanks the screen",
                    ControlLines =
                    {
                        ControlLineInfo.Input("loadx", "Set the cursor column from the bus (0-639)"),
                        ControlLineInfo.Input("loady", "Set the cursor row from the bus (0-479)"),
                        ControlLineInfo.Input("plot", "Write the RGB565 colour on the bus at the cursor, then move right"),
                        ControlLineInfo.Internal("clear", "Blank the screen and move the cursor home"),
                    }
                });
            registry.Register("blitter", c => new Blitter(c.Name, c.Id, c.Bus("host"), c.Bus("video"), c.Connection<Framebuffer>("screen")),
                new DeviceTypeInfo
                {
                    Category = "I/O",
                    Description = "A graphics coprocessor. Give it a rectangle and a colour on the host bus and start it: it then fills the rectangle by itself on its video bus, one transfer per tick, driving the connected framebuffer while the CPU carries on. status reads 1 while it is busy",
                    Ports = new List<string> { "host", "video" },
                    Connections = { new ConnectionInfo { Name = "screen", Description = "The framebuffer it draws on, on its video bus" } },
                    ControlLines =
                    {
                        ControlLineInfo.Input("loadx", "Take the rectangle's left column from the host bus", "host"),
                        ControlLineInfo.Input("loady", "Take the rectangle's top row from the host bus", "host"),
                        ControlLineInfo.Input("loadw", "Take the rectangle's width from the host bus", "host"),
                        ControlLineInfo.Input("loadh", "Take the rectangle's height from the host bus", "host"),
                        ControlLineInfo.Input("loadcolour", "Take the RGB565 fill colour from the host bus", "host"),
                        ControlLineInfo.Internal("start", "Start filling the rectangle; it then runs by itself"),
                        ControlLineInfo.Output("status", "Put 1 on the host bus while busy, 0 when idle", "host"),
                    }
                });
            registry.Register("interruptController", c => new InterruptController(c.Name, c.Id, c.Bus(),
                    Enumerable.Range(0, InterruptController.Sources).Select(i => c.InterruptSource($"irq{i}")).ToList()),
                new DeviceTypeInfo
                {
                    Category = "Control",
                    Description = "Collects interrupt requests from up to four devices (irq0 to irq3) into pending bits. Name it as decoder.interrupts and microcode can test the I condition: 1 when interrupts are enabled and an unmasked request is pending",
                    Connections =
                    {
                        new ConnectionInfo { Name = "irq0", Description = "Interrupt source for pending bit 0 (a timer, keypad or blitter)" },
                        new ConnectionInfo { Name = "irq1", Description = "Interrupt source for pending bit 1" },
                        new ConnectionInfo { Name = "irq2", Description = "Interrupt source for pending bit 2" },
                        new ConnectionInfo { Name = "irq3", Description = "Interrupt source for pending bit 3" },
                    },
                    ControlLines =
                    {
                        ControlLineInfo.Internal("enable", "Allow interrupts"),
                        ControlLineInfo.Internal("disable", "Hold interrupts off"),
                        ControlLineInfo.Input("loadmask", "Take the mask from the bus: bit n set lets irq n interrupt"),
                        ControlLineInfo.Output("output", "Put the pending, unmasked request bits on the bus"),
                        ControlLineInfo.Input("ack", "Clear the pending bits that are set in the bus value"),
                    }
                });
            registry.Register("timer", c => new TickTimer(c.Name, c.Id, c.Bus(), c.IntParameter("period", 1000, 0, 65535)),
                new DeviceTypeInfo
                {
                    Category = "I/O",
                    Description = "Counts clock ticks and raises an interrupt request every period ticks while it runs, a steady beat that does not depend on the program. Connect it to an interrupt controller",
                    Parameters = { new ParameterInfo { Name = "period", Description = "Ticks between interrupt requests", Min = 0, Max = 65535, Default = 1000 } },
                    ControlLines =
                    {
                        ControlLineInfo.Input("loadperiod", "Take the period, in ticks, from the bus"),
                        ControlLineInfo.Internal("start", "Start counting from 0"),
                        ControlLineInfo.Internal("stop", "Stop counting"),
                        ControlLineInfo.Output("output", "Put the current count on the bus"),
                    }
                });
            registry.Register("keypad", c => new Keypad(c.Name, c.Id, c.Bus()),
                new DeviceTypeInfo
                {
                    Category = "I/O",
                    Description = "The arrow keys and space, read like a register: output puts one bit per key on the bus (1 up, 2 down, 4 left, 8 right, 16 space). A quick tap is kept until the CPU reads it",
                    ControlLines =
                    {
                        ControlLineInfo.Output("output", "Put the key bits on the bus: 1 up, 2 down, 4 left, 8 right, 16 space"),
                    }
                });
            return registry;
        }

        public void Register(string type, DeviceFactory factory, DeviceTypeInfo info = null)
        {
            info ??= new DeviceTypeInfo();
            info.Type = type;
            factories[type] = factory;
            infos[type] = info;
        }
        public bool IsRegistered(string type)
        {
            return type != null && factories.ContainsKey(type);
        }
        public IEnumerable<string> Types { get { return factories.Keys; } }
        public IEnumerable<DeviceTypeInfo> TypeInfos { get { return infos.Values; } }
        public DeviceTypeInfo Info(string type)
        {
            return type != null && infos.TryGetValue(type, out var info) ? info : null;
        }
        internal DeviceFactory Factory(string type)
        {
            return factories[type];
        }
    }

    public class DeviceBuildContext
    {
        public const string DefaultPort = "data";
        private readonly DeviceDefinition definition;
        private readonly IReadOnlyDictionary<string, Bus> buses;
        private readonly Func<string, IBusDevice> resolveDevice;

        internal DeviceBuildContext(DeviceDefinition definition, IReadOnlyDictionary<string, Bus> buses, Func<string, IBusDevice> resolveDevice)
        {
            this.definition = definition;
            this.buses = buses;
            this.resolveDevice = resolveDevice;
        }
        public DeviceDefinition Definition { get { return definition; } }
        public string Id { get { return definition.Id; } }
        public string Name { get { return definition.Name ?? definition.Id; } }

        public Bus Bus(string port = DefaultPort)
        {
            var busId = definition.Ports().Where(p => p.Key == port).Select(p => p.Value).FirstOrDefault();
            if (busId == null)
            {
                throw Error(port == DefaultPort
                    ? "needs a bus connection, set \"bus\""
                    : $"needs a bus connection for port '{port}', set \"buses\": {{ \"{port}\": \"<bus id>\" }}");
            }
            return buses[busId];
        }
        public T Connection<T>(string name) where T : class, IBusDevice
        {
            if (!definition.Connections.TryGetValue(name, out var targetId))
            {
                throw Error($"needs connection '{name}'");
            }
            var target = resolveDevice(targetId);
            return target as T ?? throw Error($"connection '{name}' must be a {typeof(T).Name}, but '{targetId}' is a {target.GetType().Name}");
        }
        // An interrupt source connected by name, or null when the connection is not set.
        public IInterruptSource InterruptSource(string name)
        {
            if (!definition.Connections.TryGetValue(name, out var targetId)) return null;
            var target = resolveDevice(targetId);
            return target as IInterruptSource ?? throw Error($"connection '{name}' must be a device that raises interrupts (a timer, keypad or blitter), but '{targetId}' is a {target.GetType().Name}");
        }
        public int IntParameter(string name, int defaultValue, int min, int max)
        {
            if (!definition.Parameters.TryGetValue(name, out var value)) return defaultValue;
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result) || result < min || result > max)
            {
                throw Error($"parameter '{name}' must be a whole number between {min} and {max}");
            }
            return result;
        }
        public byte ByteParameter(string name, byte defaultValue = 0)
        {
            if (!definition.Parameters.TryGetValue(name, out var value)) return defaultValue;
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetByte(out var result))
            {
                throw Error($"parameter '{name}' must be a number between 0 and 255");
            }
            return result;
        }
        public MachineDefinitionException Error(string message)
        {
            return new MachineDefinitionException($"Device '{definition.Id}' ({definition.Type}): {message}.");
        }
    }
}
