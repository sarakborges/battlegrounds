using System.Text.Json;

namespace Battlegrounds.Content;

internal sealed class PreparationEconomyEffectModValidator
{
    private static readonly JsonDocumentOptions Options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
    private static readonly HashSet<string> UnitEvents = ["onAcquire", "onRelease", "onPlay", "onCombine", "onTurnStart", "onTurnEnd"];
    private static readonly HashSet<string> PowerEvents = ["onActivate", "onMatchStart", "onTurnStart", "onTurnEnd"];
    private static readonly HashSet<string> PreparationHistoryEvents = ["unitAcquired", "unitReleased", "unitPlayed", "actionAcquired", "actionPlayed", "powerActivated", "offerRefreshed", "tierUpgraded"];

    public IReadOnlyList<ModValidationIssue> Validate(string root)
    {
        var issues = new List<ModValidationIssue>();
        ValidateDirectory(root, "content/units", false, issues);
        ValidateDirectory(root, "content/powers", true, issues);
        return issues;
    }

    private static void ValidateDirectory(string root, string relative, bool power, List<ModValidationIssue> issues)
    {
        var directory = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(directory)) return;
        foreach (var file in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file), Options);
                var json = document.RootElement;
                if (!json.TryGetProperty("triggers", out var triggers) || triggers.ValueKind != JsonValueKind.Array) continue;
                var triggerIndex = 0;
                foreach (var trigger in triggers.EnumerateArray())
                {
                    if (trigger.ValueKind != JsonValueKind.Object) { triggerIndex++; continue; }
                    var eventName = trigger.TryGetProperty("event", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
                    if (!trigger.TryGetProperty("effects", out var effects) || effects.ValueKind != JsonValueKind.Array) { triggerIndex++; continue; }
                    var allowed = power ? PowerEvents.Contains(eventName ?? "") : UnitEvents.Contains(eventName ?? "");
                    if (eventName == "afterEventCount")
                    {
                        var counterEvent = trigger.TryGetProperty("counter", out var counter) && counter.ValueKind == JsonValueKind.Object &&
                            counter.TryGetProperty("event", out var ce) && ce.ValueKind == JsonValueKind.String ? ce.GetString() : null;
                        allowed = PreparationHistoryEvents.Contains(counterEvent ?? "");
                    }
                    var effectIndex = 0;
                    foreach (var effect in effects.EnumerateArray())
                    {
                        if (effect.ValueKind == JsonValueKind.Object && effect.TryGetProperty("kind", out var kind) &&
                            kind.ValueKind == JsonValueKind.String && kind.GetString() is "adjustUpgradeCost" or "addAcquireDiscount" or "refreshOffer" or "mutateOffer" && !allowed)
                        {
                            issues.Add(new(
                                "INVALID_EFFECT_CONTEXT",
                                relative + "/" + Path.GetFileName(file),
                                $"$.triggers[{triggerIndex}].effects[{effectIndex}].kind",
                                $"{kind.GetString()} is only valid in Preparation-only contexts."));
                        }
                        effectIndex++;
                    }
                    triggerIndex++;
                }
            }
            catch (JsonException) { }
        }
    }
}
