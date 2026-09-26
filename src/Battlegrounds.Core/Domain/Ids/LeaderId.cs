namespace Battlegrounds.Core.Domain.Ids;

public readonly record struct LeaderId
{
    public string Value { get; }

    public LeaderId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Leader id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}
