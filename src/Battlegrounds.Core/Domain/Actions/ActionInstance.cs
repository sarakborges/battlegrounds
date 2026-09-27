using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Actions;

public sealed class ActionInstance
{
    public ActionInstanceId Id { get; }
    public ActionDefinition Definition { get; }

    internal ActionInstance(ActionInstanceId id, ActionDefinition definition)
    {
        Id = id;
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }
}
