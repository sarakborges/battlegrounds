using Battlegrounds.Core.Domain.Preparation;

namespace Battlegrounds.AI;

/// <summary>
/// Immutable observation of successful Preparation commands emitted by the AI.
/// These counters never participate in decision-making or authoritative mutation.
/// </summary>
public sealed record PreparationAiCommandCounts(
    long Acquires = 0,
    long Releases = 0,
    long Deploys = 0,
    long ActionsPlayed = 0,
    long Combines = 0,
    long Refreshes = 0,
    long Upgrades = 0,
    long PowersUsed = 0,
    long UnitChoicesResolved = 0,
    long ActionChoicesResolved = 0,
    long Freezes = 0,
    long Unfreezes = 0,
    long Ends = 0)
{
    public static PreparationAiCommandCounts Zero { get; } = new();

    public long Reorders { get; init; }

    public long Total =>
        Acquires +
        Releases +
        Deploys +
        Reorders +
        ActionsPlayed +
        Combines +
        Refreshes +
        Upgrades +
        PowersUsed +
        UnitChoicesResolved +
        ActionChoicesResolved +
        Freezes +
        Unfreezes +
        Ends;

    public PreparationAiCommandCounts Add(IPreparationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command switch
        {
            AcquireUnitCommand or AcquirePlayableCommand => this with { Acquires = Acquires + 1 },
            ReleaseUnitCommand => this with { Releases = Releases + 1 },
            DeployUnitCommand => this with { Deploys = Deploys + 1 },
            ReorderFieldCommand => this with { Reorders = Reorders + 1 },
            PlayActionCommand => this with { ActionsPlayed = ActionsPlayed + 1 },
            CombineUnitsCommand => this with { Combines = Combines + 1 },
            RefreshOfferCommand => this with { Refreshes = Refreshes + 1 },
            UpgradeTierCommand => this with { Upgrades = Upgrades + 1 },
            UsePowerCommand => this with { PowersUsed = PowersUsed + 1 },
            ResolveUnitChoiceCommand => this with { UnitChoicesResolved = UnitChoicesResolved + 1 },
            ResolveActionChoiceCommand => this with { ActionChoicesResolved = ActionChoicesResolved + 1 },
            FreezeOfferCommand => this with { Freezes = Freezes + 1 },
            UnfreezeOfferCommand => this with { Unfreezes = Unfreezes + 1 },
            EndPreparationCommand => this with { Ends = Ends + 1 },
            _ => throw new ArgumentOutOfRangeException(
                nameof(command),
                command.GetType().Name,
                "Unsupported Preparation command telemetry type."),
        };
    }

    public static PreparationAiCommandCounts operator +(
        PreparationAiCommandCounts left,
        PreparationAiCommandCounts right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return new PreparationAiCommandCounts(
            left.Acquires + right.Acquires,
            left.Releases + right.Releases,
            left.Deploys + right.Deploys,
            left.ActionsPlayed + right.ActionsPlayed,
            left.Combines + right.Combines,
            left.Refreshes + right.Refreshes,
            left.Upgrades + right.Upgrades,
            left.PowersUsed + right.PowersUsed,
            left.UnitChoicesResolved + right.UnitChoicesResolved,
            left.ActionChoicesResolved + right.ActionChoicesResolved,
            left.Freezes + right.Freezes,
            left.Unfreezes + right.Unfreezes,
            left.Ends + right.Ends)
        {
            Reorders = left.Reorders + right.Reorders,
        };
    }
}
