namespace Aetherphone.Core.Animation;

internal static class TransitionTiming
{
    public const float PresentSmoothTime = 0.22f;

    public const float DismissSmoothTime = 0.18f;

    public const float PushSeconds = 0.28f;

    public const float ShellDimMax = 0.45f;

    public const float UnderParallax = 0.26f;

    public const float UnderDimMax = 0.16f;

    public const float MaxFrameSeconds = 0.1f;

    public const float BubbleSeconds = 0.34f;

    public const float RestPositionEpsilon = 0.0015f;

    public const float RestVelocityEpsilon = 0.02f;

    public static readonly EasingFunction PushCurve = Easing.EaseOutCubic;
}
