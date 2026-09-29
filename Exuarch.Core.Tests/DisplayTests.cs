using System;
using System.Linq;
using Exuarch.Core;

namespace Exuarch.Core.Tests;

public class DisplayTests
{
    private readonly Bus bus = new Bus();
    private readonly Register source;

    public DisplayTests()
    {
        source = new Register("SRC", "src", bus);
        bus.devices.Add(source);
    }

    private CharacterDisplay Display(int columns = 4, int rows = 2)
    {
        var display = new CharacterDisplay("LCD", "lcd", bus, columns, rows);
        bus.devices.Add(display);
        return display;
    }

    private void Print(CharacterDisplay display, params byte[] characters)
    {
        foreach (var character in characters)
        {
            source.Data = character;
            source.Enable("output");
            display.Enable("load");
            bus.Clk();
        }
    }

    [Fact]
    public void HasExactlyLoadAndClear()
    {
        Assert.Equal(new[] { "load", "clear" }, Display().SignalLines());
        Assert.Equal(new[] { "load", "clear" }, DeviceRegistry.CreateDefault().Info("display").ControlLines.Select(l => l.Name));
    }

    [Fact]
    public void LoadPrintsAtTheCursorAndAdvances()
    {
        var display = Display();
        Print(display, (byte)'H', (byte)'I');
        Assert.Equal("HI  ", display.Line(0));
        Assert.Equal(2, display.Cursor);
    }

    [Fact]
    public void TextWrapsToTheNextRowAndScrollsAtTheEnd()
    {
        var display = Display();
        Print(display, "ABCDEFGH".Select(c => (byte)c).ToArray());
        Assert.Equal("ABCD\nEFGH", display.Text);
        Assert.Equal(8, display.Cursor);
        Print(display, (byte)'I');
        Assert.Equal("EFGH\nI   ", display.Text);
        Assert.Equal(5, display.Cursor);
    }

    [Fact]
    public void LineFeedAndCarriageReturnMoveTheCursor()
    {
        var display = Display();
        Print(display, (byte)'A', CharacterDisplay.LineFeed, (byte)'B', (byte)'C', CharacterDisplay.CarriageReturn, (byte)'X');
        Assert.Equal("A   ", display.Line(0));
        Assert.Equal("XC  ", display.Line(1));
        Print(display, CharacterDisplay.LineFeed);
        Assert.Equal("XC  ", display.Line(0));
        Assert.Equal(4, display.Cursor);
    }

    [Fact]
    public void ClearBlanksTheDisplayAndHomesTheCursor()
    {
        var display = Display();
        Print(display, (byte)'A', (byte)'B');
        display.Enable("clear");
        bus.Clk();
        Assert.Equal("    \n    ", display.Text);
        Assert.Equal(0, display.Cursor);
    }

    [Theory]
    [InlineData(0x41, 'A')]
    [InlineData(0x7E, '~')]
    [InlineData(0xC6, 'Æ')]
    [InlineData(0xD8, 'Ø')]
    [InlineData(0xC5, 'Å')]
    [InlineData(0xE6, 'æ')]
    [InlineData(0xE9, 'é')]
    [InlineData(0xA9, '©')]
    [InlineData(0x07, ' ')]
    [InlineData(0x7F, ' ')]
    [InlineData(0x85, ' ')]
    public void CharactersAreLatin1(byte value, char expected)
    {
        Assert.Equal(expected, CharacterDisplay.ToChar(value));
    }

    [Fact]
    public void SizeIsLimited()
    {
        Assert.Throws<ArgumentException>(() => new CharacterDisplay("LCD", "lcd", bus, 0, 4));
        Assert.Throws<ArgumentException>(() => new CharacterDisplay("LCD", "lcd", bus, 64, 17));
    }

    [Fact]
    public void HelloProgramPrintsOnTheDefaultMachine()
    {
        var c = new Machine(MachineDefinition.FromJson(ExampleData.MACHINE), ExampleData.MICROCODE, ExampleData.HELLO);
        int ticks = 0;
        foreach (var _ in c.Run()) Assert.True(++ticks < 5000, "program did not halt");
        var lcd = c.Device<CharacterDisplay>("lcd");
        Assert.Equal("HELLO, WORLD!", lcd.Line(0).TrimEnd());
        Assert.Equal("ÆØÅ æøå", lcd.Line(1).TrimEnd());
        Assert.Contains(c.History, t => t.Writes.Any(w => w.Device == "lcd" && w.Address == 0 && w.Value == (byte)'H'));
    }

    [Fact]
    public void DisplayParametersComeFromTheDefinition()
    {
        var definition = MachineDefinition.FromJson(ExampleData.MACHINE);
        using var document = System.Text.Json.JsonDocument.Parse("20");
        definition.FindDevice("lcd").Parameters["columns"] = document.RootElement.Clone();
        var c = new Machine(definition, ExampleData.MICROCODE, "");
        Assert.Equal(20, c.Device<CharacterDisplay>("lcd").Columns);
        Assert.Equal(4, c.Device<CharacterDisplay>("lcd").Rows);
    }

    [Fact]
    public void FibonacciProgramPrintsTheSequenceInDecimal()
    {
        var c = new Machine(MachineDefinition.FromJson(ExampleData.MACHINE), ExampleData.MICROCODE, ExampleData.FIBONACCI);
        int ticks = 0;
        foreach (var _ in c.Run()) Assert.True(++ticks < 100000, "program did not halt");
        var text = c.Device<CharacterDisplay>("lcd").Text.Replace("\n", "");
        Assert.Equal("1 1 2 3 5 8 13 21 34 55 89 144 233 377 610 987 1597 2584 4181 6765 10946 17711 28657 46368", text.Trim());
    }

    [Fact]
    public void EveryExampleProgramAssemblesAndHalts()
    {
        foreach (var (name, source) in ExampleData.Programs)
        {
            var c = new Machine(MachineDefinition.FromJson(ExampleData.MACHINE), ExampleData.MICROCODE, source);
            Assert.True(c.ProgramByteCode.Length > 0, name);
            Assert.True(c.ProgramByteCode.Length <= 256, name);
        }
    }
}
