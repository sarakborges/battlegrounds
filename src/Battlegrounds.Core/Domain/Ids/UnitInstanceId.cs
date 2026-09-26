namespace Battlegrounds.Core.Domain.Ids;

public readonly record struct UnitInstanceId
{
    public long Value { get; }

    public UnitInstanceId(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Unit instance id must be positive.");
        }

        Value = value;
    }

    public override string ToString() => Value.ToString();
}
