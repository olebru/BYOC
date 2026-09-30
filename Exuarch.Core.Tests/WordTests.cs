using System;
using System.Linq;
using Exuarch.Core;

namespace Exuarch.Core.Tests;

// The machine is 16 bits throughout: buses, registers, memory cells, the ALU and the micro step register.
public class WordTests
{
    private static Machine Default(string src) =>
        new Machine(MachineDefinition.FromJson(ExampleData.MACHINE), ExampleData.MICROCODE, src);

    private static Machine RunToHalt(Machine c, int maxTicks = 500000)
    {
        int ticks = 0;
        foreach (var _ in c.Run()) Assert.True(++ticks < maxTicks, "program did not halt");
        return c;
    }

    [Fact]
    public void RegistersWrapAtSixteenBits()
    {
        var bus = new Bus();
        var register = new Register("R", "r", bus, 0xFFFF);
        bus.devices.Add(register);
        register.Enable("inc");
        bus.Clk();
        Assert.Equal(0, register.Data);
        register.Enable("dec");
        bus.Clk();
        Assert.Equal(0xFFFF, register.Data);
        Assert.Equal(0x1234, new Register("R", "r", bus, 0x71234).Data);
    }

    [Fact]
    public void BusCarriesSixteenBitWords()
    {
        var bus = new Bus();
        var source = new Register("S", "s", bus, 0xBEEF);
        var target = new Register("T", "t", bus);
        bus.devices.Add(source);
        bus.devices.Add(target);
        source.Enable("output");
        target.Enable("load");
        bus.Clk();
        Assert.Equal(0xBEEF, target.Data);
        Assert.Equal(16, Bus.Width);
    }

    [Theory]
    [InlineData("add", 0xFFFF, 1, 0, StatusRegister.ZeroFlag | StatusRegister.CarryFlag)]
    [InlineData("add", 0x7FFF, 1, 0x8000, StatusRegister.OverflowFlag)]
    [InlineData("add", 300, 400, 700, 0)]
    [InlineData("sub", 1000, 1000, 0, StatusRegister.ZeroFlag)]
    [InlineData("sub", 3, 5, 0xFFFE, StatusRegister.NegativeFlag | StatusRegister.CarryFlag)]
    [InlineData("sub", 0x8000, 1, 0x7FFF, StatusRegister.OverflowFlag)]
    public void AluFlagsAtSixteenBits(string function, int a, int b, int expected, int flags)
    {
        var bus = new Bus();
        var rega = new Register("A", "a", bus, a);
        var regb = new Register("B", "b", bus, b);
        var sta = new StatusRegister("S", "s", bus);
        var alu = new ALU("ALU", "alu", rega, regb, sta, bus);
        var result = new Register("R", "r", bus);
        bus.devices.Add(alu);
        bus.devices.Add(result);
        alu.Enable(function);
        result.Enable("load");
        bus.Clk();
        Assert.Equal(expected, result.Data);
        Assert.Equal(flags, sta.Data);
    }

    [Fact]
    public void DecoderHasSixtyFiveThousandStepAddresses()
    {
        Assert.Equal(16, DecoderRom.StepBits);
        Assert.Equal(65536, DecoderRom.AddressSpace);
        Assert.Equal(0x10005, DecoderRom.RomAddress(StatusRegister.ZeroFlag, 5));

        var microcode = MicrocodeDefinition.FromJson(ExampleData.MICROCODE);
        for (int i = 0; i < 300; i++)
        {
            microcode.Instructions.Add(new InstructionDefinition { Mnemonic = $"X{i}", Operands = 0, Steps = { new MicroStep { Signals = { "regi.reset", "pc.inc" } } } });
        }
        Assert.DoesNotContain(MicrocodeValidator.Validate(microcode, MachineDefinition.FromJson(ExampleData.MACHINE)), d => d.Severity == DiagnosticSeverity.Error);
        var c = new Machine(MachineDefinition.FromJson(ExampleData.MACHINE), microcode, "\tX299\n\tHLT");
        Assert.True(c.DecoderRom.FetchByteCodeFromMnemonic("X299") > 255);
        RunToHalt(c);
    }

    [Fact]
    public void MemoryAndMmuSizesComeFromParameters()
    {
        var c = Default("\tHLT");
        Assert.Equal(4096, c.Device<RamModule>("mem").Size);
        var mmu = c.Device<MMU>("mmu");
        Assert.Equal(16, mmu.RamBanks.Length);
        Assert.Equal(4096, mmu.RamBanks[0].Size);
        Assert.Equal(4095, c.Device<Register>("regsp").Data);
    }

    [Fact]
    public void TheLargestMmuBuildsAndOnlyAllocatesBanksThatAreWritten()
    {
        var definition = MachineDefinition.FromJson(ExampleData.MACHINE);
        using var banks = System.Text.Json.JsonDocument.Parse("256");
        using var bankSize = System.Text.Json.JsonDocument.Parse("65536");
        definition.FindDevice("mmu").Parameters["banks"] = banks.RootElement.Clone();
        definition.FindDevice("mmu").Parameters["bankSize"] = bankSize.RootElement.Clone();
        var c = new Machine(definition, ExampleData.MICROCODE, "\tLAI\t#7\n\tSTA\t#65535\n\tHLT");
        var mmu = c.Device<MMU>("mmu");
        Assert.Equal(256, mmu.RamBanks.Length);
        Assert.Equal(65536, mmu.RamBanks[255].Size);
        Assert.DoesNotContain(mmu.RamBanks, b => b.IsAllocated);

        foreach (var _ in c.Run()) { }
        Assert.Equal(7, mmu.RamBanks[0].ValueAt(65535));
        Assert.Single(mmu.RamBanks, b => b.IsAllocated);
        Assert.Equal(0, mmu.RamBanks[200].ValueAt(1234));
    }

    [Fact]
    public void CellsHoldSixteenUnsignedBits()
    {
        var bus = new Bus();
        var source = new Register("SRC", "src", bus);
        var ram = new RamModule("RAM", "ram", bus, 16);
        bus.devices.Add(source);
        bus.devices.Add(ram);
        Assert.False(ram.IsAllocated);
        Assert.Equal(0, ram.ValueAt(3));

        source.Data = 3;
        source.Enable("output");
        ram.Enable("loadmar");
        bus.Clk();
        source.Data = 0xFFFF;
        source.Enable("output");
        ram.Enable("load");
        bus.Clk();
        Assert.True(ram.IsAllocated);
        Assert.Equal(0xFFFF, ram.ValueAt(3));

        ram.Enable("output");
        source.Enable("load");
        bus.Clk();
        Assert.Equal(0xFFFF, source.Data);

        ram.LoadProgram(new[] { 0x1_2345, -1 });
        Assert.Equal(new[] { 0x2345, 0xFFFF }, new[] { ram.ValueAt(0), ram.ValueAt(1) });
    }

    [Theory]
    [InlineData("mem", "size", 0, "between 1 and 65536")]
    [InlineData("mmu", "banks", 300, "between 1 and 256")]
    [InlineData("mmu", "bankSize", 70000, "between 1 and 65536")]
    [InlineData("regsp", "initialValue", 70000, "between 0 and 65535")]
    public void ParametersAreRangeChecked(string device, string parameter, int value, string expected)
    {
        var definition = MachineDefinition.FromJson(ExampleData.MACHINE);
        using var document = System.Text.Json.JsonDocument.Parse(value.ToString());
        definition.FindDevice(device).Parameters[parameter] = document.RootElement.Clone();
        var e = Assert.Throws<MachineDefinitionException>(() => new Machine(definition, ExampleData.MICROCODE, ""));
        Assert.Contains(expected, e.Message);
    }

    [Fact]
    public void WidthIsNoLongerPartOfADefinition()
    {
        var e = Assert.Throws<MachineDefinitionException>(() => MachineDefinition.FromJson("{ \"buses\": [ { \"id\": \"main\", \"width\": 8 } ] }"));
        Assert.Contains("width", e.Message);
    }

    [Fact]
    public void AssemblerUsesSixteenBitCells()
    {
        var rom = new DecoderRom(MicrocodeDefinition.FromJson(ExampleData.MICROCODE));
        var assembler = new Assembler(rom);
        var cells = assembler.Assemble("\tLAI\t#1000\n\tLBI\t#0xBEEF\n\tJMP\tfar\n\t.DATA\t#65535\nfar:\tHLT");
        Assert.Equal(new[] { rom.FetchByteCodeFromMnemonic("LAI"), 1000, rom.FetchByteCodeFromMnemonic("LBI"), 0xBEEF, rom.FetchByteCodeFromMnemonic("JMP"), 7, 65535, rom.FetchByteCodeFromMnemonic("HLT") }, cells);
        Assert.Contains("between 0 and 65535", Assert.Throws<FormatException>(() => assembler.Assemble("\tLAI\t#65536")).Message);
        Assert.Contains("only holds 4", Assert.Throws<FormatException>(() => new Assembler(rom, 4).Assemble("\tLAI\t#1\n\tLBI\t#2\n\tHLT")).Message);
    }

    [Fact]
    public void TheProgramCounterIsAPlainRegisterThatAdvancesWithInc()
    {
        var registry = DeviceRegistry.CreateDefault();
        Assert.Null(registry.Info("programCounter"));
        Assert.Null(registry.Info("rom"));
        Assert.Equal(new[] { "load", "reset" }, registry.Info("instructionRegister").ControlLines.Select(l => l.Name));
        Assert.Equal("register", MachineDefinition.FromJson(ExampleData.MACHINE).FindDevice("pc").Type);
        Assert.DoesNotContain("count", new Register("PC", "pc", new Bus()).SignalLines());
        Assert.DoesNotContain(MicrocodeDefinition.FromJson(ExampleData.MICROCODE).AllInstructions.SelectMany(i => i.Steps).SelectMany(s => s.Signals), s => s == "pc.count");
        Assert.DoesNotContain("\tpc\tcount\t", ExampleData.ROMDATA);
    }

    [Fact]
    public void JumpsLandExactlyOnTheirTarget()
    {
        var c = RunToHalt(Default("\tJMP\ttarget\n\tHLT\ntarget:\tLAI\t#7\n\tHLT"));
        Assert.Equal(7, c.Device<Register>("rega").Data);
        Assert.Equal(c.Assembler.labelLUT["target"] + 2, c.Device<Register>("pc").Data);
    }

    [Fact]
    public void FibonacciRunsPastTheEightBitLimit()
    {
        var c = RunToHalt(Default(ExampleData.FIBONACCI));
        var text = c.Device<CharacterDisplay>("lcd").Text.Replace("\n", "").Trim();
        Assert.Equal("1 1 2 3 5 8 13 21 34 55 89 144 233 377 610 987 1597 2584 4181 6765 10946 17711 28657 46368", text);
    }

    [Fact]
    public void StackUsesTheTopOfBankZero()
    {
        var c = RunToHalt(Default("\tLAI\t#1234\n\tPSA\n\tLAI\t#0\n\tPOA\n\tHLT"));
        Assert.Equal(1234, c.Device<Register>("rega").Data);
        Assert.Equal(4095, c.Device<Register>("regsp").Data);
        Assert.Equal(1234, c.Device<MMU>("mmu").RamBanks[0].memory[4094]);
    }

    [Fact]
    public void EveryExampleProgramBuildsAndRuns()
    {
        foreach (var (name, source) in ExampleData.Programs)
        {
            var c = Default(source);
            Assert.True(c.ProgramByteCode.Length > 0, name);
            Assert.Empty(c.MicrocodeWarnings);
            for (int i = 0; i < 2000 && !c.IsHalted; i++) c.SingleStep();
        }
    }
}
