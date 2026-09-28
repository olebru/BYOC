using System;
using System.Linq;
using System.Text;
using BYOCCore;

namespace BYOCCore.Tests;

public class DecoderRomTests
{
    private static string Row(string clk, string device, string function, string mnemonic, string status = "xxxx")
    {
        return string.Join("\t", new[] { clk, device, function, mnemonic }.Concat(status.Select(c => c.ToString())));
    }

    [Fact]
    public void LineEndingsDoNotChangeTheRom()
    {
        var lf = ExampleData.ROMDATA.Replace("\r\n", "\n");
        var crlf = lf.Replace("\n", "\r\n");
        var a = new DecoderRom(lf);
        var b = new DecoderRom(crlf);

        foreach (var mnemonic in new[] { "FTC", "LAI", "JEQ", "HLT", "SWB" })
        {
            Assert.Equal(a.FetchByteCodeFromMnemonic(mnemonic), b.FetchByteCodeFromMnemonic(mnemonic));
        }
        Assert.Equal(a.OpCodeAddressSpaceUsedInPercent(), b.OpCodeAddressSpaceUsedInPercent());
    }

    [Fact]
    public void BlankAndTrailingLinesAreIgnored()
    {
        var rom = new DecoderRom("\n" + Row("p", "clk", "disable", "HLT") + "\n\n   \n");
        Assert.Equal(0, rom.FetchByteCodeFromMnemonic("HLT"));
    }

    [Fact]
    public void MalformedRowReportsLineNumber()
    {
        var e = Assert.Throws<FormatException>(() => new DecoderRom(Row("p", "clk", "disable", "HLT") + "\np\tclk"));
        Assert.Contains("line 2", e.Message);
    }

    [Fact]
    public void InvalidStatusValueIsRejected()
    {
        Assert.Throws<FormatException>(() => new DecoderRom(Row("p", "clk", "disable", "HLT", "xx2x")));
    }

    [Fact]
    public void MnemonicWithoutClockRowsStillReservesAnOpcode()
    {
        var rom = new DecoderRom(Row("s", "rega", "inc", "INA") + "\n" + Row("p", "clk", "disable", "HLT"));
        Assert.Equal(0, rom.FetchByteCodeFromMnemonic("INA"));
        Assert.Equal(1, rom.FetchByteCodeFromMnemonic("HLT"));
        Assert.Single(rom.FetchInstruction(0, 0));
        Assert.Equal("inc", rom.FetchInstruction(0, 0).Single().Function);
    }

    [Fact]
    public void MicroStepsGetConsecutiveOpcodes()
    {
        var rom = new DecoderRom(string.Join("\n",
            Row("p", "pc", "output", "AAA"),
            Row("s", "mem", "loadmar", "AAA"),
            Row("p", "mem", "output", "AAA"),
            Row("p", "clk", "disable", "BBB")));

        Assert.Equal(0, rom.FetchByteCodeFromMnemonic("AAA"));
        Assert.Equal(2, rom.FetchByteCodeFromMnemonic("BBB"));
        Assert.Equal(new[] { "output", "loadmar" }, rom.FetchInstruction(0, 0).Select(m => m.Function));
        Assert.Equal(new[] { "output" }, rom.FetchInstruction(0, 1).Select(m => m.Function));
    }

    [Fact]
    public void StatusSpecificRowsAreSelectedByStatusRegister()
    {
        var rom = new DecoderRom(string.Join("\n",
            Row("p", "pc", "load", "JEQ", "xxx1"),
            Row("p", "pc", "count", "JEQ", "xxx0")));

        Assert.Equal("count", rom.FetchInstruction(0, 0).Single().Function);
        Assert.Equal("load", rom.FetchInstruction(StatusRegister.ZeroFlag, 0).Single().Function);
        Assert.Equal("load", rom.FetchInstruction(StatusRegister.ZeroFlag | StatusRegister.NegativeFlag, 0).Single().Function);
    }

    [Fact]
    public void StatusBitsAboveTheFourFlagsAreIgnored()
    {
        var rom = new DecoderRom(Row("p", "clk", "disable", "HLT"));
        Assert.Single(rom.FetchInstruction(0xF0, 0));
    }

    [Fact]
    public void FullAddressSpaceIsAllowedButOneMoreIsNot()
    {
        string Build(int count) => string.Join("\n", Enumerable.Range(0, count).Select(i => Row("p", "clk", "disable", $"M{i}")));

        var full = new DecoderRom(Build(256));
        Assert.Equal(255, full.FetchByteCodeFromMnemonic("M255"));
        Assert.Equal(100, full.OpCodeAddressSpaceUsedInPercent());

        Assert.Throws<Exception>(() => new DecoderRom(Build(257)));
    }

    [Fact]
    public void UnknownMnemonicThrowsArgumentException()
    {
        var rom = new DecoderRom(ExampleData.ROMDATA);
        var e = Assert.Throws<ArgumentException>(() => rom.FetchByteCodeFromMnemonic("XYZ"));
        Assert.Contains("XYZ", e.Message);
    }

    [Fact]
    public void FetchInstructionReturnsACopy()
    {
        var rom = new DecoderRom(Row("p", "clk", "disable", "HLT"));
        rom.FetchInstruction(0, 0).Clear();
        Assert.Single(rom.FetchInstruction(0, 0));
    }
}
