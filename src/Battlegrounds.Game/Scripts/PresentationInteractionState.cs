using Battlegrounds.Core.Domain.Effects;

namespace Battlegrounds.Game;

internal enum PresentationInteractionKind
{
    None,
    DeployTarget,
    ActionTarget,
    PowerTarget,
}

internal sealed class PresentationInteractionState
{
    public PresentationInteractionKind Kind { get; private set; }
    public int? UnitReserveSlot { get; private set; }
    public int? ActionReserveSlot { get; private set; }
    public EffectTargetZone TargetZone { get; private set; } = EffectTargetZone.Field;

    public bool IsActive => Kind != PresentationInteractionKind.None;

    public void BeginDeployTarget(int reserveSlot, EffectTargetZone targetZone = EffectTargetZone.Field)
    {
        Reset();
        Kind = PresentationInteractionKind.DeployTarget;
        UnitReserveSlot = reserveSlot;
        TargetZone = targetZone;
    }

    public void BeginActionTarget(int reserveSlot, EffectTargetZone targetZone = EffectTargetZone.Field)
    {
        Reset();
        Kind = PresentationInteractionKind.ActionTarget;
        ActionReserveSlot = reserveSlot;
        TargetZone = targetZone;
    }

    public void BeginPowerTarget(EffectTargetZone targetZone = EffectTargetZone.Field)
    {
        Reset();
        Kind = PresentationInteractionKind.PowerTarget;
        TargetZone = targetZone;
    }

    public void Reset()
    {
        Kind = PresentationInteractionKind.None;
        UnitReserveSlot = null;
        ActionReserveSlot = null;
        TargetZone = EffectTargetZone.Field;
    }
}
