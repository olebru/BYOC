using System.Linq;
using Exuarch.Core;

namespace Exuarch.Core.Tests;

public class SchematicLayoutTests
{
    private static readonly DeviceRegistry Registry = DeviceRegistry.CreateDefault();

    [Fact]
    public void BuiltInMachinesHaveNoCardsOnTopOfEachOtherOrOnABus()
    {
        foreach (var package in BuiltInPackages.All)
        {
            var machine = package.Machine;
            machine.EnsureLayout();
            var cards = new SchematicLayout(machine, Registry).Cards().ToList();
            for (int i = 0; i < cards.Count; i++)
            {
                for (int j = i + 1; j < cards.Count; j++)
                {
                    var (a, b) = (cards[i], cards[j]);
                    bool overlap = a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
                    Assert.False(overlap, $"{package.Name}: {a.Id} and {b.Id} overlap");
                }
                foreach (var bus in machine.Buses)
                {
                    var c = cards[i];
                    Assert.False(bus.Layout.Y > c.Y - 4 && bus.Layout.Y < c.Y + c.Height + 4, $"{package.Name}: {c.Id} lies across bus {bus.Id}");
                }
            }
        }
    }

    [Fact]
    public void SocketsFitInsideTheirCard()
    {
        foreach (var info in Registry.TypeInfos.Where(t => t.Connections.Count > 0))
        {
            var height = SchematicLayout.HeightFor(info.Connections.Count);
            Assert.True(SchematicLayout.SocketRowY(info.Connections.Count - 1) + SchematicLayout.SocketRowHeight / 2 <= height, info.Type);
        }
        Assert.Equal(SchematicLayout.CardMinHeight, SchematicLayout.HeightFor(0));
        Assert.True(SchematicLayout.DecoderHeight > SchematicLayout.CardMinHeight);
    }

    [Fact]
    public void PortsFaceTheirBus()
    {
        var machine = BuiltInPackages.Get("HARVARD-16").Machine;
        var layout = new SchematicLayout(machine, Registry);
        foreach (var device in machine.Devices)
        {
            foreach (var port in device.Ports())
            {
                var anchor = layout.PortAnchor(device, port.Key);
                var busY = layout.BusY(port.Value);
                Assert.Equal(busY > device.Layout.Y, anchor.Bottom);
                Assert.InRange(anchor.X, device.Layout.X, device.Layout.X + SchematicLayout.CardWidth);
            }
        }
    }
}
