using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Behaviors;

public sealed record BehaviorDefinition
{
    public BehaviorId Id { get; }
    public string Name { get; }
    public NativeBehaviorKey Handler { get; }

    public BehaviorDefinition(BehaviorId id, string name, NativeBehaviorKey handler)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Behavior name cannot be empty.", nameof(name));
        }

        if (!NativeBehaviorKeys.IsSupported(handler))
        {
            throw new ArgumentException($"Unsupported native behavior handler '{handler}'.", nameof(handler));
        }

        Id = id;
        Name = name;
        Handler = handler;
    }
}
