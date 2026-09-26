using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Tests.Behaviors;

public sealed class BehaviorCatalogTests
{
    [Fact]
    public void Constructor_SortsByStableBehaviorId()
    {
        var catalog = new BehaviorCatalog(
        [
            Definition("z", NativeBehaviorKeys.TargetPriority),
            Definition("a", NativeBehaviorKeys.DamageBarrier),
        ]);

        Assert.Equal(["a", "z"], catalog.All.Select(behavior => behavior.Id.Value));
    }

    [Fact]
    public void BehaviorDefinition_RejectsUnsupportedNativeHandler()
    {
        Assert.Throws<ArgumentException>(() =>
            new BehaviorDefinition(
                new BehaviorId("custom"),
                "Custom",
                new NativeBehaviorKey("notImplementedByCore")));
    }

    private static BehaviorDefinition Definition(string id, NativeBehaviorKey handler) =>
        new(new BehaviorId(id), id, handler);
}
