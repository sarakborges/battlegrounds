namespace Battlegrounds.Core.Domain.Ids;

public readonly record struct ActionId
{
    public string Value { get; }

    public ActionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Action id cannot be empty.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;
}
