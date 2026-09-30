using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exuarch.Core;

namespace Exuarch.Core.Tests;

// The handbook tutorials, done by hand: each builds the machine its page describes on top of the one before,
// runs the page's program and checks what it shows. The page has to contain the signals and program lines the
// test uses, so the text can not drift away from a machine that works.
public class TutorialTests
{
    private static string Page(string file)
    {
        using var stream = typeof(BuiltInPackages).Assembly.GetManifestResourceStream($"Exuarch.Core.Guides/tutorials/{file}");
        Assert.True(stream != null, $"tutorial {file} is not embedded");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static MicroStep Step(params string[] signals) => new MicroStep { Signals = signals.ToList() };
    private static MicroStep When(bool z, params string[] signals) => new MicroStep { When = new FlagCondition { Z = z }, Signals = signals.ToList() };

    private static void Add(MachineDefinition machine, string mnemonic, int operands, OperandType? type, params MicroStep[] steps)
    {
        var instruction = new InstructionDefinition { Mnemonic = mnemonic, Operands = operands, Steps = steps.ToList() };
        if (type.HasValue) instruction.OperandTypes = Enumerable.Repeat(type.Value, operands).ToList();
        machine.Decoder.Microcode.Instructions.Add(instruction);
    }

    // ---- The machine after each tutorial ----

    private static MachineDefinition FirstMachine()
    {
        var machine = MachineTemplates.Minimal("Mine").Machine;
        machine.Devices.Add(new DeviceDefinition { Id = "lcd", Type = "display", Bus = "main" });
        Add(machine, "OUT", 1, OperandType.Value,
            Step("pc.output", "mem.loadmar"),
            Step("mem.output", "lcd.load", "pc.inc", "ir.reset"));
        return machine;
    }

    private static MachineDefinition WithAlu()
    {
        var machine = FirstMachine();
        machine.Devices.Add(new DeviceDefinition { Id = "a", Type = "register", Bus = "main" });
        machine.Devices.Add(new DeviceDefinition { Id = "b", Type = "register", Bus = "main" });
        machine.Devices.Add(new DeviceDefinition { Id = "alu", Type = "alu", Bus = "main", Connections = { ["a"] = "a", ["b"] = "b", ["status"] = "status" } });
        Add(machine, "LAI", 1, OperandType.Value, Step("pc.output", "mem.loadmar"), Step("mem.output", "a.load", "pc.inc", "ir.reset"));
        Add(machine, "LBI", 1, OperandType.Value, Step("pc.output", "mem.loadmar"), Step("mem.output", "b.load", "pc.inc", "ir.reset"));
        Add(machine, "ADD", 0, null, Step("alu.add", "a.load", "ir.reset"));
        Add(machine, "OUTA", 0, null, Step("a.output", "lcd.load", "ir.reset"));
        return machine;
    }

    private static MachineDefinition WithLoops()
    {
        var machine = WithAlu();
        Add(machine, "SUB", 0, null, Step("alu.sub", "a.load", "ir.reset"));
        Add(machine, "CMP", 0, null, Step("alu.cmp", "ir.reset"));
        Add(machine, "JNZ", 1, OperandType.Address,
            Step("pc.output", "mem.loadmar"),
            When(false, "mem.output", "pc.load", "ir.reset"),
            When(true, "pc.inc", "ir.reset"));
        return machine;
    }

    private static MachineDefinition WithStack()
    {
        var machine = WithLoops();
        machine.Devices.Add(new DeviceDefinition { Id = "tmp", Type = "register", Bus = "main" });
        var sp = new DeviceDefinition { Id = "sp", Type = "register", Bus = "main" };
        sp.Parameters["initialValue"] = System.Text.Json.JsonDocument.Parse("4096").RootElement.Clone();
        machine.Devices.Add(sp);
        Add(machine, "CALL", 1, OperandType.Address,
            Step("pc.output", "mem.loadmar"),
            Step("mem.output", "tmp.load", "pc.inc", "sp.dec"),
            Step("sp.output", "mem.loadmar"),
            Step("pc.output", "mem.load"),
            Step("tmp.output", "pc.load", "ir.reset"));
        Add(machine, "RET", 0, null,
            Step("sp.output", "mem.loadmar"),
            Step("mem.output", "pc.load", "sp.inc", "ir.reset"));
        return machine;
    }

    private static MachineDefinition WithKeypad()
    {
        var machine = WithStack();
        machine.Devices.Add(new DeviceDefinition { Id = "keypad", Type = "keypad", Bus = "main" });
        Add(machine, "KEYS", 0, null, Step("keypad.output", "a.load", "ir.reset"));
        return machine;
    }

    // ---- The programs, as the pages write them ----

    private const string HiProgram = "        OUT 'H'\n        OUT 'i'\n        HLT";
    private const string AbcProgram = "        LAI 'A'\n        OUTA\n        LBI 1\n        ADD\n        OUTA\n        ADD\n        OUTA\n        HLT";
    private const string CountdownProgram = "        LAI '5'\nloop:   OUTA\n        LBI 1\n        SUB\n        LBI '0'\n        CMP\n        JNZ loop\n        HLT";
    private const string TwiceProgram = "        LAI 'O'\n        CALL twice\n        LAI 'K'\n        CALL twice\n        HLT\n\ntwice:  OUTA\n        OUTA\n        RET";
    private const string KeysProgram = "wait:   KEYS\n        LBI 0\n        CMP\n        JNZ got\n        JMP wait\n\ngot:    LBI 16\n        CMP\n        JNZ show\n        HLT\n\nshow:   LBI '0'\n        ADD\n        OUTA\n        JMP wait";

    private static Machine Build(MachineDefinition machine, string program)
    {
        Assert.Empty(Machine.ValidateDefinition(machine, DeviceRegistry.CreateDefault()));
        Assert.DoesNotContain(MicrocodeValidator.Validate(machine.Decoder.Microcode, machine), d => d.Severity == DiagnosticSeverity.Error);
        var analysis = new AssemblyLanguage(machine.Decoder.Microcode, MemoryModule.DefaultSize).Analyze(program);
        Assert.True(analysis.Diagnostics.Count == 0, string.Join(" | ", analysis.Diagnostics));
        return new Machine(machine, program);
    }

    private static int RunToHalt(Machine c, int limit = 2000)
    {
        int ticks = 0;
        while (!c.IsHalted) { c.SingleStep(); Assert.True(++ticks < limit, "the program did not halt"); }
        return ticks;
    }

    private static string Lcd(Machine c) => c.Device<CharacterDisplay>("lcd").Line(0).TrimEnd();

    // The page shows every program in an asm block and names each signal of the new instructions.
    private static void PageShows(string page, string program, params string[] signals)
    {
        Assert.Contains("```asm\n" + program + "\n```", page.Replace("\r\n", "\n"));
        foreach (var signal in signals) Assert.Contains($"`{signal}`", page);
    }

    [Fact]
    public void YourFirstMachineSaysHi()
    {
        var c = Build(FirstMachine(), HiProgram);
        Assert.Equal(11, RunToHalt(c));
        Assert.Equal("Hi", Lcd(c));
        var page = Page("01-first-machine.md");
        Assert.StartsWith("# Your first machine", page);
        PageShows(page, HiProgram, "pc.output", "mem.loadmar", "mem.output", "lcd.load", "pc.inc", "ir.reset");
        Assert.Contains("`lcd`", page);
        Assert.Contains("11 ticks", page);
    }

    [Fact]
    public void RegistersAndTheAluCountAlongTheAlphabet()
    {
        var c = Build(WithAlu(), AbcProgram);
        RunToHalt(c);
        Assert.Equal("ABC", Lcd(c));
        var page = Page("02-registers-and-the-alu.md");
        Assert.StartsWith("# Registers and the ALU", page);
        PageShows(page, AbcProgram, "a.load", "b.load", "alu.add", "a.output");
        foreach (var name in new[] { "LAI", "LBI", "ADD", "OUTA" }) Assert.Contains($"`{name}`", page);
    }

    [Fact]
    public void LoopsAndFlagsCountDown()
    {
        var c = Build(WithLoops(), CountdownProgram);
        RunToHalt(c);
        Assert.Equal("54321", Lcd(c));
        // The trace numbers steps as written: the jump is step 2, falling through is step 3.
        Assert.Contains(c.History, t => t.Instruction == "JNZ" && t.StepIndex == 1);
        Assert.Contains(c.History, t => t.Instruction == "JNZ" && t.StepIndex == 2);
        var page = Page("03-loops-and-flags.md");
        Assert.StartsWith("# Loops and flags", page);
        PageShows(page, CountdownProgram, "alu.sub", "alu.cmp", "mem.output", "pc.load", "pc.inc");
        Assert.Contains("Z=0", page);
        Assert.Contains("Z=1", page);
    }

    [Fact]
    public void SubroutinesReturnWhereTheyWereCalled()
    {
        var c = Build(WithStack(), TwiceProgram);
        RunToHalt(c);
        Assert.Equal("OOKK", Lcd(c));
        // Every push was popped again.
        Assert.Equal(4096, c.Device<Register>("sp").Data);
        var page = Page("04-subroutines-and-the-stack.md");
        Assert.StartsWith("# Subroutines and the stack", page);
        PageShows(page, TwiceProgram, "tmp.load", "sp.dec", "sp.output", "mem.load", "tmp.output", "sp.inc");
        Assert.Contains("4096", page);
    }

    [Fact]
    public void TheKeypadEchoesKeysUntilSpace()
    {
        var c = Build(WithKeypad(), KeysProgram);
        var keypad = c.Device<Keypad>("keypad");
        void Tick(int n) { for (int i = 0; i < n; i++) { c.SingleStep(); Assert.False(c.IsHalted, "halted before space"); } }
        void Tap(Keypad.Keys key) { keypad.Press(key); keypad.Release(key); }

        // Nothing pressed: the program waits.
        Tick(200);
        Assert.Equal("", Lcd(c));
        // A tap is remembered until the program reads it, and shown once.
        Tap(Keypad.Keys.Up);
        Tick(200);
        Assert.Equal("1", Lcd(c));
        Tap(Keypad.Keys.Right);
        Tick(200);
        Assert.Equal("18", Lcd(c));
        Tap(Keypad.Keys.Space);
        RunToHalt(c, 200);
        Assert.Equal("18", Lcd(c));

        var page = Page("05-reading-the-keypad.md");
        Assert.StartsWith("# Reading the keypad", page);
        PageShows(page, KeysProgram, "keypad.output", "a.load");
        Assert.Contains("`keypad`", page);
    }

    [Fact]
    public void ATutorialsLinksLeadSomewhere()
    {
        foreach (var file in new[] { "01-first-machine.md", "02-registers-and-the-alu.md", "03-loops-and-flags.md", "04-subroutines-and-the-stack.md", "05-reading-the-keypad.md" })
        {
            var page = Page(file);
            Assert.Contains("## Next", page);
            Assert.NotEmpty(ReadmeLinks.In(page));
        }
    }
}
