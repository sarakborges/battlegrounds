namespace Battlegrounds.Core.Domain.Ids;

public readonly record struct PowerId
{
    public string Value { get; }

    public PowerId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Power id cannot be empty.", nameof(value));

        Value = value;
    }

    public override string ToString() => Value;
}
