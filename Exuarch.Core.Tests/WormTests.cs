using System;
using System.Linq;
using Exuarch.Core;

namespace Exuarch.Core.Tests;

// WORM-16: no jumps; the program is a queue crawling round a ring memory, and every instruction decides its fate.
public class WormTests
{
    private static readonly MachinePackage Worm = BuiltInPackages.Get("WORM-16");

    private static (Machine Machine, long Ticks) Run(string source, long limit = 2_000_000)
    {
        var c = new Machine(Worm.Machine, source) { RecordHistory = false };
        long ticks = 0;
        while (!c.IsHalted)
        {
            c.SingleStep();
            Assert.True(++ticks < limit, "the program did not halt");
        }
        Assert.Empty(c.MicrocodeWarnings);
        return (c, ticks);
    }
    private static string Lcd(Machine c) => c.Device<CharacterDisplay>("lcd").Text.Replace("\n", "").TrimEnd();
    private static int Head(Machine c) => c.Device<Register>("head").Data;
    private static int Tail(Machine c) => c.Device<Register>("tail").Data;

    [Theory]
    [InlineData("Count down and go", "9876543210GO", 712)]
    [InlineData("Cell division", "* ** **** ********", 407)]
    [InlineData("Fibonacci", "0 1 1 2 3 5 8 13 21 34 55 89", 1151)]
    [InlineData("Two phases", "54321 EDCBA", 815)]
    public void TheProgramsPrintWhatTheReadmeSays(string name, string expected, long ticks)
    {
        var run = Run(Worm.Program(name).Source);
        Assert.Equal(expected, Lcd(run.Machine));
        Assert.Equal(ticks, run.Ticks);
        Assert.Contains($"`{expected}`", Worm.Readme);
    }

    [Fact]
    public void TheReadmeGivesTheCountdownsTicks()
    {
        Assert.Contains("`9876543210GO` in 712 ticks", Worm.Readme);
    }

    // There is no instruction that changes the head except the fetch moving it on: the program crawls forward.
    [Fact]
    public void NoInstructionJumps()
    {
        var microcode = Worm.Machine.Decoder.Microcode;
        foreach (var instruction in microcode.AllInstructions)
        {
            foreach (var step in instruction.Steps) Assert.DoesNotContain("head.load", step.Signals);
        }
    }

    // A dying instruction is gone after it runs, a kept one is copied to the tail, and a dividing one twice.
    [Theory]
    [InlineData("OUTC_D 'x'", 0)]
    [InlineData("OUTC 'x'", 2)]
    [InlineData("OUTC_2 'x'", 4)]
    public void EachFateWritesItsCopiesToTheTail(string instruction, int cellsWritten)
    {
        // TAIL_D end is cells 0 and 1, the instruction cells 2 and 3; HLT stops before the copies come round.
        var run = Run($"TAIL_D end\n{instruction}\nHLT\nend:");
        var tailStart = run.Machine.Assembler.labelLUT["end"];
        Assert.Equal(tailStart + cellsWritten, Tail(run.Machine));
        var mem = run.Machine.Device<RamModule>("mem");
        for (int i = 0; i < cellsWritten; i += 2)
        {
            Assert.Equal(mem.ValueAt(2), mem.ValueAt(tailStart + i));
            Assert.Equal('x', mem.ValueAt(tailStart + i + 1));
        }
    }

    // _P instructions run and stay while N is clear, and are skipped and dropped once it is set.
    [Fact]
    public void LivingWhileNIsClearMeansSkippedOnceItIsSet()
    {
        var open = Run("TAIL_D end\nLDA_D 1\nOUTC_P 'a'\nHLT\nend:");
        Assert.Equal("a", Lcd(open.Machine));
        Assert.Equal(open.Machine.Assembler.labelLUT["end"] + 2, Tail(open.Machine));
        var closed = Run("TAIL_D end\nLDA_D -1\nOUTC_P 'a'\nHLT\nend:");
        Assert.Equal("", Lcd(closed.Machine));
        Assert.Equal(closed.Machine.Assembler.labelLUT["end"], Tail(closed.Machine));
    }

    // UPTO is GATE the other way round: open, and kept, while A <= v; closed, with N set and its copy taken back, when
    // A > v. Either way A is left as it was.
    [Theory]
    [InlineData(5, 9, true)]
    [InlineData(9, 9, true)]
    [InlineData(10, 9, false)]
    public void UptoIsOpenWhileAIsAtMostTheValue(int a, int v, bool open)
    {
        var c = new Machine(Worm.Machine, $"TAIL_D end\nLDA_D {a}\nUPTO {v}\nHLT\nend:") { RecordHistory = false };
        while (!c.IsHalted) c.SingleStep();
        var end = c.Assembler.labelLUT["end"];
        Assert.Equal(open ? end + 2 : end, Tail(c));
        Assert.Equal(open ? 0 : StatusRegister.NegativeFlag, c.Status & StatusRegister.NegativeFlag);
        Assert.Equal(a, c.Device<Register>("a").Data);
    }

    // OUT2 prints one or two digits, without a leading zero, and leaves A and the flags as they were.
    [Theory]
    [InlineData(0, "0")]
    [InlineData(9, "9")]
    [InlineData(10, "10")]
    [InlineData(42, "42")]
    [InlineData(99, "99")]
    public void Out2PrintsUpToTwoDigits(int a, string expected)
    {
        var c = new Machine(Worm.Machine, $"TAIL_D end\nLDA_D {a}\nOUT2 10, '0'\nHLT\nend:") { RecordHistory = false };
        int flags = -1;
        while (!c.IsHalted)
        {
            if (c.CurrentInstructionAddress == 4 && flags < 0) flags = c.Status;
            c.SingleStep();
        }
        Assert.Equal(expected, Lcd(c));
        Assert.Equal(a, c.Device<Register>("a").Data);
        Assert.Equal(flags, c.Status);
    }

    // HATCH carries its egg along unrun, leaving A and the flags as they were, until N is set.
    [Fact]
    public void HatchCarriesItsEggUntilNIsSet()
    {
        // Cells: TAIL_D end 0-1, LDA_D 7 2-3, HATCH 3 4-5, OUTC 'e' 6-7, HLT 8, GATE 99 9-10, end at 11. On the first
        // lap N is clear, so the egg is carried; GATE 99 then closes and sets N, and on the second lap it hatches.
        var source = "TAIL_D end\nLDA_D 7\nHATCH 3\nOUTC 'e'\nHLT\nGATE 99\nend:";
        var c = new Machine(Worm.Machine, source) { RecordHistory = false };
        while (c.CurrentInstructionAddress != 9) c.SingleStep();
        Assert.Equal("", Lcd(c));
        Assert.Equal(7, c.Device<Register>("a").Data);
        Assert.Equal(0, c.Status & StatusRegister.NegativeFlag);
        var mem = c.Device<RamModule>("mem");
        int end = c.Assembler.labelLUT["end"];
        // HATCH 3 and its egg, OUTC 'e' and HLT, are at the tail in the same order.
        for (int i = 0; i < 5; i++) Assert.Equal(mem.ValueAt(4 + i), mem.ValueAt(end + i));
        Assert.Equal("e", Lcd(Run(source).Machine));
    }

    // The worm keeps going round: 1500 laps of 9 cells is more than three times round the 4096 cell ring.
    [Fact]
    public void TheWormGoesRoundTheRing()
    {
        var run = Run("TAIL_D end\nLDA_D 1500\nGATE 1\nSUB_P 1\nHATCH 3\nOUTC '!'\nHLT\nend:");
        Assert.Equal("!", Lcd(run.Machine));
        Assert.True(Head(run.Machine) > 3 * 4096, $"the head only got to {Head(run.Machine)}");
    }

    // Without its gate and egg, Cell division grows until its tail runs into its head.
    [Fact]
    public void AWormThatOnlyGrowsCatchesItsOwnHead()
    {
        var c = new Machine(Worm.Machine, "TAIL_D end\nOUTC_2 '*'\nend:") { RecordHistory = false };
        long ticks = 0;
        while (Tail(c) - Head(c) < 4096 && !c.IsHalted)
        {
            c.SingleStep();
            Assert.True(++ticks < 2_000_000, "the worm never filled the ring");
        }
        Assert.True(Tail(c) - Head(c) >= 4096);
    }

    // HATCH's loop loads the instruction register from HATCH LOOP, not from memory: that is a jump inside its
    // microcode, not a new instruction, so the instruction running is still HATCH.
    [Fact]
    public void HatchsMicrocodeLoopIsNotAFetch()
    {
        // HATCH 5 is at cell 4 and carries OUTC 'a', OUTC 'b' and HLT, five cells, so the next instruction fetched is
        // the first copy at the tail: HATCH again.
        var c = new Machine(Worm.Machine, "TAIL_D end\nLDA_D 1\nHATCH 5\nOUTC 'a'\nOUTC 'b'\nHLT\nend:") { RecordHistory = false };
        while (c.CurrentInstructionAddress != 4) c.SingleStep();
        while (c.CurrentInstructionAddress == 4) c.SingleStep();
        Assert.Equal(c.Assembler.labelLUT["end"], c.CurrentInstructionAddress);
        Assert.Equal("HATCH", c.InstructionAt(c.CurrentInstructionAddress.Value).Mnemonic);
    }

    // The Run view shows what runs: copies the assembler never saw decode from memory.
    [Fact]
    public void TheInstructionAtACopyDecodesFromMemory()
    {
        var c = new Machine(Worm.Machine, Worm.Program("Count down and go").Source) { RecordHistory = false };
        var end = c.Assembler.labelLUT["end"];
        while (c.CurrentInstructionAddress != end) c.SingleStep();
        var line = c.InstructionAt(end);
        Assert.Equal("GATE", line.Mnemonic);
        Assert.Equal(new[] { "0030" }, line.Operands);
        Assert.Null(c.Assembler.Listing.FirstOrDefault(l => l.IsInstruction && l.Address == end));
        // Where memory still holds what was assembled, the assembled line, with its label and source operands.
        Assert.Equal("'9'", c.InstructionAt(2).Operands[0]);
    }
}
