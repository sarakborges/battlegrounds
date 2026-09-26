using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Units;

namespace Battlegrounds.Core.Domain.Effects;

public sealed record EffectEvent(
    NativeTriggerKey Event,
    UnitInstance Subject,
    PlayerId SubjectPlayerId);

internal sealed class EffectEventQueue
{
    private const int MaximumProcessedEvents = 10_000;
    private readonly Queue<EffectEvent> _events = new();
    private int _processedEvents;

    public int Count => _events.Count;

    public void Enqueue(EffectEvent effectEvent)
    {
        ArgumentNullException.ThrowIfNull(effectEvent);
        if (!NativeTriggerKeys.IsSupported(effectEvent.Event))
        {
            throw new ArgumentException($"Unsupported trigger '{effectEvent.Event}'.", nameof(effectEvent));
        }

        _events.Enqueue(effectEvent);
    }

    public EffectEvent Dequeue()
    {
        if (_events.Count == 0)
        {
            throw new InvalidOperationException("Effect event queue is empty.");
        }

        _processedEvents++;
        if (_processedEvents > MaximumProcessedEvents)
        {
            throw new InvalidOperationException(
                "Effect event queue exceeded its internal safety budget. The mod likely contains a recursive effect loop.");
        }

        return _events.Dequeue();
    }
}
