namespace Battlegrounds.Core.Domain.Ids;

public readonly record struct BehaviorId
{
    public string Value { get; }

    public BehaviorId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Behavior id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}
