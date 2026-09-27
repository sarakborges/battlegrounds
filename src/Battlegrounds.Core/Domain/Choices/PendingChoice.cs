using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Choices;

public enum PendingChoiceKind
{
    Unit,
    Action,
}

public abstract class PendingChoice
{
    public ChoiceId Id { get; }
    public abstract PendingChoiceKind Kind { get; }

    protected PendingChoice(ChoiceId id)
    {
        Id = id;
    }
}
