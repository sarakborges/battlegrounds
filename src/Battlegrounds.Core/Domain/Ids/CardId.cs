namespace Battlegrounds.Core.Domain.Ids;

public readonly record struct CardId
{
    public string Value { get; }

    public CardId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Card id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}
