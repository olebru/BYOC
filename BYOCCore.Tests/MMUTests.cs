using BYOCCore;

namespace BYOCCore.Tests;

public class MMUTests
{
    private readonly Bus bus = new Bus();
    private readonly Register reg;
    private readonly MMU mmu;

    public MMUTests()
    {
        reg = new Register("REG", "reg", bus);
        mmu = new MMU("MMU", "mmu", bus);
        bus.devices.Add(reg);
        bus.devices.Add(mmu);
    }

    [Fact]
    public void Select0StackAppliesBeforeBankFunctionsRegardlessOfEnableOrder()
    {
        mmu.ChipSelectRegister.Data = 1;
        mmu.RamBanks[0].memory[0] = 11;
        mmu.RamBanks[1].memory[0] = 22;

        mmu.Enable("output");
        mmu.Enable("select0stack");
        reg.Enable("load");
        bus.Clk();

        Assert.Equal(11, reg.Data);
        Assert.Equal(0, mmu.ChipSelectRegister.Data);
    }

    [Fact]
    public void BankFunctionsFollowChipSelectLoadedInSameStep()
    {
        reg.Data = 5;
        reg.Enable("output");
        mmu.Enable("loadcs");
        mmu.Enable("loadmar");
        bus.Clk();

        Assert.Equal(5, mmu.ChipSelectRegister.Data);
        Assert.Equal(5, mmu.RamBanks[5].memoryAddress);
        Assert.Equal(0, mmu.RamBanks[0].memoryAddress);
    }

    [Fact]
    public void NoBankKeepsStaleOutputAfterBankSwitch()
    {
        mmu.ChipSelectRegister.Data = 1;
        mmu.Enable("output");
        mmu.Enable("select0stack");
        reg.Enable("load");
        bus.Clk();

        Assert.False(mmu.IsOutputEnabled());
        Assert.DoesNotContain(mmu.RamBanks, bank => bank.IsOutputEnabled());
    }

    [Fact]
    public void UnknownFunctionThrowsAtEnable()
    {
        Assert.Throws<System.Exception>(() => mmu.Enable("bogus"));
    }
}
