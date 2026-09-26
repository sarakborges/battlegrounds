namespace Battlegrounds.Core.Domain.Leaders;

public sealed class LeaderState
{
    public LeaderDefinition Definition { get; }
    public int Armor { get; private set; }

    internal LeaderState(LeaderDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Armor = definition.StartingArmor;
    }

    internal int AbsorbDamage(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        var absorbed = Math.Min(Armor, amount);
        Armor -= absorbed;
        return absorbed;
    }
}
