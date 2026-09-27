namespace Battlegrounds.Core.Domain.Ids;

public readonly record struct ChoiceId
{
    public long Value { get; }

    public ChoiceId(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Choice id must be positive.");
        }

        Value = value;
    }

    public override string ToString() => Value.ToString();
}
