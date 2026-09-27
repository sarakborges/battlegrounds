using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

internal sealed class ModPresentationCuePlayer
{
    private readonly Node _owner;
    private readonly string _modDirectory;
    private readonly ModPresentationCueCatalog _cues;
    private readonly Dictionary<string, AudioStream> _audioStreams = new(StringComparer.Ordinal);
    private AudioStreamPlayer? _audioPlayer;

    public ModPresentationCuePlayer(Node owner, string modDirectory)
        : this(owner, modDirectory, new ModPresentationCueLoader().Load(modDirectory))
    {
    }

    public ModPresentationCuePlayer(Node owner, string modDirectory, ModPresentationCueCatalog cues)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrWhiteSpace(modDirectory)) throw new ArgumentException("Mod directory cannot be empty.", nameof(modDirectory));
        _modDirectory = Path.GetFullPath(modDirectory);
        _cues = cues ?? throw new ArgumentNullException(nameof(cues));
    }

    public void Play(
        Control target,
        ModPresentationEntityKind entityKind,
        string entityId,
        string role,
        ModPresentationAnimation fallbackAnimation,
        double fallbackDurationSeconds)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        var hasCue = _cues.TryGet(entityKind, entityId, role, out var cue);
        var animation = hasCue && cue.Animation.HasValue ? cue.Animation.Value : fallbackAnimation;
        var duration = hasCue && cue.DurationSeconds.HasValue ? cue.DurationSeconds.Value : fallbackDurationSeconds;
        ApplyAnimation(target, animation, duration);

        if (hasCue && cue.Audio is not null)
            TryPlayAudio(cue.Audio);
    }

    private void TryPlayAudio(ModPresentationAssetReference audio)
    {
        if (audio.Type != ModPresentationAssetType.Audio) return;

        if (!_audioStreams.TryGetValue(audio.RelativePath, out var stream))
        {
            var fullPath = Path.Combine(
                _modDirectory,
                audio.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!string.Equals(Path.GetExtension(fullPath), ".wav", StringComparison.OrdinalIgnoreCase)) return;

            stream = AudioStreamWav.LoadFromFile(fullPath);
            if (stream is null) return;
            _audioStreams.Add(audio.RelativePath, stream);
        }

        _audioPlayer ??= CreateAudioPlayer();
        _audioPlayer.Stream = stream;
        _audioPlayer.Play();
    }

    private AudioStreamPlayer CreateAudioPlayer()
    {
        var player = new AudioStreamPlayer { Name = "PresentationCueAudio" };
        _owner.AddChild(player);
        return player;
    }

    private static void ApplyAnimation(Control target, ModPresentationAnimation animation, double durationSeconds)
    {
        if (animation == ModPresentationAnimation.None) return;

        var duration = Math.Clamp(durationSeconds, 0.05, 5.0);
        var tween = target.CreateTween();

        switch (animation)
        {
            case ModPresentationAnimation.Pop:
                target.Scale = new Vector2(0.88f, 0.88f);
                target.Modulate = new Color(1, 1, 1, 0.35f);
                tween.SetParallel();
                tween.TweenProperty(target, "scale", Vector2.One, duration);
                tween.TweenProperty(target, "modulate", Colors.White, duration);
                break;
            case ModPresentationAnimation.Fade:
                tween.SetParallel();
                tween.TweenProperty(target, "scale", new Vector2(0.9f, 0.9f), duration);
                tween.TweenProperty(target, "modulate", new Color(1, 1, 1, 0.25f), duration);
                break;
            case ModPresentationAnimation.Shake:
                target.Rotation = -0.025f;
                tween.TweenProperty(target, "rotation", 0.025f, duration * 0.5);
                tween.TweenProperty(target, "rotation", 0.0f, duration * 0.5);
                break;
            case ModPresentationAnimation.Lunge:
                target.Scale = new Vector2(1.08f, 1.08f);
                tween.TweenProperty(target, "scale", Vector2.One, duration);
                break;
            case ModPresentationAnimation.Pulse:
                target.Scale = new Vector2(1.05f, 1.05f);
                tween.TweenProperty(target, "scale", Vector2.One, duration);
                break;
        }
    }
}
