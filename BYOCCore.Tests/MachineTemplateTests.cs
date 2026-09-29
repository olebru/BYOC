using System.Linq;
using BYOCCore;

namespace BYOCCore.Tests;

public class MachineTemplateTests
{
    [Fact]
    public void TheMinimalCpuIsValidAndRunsTheStarterProgram()
    {
        var package = MachineTemplates.Minimal("Mine");
        Assert.Equal("Mine", package.Name);
        Assert.Equal("Mine", package.Machine.Name);
        Assert.Empty(package.Programs);
        Assert.Empty(Machine.ValidateDefinition(package.Machine, DeviceRegistry.CreateDefault()));
        Assert.Empty(MicrocodeValidator.Validate(package.Machine.Decoder.Microcode, package.Machine));
        Assert.All(package.Machine.Devices, d => Assert.NotNull(d.Layout));

        var c = new Machine(package.Machine, MachineTemplates.StarterProgram);
        int ticks = 0;
        while (!c.IsHalted) { c.SingleStep(); Assert.True(++ticks < 100); }
        Assert.Empty(c.MicrocodeWarnings);
    }

    [Fact]
    public void TheMinimalCpuJumps()
    {
        var c = new Machine(MachineTemplates.Minimal("Mine").Machine, "\tJMP\tend\n\tNOP\nend:\tHLT");
        int ticks = 0;
        while (!c.IsHalted) { c.SingleStep(); Assert.True(++ticks < 100); }
        Assert.Equal(c.Assembler.labelLUT["end"] + 1, c.Device<Register>("pc").Data);
    }

    [Fact]
    public void AnEmptyMachineHasOneBusAndRoundTrips()
    {
        var package = MachineTemplates.Empty("Blank");
        Assert.Equal(new[] { "main" }, package.Machine.Buses.Select(b => b.Id));
        Assert.Empty(package.Machine.Devices);
        Assert.Null(package.Machine.Decoder);
        var back = MachinePackage.FromJson(package.ToJson());
        Assert.Equal(package.ToJson(), back.ToJson());
        Assert.NotEmpty(Machine.ValidateDefinition(package.Machine, DeviceRegistry.CreateDefault()));
    }

    [Fact]
    public void ACopyIsIndependentAndRenamed()
    {
        var original = BuiltInPackages.Get("RISC-16");
        var copy = MachineTemplates.CopyOf(original, "My RISC");
        Assert.Equal("My RISC", copy.Name);
        Assert.Equal("My RISC", copy.Machine.Name);
        Assert.Equal(original.Programs.Count, copy.Programs.Count);
        copy.Machine.Devices.Clear();
        Assert.NotEmpty(original.Machine.Devices);
    }
}
