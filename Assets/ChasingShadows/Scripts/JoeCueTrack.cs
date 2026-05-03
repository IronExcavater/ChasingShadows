using UnityEngine.Timeline;

namespace ChasingShadows.Characters
{
    [TrackColor(0.45f, 0.78f, 0.45f)]
    [TrackClipType(typeof(JoeCueClip))]
    [TrackBindingType(typeof(JoeCinematicController))]
    public sealed class JoeCueTrack : TrackAsset
    {
    }
}
