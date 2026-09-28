using System;
using System.Linq;
namespace BYOCCore
{
    // Edits that keep every reference in a definition consistent, used by the machine editor.
    public static class MachineDefinitionEditing
    {
        public static DeviceDefinition FindDevice(this MachineDefinition definition, string id)
        {
            return definition.Devices.FirstOrDefault(d => d.Id == id);
        }
        public static BusDefinition FindBus(this MachineDefinition definition, string id)
        {
            return definition.Buses.FirstOrDefault(b => b.Id == id);
        }

        // Returns prefix if unused, otherwise prefix2, prefix3, ...
        public static string NextFreeId(this MachineDefinition definition, string prefix)
        {
            bool Used(string id) => definition.FindDevice(id) != null || definition.FindBus(id) != null;
            if (!Used(prefix)) return prefix;
            for (int i = 2; ; i++)
            {
                if (!Used(prefix + i)) return prefix + i;
            }
        }

        public static void RenameDevice(this MachineDefinition definition, string oldId, string newId)
        {
            var device = definition.FindDevice(oldId) ?? throw new ArgumentException($"No device '{oldId}'.");
            if (oldId == newId) return;
            CheckNewId(definition, newId);
            device.Id = newId;
            foreach (var other in definition.Devices)
            {
                foreach (var connection in other.Connections.Where(c => c.Value == oldId).ToList())
                {
                    other.Connections[connection.Key] = newId;
                }
            }
            if (definition.Decoder != null)
            {
                if (definition.Decoder.Status == oldId) definition.Decoder.Status = newId;
                if (definition.Decoder.Instruction == oldId) definition.Decoder.Instruction = newId;
            }
            if (definition.Halt == oldId) definition.Halt = newId;
            if (definition.ProgramMemory == oldId) definition.ProgramMemory = newId;
        }

        public static void RemoveDevice(this MachineDefinition definition, string id)
        {
            definition.Devices.RemoveAll(d => d.Id == id);
            foreach (var other in definition.Devices)
            {
                foreach (var connection in other.Connections.Where(c => c.Value == id).ToList())
                {
                    other.Connections.Remove(connection.Key);
                }
            }
            if (definition.Decoder != null)
            {
                if (definition.Decoder.Status == id) definition.Decoder.Status = null;
                if (definition.Decoder.Instruction == id) definition.Decoder.Instruction = null;
            }
            if (definition.Halt == id) definition.Halt = null;
            if (definition.ProgramMemory == id) definition.ProgramMemory = null;
        }

        public static void RenameBus(this MachineDefinition definition, string oldId, string newId)
        {
            var bus = definition.FindBus(oldId) ?? throw new ArgumentException($"No bus '{oldId}'.");
            if (oldId == newId) return;
            CheckNewId(definition, newId);
            bus.Id = newId;
            foreach (var device in definition.Devices)
            {
                foreach (var port in device.Ports().Where(p => p.Value == oldId).ToList())
                {
                    device.SetPortBus(port.Key, newId);
                }
            }
        }

        public static void RemoveBus(this MachineDefinition definition, string id)
        {
            definition.Buses.RemoveAll(b => b.Id == id);
            foreach (var device in definition.Devices)
            {
                foreach (var port in device.Ports().Where(p => p.Value == id).ToList())
                {
                    device.SetPortBus(port.Key, null);
                }
            }
        }

        public static MachineDefinition Clone(this MachineDefinition definition)
        {
            return MachineDefinition.FromJson(definition.ToJson());
        }

        private static void CheckNewId(MachineDefinition definition, string newId)
        {
            if (string.IsNullOrWhiteSpace(newId)) throw new ArgumentException("ID can not be empty.");
            if (newId.Any(char.IsWhiteSpace)) throw new ArgumentException("ID can not contain spaces.");
            if (definition.FindDevice(newId) != null || definition.FindBus(newId) != null)
            {
                throw new ArgumentException($"'{newId}' is already used.");
            }
        }
    }
}
