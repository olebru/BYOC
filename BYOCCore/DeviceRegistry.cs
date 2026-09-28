using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
namespace BYOCCore
{
    public delegate IBusDevice DeviceFactory(DeviceBuildContext context);

    // Maps the "type" of a device definition to the code that builds it. Register your own device types
    // here to use them from a machine definition.
    public class DeviceRegistry
    {
        private readonly Dictionary<string, DeviceFactory> factories = new Dictionary<string, DeviceFactory>();

        public static DeviceRegistry CreateDefault()
        {
            var registry = new DeviceRegistry();
            registry.Register("register", c => new Register(c.Name, c.Id, c.Bus(), c.ByteParameter("initialValue")));
            registry.Register("statusRegister", c => new StatusRegister(c.Name, c.Id, c.Bus()));
            registry.Register("instructionRegister", c => new InstructionRegister(c.Name, c.Id, c.Bus()));
            registry.Register("programCounter", c => new ProgramCounter(c.Name, c.Id, c.Bus()));
            registry.Register("dualPortRegister", c => new DualPortRegister(c.Name, c.Id, c.Bus("a"), c.Bus("b"), c.ByteParameter("initialValue")));
            registry.Register("alu", c => new ALU(c.Name, c.Id, c.Connection<Register>("a"), c.Connection<Register>("b"), c.Connection<Register>("status"), c.Bus()));
            registry.Register("rom", c => new RomModule(c.Name, c.Id, c.Bus()));
            registry.Register("ram", c => new RamModule(c.Name, c.Id, c.Bus()));
            registry.Register("mmu", c => new MMU(c.Name, c.Id, c.Bus()));
            registry.Register("clock", c => new Clock(c.Name, c.Id));
            return registry;
        }

        public void Register(string type, DeviceFactory factory)
        {
            factories[type] = factory;
        }
        public bool IsRegistered(string type)
        {
            return type != null && factories.ContainsKey(type);
        }
        public IEnumerable<string> Types { get { return factories.Keys; } }
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
