using System;
using System.Linq;
using BYOCCore;

namespace BYOCCore.Tests;

public class BusDeviceTests
{
    public static TheoryData<Type> BusDeviceTypes()
    {
        var data = new TheoryData<Type>();
        foreach (var type in typeof(IBusDevice).Assembly.GetTypes()
                     .Where(t => typeof(IBusDevice).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract))
        {
            data.Add(type);
        }
        return data;
    }

    // Guards against 'new' method hiding: the bus calls devices through IBusDevice, so the
    // interface must dispatch to the most derived implementation.
    [Theory]
    [MemberData(nameof(BusDeviceTypes))]
    public void InterfaceCallsReachMostDerivedImplementation(Type deviceType)
    {
        var map = deviceType.GetInterfaceMap(typeof(IBusDevice));
        for (int i = 0; i < map.InterfaceMethods.Length; i++)
        {
            var interfaceMethod = map.InterfaceMethods[i];
            var mostDerived = deviceType.GetMethod(interfaceMethod.Name,
                interfaceMethod.GetParameters().Select(p => p.ParameterType).ToArray());
            Assert.Equal(mostDerived, map.TargetMethods[i]);
        }
    }

    [Fact]
    public void RamModuleLoadWorksThroughBusInterface()
    {
        var bus = new Bus();
        var source = new Register("SRC", "src", bus, 42);
        var ram = new RamModule("RAM", "mem", bus);
        bus.devices.Add(source);
        bus.devices.Add(ram);
        ram.memoryAddress = 7;

        source.Enable("output");
        ((IBusDevice)ram).Enable("load");
        bus.Clk();

        Assert.Equal(42, ram.memory[7]);
        Assert.Contains("load", ((IBusDevice)ram).SignalLines());
    }

    [Fact]
    public void OutputMarDrivesBusForDevicesListedBeforeMemory()
    {
        var bus = new Bus();
        var target = new Register("DST", "dst", bus);
        var ram = new RamModule("RAM", "mem", bus);
        bus.devices.Add(target);
        bus.devices.Add(ram);
        ram.memoryAddress = 99;

        ram.Enable("outputmar");
        target.Enable("load");
        Assert.True(ram.IsOutputEnabled());
        bus.Clk();

        Assert.Equal(99, target.Data);
        Assert.False(ram.IsOutputEnabled());
    }

    [Fact]
    public void TwoOutputtingDevicesThrowBlueSmoke()
    {
        var bus = new Bus();
        var a = new Register("A", "a", bus, 1);
        var b = new Register("B", "b", bus, 2);
        bus.devices.Add(a);
        bus.devices.Add(b);

        a.Enable("output");
        b.Enable("output");

        var e = Assert.Throws<Exception>(() => bus.Clk());
        Assert.Contains("blue smoke", e.Message);
        Assert.Contains("a", e.Message);
        Assert.Contains("b", e.Message);
    }

    [Fact]
    public void InstructionRegisterCountsEveryTickAndObeysLoadAndReset()
    {
        var bus = new Bus();
        var step = new InstructionRegister("STEP", "step", bus);
        var source = new Register("SRC", "src", bus);
        bus.devices.Add(step);
        bus.devices.Add(source);
        Assert.Equal(new[] { "load", "reset" }, step.SignalLines());
        Assert.False(step.IsOutputEnabled());

        bus.Clk();
        bus.Clk();
        Assert.Equal(2, step.Data);

        source.Data = 0x40;
        source.Enable("output");
        step.Enable("load");
        bus.Clk();
        Assert.Equal(0x40, step.Data);
        bus.Clk();
        Assert.Equal(0x41, step.Data);

        step.Enable("reset");
        bus.Clk();
        Assert.Equal(0, step.Data);

        // reset wins over load in the same tick, and the count wraps at 16 bits.
        source.Enable("output");
        step.Enable("load");
        step.Enable("reset");
        bus.Clk();
        Assert.Equal(0, step.Data);
        step.Data = 0xFFFF;
        bus.Clk();
        Assert.Equal(0, step.Data);
        Assert.Throws<Exception>(() => step.Enable("output"));
    }

    [Fact]
    public void LoadBytesRejectsProgramLargerThanMemory()
    {
        var ram = new RamModule("RAM", "mem", new Bus(), 256);
        Assert.Throws<ArgumentException>(() => ram.LoadBytes(new byte[257]));
    }

    [Fact]
    public void RegisterIncrementWrapsAtSixteenBits()
    {
        var bus = new Bus();
        var pc = new Register("PC", "pc", bus);
        bus.devices.Add(pc);
        pc.Data = 0xFFFF;
        pc.Enable("inc");
        bus.Clk();

        Assert.Equal(0, pc.Data);
        Assert.DoesNotContain("count", pc.SignalLines());
    }
}
