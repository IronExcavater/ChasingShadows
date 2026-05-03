using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Splines;
using UnityEngine.Timeline;

namespace ChasingShadows.Characters
{
    public enum JoeTimelineMotionMode
    {
        Hold,
        MoveTo,
        Spline
    }

    public sealed class JoeMovementTimelineClip : PlayableAsset, ITimelineClipAsset
    {
        public ExposedReference<JoeCinematicController> controller;
        public JoeTimelineMotionMode mode = JoeTimelineMotionMode.MoveTo;
        public ExposedReference<Transform> start;
        public ExposedReference<Transform> end;
        public ExposedReference<SplineContainer> spline;
        [Range(0f, 1f)] public float startSplineT = 0f;
        [Range(0f, 1f)] public float endSplineT = 1f;
        public bool faceMotion = true;
        public bool projectToGround = true;
        public bool smoothStep = true;

        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation | ClipCaps.SpeedMultiplier;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<JoeMovementTimelineBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            var resolver = graph.GetResolver();
            behaviour.controller = controller.Resolve(resolver);
            behaviour.mode = mode;
            behaviour.start = start.Resolve(resolver);
            behaviour.end = end.Resolve(resolver);
            behaviour.spline = spline.Resolve(resolver);
            behaviour.startSplineT = startSplineT;
            behaviour.endSplineT = endSplineT;
            behaviour.faceMotion = faceMotion;
            behaviour.projectToGround = projectToGround;
            behaviour.smoothStep = smoothStep;
            return playable;
        }
    }

    public sealed class JoeMovementTimelineBehaviour : PlayableBehaviour
    {
        public JoeCinematicController controller;
        public JoeTimelineMotionMode mode;
        public Transform start;
        public Transform end;
        public SplineContainer spline;
        public float startSplineT;
        public float endSplineT = 1f;
        public bool faceMotion = true;
        public bool projectToGround = true;
        public bool smoothStep = true;

        private bool initialized;
        private Vector3 fallbackStartPosition;
        private Quaternion fallbackStartRotation;
        private Vector3 previousPosition;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            initialized = false;
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            controller ??= playerData as JoeCinematicController;
            if (controller == null)
            {
                return;
            }

            if (!initialized)
            {
                fallbackStartPosition = start != null ? start.position : controller.transform.position;
                fallbackStartRotation = start != null ? start.rotation : controller.transform.rotation;
                previousPosition = fallbackStartPosition;
                initialized = true;
            }

            if (mode == JoeTimelineMotionMode.Hold)
            {
                return;
            }

            var duration = playable.GetDuration();
            var t = duration > 0.0001d ? Mathf.Clamp01((float)(playable.GetTime() / duration)) : 1f;
            if (smoothStep)
            {
                t = t * t * (3f - (2f * t));
            }

            var position = fallbackStartPosition;
            var rotation = fallbackStartRotation;
            var useRotation = false;

            if (mode == JoeTimelineMotionMode.MoveTo)
            {
                var from = start != null ? start.position : fallbackStartPosition;
                var to = end != null ? end.position : fallbackStartPosition;
                position = Vector3.LerpUnclamped(from, to, t);
                rotation = ResolveMotionRotation(position, previousPosition, start != null ? start.rotation : fallbackStartRotation);
                useRotation = faceMotion;
            }
            else if (mode == JoeTimelineMotionMode.Spline && spline != null)
            {
                var splineT = Mathf.Lerp(startSplineT, endSplineT, t);
                EvaluateSplinePose(spline, splineT, out position, out rotation);
                useRotation = faceMotion;
            }

            controller.SetTimelinePose(position, rotation, useRotation, projectToGround);
            previousPosition = position;
        }

        private static Quaternion ResolveMotionRotation(Vector3 position, Vector3 previousPosition, Quaternion fallback)
        {
            var direction = position - previousPosition;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : fallback;
        }

        private static void EvaluateSplinePose(SplineContainer spline, float normalizedTime, out Vector3 position, out Quaternion rotation)
        {
            var rawPosition = spline.EvaluatePosition(normalizedTime);
            var rawTangent = spline.EvaluateTangent(normalizedTime);
            position = new Vector3(rawPosition.x, rawPosition.y, rawPosition.z);

            var tangent = new Vector3(rawTangent.x, rawTangent.y, rawTangent.z);
            tangent.y = 0f;
            rotation = tangent.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(tangent.normalized, Vector3.up)
                : Quaternion.identity;
        }
    }
}
