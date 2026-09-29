using Battlegrounds.Application;
using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Game;

public partial class Main
{
    private long _observedCombatSequence;
    private double _combatPlaybackAccumulator;
    private CombatPlaybackState? _combatPlayback;
    private readonly Dictionary<UnitInstanceId, UnitId> _combatUnitDefinitions = [];

    private void ObserveLatestCombat()
    {
        if (_session?.LastCombat is not SessionCombatRecord record || record.Sequence <= _observedCombatSequence)
            return;

        _observedCombatSequence = record.Sequence;
        var text = _presentationText ?? _session.Mod.Presentation.Resolve(null);
        var playback = CombatPlaybackState.TryCreate(record, _session.HumanPlayerId, text);
        if (playback is null) return;

        _combatPlayback = playback;
        _combatPlaybackAccumulator = 0;
        BuildCombatUnitDefinitionIndex(playback);
        PushWebUiState();
    }

    private void BuildCombatUnitDefinitionIndex(CombatPlaybackState playback)
    {
        _combatUnitDefinitions.Clear();
        foreach (var snapshot in playback.Record.StartingUnits)
            _combatUnitDefinitions[snapshot.InstanceId] = snapshot.UnitId;

        foreach (var timelineEvent in playback.Settlement.CombatResult.Timeline)
        {
            switch (timelineEvent)
            {
                case CombatUnitSummonedTimelineEvent summoned:
                    _combatUnitDefinitions[summoned.Unit.InstanceId] = summoned.Unit.UnitId;
                    break;
                case CombatUnitRevivedTimelineEvent revived:
                    _combatUnitDefinitions[revived.Unit.InstanceId] = revived.Unit.UnitId;
                    break;
            }
        }
    }

    private void AdvanceWebCombatPlayback(double delta)
    {
        ObserveLatestCombat();
        if (_combatPlayback is null)
            return;

        _combatPlaybackAccumulator += delta;
        var interval = ResolveCombatPlaybackInterval();
        if (_combatPlaybackAccumulator < interval)
            return;

        _combatPlaybackAccumulator = 0;
        if (_combatPlayback.Advance())
        {
            FinishCombatPlayback();
            return;
        }

        PushWebUiState();
    }

    private double ResolveCombatPlaybackInterval()
    {
        if (_modTheme?.Metrics.TryGetValue(ModThemeMetricKeys.Motion.CombatPlaybackStepSeconds, out var value) == true)
            return Math.Clamp(value, 0.05, 10.0);
        return 0.35;
    }

    private void FinishCombatPlayback()
    {
        _combatPlayback = null;
        _combatUnitDefinitions.Clear();
        _combatPlaybackAccumulator = 0;
        PushWebUiState();
    }

    private UnitId? ResolveCombatUnitId(UnitInstanceId instanceId) =>
        _combatUnitDefinitions.TryGetValue(instanceId, out var unitId) ? unitId : null;

    private int ResolveCombatUnitTier(UnitId? unitId)
    {
        if (_session is null || unitId is not UnitId knownUnitId) return 0;
        return _session.Mod.Units.GetRequired(knownUnitId).Tier;
    }
}
