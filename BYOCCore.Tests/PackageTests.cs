using System;
using System.Linq;
using BYOCCore;

namespace BYOCCore.Tests;

public class PackageTests
{
    private static Machine Run(MachinePackage package, string source, int limit = 100000, string name = "program")
    {
        var c = new Machine(package.Machine, source) { RecordHistory = false };
        int ticks = 0;
        while (!c.IsHalted) { c.SingleStep(); Assert.True(++ticks < limit, $"{name} did not halt"); }
        Assert.Empty(c.MicrocodeWarnings);
        return c;
    }
    private static Machine Risc(string source, int limit = 100000)
    {
        return Run(BuiltInPackages.Get("RISC-16"), source, limit);
    }
    private static int[] Registers(Machine c) { return Enumerable.Range(0, 4).Select(n => c.Device<Register>($"r{n}").Data).ToArray(); }
    private static int Flags(Machine c) { return c.Device<StatusRegister>("cpsr").Data; }

    [Fact]
    public void BuiltInPackagesLoadWithTheDefaultFirst()
    {
        Assert.Equal(new[] { "BYOC-16", "RISC-16" }, BuiltInPackages.All.Select(p => p.Name));
        Assert.Same(BuiltInPackages.All[0], BuiltInPackages.Default);
        foreach (var package in BuiltInPackages.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(package.Description), package.Name);
            Assert.NotEmpty(package.Programs);
            Assert.Empty(MicrocodeValidator.Validate(package.Machine.Decoder.Microcode, package.Machine));
        }
    }

    // These loop for ever on purpose: the long running demo and the interactive sketch.
    private static readonly string[] LoopingPrograms = { "Stack and memory", "Sketch with the arrow keys" };

    [Fact]
    public void EveryProgramInEveryPackageAssemblesWithoutWarningsAndHalts()
    {
        foreach (var package in BuiltInPackages.All)
        {
            var language = new AssemblyLanguage(package.Machine.Decoder.Microcode, 4096);
            foreach (var program in package.Programs)
            {
                var result = language.Analyze(program.Source);
                Assert.True(result.Success && result.Diagnostics.Count == 0, $"{package.Name} / {program.Name}: {string.Join(" | ", result.Diagnostics)}");
                Assert.Equal(program.Source, language.FormatDocument(program.Source));
                if (LoopingPrograms.Contains(program.Name))
                {
                    var looping = new Machine(package.Machine, program.Source) { RecordHistory = false };
                    for (int i = 0; i < 10000; i++) looping.SingleStep();
                    Assert.False(looping.IsHalted);
                    Assert.Empty(looping.MicrocodeWarnings);
                    continue;
                }
                Run(package, program.Source, 5_000_000, $"{package.Name} / {program.Name}");
            }
        }
    }

    [Fact]
    public void GetReturnsACopy()
    {
        var copy = BuiltInPackages.Get("RISC-16");
        copy.Programs.Clear();
        copy.Machine.Devices.Clear();
        Assert.NotEmpty(BuiltInPackages.Get("RISC-16").Programs);
        Assert.NotEmpty(BuiltInPackages.All[1].Machine.Devices);
        Assert.Throws<ArgumentException>(() => BuiltInPackages.Get("nope"));
    }

    [Fact]
    public void PackageJsonRoundTrips()
    {
        var package = BuiltInPackages.Default;
        var json = package.ToJson();
        var back = MachinePackage.FromJson(json);
        Assert.Equal(package.Name, back.Name);
        Assert.Equal(package.Programs.Select(p => (p.Name, p.Description, p.Source)), back.Programs.Select(p => (p.Name, p.Description, p.Source)));
        Assert.Equal(package.Machine.ToJson(), back.Machine.ToJson());
        Assert.Equal(json, back.ToJson());
    }

    [Theory]
    [InlineData("{", "not valid JSON")]
    [InlineData("{\"name\": \"x\"}", "needs a \"machine\"")]
    public void BadPackagesAreReported(string json, string expected)
    {
        var e = Assert.Throws<MachineDefinitionException>(() => MachinePackage.FromJson(json));
        Assert.Contains(expected, e.Message);
    }

    [Fact]
    public void ExampleDataReadsTheDefaultPackage()
    {
        Assert.Equal(BuiltInPackages.Default.Programs.Select(p => p.Name), ExampleData.Programs.Select(p => p.Name));
        Assert.Equal(BuiltInPackages.Default.Machine.ToJson(), ExampleData.MACHINE);
    }

    [Fact]
    public void RiscRegistersAreExplicitRegisterDevices()
    {
        var machine = BuiltInPackages.Get("RISC-16").Machine;
        Assert.Equal(new[] { "r0", "r1", "r2", "r3" }, machine.Devices.Where(d => d.Id.StartsWith("r") && d.Id.Length == 2).Select(d => d.Id));
        Assert.All(machine.Devices.Where(d => d.Id.StartsWith("r") && d.Id.Length == 2), d => Assert.Equal("register", d.Type));
        Assert.Equal("r0", machine.FindDevice("alu").Connections["a"]);
        Assert.DoesNotContain(machine.Decoder.Microcode.Instructions, i => i.OperandTypes?.Count > 1);
    }

    [Fact]
    public void RiscHelloCallsASubroutineAndPrints()
    {
        var c = Risc(BuiltInPackages.Get("RISC-16").Program("Hello, world on the LCD").Source);
        Assert.Equal("Hello from RISC-16!", c.Device<CharacterDisplay>("lcd").Line(0).TrimEnd());
    }

    [Fact]
    public void RiscFibonacciPrintsTheSequenceInDecimal()
    {
        var c = Risc(BuiltInPackages.Get("RISC-16").Program("Fibonacci on the LCD").Source, 1_000_000);
        var text = c.Device<CharacterDisplay>("lcd").Text.Replace("\n", "");
        Assert.Equal("1 1 2 3 5 8 13 21 34 55 89 144 233 377 610 987 1597 2584 4181 6765 10946 17711 28657 46368", text.Trim());
    }

    [Fact]
    public void RiscGradientFillsTheScreen()
    {
        var c = Risc(BuiltInPackages.Get("RISC-16").Program("Colour gradient on the screen").Source, 20_000_000);
        var fb = c.Device<Framebuffer>("fb");
        Assert.Equal(Framebuffer.Width * Framebuffer.Height, fb.WriteCount);
        for (int y = 0; y < Framebuffer.Height; y += 7)
        {
            for (int x = 0; x < Framebuffer.Width; x += 13)
            {
                int expected = (x / 20) << 11 | (y / 8) << 5 | 0x10;
                Assert.True(expected == fb.Pixels[y * Framebuffer.Width + x], $"pixel {x},{y}: expected 0x{expected:X4}, found 0x{fb.Pixels[y * Framebuffer.Width + x]:X4}");
            }
        }
    }

    [Fact]
    public void MovesCopyBetweenRegisters()
    {
        var c = Risc(@"
            MOVI_R1 11
            MOV_R2_R1
            MOV_R3_R2
            MOV_R0_R3
            MOVI_R1 22
            HLT");
        Assert.Equal(new[] { 11, 22, 11, 11 }, Registers(c));
    }

    [Fact]
    public void AluOpsWorkOnTheAccumulator()
    {
        var c = Risc(@"
            MOVI_R1 0x00F0
            MOVI_R2 0x0F3C
            MOV_R0_R1
            ADD_R2
            MOV_R3_R0
            HLT");
        Assert.Equal(0x102C, Registers(c)[3]);
        Assert.Equal(0x102C, Registers(c)[0]);

        foreach (var (op, expected) in new[] { ("SUB_R1", 0xE4C), ("AND_R1", 0x30), ("ORR_R1", 0xFFC), ("EOR_R1", 0xFCC) })
        {
            c = Risc($@"
                MOVI_R1 0x00F0
                MOVI_R0 0x0F3C
                {op}
                HLT");
            Assert.True(expected == Registers(c)[0], $"{op}: 0x{Registers(c)[0]:X}");
            Assert.Equal(0xF0, Registers(c)[1]);
        }
    }

    [Fact]
    public void ImmediatesAndShifts()
    {
        var c = Risc(@"
            MOVI_R0 40
            ADDI 3
            SUBI 1
            ANDI 0x2E
            HLT");
        Assert.Equal(42, Registers(c)[0]);

        c = Risc(@"
            MOVI_R0 0x8001
            MOVI_R1 1
            LSL_R1
            HLT");
        Assert.Equal(0x0002, Registers(c)[0]);
        Assert.NotEqual(0, Flags(c) & StatusRegister.CarryFlag);

        c = Risc(@"
            MOVI_R0 0x8001
            MOVI_R2 17
            LSR_R2
            HLT");
        Assert.Equal(0x4000, Registers(c)[0]);
        Assert.NotEqual(0, Flags(c) & StatusRegister.CarryFlag);

        c = Risc(@"
            MOVI_R0 0x00FF
            MOVI_R3 8
            LSL_R3
            HLT");
        Assert.Equal(0xFF00, Registers(c)[0]);
        Assert.Equal(StatusRegister.NegativeFlag, Flags(c));
    }

    [Fact]
    public void LogicOpsSetZeroAndNegative()
    {
        var c = Risc(@"
            MOVI_R0 0x0F0F
            MOVI_R1 0xF0F0
            AND_R1
            HLT");
        Assert.Equal(StatusRegister.ZeroFlag, Flags(c));
        c = Risc(@"
            MOVI_R0 0x0F0F
            MOVI_R1 0xF0F0
            EOR_R1
            HLT");
        Assert.Equal(0xFFFF, Registers(c)[0]);
        Assert.Equal(StatusRegister.NegativeFlag, Flags(c));
    }

    [Fact]
    public void CompareSetsFlagsWithoutChangingRegisters()
    {
        var c = Risc(@"
            MOVI_R0 5
            CMPI 5
            HLT");
        Assert.Equal(StatusRegister.ZeroFlag, Flags(c));
        Assert.Equal(5, Registers(c)[0]);
        c = Risc(@"
            MOVI_R0 3
            MOVI_R2 5
            CMP_R2
            HLT");
        Assert.Equal(StatusRegister.NegativeFlag | StatusRegister.CarryFlag, Flags(c) & (StatusRegister.NegativeFlag | StatusRegister.CarryFlag));
        Assert.Equal(new[] { 3, 0, 5 }, Registers(c).Take(3));
    }

    [Fact]
    public void IncAndDecLeaveTheFlagsAlone()
    {
        var c = Risc(@"
            MOVI_R0 1
            CMPI 1
            MOVI_R2 0
            DEC_R2
            INC_R3
            INC_R3
            HLT");
        Assert.Equal(new[] { 1, 0, 0xFFFF, 2 }, Registers(c));
        Assert.Equal(StatusRegister.ZeroFlag, Flags(c));
    }

    [Fact]
    public void LoadAndStoreGoThroughAnAddressRegister()
    {
        var c = Risc(@"
            ADR_R1 data
            LDR_R1
            ADDI 1
            INC_R1
            STR_R1
            HLT
    data:   .WORD 41, 0");
        Assert.Equal(42, Registers(c)[0]);
        var data = c.Assembler.labelLUT["data"];
        Assert.Equal(42, c.Device<RamModule>("mem").ValueAt(data + 1));
    }

    [Fact]
    public void PushAndPopUseAFullDescendingStack()
    {
        var c = Risc(@"
            MOVI_R1 11
            MOVI_R2 22
            PUSH_R1
            PUSH_R2
            POP_R3
            POP_R0
            HLT");
        Assert.Equal(new[] { 11, 11, 22, 22 }, Registers(c));
        Assert.Equal(4096, c.Device<Register>("sp").Data);
        Assert.Equal(11, c.Device<RamModule>("mem").ValueAt(4095));
        Assert.Equal(22, c.Device<RamModule>("mem").ValueAt(4094));
    }

    [Fact]
    public void BranchWithLinkReturnsToTheNextInstruction()
    {
        var c = Risc(@"
            BL      sub
            MOVI_R2 2
            HLT
    sub:    MOVI_R1 1
            RET");
        Assert.Equal(new[] { 0, 1, 2 }, Registers(c).Take(3));
        Assert.Equal(2, c.Device<Register>("lr").Data);
    }

    [Theory]
    [InlineData("BEQ", 5, 5, true)]
    [InlineData("BEQ", 5, 6, false)]
    [InlineData("BNE", 5, 6, true)]
    [InlineData("BNE", 5, 5, false)]
    [InlineData("BCS", 3, 5, true)]
    [InlineData("BCS", 5, 3, false)]
    [InlineData("BCC", 5, 3, true)]
    [InlineData("BCC", 3, 5, false)]
    [InlineData("BMI", 3, 5, true)]
    [InlineData("BMI", 5, 5, false)]
    [InlineData("BPL", 5, 5, true)]
    [InlineData("BPL", 3, 5, false)]
    public void ConditionalBranchesFollowTheFlags(string branch, int a, int b, bool taken)
    {
        var c = Risc($@"
            MOVI_R0 {a}
            CMPI {b}
            {branch} yes
            MOVI_R3 1
            HLT
    yes:    MOVI_R3 2
            HLT");
        Assert.Equal(taken ? 2 : 1, Registers(c)[3]);
    }
}
