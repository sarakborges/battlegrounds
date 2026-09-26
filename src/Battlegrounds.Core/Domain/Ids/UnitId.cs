namespace Battlegrounds.Core.Domain.Ids;

public readonly record struct UnitId
{
    public string Value { get; }

    public UnitId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Unit id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}
