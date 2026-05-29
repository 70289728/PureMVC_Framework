using System.Collections.Generic;

/// <summary>
/// Runs multiple sub-clips in parallel. All sub-clips use the same parent timeline time.
/// OnEnter enters all sub-clips. OnUpdate updates all active sub-clips.
/// OnExit exits all remaining active sub-clips.
///
/// Sub-clip startTime is interpreted relative to this clip's startTime.
/// The parent clip's duration should be at least max(subClip.startTime + subClip.duration).
/// </summary>
public class ParallelAction : ITimelineAction
{
    private List<TimelineClip> subClips;
    private TimelineContext parentCtx;
    private float localStartTime; // parent clip's absolute startTime
    private List<int> activeSubIndices;
    private int nextSubIndex;

    public ParallelAction(List<TimelineClip> subClips)
    {
        this.subClips = subClips ?? new List<TimelineClip>();
        // Sort sub-clips by startTime
        this.subClips.Sort((a, b) => a.startTime.CompareTo(b.startTime));
        activeSubIndices = new List<int>();
        nextSubIndex = 0;
    }

    public void OnEnter(TimelineContext ctx)
    {
        parentCtx = ctx;
        localStartTime = ctx.clipStartTime;
        activeSubIndices.Clear();
        nextSubIndex = 0;

        // Enter any sub-clips starting at time 0
        TryEnterSubClips(0f);
    }

    public void OnUpdate(TimelineContext ctx, float elapsed)
    {
        // Enter new sub-clips
        TryEnterSubClips(elapsed);

        // Update active sub-clips
        for (int i = activeSubIndices.Count - 1; i >= 0; i--)
        {
            int idx = activeSubIndices[i];
            TimelineClip sub = subClips[idx];
            float subElapsed = elapsed - sub.startTime;
            if (subElapsed >= 0)
            {
                TimelineContext subCtx = MakeSubContext(idx, parentCtx);
                sub.action.OnUpdate(subCtx, subElapsed);
            }

            // Exit if duration expired
            if (sub.startTime + sub.duration <= elapsed)
            {
                TimelineContext subCtxEx = MakeSubContext(idx, parentCtx);
                sub.action.OnExit(subCtxEx);
                activeSubIndices.RemoveAt(i);
            }
        }
    }

    public void OnExit(TimelineContext ctx)
    {
        // Exit all remaining active sub-clips
        for (int i = activeSubIndices.Count - 1; i >= 0; i--)
        {
            int idx = activeSubIndices[i];
            TimelineContext subCtx = MakeSubContext(idx, parentCtx);
            subClips[idx].action.OnExit(subCtx);
        }
        activeSubIndices.Clear();
    }

    private void TryEnterSubClips(float elapsed)
    {
        for (int i = nextSubIndex; i < subClips.Count; i++)
        {
            TimelineClip sub = subClips[i];
            if (sub.startTime > elapsed) break;

            TimelineContext subCtx = MakeSubContext(i, parentCtx);
            sub.action.OnEnter(subCtx);

            if (sub.duration > 0f)
            {
                activeSubIndices.Add(i);
            }
            else
            {
                // Instant sub-clip
                sub.action.OnExit(subCtx);
            }

            nextSubIndex = i + 1;
        }
    }

    private TimelineContext MakeSubContext(int subIndex, TimelineContext parent)
    {
        TimelineClip sub = subClips[subIndex];
        return new TimelineContext
        {
            playerId = parent.playerId,
            clipIndex = subIndex,
            clipStartTime = localStartTime + sub.startTime,
            clipDuration = sub.duration,
            userData = null,
        };
    }
}
