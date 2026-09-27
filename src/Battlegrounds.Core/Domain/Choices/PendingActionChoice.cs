using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Actions;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Choices;

public sealed class PendingActionChoice : PendingChoice
{
    private readonly ReadOnlyCollection<ActionDefinition> _options;

    public override PendingChoiceKind Kind => PendingChoiceKind.Action;
    public IReadOnlyList<ActionDefinition> Options => _options;

    internal PendingActionChoice(ChoiceId id, IEnumerable<ActionDefinition> options)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(options);
        var materialized = options.ToArray();
        if (materialized.Length == 0)
            throw new ArgumentException("A pending choice requires at least one option.", nameof(options));
        if (materialized.Any(option => option is null))
            throw new ArgumentException("Choice options cannot contain null values.", nameof(options));
        if (materialized.Select(option => option.Id).Distinct().Count() != materialized.Length)
            throw new ArgumentException("Choice options must be unique by action id.", nameof(options));

        _options = Array.AsReadOnly(materialized);
    }
}
