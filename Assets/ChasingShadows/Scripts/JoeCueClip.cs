using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ChasingShadows.Characters
{
    public sealed class JoeCueClip : PlayableAsset, ITimelineClipAsset
    {
        public ExposedReference<Transform> lookTarget;
        public ExposedReference<Transform> leftHandTarget;
        public ExposedReference<Transform> rightHandTarget;
        public string animatorTrigger;
        [Range(0f, 1f)] public float lookWeight;
        [Range(0f, 1f)] public float handWeight;
        [Range(0f, 1f)] public float footWeight = 0.85f;
        public bool setRootMotion;
        public bool rootMotionEnabled;
        public bool clearRootMotionOnExit;
        public bool clearHandsOnExit = true;

        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<JoeCueBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            var resolver = graph.GetResolver();
            behaviour.lookTarget = lookTarget.Resolve(resolver);
            behaviour.leftHandTarget = leftHandTarget.Resolve(resolver);
            behaviour.rightHandTarget = rightHandTarget.Resolve(resolver);
            behaviour.animatorTrigger = animatorTrigger;
            behaviour.lookWeight = lookWeight;
            behaviour.handWeight = handWeight;
            behaviour.footWeight = footWeight;
            behaviour.setRootMotion = setRootMotion;
            behaviour.rootMotionEnabled = rootMotionEnabled;
            behaviour.clearRootMotionOnExit = clearRootMotionOnExit;
            behaviour.clearHandsOnExit = clearHandsOnExit;
            return playable;
        }
    }

    public sealed class JoeCueBehaviour : PlayableBehaviour
    {
        public Transform lookTarget;
        public Transform leftHandTarget;
        public Transform rightHandTarget;
        public string animatorTrigger;
        public float lookWeight;
        public float handWeight;
        public float footWeight;
        public bool setRootMotion;
        public bool rootMotionEnabled;
        public bool clearRootMotionOnExit;
        public bool clearHandsOnExit;

        private JoeCinematicController controller;
        private bool triggerFired;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            triggerFired = false;
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            controller ??= playerData as JoeCinematicController;
            if (controller == null)
            {
                return;
            }

            if (!triggerFired)
            {
                controller.PlayActionTrigger(animatorTrigger);
                triggerFired = true;
            }

            if (setRootMotion)
            {
                controller.SetRootMotionEnabled(rootMotionEnabled);
            }

            controller.SetLookTarget(lookTarget, lookWeight);
            controller.SetHandTargets(leftHandTarget, rightHandTarget, handWeight);
            controller.SetIkWeights(lookWeight, handWeight, footWeight);
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (clearHandsOnExit && controller != null)
            {
                controller.SetHandTargets(null, null, 0f);
            }

            if (clearRootMotionOnExit && controller != null)
            {
                controller.SetRootMotionEnabled(false);
            }
        }
    }
}
