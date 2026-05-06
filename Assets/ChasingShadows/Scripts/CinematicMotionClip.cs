using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ChasingShadows.Characters
{
    [Serializable]
    public struct CinematicMotionKnot
    {
        public Vector3 position;
        public Vector3 inTangent;
        public Vector3 outTangent;
        public Vector3 euler;

        public CinematicMotionKnot(Vector3 position, Vector3 euler)
        {
            this.position = position;
            this.euler = euler;
            inTangent = Vector3.zero;
            outTangent = Vector3.zero;
        }
    }

    public sealed class CinematicMotionClip : PlayableAsset, ITimelineClipAsset
    {
        [Header("Spline Motion")]
        public CinematicMotionKnot[] knots =
        {
            new(Vector3.zero, Vector3.zero),
            new(new Vector3(0f, 0f, 5f), Vector3.zero)
        };

        public bool faceAlongSpline = true;
        public Vector3 rotationOffset;
        public Vector3 worldUp = Vector3.up;

        [Header("Output")]
        public bool applyPosition = true;
        public bool applyRotation = true;
        public AnimationCurve positionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public AnimationCurve rotationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

        private void OnValidate()
        {
            if (knots == null || knots.Length == 0)
            {
                knots = new[]
                {
                    new CinematicMotionKnot(Vector3.zero, Vector3.zero),
                    new CinematicMotionKnot(new Vector3(0f, 0f, 5f), Vector3.zero)
                };
            }
        }

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<CinematicMotionBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.knots = knots;
            behaviour.faceAlongSpline = faceAlongSpline;
            behaviour.rotationOffset = Quaternion.Euler(rotationOffset);
            behaviour.worldUp = worldUp.sqrMagnitude > 0.0001f ? worldUp.normalized : Vector3.up;
            behaviour.applyPosition = applyPosition;
            behaviour.applyRotation = applyRotation;
            behaviour.positionCurve = positionCurve;
            behaviour.rotationCurve = rotationCurve;
            return playable;
        }
    }

    public sealed class CinematicMotionBehaviour : PlayableBehaviour
    {
        public CinematicMotionKnot[] knots;
        public bool faceAlongSpline;
        public Quaternion rotationOffset;
        public Vector3 worldUp;
        public bool applyPosition;
        public bool applyRotation;
        public AnimationCurve positionCurve;
        public AnimationCurve rotationCurve;

        public void Evaluate(Transform target, double localTime, double duration, out Vector3 position, out Quaternion rotation)
        {
            var normalized = duration > 0d ? Mathf.Clamp01((float)(localTime / duration)) : 1f;
            var positionT = positionCurve != null ? Mathf.Clamp01(positionCurve.Evaluate(normalized)) : normalized;
            var rotationT = rotationCurve != null ? Mathf.Clamp01(rotationCurve.Evaluate(normalized)) : normalized;

            EvaluateSpline(positionT, rotationT, out position, out rotation);
        }

        private void EvaluateSpline(float positionT, float rotationT, out Vector3 position, out Quaternion rotation)
        {
            var source = knots;
            if (source == null || source.Length == 0)
            {
                source = new[]
                {
                    new CinematicMotionKnot(Vector3.zero, Vector3.zero),
                    new CinematicMotionKnot(new Vector3(0f, 0f, 5f), Vector3.zero)
                };
            }

            if (source.Length == 1)
            {
                position = source[0].position;
                rotation = Quaternion.Euler(source[0].euler) * rotationOffset;
                return;
            }

            var segmentCount = source.Length - 1;
            var scaledT = Mathf.Clamp01(positionT) * segmentCount;
            var segment = Mathf.Min(Mathf.FloorToInt(scaledT), segmentCount - 1);
            var segmentT = scaledT - segment;
            var current = source[segment];
            var next = source[segment + 1];

            var p0 = current.position;
            var p1 = current.position + current.outTangent;
            var p2 = next.position + next.inTangent;
            var p3 = next.position;
            position = CubicBezier(p0, p1, p2, p3, segmentT);

            var knotRotation = Quaternion.SlerpUnclamped(Quaternion.Euler(current.euler), Quaternion.Euler(next.euler), SegmentRotationT(rotationT, segment, segmentCount));
            if (faceAlongSpline)
            {
                var tangent = CubicBezierDerivative(p0, p1, p2, p3, segmentT);
                rotation = tangent.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(tangent.normalized, worldUp) * rotationOffset
                    : knotRotation * rotationOffset;
            }
            else
            {
                rotation = knotRotation * rotationOffset;
            }
        }

        private static float SegmentRotationT(float rotationT, int segment, int segmentCount)
        {
            if (segmentCount <= 1)
            {
                return Mathf.Clamp01(rotationT);
            }

            var scaledT = Mathf.Clamp01(rotationT) * segmentCount;
            return Mathf.Clamp01(scaledT - segment);
        }

        private static Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            var oneMinusT = 1f - t;
            return (oneMinusT * oneMinusT * oneMinusT * p0)
                   + (3f * oneMinusT * oneMinusT * t * p1)
                   + (3f * oneMinusT * t * t * p2)
                   + (t * t * t * p3);
        }

        private static Vector3 CubicBezierDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            var oneMinusT = 1f - t;
            return (3f * oneMinusT * oneMinusT * (p1 - p0))
                   + (6f * oneMinusT * t * (p2 - p1))
                   + (3f * t * t * (p3 - p2));
        }
    }
}
