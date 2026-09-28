using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

internal sealed record ModPresentationMotionProfile(
    float PulseScale,
    double PulseDurationSeconds,
    float ShakeRotationDegrees,
    double ShakeDurationSeconds,
    float LungeScale,
    double LungeDurationSeconds,
    float FadeScale,
    float FadeOpacity,
    double FadeDurationSeconds,
    float PopScale,
    float PopOpacity,
    double PopDurationSeconds)
{
    public double DurationFor(ModPresentationAnimation animation) => animation switch
    {
        ModPresentationAnimation.Pulse => PulseDurationSeconds,
        ModPresentationAnimation.Shake => ShakeDurationSeconds,
        ModPresentationAnimation.Lunge => LungeDurationSeconds,
        ModPresentationAnimation.Fade => FadeDurationSeconds,
        ModPresentationAnimation.Pop => PopDurationSeconds,
        ModPresentationAnimation.None => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(animation), animation, null),
    };
}

internal sealed class ModPresentationCuePlayer
{
    private const string LungeDirectionMeta = "presentation_lunge_direction";

    private readonly Node _owner;
    private readonly string _modDirectory;
    private readonly ModPresentationCueCatalog _cues;
    private readonly ModPresentationMotionProfile _motion;
    private readonly Dictionary<string, AudioStream> _audioStreams = new(StringComparer.Ordinal);
    private AudioStreamPlayer? _audioPlayer;

    public ModPresentationCuePlayer(Node owner, string modDirectory, ModPresentationMotionProfile motion)
        : this(owner, modDirectory, new ModPresentationCueLoader().Load(modDirectory), motion)
    {
    }

    public ModPresentationCuePlayer(
        Node owner,
        string modDirectory,
        ModPresentationCueCatalog cues,
        ModPresentationMotionProfile motion)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrWhiteSpace(modDirectory)) throw new ArgumentException("Mod directory cannot be empty.", nameof(modDirectory));
        _modDirectory = Path.GetFullPath(modDirectory);
        _cues = cues ?? throw new ArgumentNullException(nameof(cues));
        _motion = motion ?? throw new ArgumentNullException(nameof(motion));
    }

    public void Play(
        Control target,
        ModPresentationEntityKind entityKind,
        string entityId,
        string role,
        ModPresentationAnimation fallbackAnimation,
        double? fallbackDurationSeconds = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        var hasCue = _cues.TryGet(entityKind, entityId, role, out var cue);
        var animation = hasCue && cue.Animation.HasValue ? cue.Animation.Value : fallbackAnimation;
        var duration = hasCue && cue.DurationSeconds.HasValue
            ? cue.DurationSeconds.Value
            : fallbackDurationSeconds ?? _motion.DurationFor(animation);
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

    private void ApplyAnimation(Control target, ModPresentationAnimation animation, double durationSeconds)
    {
        if (animation == ModPresentationAnimation.None) return;

        var duration = Math.Clamp(durationSeconds, 0.05, 5.0);
        target.PivotOffset = target.Size * 0.5f;
        var tween = target.CreateTween();

        switch (animation)
        {
            case ModPresentationAnimation.Pop:
                target.Scale = new Vector2(_motion.PopScale, _motion.PopScale);
                target.Modulate = new Color(1, 1, 1, _motion.PopOpacity);
                tween.SetParallel();
                tween.TweenProperty(target, "scale", Vector2.One, duration);
                tween.TweenProperty(target, "modulate", Colors.White, duration);
                break;
            case ModPresentationAnimation.Fade:
                tween.SetParallel();
                tween.TweenProperty(target, "scale", new Vector2(_motion.FadeScale, _motion.FadeScale), duration);
                tween.TweenProperty(target, "modulate", new Color(1, 1, 1, _motion.FadeOpacity), duration);
                break;
            case ModPresentationAnimation.Shake:
                var rotation = Mathf.DegToRad(_motion.ShakeRotationDegrees);
                target.Rotation = -rotation;
                tween.TweenProperty(target, "rotation", rotation, duration * 0.5);
                tween.TweenProperty(target, "rotation", 0.0f, duration * 0.5);
                break;
            case ModPresentationAnimation.Lunge:
                ApplyLunge(target, tween, duration);
                break;
            case ModPresentationAnimation.Pulse:
                target.Scale = new Vector2(_motion.PulseScale, _motion.PulseScale);
                tween.TweenProperty(target, "scale", Vector2.One, duration);
                break;
        }
    }

    private void ApplyLunge(Control target, Tween tween, double duration)
    {
        if (!target.HasMeta(LungeDirectionMeta))
        {
            target.Scale = new Vector2(_motion.LungeScale, _motion.LungeScale);
            tween.TweenProperty(target, "scale", Vector2.One, duration);
            return;
        }

        var direction = target.GetMeta(LungeDirectionMeta).AsVector2().Normalized();
        if (direction == Vector2.Zero)
        {
            target.Scale = new Vector2(_motion.LungeScale, _motion.LungeScale);
            tween.TweenProperty(target, "scale", Vector2.One, duration);
            return;
        }

        var origin = target.Position;
        var magnitude = Mathf.Max(target.Size.X, target.Size.Y);
        var distance = magnitude * Mathf.Max(0.18f, (_motion.LungeScale - 1.0f) * 4.0f);
        var strikePosition = origin + direction * distance;
        var strikeScale = new Vector2(_motion.LungeScale, _motion.LungeScale);
        var outwardDuration = duration * 0.38;
        var returnDuration = duration - outwardDuration;

        tween.SetParallel();
        tween.TweenProperty(target, "position", strikePosition, outwardDuration)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(target, "scale", strikeScale, outwardDuration)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.Chain().SetParallel();
        tween.TweenProperty(target, "position", origin, returnDuration)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(target, "scale", Vector2.One, returnDuration)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
    }
}
