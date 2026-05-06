using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ChasingShadows.Characters
{
    [TrackColor(0.2f, 0.55f, 0.95f)]
    [TrackBindingType(typeof(Transform))]
    [TrackClipType(typeof(CinematicMotionClip))]
    public sealed class CinematicMotionTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<CinematicMotionMixer>.Create(graph, inputCount);
        }
    }

    public sealed class CinematicMotionMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var target = playerData as Transform;
            if (target == null)
            {
                return;
            }

            var totalWeight = 0f;
            var position = Vector3.zero;
            var rotation = Quaternion.identity;
            var hasRotation = false;
            var applyPosition = false;
            var applyRotation = false;

            for (var i = 0; i < playable.GetInputCount(); i++)
            {
                var weight = playable.GetInputWeight(i);
                if (weight <= 0f)
                {
                    continue;
                }

                var input = (ScriptPlayable<CinematicMotionBehaviour>)playable.GetInput(i);
                var behaviour = input.GetBehaviour();
                behaviour.Evaluate(target, input.GetTime(), input.GetDuration(), out var samplePosition, out var sampleRotation);

                totalWeight += weight;
                applyPosition |= behaviour.applyPosition;
                applyRotation |= behaviour.applyRotation;

                if (behaviour.applyPosition)
                {
                    position += samplePosition * weight;
                }

                if (!behaviour.applyRotation)
                {
                    continue;
                }

                rotation = hasRotation
                    ? Quaternion.Slerp(rotation, sampleRotation, weight / totalWeight)
                    : sampleRotation;
                hasRotation = true;
            }

            if (totalWeight <= 0f)
            {
                return;
            }

            var normalizedPosition = position / totalWeight;
            if (applyPosition)
            {
                target.position = normalizedPosition;
            }

            if (applyRotation && hasRotation)
            {
                target.rotation = rotation;
            }
        }
    }
}
