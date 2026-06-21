using Aetherphone.Core.Animation;
using Aetherphone.Core.Messaging;

namespace Aetherphone.Apps.Messages;

// Decides which bubbles pop in. Opening a thread shows its history settled — only lines that
// arrive while the thread is on screen animate, the way iMessage leaves the backlog alone and
// inflates just the new message. Tracks per-line elapsed time for the few bubbles currently
// animating; everything else reports fully settled.
internal sealed class ChatEntranceAnimator
{
    private struct Entrance
    {
        public int Line;
        public float Elapsed;
    }

    private readonly List<Entrance> active = new();

    private Conversation? tracked;
    private int settledCount;

    public void Sync(Conversation conversation, float deltaSeconds)
    {
        if (!ReferenceEquals(conversation, tracked))
        {
            tracked = conversation;
            settledCount = conversation.Lines.Count;
            active.Clear();
            return;
        }

        var lineCount = conversation.Lines.Count;
        while (settledCount < lineCount)
        {
            active.Add(new Entrance { Line = settledCount, Elapsed = 0f });
            settledCount++;
        }

        for (var index = active.Count - 1; index >= 0; index--)
        {
            var entrance = active[index];
            entrance.Elapsed += deltaSeconds;
            if (entrance.Elapsed >= TransitionTiming.BubbleSeconds)
            {
                active.RemoveAt(index);
            }
            else
            {
                active[index] = entrance;
            }
        }
    }

    // Linear progress in [0,1]; 1 means the bubble is settled and should draw at rest.
    public float Progress(int line)
    {
        for (var index = 0; index < active.Count; index++)
        {
            if (active[index].Line == line)
            {
                return active[index].Elapsed / TransitionTiming.BubbleSeconds;
            }
        }

        return 1f;
    }
}
