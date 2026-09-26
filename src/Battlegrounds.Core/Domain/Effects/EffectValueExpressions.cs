namespace Battlegrounds.Core.Domain.Effects;

public abstract record EffectValueExpression;

public sealed record ConstantEffectValueExpression(int Value) : EffectValueExpression;

public sealed record SourceStatEffectValueExpression(EffectStat Stat) : EffectValueExpression;

public sealed record TargetStatEffectValueExpression(EffectStat Stat) : EffectValueExpression;

public sealed record UnitCountEffectValueExpression : EffectValueExpression
{
    public EffectUnitQuery Query { get; }

    public UnitCountEffectValueExpression(EffectUnitQuery query)
    {
        Query = query ?? throw new ArgumentNullException(nameof(query));
    }
}

public enum EffectValueOperation
{
    Add,
    Multiply,
    Min,
    Max,
}

public sealed record CompositeEffectValueExpression : EffectValueExpression
{
    private readonly IReadOnlyList<EffectValueExpression> _values;

    public EffectValueOperation Operation { get; }
    public IReadOnlyList<EffectValueExpression> Values => _values;

    public CompositeEffectValueExpression(
        EffectValueOperation operation,
        IEnumerable<EffectValueExpression> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var materialized = values.ToArray();
        if (materialized.Length < 2)
            throw new ArgumentException("Composite effect values require at least two operands.", nameof(values));
        if (materialized.Any(value => value is null))
            throw new ArgumentException("Composite effect values cannot contain null operands.", nameof(values));

        Operation = operation;
        _values = Array.AsReadOnly(materialized);
    }
}
