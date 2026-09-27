namespace Battlegrounds.Core.Domain.Ids;

public readonly record struct ActionInstanceId
{
    public long Value { get; }

    public ActionInstanceId(long value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Action instance id must be positive.");
        Value = value;
    }

    public override string ToString() => Value.ToString();
}
