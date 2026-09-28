using System;
using BYOCCore;

namespace BYOCCore.Tests;

public class AssemblerTests
{
    private readonly DecoderRom rom = new DecoderRom(ExampleData.ROMDATA);

    private byte Op(string mnemonic) => rom.FetchByteCodeFromMnemonic(mnemonic);

    [Fact]
    public void AssemblesExampleProgram()
    {
        var bytes = new Assembler(rom).Assemble(ExampleData.SRC);
        Assert.Equal(new byte[]
        {
            Op("LAI"), 15, Op("PSA"), Op("PSA"),
            Op("LRA"), 11, Op("LRB"), 12,
            Op("PSA"), Op("ADD"), Op("PSA"),
            65, 1
        }, bytes);
    }

    [Fact]
    public void AssemblingTwiceGivesTheSameResult()
    {
        var assembler = new Assembler(rom);
        var first = assembler.Assemble(ExampleData.SRC);
        var second = assembler.Assemble(ExampleData.SRC);
        Assert.Equal(first, second);
    }

    [Fact]
    public void LineEndingsBlankLinesAndTrailingTabsAreTolerated()
    {
        var src = "start:\tLAI\t#1\t\r\n\r\n\tJMP\tstart\n";
        var bytes = new Assembler(rom).Assemble(src);
        Assert.Equal(new byte[] { Op("LAI"), 1, Op("JMP"), 0 }, bytes);
    }

    [Fact]
    public void LabelOnItsOwnLinePointsAtNextInstruction()
    {
        var src = "\tNOP\nloop:\n\tJMP\tloop";
        var bytes = new Assembler(rom).Assemble(src);
        Assert.Equal(new byte[] { Op("NOP"), Op("JMP"), 1 }, bytes);
    }

    [Fact]
    public void ByteDirectiveAcceptsMultipleOperands()
    {
        var bytes = new Assembler(rom).Assemble("data:\t.BYTE\t#1,#2,data");
        Assert.Equal(new byte[] { 1, 2, 0 }, bytes);
    }

    [Theory]
    [InlineData("\tFOO", "unknown mnemonic 'FOO'")]
    [InlineData("\tJMP\tnowhere", "unknown label 'nowhere'")]
    [InlineData("\tLAI\t#256", "'#256' is not a number")]
    [InlineData("\t.WORD\t#1", "unknown directive '.WORD'")]
    [InlineData("a:\tNOP\na:\tNOP", "defined more than once")]
    [InlineData("\tLAI\t#1\tcomment", "too many columns")]
    [InlineData("label\tNOP", "must end with ':'")]
    public void ErrorsReportLineAndCause(string src, string expected)
    {
        var e = Assert.Throws<FormatException>(() => new Assembler(rom).Assemble(src));
        Assert.Contains("Line ", e.Message);
        Assert.Contains(expected, e.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProgramLargerThanMemoryIsRejected()
    {
        var src = string.Join("\n", System.Linq.Enumerable.Repeat("\tNOP", 257));
        Assert.Throws<FormatException>(() => new Assembler(rom).Assemble(src));
    }
}
