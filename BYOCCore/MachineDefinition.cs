using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace BYOCCore
{
    public class MachineDefinition
    {
        public string Name { get; set; }
        public List<BusDefinition> Buses { get; set; } = new List<BusDefinition>();
        public List<DeviceDefinition> Devices { get; set; } = new List<DeviceDefinition>();
        public DecoderDefinition Decoder { get; set; }
        // ID of the clock device whose "disable" line halts the machine. Optional.
        public string Halt { get; set; }
        // ID of the memory device the assembled program is loaded into. Required when a program is given.
        public string ProgramMemory { get; set; }

        public static MachineDefinition FromJson(string json)
        {
            try
            {
                return JsonSerializer.Deserialize(json, MachineDefinitionJsonContext.Default.MachineDefinition)
                       ?? throw new MachineDefinitionException("Machine definition is empty.");
            }
            catch (JsonException e)
            {
                var location = e.Path == null ? "" : $" at {e.Path} (line {e.LineNumber + 1})";
                throw new MachineDefinitionException($"Machine definition is not valid JSON{location}: {e.Message}");
            }
        }
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, MachineDefinitionJsonContext.Default.MachineDefinition);
        }
    }
    public class BusDefinition
    {
        public string Id { get; set; }
        public int Width { get; set; } = 8;
    }
    public class DeviceDefinition
    {
        public string Id { get; set; }
        public string Type { get; set; }
        // Display name, defaults to the ID.
        public string Name { get; set; }
        // Shorthand for a device with a single bus connection: connects port "data" to this bus.
        public string Bus { get; set; }
        // Port name to bus ID, for devices with more than one bus connection.
        public Dictionary<string, string> Buses { get; set; } = new Dictionary<string, string>();
        // Named references to other devices, for example the ALU operand registers.
        public Dictionary<string, string> Connections { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, JsonElement> Parameters { get; set; } = new Dictionary<string, JsonElement>();

        public IEnumerable<KeyValuePair<string, string>> Ports()
        {
            if (!string.IsNullOrEmpty(Bus)) yield return new KeyValuePair<string, string>(DeviceBuildContext.DefaultPort, Bus);
            foreach (var port in Buses) yield return port;
        }
    }
    public class DecoderDefinition
    {
        // Register whose low 4 bits (NVCZ) select the status variant of a micro instruction.
        public string Status { get; set; }
        // Register holding the current micro instruction address.
        public string Instruction { get; set; }
    }
    public class MachineDefinitionException : Exception
    {
        public IReadOnlyList<string> Errors { get; }
        public MachineDefinitionException(string error) : this(new[] { error }) { }
        public MachineDefinitionException(IReadOnlyList<string> errors)
            : base(errors.Count == 1 ? errors[0] : "Machine definition has errors:" + Environment.NewLine + string.Join(Environment.NewLine, errors))
        {
            Errors = errors;
        }
    }

    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
    [JsonSerializable(typeof(MachineDefinition))]
    internal partial class MachineDefinitionJsonContext : JsonSerializerContext
    {
    }
}
