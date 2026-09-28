using System;
using System.Collections.Generic;
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
            foreach (var step in MicroSteps(definition))
            {
                for (int i = 0; i < step.Signals.Count; i++)
                {
                    if (Signal.TryParse(step.Signals[i], out var signal) && signal.Device == oldId)
                    {
                        step.Signals[i] = $"{newId}.{signal.Line}";
                    }
                }
            }
        }

        // Every place the microcode enables a control line of the device: instruction, step index and signal.
        public static List<(InstructionDefinition Instruction, int Step, string Signal)> SignalUsages(this MachineDefinition definition, string deviceId)
        {
            var usages = new List<(InstructionDefinition, int, string)>();
            var microcode = definition.Decoder?.Microcode;
            if (microcode == null) return usages;
            foreach (var instruction in microcode.AllInstructions)
            {
                for (int step = 0; step < instruction.Steps.Count; step++)
                {
                    foreach (var text in instruction.Steps[step].Signals)
                    {
                        if (Signal.TryParse(text, out var signal) && signal.Device == deviceId) usages.Add((instruction, step, text));
                    }
                }
            }
            return usages;
        }

        // Removes the device's signals from the microcode. Steps left empty stay, so step numbering is kept.
        public static int RemoveSignalsOf(this MachineDefinition definition, string deviceId)
        {
            int removed = 0;
            foreach (var step in MicroSteps(definition))
            {
                removed += step.Signals.RemoveAll(text => Signal.TryParse(text, out var signal) && signal.Device == deviceId);
            }
            return removed;
        }

        private static IEnumerable<MicroStep> MicroSteps(MachineDefinition definition)
        {
            var microcode = definition.Decoder?.Microcode;
            return microcode == null ? Enumerable.Empty<MicroStep>() : microcode.AllInstructions.SelectMany(i => i.Steps);
        }

        // Removes the device and every reference to it, except its microcode signals: those become errors
        // unless RemoveSignalsOf is called as well.
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

        public const double LayoutCardWidth = 150;
        public const double LayoutFirstBusY = 190;
        public const double LayoutBusSpacing = 320;

        // Gives buses and devices without a position one: buses stacked, devices in rows around their first bus,
        // one row above it and the rest below. With force, everything is laid out again.
        public static void EnsureLayout(this MachineDefinition definition, bool force = false)
        {
            for (int i = 0; i < definition.Buses.Count; i++)
            {
                if (force || definition.Buses[i].Layout == null)
                {
                    definition.Buses[i].Layout = new Position { X = 0, Y = LayoutFirstBusY + i * LayoutBusSpacing };
                }
            }
            const int perRow = 6;
            var unplaced = definition.Devices.Where(d => force || d.Layout == null).ToList();
            foreach (var band in unplaced.GroupBy(d => Math.Max(0, definition.Buses.FindIndex(b => b.Id == d.Ports().Select(p => p.Value).FirstOrDefault()))))
            {
                var busY = definition.Buses.Count == 0 ? LayoutFirstBusY : definition.Buses[Math.Min(band.Key, definition.Buses.Count - 1)].Layout.Y;
                int index = 0;
                foreach (var device in band)
                {
                    int row = index / perRow;
                    int column = index % perRow;
                    double y = row == 0 ? busY - 140 : busY + 60 + (row - 1) * 130;
                    device.Layout = new Position { X = 30 + column * (LayoutCardWidth + 36), Y = y };
                    index++;
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
