using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Preparation;

public interface IPreparationCommand
{
    PlayerId PlayerId { get; }
}

public sealed record AcquireUnitCommand(PlayerId PlayerId, int OfferSlot) : IPreparationCommand;
public sealed record AcquirePlayableCommand(PlayerId PlayerId, int OfferSlot) : IPreparationCommand;
public sealed record ReleaseUnitCommand(PlayerId PlayerId, int FieldSlot) : IPreparationCommand;
public sealed record DeployUnitCommand(PlayerId PlayerId, int ReserveSlot, UnitInstanceId? TargetUnitInstanceId = null) : IPreparationCommand;
public sealed record ReorderFieldCommand(
    PlayerId PlayerId,
    IReadOnlyList<UnitInstanceId> UnitInstanceIds) : IPreparationCommand;
public sealed record PlayActionCommand(PlayerId PlayerId, int ReserveSlot, UnitInstanceId? TargetUnitInstanceId = null) : IPreparationCommand;
public sealed record CombineUnitsCommand(
    PlayerId PlayerId,
    UnitCombineId CombineId,
    IReadOnlyList<UnitInstanceId> UnitInstanceIds) : IPreparationCommand;
public sealed record RefreshOfferCommand(PlayerId PlayerId) : IPreparationCommand;
public sealed record UpgradeTierCommand(PlayerId PlayerId) : IPreparationCommand;
public sealed record UsePowerCommand(PlayerId PlayerId, UnitInstanceId? TargetUnitInstanceId = null) : IPreparationCommand;
public sealed record ResolveUnitChoiceCommand(PlayerId PlayerId, ChoiceId ChoiceId, int OptionIndex) : IPreparationCommand;
public sealed record ResolveActionChoiceCommand(PlayerId PlayerId, ChoiceId ChoiceId, int OptionIndex) : IPreparationCommand;
public sealed record FreezeOfferCommand(PlayerId PlayerId) : IPreparationCommand;
public sealed record UnfreezeOfferCommand(PlayerId PlayerId) : IPreparationCommand;
public sealed record FreezeOfferSlotCommand(PlayerId PlayerId, int OfferSlot) : IPreparationCommand;
public sealed record UnfreezeOfferSlotCommand(PlayerId PlayerId, int OfferSlot) : IPreparationCommand;
public sealed record EndPreparationCommand(PlayerId PlayerId) : IPreparationCommand;
