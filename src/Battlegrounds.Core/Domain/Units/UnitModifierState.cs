namespace Battlegrounds.Core.Domain.Units;

public enum UnitModifierDuration
{
    Persistent,
    UntilCombatEnd,
}

public sealed record UnitModifierState
{
    public string Key { get; }
    public int AttackDelta { get; }
    public int HealthDelta { get; }
    public UnitModifierDuration Duration { get; }

    public UnitModifierState(
        string key,
        int attackDelta,
        int healthDelta,
        UnitModifierDuration duration = UnitModifierDuration.Persistent)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Modifier key cannot be empty.", nameof(key));
        if (attackDelta == 0 && healthDelta == 0)
            throw new ArgumentException("A modifier must change Attack and/or Health.");

        Key = key;
        AttackDelta = attackDelta;
        HealthDelta = healthDelta;
        Duration = duration;
    }
}
