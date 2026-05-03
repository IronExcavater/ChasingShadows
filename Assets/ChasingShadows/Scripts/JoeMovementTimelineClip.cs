using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Splines;
using UnityEngine.Timeline;

namespace ChasingShadows.Characters
{
    public sealed class JoeMovementTimelineClip : PlayableAsset, ITimelineClipAsset
    {
        public ExposedReference<JoeCinematicController> controller;
        public ExposedReference<SplineContainer> spline;
        public ExposedReference<Transform> navDestination;
        public JoeMovementProfile profile;
        public JoeCinematicController.MotionDriver driver = JoeCinematicController.MotionDriver.NavMesh;
        public bool useClipTimeForSpline = true;
        [Range(0f, 1f)] public float startSplineT = 0f;
        [Range(0f, 1f)] public float endSplineT = 1f;
        public bool restoreNavMeshDriverOnExit = true;

        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation | ClipCaps.SpeedMultiplier;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<JoeMovementTimelineBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.controller = controller.Resolve(graph.GetResolver());
            behaviour.spline = spline.Resolve(graph.GetResolver());
            behaviour.navDestination = navDestination.Resolve(graph.GetResolver());
            behaviour.profile = profile;
            behaviour.driver = driver;
            behaviour.useClipTimeForSpline = useClipTimeForSpline;
            behaviour.startSplineT = startSplineT;
            behaviour.endSplineT = endSplineT;
            behaviour.restoreNavMeshDriverOnExit = restoreNavMeshDriverOnExit;
            return playable;
        }
    }

    public sealed class JoeMovementTimelineBehaviour : PlayableBehaviour
    {
        public JoeCinematicController controller;
        public SplineContainer spline;
        public Transform navDestination;
        public JoeMovementProfile profile;
        public JoeCinematicController.MotionDriver driver;
        public bool useClipTimeForSpline = true;
        public float startSplineT;
        public float endSplineT = 1f;
        public bool restoreNavMeshDriverOnExit = true;

        private bool initialized;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            initialized = false;
            ApplyInitialState();
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (controller == null)
            {
                controller = playerData as JoeCinematicController;
            }

            if (controller == null)
            {
                return;
            }

            if (!initialized)
            {
                ApplyInitialState();
            }

            if (driver != JoeCinematicController.MotionDriver.Spline || spline == null || !useClipTimeForSpline)
            {
                return;
            }

            var duration = playable.GetDuration();
            var normalized = duration > 0.0001d ? Mathf.Clamp01((float)(playable.GetTime() / duration)) : 1f;
            controller.SetSplineNormalized(Mathf.Lerp(startSplineT, endSplineT, normalized));
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (restoreNavMeshDriverOnExit && controller != null && driver != JoeCinematicController.MotionDriver.NavMesh)
            {
                controller.SetMotionDriver(JoeCinematicController.MotionDriver.NavMesh);
            }
        }

        private void ApplyInitialState()
        {
            if (controller == null)
            {
                return;
            }

            controller.ApplyProfile(profile);

            if (driver == JoeCinematicController.MotionDriver.Spline && spline != null)
            {
                controller.trackedSpline = spline;
                // When clip time drives the spline, use External so TickSpline never
                // auto-advances splineT and fights ProcessFrame's SetSplineNormalized.
                var effectiveDriver = useClipTimeForSpline
                    ? JoeCinematicController.MotionDriver.External
                    : JoeCinematicController.MotionDriver.Spline;
                controller.SetMotionDriver(effectiveDriver);
                controller.SetSplineNormalized(startSplineT);
            }
            else
            {
                controller.SetMotionDriver(driver);
                if (driver == JoeCinematicController.MotionDriver.NavMesh && navDestination != null)
                {
                    controller.SetNavDestination(navDestination);
                }
            }

            initialized = true;
        }
    }
}
