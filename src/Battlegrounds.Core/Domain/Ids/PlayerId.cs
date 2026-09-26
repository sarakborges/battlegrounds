namespace Battlegrounds.Core.Domain.Ids;

public readonly record struct PlayerId
{
    public int Value { get; }

    public PlayerId(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Player id cannot be negative.");
        }

        Value = value;
    }

    public override string ToString() => Value.ToString();
}
