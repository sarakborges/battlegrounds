using System.Collections.ObjectModel;
using System.Globalization;

namespace Battlegrounds.Content;

public static class ModPresentationKeys
{
    public static IReadOnlyList<string> RequiredTerminology { get; } = Array.AsReadOnly(new[]
    {
        "unit", "units", "action", "actions", "leader", "leaders", "power", "powers",
        "armor", "health", "resource", "offer", "tier", "reserve", "field", "acquire",
        "release", "preparation", "combat", "round",
    });

    public static IReadOnlyList<string> RequiredDefaultStrings { get; } = Array.AsReadOnly(new[]
    {
        "ui.loading",
        "ui.bootstrapFailed",
        "ui.sessionLog",
        "ui.confirm",
        "ui.cancel",
        "ui.chooseLeader",
        "ui.leaderSelectionStatus",
        "ui.healthModifierPositive",
        "ui.healthModifierNegative",
        "ui.healthBase",
        "ui.leaderOption",
        "ui.matchStatus",
        "ui.playerSummary",
        "ui.noLeader",
        "ui.you",
        "ui.ai",
        "ui.ready",
        "ui.active",
        "ui.eliminated",
        "ui.maximum",
        "ui.frozen",
        "ui.open",
        "ui.pendingChoiceSuffix",
        "ui.humanSummary",
        "ui.resolvePendingChoice",
        "ui.actionTargetPrompt",
        "ui.powerTargetPrompt",
        "ui.chooseUnitOption",
        "ui.chooseActionOption",
        "ui.targetCandidate",
        "ui.noTargetCandidates",
        "ui.noCombine",
        "ui.chooseCombineRecipe",
        "ui.combineRecipe",
        "ui.combineComponentsPrompt",
        "ui.combineComponentsHint",
        "ui.combineConfirm",
        "ui.noOfferEntries",
        "ui.acquireEntry",
        "ui.reserveEmpty",
        "ui.combineReserveEntry",
        "ui.deploy",
        "ui.play",
        "ui.reserveEntry",
        "ui.fieldEmpty",
        "ui.combineFieldEntry",
        "ui.releaseUnit",
        "ui.refresh",
        "ui.upgradeTier",
        "ui.freezeOffer",
        "ui.unfreezeOffer",
        "ui.usePower",
        "ui.combineUnits",
        "ui.endPreparation",
        "ui.combatPlayback",
        "ui.combatResolvedTitle",
        "ui.combatInitialBoards",
        "ui.combatEventProgress",
        "ui.combatSettlementProgress",
        "ui.next",
        "ui.continue",
        "ui.skipSettlement",
        "ui.emptyField",
        "ui.sideYou",
        "ui.sideAi",
        "ui.sideArchived",
        "ui.combatReady",
        "ui.combatResourceChanged",
        "ui.combatPowerChanged",
        "ui.combatGenericEvent",
        "ui.combatUnitTriggered",
        "ui.combatPowerSource",
        "ui.combatSource",
        "ui.combatSourceTriggered",
        "ui.combatAttackStarted",
        "ui.combatSummoned",
        "ui.combatStatsChanged",
        "ui.combatDamageTaken",
        "ui.combatDestroyed",
        "ui.combatDied",
        "ui.combatRevived",
        "ui.combatBehaviorChanged",
        "ui.combatUnitFallback",
        "ui.archivedPlayer",
        "ui.draw",
        "ui.playerDamage",
        "ui.noPlayerDamage",
        "ui.combatComplete",
    });

    public static string Term(string terminologyKey) => $"term.{terminologyKey}";
}

public sealed class ModPresentationCatalog
{
    private readonly ReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _locales;
    private readonly ReadOnlyDictionary<string, string> _terminology;
    private readonly ReadOnlyDictionary<string, string> _entityFallbacks;

    public string DefaultLocale { get; }
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Locales => _locales;
    public IReadOnlyDictionary<string, string> Terminology => _terminology;
    public IReadOnlyDictionary<string, string> EntityFallbacks => _entityFallbacks;

    internal ModPresentationCatalog(
        string defaultLocale,
        IReadOnlyDictionary<string, string> terminology,
        IReadOnlyDictionary<string, string> entityFallbacks,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> locales)
    {
        if (string.IsNullOrWhiteSpace(defaultLocale)) throw new ArgumentException("Default locale cannot be empty.", nameof(defaultLocale));
        ArgumentNullException.ThrowIfNull(terminology);
        ArgumentNullException.ThrowIfNull(entityFallbacks);
        ArgumentNullException.ThrowIfNull(locales);
        if (!locales.ContainsKey(defaultLocale)) throw new ArgumentException($"Default locale '{defaultLocale}' is missing.", nameof(locales));

        DefaultLocale = defaultLocale;
        _terminology = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(terminology, StringComparer.Ordinal));
        _entityFallbacks = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(entityFallbacks, StringComparer.Ordinal));
        _locales = new ReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>(
            locales.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyDictionary<string, string>)new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>(pair.Value, StringComparer.Ordinal)),
                StringComparer.OrdinalIgnoreCase));
    }

    public ModPresentationText Resolve(string? requestedLocale)
    {
        var selectedLocale = ResolveLocale(requestedLocale);
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in _terminology)
            strings[ModPresentationKeys.Term(pair.Key)] = pair.Value;
        Overlay(strings, _entityFallbacks);
        Overlay(strings, _locales[DefaultLocale]);

        var languageLocale = ResolveLanguageLocale(selectedLocale);
        if (languageLocale is not null && !string.Equals(languageLocale, DefaultLocale, StringComparison.OrdinalIgnoreCase))
            Overlay(strings, _locales[languageLocale]);

        if (!string.Equals(selectedLocale, DefaultLocale, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(selectedLocale, languageLocale, StringComparison.OrdinalIgnoreCase))
            Overlay(strings, _locales[selectedLocale]);

        return new ModPresentationText(selectedLocale, strings);
    }

    private string ResolveLocale(string? requestedLocale)
    {
        if (string.IsNullOrWhiteSpace(requestedLocale)) return DefaultLocale;
        var normalized = requestedLocale.Replace('_', '-');
        var exact = _locales.Keys.FirstOrDefault(value => string.Equals(value, normalized, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        var separator = normalized.IndexOf('-');
        if (separator > 0)
        {
            var language = normalized[..separator];
            var languageMatch = _locales.Keys.FirstOrDefault(value => string.Equals(value, language, StringComparison.OrdinalIgnoreCase));
            if (languageMatch is not null) return languageMatch;
        }

        return DefaultLocale;
    }

    private string? ResolveLanguageLocale(string locale)
    {
        var separator = locale.IndexOf('-');
        if (separator <= 0) return null;
        var language = locale[..separator];
        return _locales.Keys.FirstOrDefault(value => string.Equals(value, language, StringComparison.OrdinalIgnoreCase));
    }

    private static void Overlay(IDictionary<string, string> target, IReadOnlyDictionary<string, string> source)
    {
        foreach (var pair in source) target[pair.Key] = pair.Value;
    }
}

public sealed class ModPresentationText
{
    private readonly ReadOnlyDictionary<string, string> _strings;

    public string Locale { get; }
    public IReadOnlyDictionary<string, string> Strings => _strings;

    internal ModPresentationText(string locale, IReadOnlyDictionary<string, string> strings)
    {
        Locale = locale;
        _strings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(strings, StringComparer.Ordinal));
    }

    public string Get(string key) =>
        _strings.TryGetValue(key, out var value)
            ? value
            : throw new KeyNotFoundException($"Presentation string '{key}' is not available for locale '{Locale}'.");

    public bool TryGet(string key, out string value) => _strings.TryGetValue(key, out value!);

    public string Term(string terminologyKey) => Get(ModPresentationKeys.Term(terminologyKey));

    public string EntityName(ModPresentationEntityKind kind, string id) =>
        Get(ModPresentationEntityKeys.Name(kind, id));

    public bool TryEntityDescription(ModPresentationEntityKind kind, string id, out string value) =>
        TryGet(ModPresentationEntityKeys.Description(kind, id), out value!);

    public string Format(string key, params (string Name, object? Value)[] values)
    {
        var result = Get(key);
        foreach (var (name, value) in values)
        {
            var formatted = value switch
            {
                null => string.Empty,
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty,
            };
            result = result.Replace("{" + name + "}", formatted, StringComparison.Ordinal);
        }
        return result;
    }
}
