using System.Linq;
using Exuarch.Core;

namespace Exuarch.Core.Tests;

public class DeviceDescriptionTests
{
    [Fact]
    public void EveryDeviceTypeDescribesItselfAndItsLines()
    {
        var registry = DeviceRegistry.CreateDefault();
        foreach (var info in registry.TypeInfos)
        {
            Assert.True(info.Description.Length >= 40, $"{info.Type} needs a real description, not '{info.Description}'");
            Assert.NotEqual("Other", info.Category);
            Assert.NotEmpty(info.ControlLines);
            Assert.All(info.ControlLines, l => Assert.False(string.IsNullOrWhiteSpace(l.Description), $"{info.Type}.{l.Name} has no description"));
            Assert.All(info.Parameters, p => Assert.False(string.IsNullOrWhiteSpace(p.Description), $"{info.Type} parameter {p.Name} has no description"));
            Assert.All(info.Connections, c => Assert.False(string.IsNullOrWhiteSpace(c.Description), $"{info.Type} connection {c.Name} has no description"));
        }
    }
}
