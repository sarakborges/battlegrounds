using System.Text.Json;
using System.Text.Json.Serialization;

namespace Battlegrounds.Content;

public enum CombineInteractionMode
{
    Manual,
    Automatic,
}

public sealed record ModInteractionSettings(int Version, CombineInteractionMode CombineMode)
{
    public static ModInteractionSettings Default { get; } = new(1, CombineInteractionMode.Manual);
}

public sealed class ModInteractionSettingsLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public ModInteractionSettings Load(string modDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modDirectory);
        var issues = new ModInteractionSettingsValidator().Validate(modDirectory);
        if (issues.Count > 0) throw new ModValidationException(new ModValidationReport(issues));
        return LoadValidated(modDirectory);
    }

    internal static ModInteractionSettings LoadValidated(string modDirectory)
    {
        var path = Path.Combine(modDirectory, "presentation", "interaction.json");
        if (!File.Exists(path)) return ModInteractionSettings.Default;

        var data = JsonSerializer.Deserialize<InteractionData>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Validated interaction settings contained no data.");
        return new ModInteractionSettings(data.Version, data.CombineMode);
    }

    private sealed class InteractionData
    {
        public int Version { get; set; }
        public CombineInteractionMode CombineMode { get; set; } = CombineInteractionMode.Manual;
    }
}
