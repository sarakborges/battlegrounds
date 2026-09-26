namespace Battlegrounds.Core.Domain.Ids;

public readonly record struct UnitTypeId
{
    public string Value { get; }

    public UnitTypeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Unit type id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}
