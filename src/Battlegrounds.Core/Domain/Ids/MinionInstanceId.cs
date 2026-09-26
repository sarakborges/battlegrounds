namespace Battlegrounds.Core.Domain.Ids;

public readonly record struct MinionInstanceId
{
    public long Value { get; }

    public MinionInstanceId(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Minion instance id must be positive.");
        }

        Value = value;
    }

    public override string ToString() => Value.ToString();
}
