namespace Battlegrounds.Core.Domain.Units;

public sealed record UnitModifierState
{
    public string Key { get; }
    public int AttackDelta { get; }
    public int HealthDelta { get; }

    public UnitModifierState(string key, int attackDelta, int healthDelta)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Modifier key cannot be empty.", nameof(key));
        if (attackDelta == 0 && healthDelta == 0)
            throw new ArgumentException("A modifier must change Attack and/or Health.");

        Key = key;
        AttackDelta = attackDelta;
        HealthDelta = healthDelta;
    }
}
