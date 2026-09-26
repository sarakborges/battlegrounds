namespace Battlegrounds.Core.Domain.Ids;

public readonly record struct TagId
{
    public string Value { get; }

    public TagId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Tag id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}
