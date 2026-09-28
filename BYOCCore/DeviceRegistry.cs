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
            var initialValue = new ParameterInfo { Name = "initialValue", Description = "Value after power on" };
            var registry = new DeviceRegistry();
            registry.Register("register", c => new Register(c.Name, c.Id, c.Bus(), c.ByteParameter("initialValue")),
                new DeviceTypeInfo { Category = "Registers", Description = "8 bit register: output, load, reset, inc, dec", Parameters = { initialValue } });
            registry.Register("statusRegister", c => new StatusRegister(c.Name, c.Id, c.Bus()),
                new DeviceTypeInfo { Category = "Registers", Description = "Holds the NVCZ flags written by the ALU" });
            registry.Register("dualPortRegister", c => new DualPortRegister(c.Name, c.Id, c.Bus("a"), c.Bus("b"), c.ByteParameter("initialValue")),
                new DeviceTypeInfo { Category = "Registers", Description = "Register on two buses, moves values between them", Ports = new List<string> { "a", "b" }, Parameters = { initialValue } });
            registry.Register("instructionRegister", c => new InstructionRegister(c.Name, c.Id, c.Bus()),
                new DeviceTypeInfo { Category = "Control", Description = "Micro step counter, advances every tick unless loaded or reset" });
            registry.Register("programCounter", c => new ProgramCounter(c.Name, c.Id, c.Bus()),
                new DeviceTypeInfo { Category = "Control", Description = "Register with count, points at the next program byte" });
            registry.Register("clock", c => new Clock(c.Name, c.Id),
                new DeviceTypeInfo { Category = "Control", Description = "Counts cycles, its disable line halts the machine", Ports = new List<string>() });
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
                    }
                });
            registry.Register("rom", c => new RomModule(c.Name, c.Id, c.Bus()),
                new DeviceTypeInfo { Category = "Memory", Description = "256 bytes read only memory with address register" });
            registry.Register("ram", c => new RamModule(c.Name, c.Id, c.Bus()),
                new DeviceTypeInfo { Category = "Memory", Description = "256 bytes memory with address register" });
            registry.Register("mmu", c => new MMU(c.Name, c.Id, c.Bus()),
                new DeviceTypeInfo { Category = "Memory", Description = "256 banks of 256 bytes, selected by a chip select register" });
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
