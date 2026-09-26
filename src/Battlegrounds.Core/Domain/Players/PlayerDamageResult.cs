namespace Battlegrounds.Core.Domain.Players;

public sealed record PlayerDamageResult(
    int IncomingDamage,
    int ArmorAbsorbed,
    int HealthDamage,
    int ArmorAfter,
    int HealthAfter);
