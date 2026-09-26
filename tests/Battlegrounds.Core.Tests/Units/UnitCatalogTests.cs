using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Units;

namespace Battlegrounds.Core.Tests.Units;

public sealed class UnitCatalogTests
{
    [Fact]
    public void Constructor_RejectsDuplicateUnitIds()
    {
        var id = new UnitId("duplicate");

        var exception = Assert.Throws<ArgumentException>(() => new UnitCatalog(
        [
            new UnitDefinition(id, "First", 1, 1, 1),
            new UnitDefinition(id, "Second", 2, 2, 2),
        ]));

        Assert.Contains("Duplicate unit id", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void All_IsSortedByStableUnitId()
    {
        var catalog = new UnitCatalog(
        [
            new UnitDefinition(new UnitId("z-unit"), "Z", 1, 1, 1),
            new UnitDefinition(new UnitId("a-unit"), "A", 1, 1, 1),
        ]);

        Assert.Equal(["a-unit", "z-unit"], catalog.All.Select(unit => unit.Id.Value));
    }
}
