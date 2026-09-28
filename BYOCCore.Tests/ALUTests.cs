using BYOCCore;

namespace BYOCCore.Tests;

public class ALUTests
{
    private const byte Z = StatusRegister.ZeroFlag;
    private const byte C = StatusRegister.CarryFlag;
    private const byte V = StatusRegister.OverflowFlag;
    private const byte N = StatusRegister.NegativeFlag;

    private static (int result, int status) Run(string function, int a, int b)
    {
        var bus = new Bus();
        var rega = new Register("A", "rega", bus, a);
        var regb = new Register("B", "regb", bus, b);
        var sta = new StatusRegister("S", "regsta", bus);
        var alu = new ALU("ALU", "alu", rega, regb, sta, bus);
        var result = new Register("R", "res", bus);
        bus.devices.Add(alu);
        bus.devices.Add(result);
        bus.devices.Add(sta);

        alu.Enable(function);
        if (alu.IsOutputEnabled()) result.Enable("load");
        bus.Clk();
        return (result.Data, sta.Data);
    }

    [Theory]
    [InlineData(2, 3, 5, 0)]
    [InlineData(0, 0, 0, Z)]
    [InlineData(0xFFFF, 1, 0, Z | C)]
    [InlineData(40000, 30000, 4464, C)]
    [InlineData(0x7FFF, 1, 0x8000, V)]
    [InlineData(0x8000, 0x8000, 0, Z | C | V)]
    public void Add(int a, int b, int expected, int flags)
    {
        var (result, status) = Run("add", a, b);
        Assert.Equal(expected, result);
        Assert.Equal(flags, status);
    }

    [Theory]
    [InlineData(5, 3, 2, 0)]
    [InlineData(4, 4, 0, Z)]
    [InlineData(3, 5, 0xFFFE, N | C)]
    [InlineData(0x8000, 1, 0x7FFF, V)]
    [InlineData(0, 0x8000, 0x8000, N | C | V)]
    public void Sub(int a, int b, int expected, int flags)
    {
        var (result, status) = Run("sub", a, b);
        Assert.Equal(expected, result);
        Assert.Equal(flags, status);
    }

    [Theory]
    [InlineData(4, 4, Z)]
    [InlineData(3, 5, N | C)]
    [InlineData(9, 2, 0)]
    public void CmpSetsSameFlagsAsSubWithoutDrivingBus(int a, int b, int flags)
    {
        var (result, status) = Run("cmp", a, b);
        Assert.Equal(0, result);
        Assert.Equal(flags, status);
    }

    [Fact]
    public void StatusRegisterFlagProperties()
    {
        var sta = new StatusRegister("S", "regsta", new Bus());
        sta.Data = N | Z;
        Assert.True(sta.Negative8);
        Assert.True(sta.Zero1);
        Assert.False(sta.Carry2);
        Assert.False(sta.Overflow4);
    }
}
