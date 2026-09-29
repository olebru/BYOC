using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
namespace BYOCCore
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
            var initialValue = new ParameterInfo { Name = "initialValue", Description = "Value after power on", Max = 65535 };
            var size = new ParameterInfo { Name = "size", Description = "Number of 16 bit cells", Min = 1, Max = 65536, Default = RomModule.DefaultSize };
            List<ControlLineInfo> RegisterLines() => new List<ControlLineInfo>
            {
                ControlLineInfo.Output("output", "Put the value on the bus"),
                ControlLineInfo.Input("load", "Take the value from the bus"),
                ControlLineInfo.Internal("reset", "Set to 0"),
                ControlLineInfo.Internal("inc", "Add 1"),
                ControlLineInfo.Internal("dec", "Subtract 1"),
            };
            List<ControlLineInfo> RomLines() => new List<ControlLineInfo>
            {
                ControlLineInfo.Input("loadmar", "Take the address from the bus"),
                ControlLineInfo.Output("outputmar", "Put the address on the bus"),
                ControlLineInfo.Output("output", "Put the cell at the address on the bus"),
            };
            List<ControlLineInfo> RamLines()
            {
                var lines = RomLines();
                lines.Add(ControlLineInfo.Input("load", "Store the bus value at the address"));
                return lines;
            }

            var registry = new DeviceRegistry();
            registry.Register("register", c => new Register(c.Name, c.Id, c.Bus(), c.IntParameter("initialValue", 0, 0, 65535)),
                new DeviceTypeInfo { Category = "Registers", Description = "16 bit register: output, load, reset, inc, dec", Parameters = { initialValue }, ControlLines = RegisterLines() });
            registry.Register("statusRegister", c => new StatusRegister(c.Name, c.Id, c.Bus()),
                new DeviceTypeInfo { Category = "Registers", Description = "Holds the NVCZ flags written by the ALU", ControlLines = RegisterLines() });
            registry.Register("dualPortRegister", c => new DualPortRegister(c.Name, c.Id, c.Bus("a"), c.Bus("b"), c.IntParameter("initialValue", 0, 0, 65535)),
                new DeviceTypeInfo
                {
                    Category = "Registers",
                    Description = "Register on two buses, moves values between them",
                    Ports = new List<string> { "a", "b" },
                    Parameters = { initialValue },
                    ControlLines =
                    {
                        ControlLineInfo.Input("loada", "Take the value from bus a", "a"),
                        ControlLineInfo.Input("loadb", "Take the value from bus b", "b"),
                        ControlLineInfo.Output("outputa", "Put the value on bus a", "a"),
                        ControlLineInfo.Output("outputb", "Put the value on bus b", "b"),
                        ControlLineInfo.Internal("reset", "Set to 0"),
                        ControlLineInfo.Internal("inc", "Add 1"),
                        ControlLineInfo.Internal("dec", "Subtract 1"),
                    }
                });
            registry.Register("instructionRegister", c => new InstructionRegister(c.Name, c.Id, c.Bus()),
                new DeviceTypeInfo
                {
                    Category = "Control",
                    Description = "Micro step counter, advances every tick unless loaded or reset. Selects one of 65536 decoder ROM step addresses",
                    ControlLines = RegisterLines()
                });
            registry.Register("programCounter", c => new ProgramCounter(c.Name, c.Id, c.Bus()),
                new DeviceTypeInfo { Category = "Control", Description = "Register pointing at the next program cell; inc advances it", ControlLines = RegisterLines() });
            registry.Register("clock", c => new Clock(c.Name, c.Id),
                new DeviceTypeInfo
                {
                    Category = "Control",
                    Description = "Counts cycles, its disable line halts the machine",
                    Ports = new List<string>(),
                    ControlLines = { ControlLineInfo.Internal("disable", "Halt the machine") }
                });
            registry.Register("alu", c => new ALU(c.Name, c.Id, c.Connection<Register>("a"), c.Connection<Register>("b"), c.Connection<Register>("status"), c.Bus()),
                new DeviceTypeInfo
                {
                    Category = "Arithmetic",
                    Description = "add, sub and cmp on two registers, writes flags to a status register",
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
                    }
                });
            registry.Register("rom", c => new RomModule(c.Name, c.Id, c.Bus(), c.IntParameter("size", RomModule.DefaultSize, 1, 65536)),
                new DeviceTypeInfo { Category = "Memory", Description = "Read only memory with address register", Parameters = { size }, ControlLines = RomLines() });
            registry.Register("ram", c => new RamModule(c.Name, c.Id, c.Bus(), c.IntParameter("size", RomModule.DefaultSize, 1, 65536)),
                new DeviceTypeInfo { Category = "Memory", Description = "Memory with address register", Parameters = { size }, ControlLines = RamLines() });
            var mmuLines = RamLines();
            mmuLines.Add(ControlLineInfo.Input("loadcs", "Select the bank given on the bus"));
            mmuLines.Add(ControlLineInfo.Output("outputcs", "Put the selected bank number on the bus"));
            mmuLines.Add(ControlLineInfo.Internal("select0stack", "Select bank 0, the stack bank"));
            registry.Register("mmu", c =>
                {
                    int banks = c.IntParameter("banks", MMU.DefaultBanks, 1, 256), bankSize = c.IntParameter("bankSize", RomModule.DefaultSize, 1, 65536);
                    if ((long)banks * bankSize > MMU.MaxCells) throw c.Error($"{banks} banks of {bankSize} cells is more than {MMU.MaxCells} cells");
                    return new MMU(c.Name, c.Id, c.Bus(), banks, bankSize);
                },
                new DeviceTypeInfo
                {
                    Category = "Memory",
                    Description = "Banks of memory, selected by a chip select register",
                    Parameters =
                    {
                        new ParameterInfo { Name = "banks", Description = "Number of banks", Min = 1, Max = 256, Default = MMU.DefaultBanks },
                        new ParameterInfo { Name = "bankSize", Description = "16 bit cells per bank", Min = 1, Max = 65536, Default = RomModule.DefaultSize },
                    },
                    ControlLines = mmuLines
                });
            registry.Register("display", c => new CharacterDisplay(c.Name, c.Id, c.Bus(), c.IntParameter("columns", 16, 1, 64), c.IntParameter("rows", 4, 1, 16)),
                new DeviceTypeInfo
                {
                    Category = "I/O",
                    Description = "Character display, 8 bit Latin-1 (ISO-8859-1). load prints the bus value at the cursor, clear blanks it",
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
                    Description = "640 x 480 colour display, one RGB565 word per pixel. plot writes at the cursor and moves right",
                    ControlLines =
                    {
                        ControlLineInfo.Input("loadx", "Set the cursor column from the bus (0-639)"),
                        ControlLineInfo.Input("loady", "Set the cursor row from the bus (0-479)"),
                        ControlLineInfo.Input("plot", "Write the RGB565 colour on the bus at the cursor, then move right"),
                        ControlLineInfo.Internal("clear", "Blank the screen and move the cursor home"),
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
