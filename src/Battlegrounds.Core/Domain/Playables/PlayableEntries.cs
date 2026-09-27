using Battlegrounds.Core.Domain.Actions;
using Battlegrounds.Core.Domain.Units;

namespace Battlegrounds.Core.Domain.Playables;

public enum PlayableKind
{
    Unit,
    Action,
}

public sealed record PlayableOfferEntry
{
    public int Slot { get; }
    public PlayableKind Kind { get; }
    public UnitDefinition? Unit { get; }
    public ActionDefinition? Action { get; }
    public string Id => Unit?.Id.Value ?? Action!.Id.Value;
    public string Name => Unit?.Name ?? Action!.Name;
    public int Tier => Unit?.Tier ?? Action!.Tier;
    public int? Cost => Action?.Cost;

    private PlayableOfferEntry(int slot, PlayableKind kind, UnitDefinition? unit, ActionDefinition? action)
    {
        Slot = slot;
        Kind = kind;
        Unit = unit;
        Action = action;
    }

    public static PlayableOfferEntry ForUnit(int slot, UnitDefinition definition) =>
        new(slot, PlayableKind.Unit, definition ?? throw new ArgumentNullException(nameof(definition)), null);

    public static PlayableOfferEntry ForAction(int slot, ActionDefinition definition) =>
        new(slot, PlayableKind.Action, null, definition ?? throw new ArgumentNullException(nameof(definition)));
}

public sealed record PlayableReserveEntry
{
    public int Slot { get; }
    public PlayableKind Kind { get; }
    public UnitInstance? Unit { get; }
    public ActionInstance? Action { get; }
    public string DefinitionId => Unit?.Definition.Id.Value ?? Action!.Definition.Id.Value;
    public string Name => Unit?.Definition.Name ?? Action!.Definition.Name;

    private PlayableReserveEntry(int slot, PlayableKind kind, UnitInstance? unit, ActionInstance? action)
    {
        Slot = slot;
        Kind = kind;
        Unit = unit;
        Action = action;
    }

    public static PlayableReserveEntry ForUnit(int slot, UnitInstance instance) =>
        new(slot, PlayableKind.Unit, instance ?? throw new ArgumentNullException(nameof(instance)), null);

    public static PlayableReserveEntry ForAction(int slot, ActionInstance instance) =>
        new(slot, PlayableKind.Action, null, instance ?? throw new ArgumentNullException(nameof(instance)));
}
