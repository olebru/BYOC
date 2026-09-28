using System;
using System.Collections.Generic;
using System.Linq;
using BYOCCore;

namespace BYOCCore.Tests;

public class MachineDefinitionTests
{
    private static string Row(string clk, string device, string function, string mnemonic)
        => $"{clk}\t{device}\t{function}\t{mnemonic}\tx\tx\tx\tx";

    // Minimal machine: fetch, halt, and whatever extra microcode a test adds.
    private const string MinimalJson = """
        {
          "name": "mini",
          "buses": [ { "id": "main" } ],
          "devices": [
            { "id": "pc", "type": "programCounter", "bus": "main" },
            { "id": "mem", "type": "ram", "bus": "main" },
            { "id": "ir", "type": "instructionRegister", "bus": "main" },
            { "id": "st", "type": "statusRegister", "bus": "main" },
            { "id": "clk", "type": "clock" }
          ],
          "decoder": { "status": "st", "instruction": "ir" },
          "halt": "clk",
          "programMemory": "mem"
        }
        """;

    private static readonly string FetchAndHalt = string.Join("\n",
        Row("p", "pc", "output", "FTC"),
        Row("s", "mem", "loadmar", "FTC"),
        Row("p", "mem", "output", "FTC"),
        Row("s", "ir", "load", "FTC"),
        Row("s", "pc", "count", "FTC"),
        Row("p", "clk", "disable", "HLT"));

    private static MachineDefinitionException Fails(string json, string microcode = null, DeviceRegistry registry = null)
    {
        return Assert.Throws<MachineDefinitionException>(() => Machine.FromJson(json, microcode ?? FetchAndHalt, "", registry));
    }

    [Fact]
    public void DefaultMachineLoads()
    {
        var c = Machine.CreateDefault();
        Assert.Equal("BYOC-8", c.Definition.Name);
        Assert.Equal(new[] { "regi", "pc", "regsp", "rega", "regb", "regc", "regs", "alu", "regsta", "mem", "mmu", "clk" },
            c.Devices.Select(d => d.ID()));
        Assert.Equal(255, c.Device<Register>("regsp").Data);
        Assert.Equal("REGSP", c.Device<Register>("regsp").DisplayName());
    }

    [Fact]
    public void DefinitionRoundTripsThroughJson()
    {
        var definition = MachineDefinition.FromJson(ExampleData.MACHINE);
        var again = MachineDefinition.FromJson(definition.ToJson());
        Assert.Equal(definition.ToJson(), again.ToJson());
    }

    [Fact]
    public void CommentsAndTrailingCommasAreAllowed()
    {
        var json = MinimalJson.Replace("\"name\": \"mini\",", "// a comment\n\"name\": \"mini\",").Replace("\"programMemory\": \"mem\"", "\"programMemory\": \"mem\",");
        Assert.Equal("mini", Machine.FromJson(json, FetchAndHalt, "").Definition.Name);
    }

    [Fact]
    public void MinimalMachineRunsToHalt()
    {
        var c = Machine.FromJson(MinimalJson, FetchAndHalt, "\tHLT");
        Assert.Equal(3, c.Run().Count());
        Assert.True(c.IsHalted);
    }

    [Fact]
    public void MisspelledPropertyIsRejected()
    {
        var e = Fails(MinimalJson.Replace("\"halt\"", "\"hlat\""));
        Assert.Contains("hlat", e.Message);
    }

    [Fact]
    public void InvalidJsonIsReported()
    {
        Assert.Contains("not valid JSON", Fails("{ \"name\": ").Message);
    }

    [Fact]
    public void AllStructuralErrorsAreReportedTogether()
    {
        var json = """
            {
              "buses": [ { "id": "main" }, { "id": "wide", "width": 16 } ],
              "devices": [
                { "id": "a", "type": "register", "bus": "nowhere" },
                { "id": "a", "type": "register", "bus": "main" },
                { "id": "x", "type": "flux-capacitor", "bus": "main" },
                { "id": "alu", "type": "alu", "bus": "main", "connections": { "a": "ghost" } }
              ],
              "decoder": { "status": "missing" },
              "halt": "nope"
            }
            """;
        var errors = Fails(json).Errors;
        Assert.Contains(errors, e => e.Contains("only 8 bit buses"));
        Assert.Contains(errors, e => e.Contains("unknown bus 'nowhere'"));
        Assert.Contains(errors, e => e.Contains("'a' is defined more than once"));
        Assert.Contains(errors, e => e.Contains("unknown type 'flux-capacitor'"));
        Assert.Contains(errors, e => e.Contains("unknown device 'ghost'"));
        Assert.Contains(errors, e => e.Contains("\"decoder.status\" refers to unknown device 'missing'"));
        Assert.Contains(errors, e => e.Contains("\"decoder.instruction\" is required"));
        Assert.Contains(errors, e => e.Contains("\"halt\" refers to unknown device 'nope'"));
    }

    [Fact]
    public void ConnectionOfWrongDeviceTypeIsRejected()
    {
        var json = MinimalJson.Replace("{ \"id\": \"clk\", \"type\": \"clock\" }",
            "{ \"id\": \"clk\", \"type\": \"clock\" }, { \"id\": \"alu\", \"type\": \"alu\", \"bus\": \"main\", \"connections\": { \"a\": \"clk\", \"b\": \"st\", \"status\": \"st\" } }");
        var e = Fails(json);
        Assert.Contains("connection 'a' must be a Register", e.Message);
    }

    [Fact]
    public void MissingBusForDeviceIsRejected()
    {
        var e = Fails(MinimalJson.Replace("{ \"id\": \"st\", \"type\": \"statusRegister\", \"bus\": \"main\" }", "{ \"id\": \"st\", \"type\": \"statusRegister\" }"));
        Assert.Contains("Device 'st'", e.Message);
        Assert.Contains("needs a bus connection", e.Message);
    }

    [Fact]
    public void DecoderMustNameRegisters()
    {
        var e = Fails(MinimalJson.Replace("\"status\": \"st\"", "\"status\": \"clk\""));
        Assert.Contains("\"decoder.status\" must name a Register", e.Message);
    }

    [Fact]
    public void ConnectionCycleIsReported()
    {
        var registry = DeviceRegistry.CreateDefault();
        registry.Register("loop", c => { c.Connection<IBusDevice>("next"); return new Clock(c.Name, c.Id); });
        var json = MinimalJson.Replace("{ \"id\": \"clk\", \"type\": \"clock\" }",
            "{ \"id\": \"clk\", \"type\": \"clock\" }, { \"id\": \"l1\", \"type\": \"loop\", \"connections\": { \"next\": \"l2\" } }, { \"id\": \"l2\", \"type\": \"loop\", \"connections\": { \"next\": \"l1\" } }");
        Assert.Contains("cycle: l1 -> l2 -> l1", Fails(json, registry: registry).Message);
    }

    [Fact]
    public void MicrocodeIsCheckedAgainstDevices()
    {
        var microcode = FetchAndHalt + "\n" + Row("p", "ghost", "load", "BAD") + "\n" + Row("p", "pc", "explode", "BAD");
        var errors = Fails(MinimalJson, microcode).Errors;
        Assert.Contains(errors, e => e.Contains("unknown device 'ghost'"));
        Assert.Contains(errors, e => e.Contains("device 'pc' has no control line 'explode'"));
    }

    [Fact]
    public void ProgramNeedsProgramMemory()
    {
        var json = MinimalJson.Replace(",\n  \"programMemory\": \"mem\"", "").Replace(",\r\n  \"programMemory\": \"mem\"", "");
        var e = Assert.Throws<MachineDefinitionException>(() => Machine.FromJson(json, FetchAndHalt, "\tHLT"));
        Assert.Contains("programMemory", e.Message);
    }

    [Fact]
    public void CustomDeviceTypesCanBeRegistered()
    {
        var registry = DeviceRegistry.CreateDefault();
        registry.Register("counter", c => new ProgramCounter(c.Name, c.Id, c.Bus()));
        var json = MinimalJson.Replace("\"type\": \"programCounter\"", "\"type\": \"counter\"");
        var c = Machine.FromJson(json, FetchAndHalt, "\tHLT", registry);
        Assert.IsType<ProgramCounter>(c.Device("pc"));
    }

    [Fact]
    public void ValueCrossesBusesThroughDualPortRegister()
    {
        var json = """
            {
              "buses": [ { "id": "main" }, { "id": "io" } ],
              "devices": [
                { "id": "pc", "type": "programCounter", "bus": "main" },
                { "id": "mem", "type": "ram", "bus": "main" },
                { "id": "ir", "type": "instructionRegister", "bus": "main" },
                { "id": "st", "type": "statusRegister", "bus": "main" },
                { "id": "rega", "type": "register", "bus": "main", "parameters": { "initialValue": 42 } },
                { "id": "bridge", "type": "dualPortRegister", "buses": { "a": "main", "b": "io" } },
                { "id": "out", "type": "register", "bus": "io" },
                { "id": "clk", "type": "clock" }
              ],
              "decoder": { "status": "st", "instruction": "ir" },
              "halt": "clk",
              "programMemory": "mem"
            }
            """;
        var microcode = FetchAndHalt + "\n" + string.Join("\n",
            Row("p", "rega", "output", "OUT"),
            Row("s", "bridge", "loada", "OUT"),
            Row("p", "bridge", "outputb", "OUT"),
            Row("s", "out", "load", "OUT"),
            Row("s", "ir", "reset", "OUT"));

        var c = Machine.FromJson(json, microcode, "\tOUT\n\tHLT");
        foreach (var _ in c.Run().Take(100)) { }

        Assert.True(c.IsHalted);
        Assert.Equal(42, c.Device<Register>("out").Data);
        Assert.Contains(c.Device("bridge"), c.Buses["main"].devices);
        Assert.Contains(c.Device("bridge"), c.Buses["io"].devices);
        Assert.DoesNotContain(c.Device("out"), c.Buses["main"].devices);
    }

    // Two signals driving the same bus in one step are caught when the machine is built, not at run time.
    [Fact]
    public void BusConflictInMicrocodeIsRejectedAtLoad()
    {
        var microcode = FetchAndHalt + "\n" + Row("p", "pc", "output", "BAD") + "\n" + Row("s", "ir", "output", "BAD");
        var e = Fails(MinimalJson, microcode);
        Assert.Contains("bus 'main'", e.Message);
        Assert.Contains("pc.output", e.Message);
        Assert.Contains("ir.output", e.Message);
    }
}
