using System.Numerics;
using Aetherphone.Core.Animation;

namespace Aetherphone.Core.Apps;

internal delegate void RouterDraw<TView>(TView view, Rect area, int depth);

internal sealed class ViewRouter<TView>
{
    private static readonly string[] DepthIds =
    {
        "view0", "view1", "view2", "view3", "view4", "view5", "view6", "view7",
    };

    private readonly List<TView> stack = new();
    private readonly TransitionPlayer player = new();

    private TView outgoing = default!;
    private int outgoingDepth;
    private SlideDirection direction;

    public ViewRouter(TView root) => stack.Add(root);

    public TView Current => stack[stack.Count - 1];

    public int Depth => stack.Count;

    public bool IsTransitioning => player.IsPlaying;

    public void Push(TView view) => Push(view, true);

    public void Push(TView view, bool animate)
    {
        if (animate)
        {
            BeginOutgoing(SlideDirection.Forward);
        }

        stack.Add(view);

        if (animate)
        {
            player.Start(TransitionTiming.PushSeconds, TransitionTiming.PushCurve);
        }
    }

    public bool Pop()
    {
        if (stack.Count <= 1)
        {
            return false;
        }

        BeginOutgoing(SlideDirection.Back);
        stack.RemoveAt(stack.Count - 1);
        player.Start(TransitionTiming.PushSeconds, TransitionTiming.PushCurve);
        return true;
    }

    public void Reset()
    {
        player.Finish();
        outgoing = default!;
        if (stack.Count > 1)
        {
            stack.RemoveRange(1, stack.Count - 1);
        }
    }

    public void Draw(Rect area, Vector4 background, float deltaSeconds, RouterDraw<TView> draw)
    {
        player.Advance(MathF.Min(deltaSeconds, TransitionTiming.MaxFrameSeconds));

        if (!player.IsPlaying)
        {
            outgoing = default!;
            var current = Current;
            var depth = Depth;
            SceneCompositor.DrawLayer(area, new SceneCompositor.Layer(Key(depth), Vector2.Zero, 0f, target => draw(current, target, depth), background));
            return;
        }

        var progress = player.Progress;
        var width = area.Width;
        var incoming = Current;
        var incomingDepth = Depth;
        var leaving = outgoing;
        var leavingDepth = outgoingDepth;

        SceneCompositor.Layer under;
        SceneCompositor.Layer over;
        if (direction == SlideDirection.Forward)
        {
            var underOffset = new Vector2(-TransitionTiming.UnderParallax * progress * width, 0f);
            var overOffset = new Vector2((1f - progress) * width, 0f);
            under = new SceneCompositor.Layer(Key(leavingDepth), underOffset, TransitionTiming.UnderDimMax * progress, target => draw(leaving, target, leavingDepth), background, true);
            over = new SceneCompositor.Layer(Key(incomingDepth), overOffset, 0f, target => draw(incoming, target, incomingDepth), background, true);
        }
        else
        {
            var underOffset = new Vector2(-TransitionTiming.UnderParallax * (1f - progress) * width, 0f);
            var overOffset = new Vector2(progress * width, 0f);
            under = new SceneCompositor.Layer(Key(incomingDepth), underOffset, TransitionTiming.UnderDimMax * (1f - progress), target => draw(incoming, target, incomingDepth), background, true);
            over = new SceneCompositor.Layer(Key(leavingDepth), overOffset, 0f, target => draw(leaving, target, leavingDepth), background, true);
        }

        SceneCompositor.Composite(area, under, over);
    }

    private void BeginOutgoing(SlideDirection slideDirection)
    {
        if (player.IsPlaying)
        {
            player.Finish();
        }

        outgoing = Current;
        outgoingDepth = stack.Count;
        direction = slideDirection;
    }

    private static string Key(int depth) => depth >= 0 && depth < DepthIds.Length ? DepthIds[depth] : "view" + depth;
}
