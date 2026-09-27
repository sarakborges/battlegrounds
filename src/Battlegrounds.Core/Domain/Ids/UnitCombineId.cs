namespace Battlegrounds.Core.Domain.Ids;

public readonly record struct UnitCombineId
{
    public string Value { get; }

    public UnitCombineId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Unit combine id cannot be empty.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;
}
