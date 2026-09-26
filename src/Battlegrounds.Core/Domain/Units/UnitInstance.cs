using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Units;

public sealed class UnitInstance
{
    public UnitInstanceId Id { get; }
    public UnitDefinition Definition { get; }
    public int Attack { get; private set; }
    public int Health { get; private set; }

    internal UnitInstance(UnitInstanceId id, UnitDefinition definition)
    {
        Id = id;
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Attack = definition.BaseAttack;
        Health = definition.BaseHealth;
    }
}
