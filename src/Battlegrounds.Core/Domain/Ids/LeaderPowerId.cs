namespace Battlegrounds.Core.Domain.Ids;

public readonly record struct LeaderPowerId
{
    public string Value { get; }

    public LeaderPowerId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Leader power id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}
