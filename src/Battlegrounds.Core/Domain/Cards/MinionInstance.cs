using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Cards;

public sealed class MinionInstance
{
    public MinionInstanceId Id { get; }
    public CardDefinition Definition { get; }
    public int Attack { get; private set; }
    public int Health { get; private set; }

    internal MinionInstance(MinionInstanceId id, CardDefinition definition)
    {
        Id = id;
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Attack = definition.BaseAttack;
        Health = definition.BaseHealth;
    }
}
