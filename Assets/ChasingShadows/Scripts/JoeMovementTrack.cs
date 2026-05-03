using UnityEngine.Timeline;

namespace ChasingShadows.Characters
{
    [TrackColor(0.2f, 0.65f, 0.9f)]
    [TrackClipType(typeof(JoeMovementTimelineClip))]
    [TrackBindingType(typeof(JoeCinematicController))]
    public sealed class JoeMovementTrack : TrackAsset
    {
    }
}
