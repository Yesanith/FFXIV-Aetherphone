namespace Aetherphone.Core.Animation;

internal sealed class TransitionPlayer
{
    private float elapsed;
    private float duration;
    private EasingFunction easing = Easing.Linear;

    public bool IsPlaying { get; private set; }

    public float RawProgress => duration <= 0f ? 1f : Math.Clamp(elapsed / duration, 0f, 1f);

    public float Progress => easing(RawProgress);

    public void Start(float durationSeconds, EasingFunction curve)
    {
        duration = durationSeconds;
        easing = curve;
        elapsed = 0f;
        IsPlaying = durationSeconds > 0f;
    }

    public void Advance(float deltaSeconds)
    {
        if (!IsPlaying)
        {
            return;
        }

        elapsed += deltaSeconds;
        if (RawProgress >= 1f)
        {
            IsPlaying = false;
        }
    }

    public void Finish() => IsPlaying = false;
}
