using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Tests.Effects;

public sealed class DynamicEffectValueTests
{
    [Fact]
    public void EvaluateValue_ComposesSourceStatAndFilteredUnitCount()
    {
        var context = Context(
            Unit(1, 0, attack: 3, health: 4, types: ["organic"]),
            Unit(2, 0, attack: 1, health: 2, types: ["organic"]),
            Unit(3, 0, attack: 5, health: 2, types: ["construct"]),
            Unit(4, 1, attack: 2, health: 2, types: ["organic"]));
        var expression = new CompositeEffectValueExpression(
            EffectValueOperation.Multiply,
            [
                new SourceStatEffectValueExpression(EffectStat.Attack),
                new UnitCountEffectValueExpression(
                    new EffectUnitQuery(
                        EffectTargetScope.Friendly,
                        requiredTypeId: new UnitTypeId("organic"))),
            ]);

        var value = new EffectPipeline().EvaluateValue(expression, context);

        Assert.Equal(6, value);
    }

    [Fact]
    public void EvaluateValue_TargetStatUsesRequestedTarget()
    {
        var context = Context(
            Unit(1, 0, attack: 3, health: 4),
            Unit(2, 1, attack: 7, health: 9));

        var value = new EffectPipeline().EvaluateValue(
            new TargetStatEffectValueExpression(EffectStat.Health),
            context,
            new UnitInstanceId(2));

        Assert.Equal(9, value);
    }

    [Fact]
    public void EvaluateValue_NestedMinMaxAndAddAreDeterministic()
    {
        var context = Context(Unit(1, 0, attack: 2, health: 8));
        var expression = new CompositeEffectValueExpression(
            EffectValueOperation.Max,
            [
                1.AsValue(),
                new CompositeEffectValueExpression(
                    EffectValueOperation.Add,
                    [
                        new SourceStatEffectValueExpression(EffectStat.Attack),
                        (-5).AsValue(),
                    ]),
            ]);

        Assert.Equal(1, new EffectPipeline().EvaluateValue(expression, context));
    }

    private static EffectResolutionContext Context(params EffectUnitSnapshot[] units) =>
        new(new UnitInstanceId(1), new PlayerId(0), units);

    private static EffectUnitSnapshot Unit(
        long id,
        int owner,
        int attack,
        int health,
        string[]? types = null) =>
        new(
            new UnitInstanceId(id),
            new PlayerId(owner),
            isAlive: true,
            attack,
            health,
            position: (int)id - 1,
            isSelectable: true,
            (types ?? []).Select(value => new UnitTypeId(value)));
}

internal static class DynamicEffectValueTestExtensions
{
    public static EffectValueExpression AsValue(this int value) => new ConstantEffectValueExpression(value);
}
