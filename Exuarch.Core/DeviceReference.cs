using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace Exuarch.Core
{
    // A handbook page per device type, written from what the device registry says about it, so the reference
    // always matches the devices the app has.
    public static class DeviceReference
    {
        private static readonly Lazy<DeviceRegistry> registry = new Lazy<DeviceRegistry>(DeviceRegistry.CreateDefault);

        // The handbook pages that explain what a device type is for, besides the general one on devices.
        private static readonly Dictionary<string, string[]> Concepts = new Dictionary<string, string[]>
        {
            ["register"] = new[] { "registers-and-the-alu" },
            ["registerFile"] = new[] { "operands", "registers-and-the-alu" },
            ["statusRegister"] = new[] { "flags-and-conditions" },
            ["dualPortRegister"] = new[] { "bridges" },
            ["instructionRegister"] = new[] { "fetch-and-the-instruction-register", "ground-zero", "fetch-routine" },
            ["clock"] = new[] { "buses-and-ticks" },
            ["alu"] = new[] { "registers-and-the-alu", "flags-and-conditions" },
            ["ram"] = new[] { "memory-and-banks" },
            ["mmu"] = new[] { "memory-and-banks" },
            ["display"] = new[] { "first-machine" },
            ["framebuffer"] = new[] { "bus-masters", "graphics-pipeline" },
            ["doubleFramebuffer"] = new[] { "bus-masters", "graphics-pipeline" },
            ["blitter"] = new[] { "bus-masters", "interrupts" },
            ["interruptController"] = new[] { "interrupts" },
            ["timer"] = new[] { "interrupts" },
            ["keypad"] = new[] { "reading-the-keypad", "interrupts" },
            ["rasterizer"] = new[] { "graphics-pipeline", "bus-masters", "interrupts" },
            ["depthBuffer"] = new[] { "graphics-pipeline" },
            ["mac"] = new[] { "graphics-pipeline" },
        };

        public static IEnumerable<DeviceTypeInfo> Types { get { return registry.Value.TypeInfos; } }

        // Registration order, grouped by category the way the palette groups them.
        public static IEnumerable<IGrouping<string, DeviceTypeInfo>> ByCategory { get { return Types.GroupBy(t => t.Category); } }

        public static bool Exists(string type)
        {
            return type != null && registry.Value.IsRegistered(type);
        }

        public static string Markdown(string type)
        {
            var info = registry.Value.Info(type);
            var text = new StringBuilder();
            text.Append($"# {info.Type}\n\n");
            text.Append($"{Sentence(info.Description)}\n\n");
            text.Append($"A device in the *{info.Category}* group of the palette. ");
            text.Append(info.Ports.Count switch
            {
                0 => "It is on no bus.",
                1 => $"It has one bus port, `{info.Ports[0]}`.",
                _ => $"It has {info.Ports.Count} bus ports: {string.Join(", ", info.Ports.Select(p => $"`{p}`"))}.",
            });
            text.Append("\n\n## Control lines\n\n");
            text.Append("Microcode turns these on for one tick, written `<id>.<line>`.\n\n");
            text.Append("| Line | What it does | Bus |\n|---|---|---|\n");
            foreach (var line in info.ControlLines)
            {
                var bus = line.Drives != null ? $"drives `{line.Drives}`" : line.Reads != null ? $"reads `{line.Reads}`" : "";
                text.Append($"| `{line.Name}` | {Cell(line.Description)} | {bus} |\n");
            }
            if (info.Connections.Count > 0)
            {
                text.Append("\n## Connections\n\n");
                text.Append("Wired to other devices in the hardware design, outside the buses.\n\n");
                foreach (var connection in info.Connections) text.Append($"- `{connection.Name}`: {Sentence(connection.Description)}\n");
            }
            if (info.Parameters.Count > 0)
            {
                text.Append("\n## Parameters\n\n");
                text.Append("| Parameter | Range | Default | What it sets |\n|---|---|---|---|\n");
                foreach (var parameter in info.Parameters)
                {
                    text.Append($"| `{parameter.Name}` | {parameter.Min}–{parameter.Max} | {parameter.Default} | {Cell(parameter.Description)} |\n");
                }
            }
            var users = BuiltInPackages.All
                .Select(p => (Package: p, Ids: p.Machine.Devices.Where(d => d.Type == info.Type).Select(d => d.Id).ToList()))
                .Where(u => u.Ids.Count > 0)
                .ToList();
            if (users.Count > 0)
            {
                text.Append("\n## In the examples\n\n");
                foreach (var (package, ids) in users)
                {
                    text.Append($"- [{package.Name}](exuarch:package/{package.Name}): {string.Join(", ", ids.Select(id => $"`{id}`"))}\n");
                }
            }
            text.Append("\n## See also\n\n");
            text.Append("- [Devices and control lines](exuarch:guide/devices-and-control-lines)\n");
            foreach (var id in Concepts.TryGetValue(info.Type, out var ids) ? ids : Array.Empty<string>())
            {
                var guide = Guides.Find(id);
                text.Append($"- [{guide?.Title ?? id}](exuarch:guide/{id})\n");
            }
            return text.ToString().TrimEnd('\n');
        }

        private static string Sentence(string text)
        {
            text = (text ?? "").Trim();
            return text.Length == 0 || text.EndsWith(".") ? text : text + ".";
        }

        private static string Cell(string text)
        {
            return (text ?? "").Replace("|", "\\|").Replace("\n", " ");
        }
    }
}
