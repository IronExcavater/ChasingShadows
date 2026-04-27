using UnityEngine;
using UnityEngine.Splines;

namespace ChasingShadows.Characters
{
    public sealed class JoeMovementShowcase : MonoBehaviour
    {
        public enum ShowcasePhase
        {
            NavMesh,
            Spline,
            RootMotion,
            Ik
        }

        public JoeCinematicController controller;
        public JoeMovementProfile profile;
        public SplineContainer spline;
        public Transform[] navMarks;
        public Transform lookTarget;
        public Transform leftHandTarget;
        public Transform rightHandTarget;
        public float navLegSeconds = 4f;
        public float splineLegSeconds = 7f;
        public float rootMotionBeatSeconds = 2f;
        public float ikHoldSeconds = 3f;
        public string rootMotionTrigger = "";
        public bool playOnStart = true;

        [SerializeField] private ShowcasePhase phase;
        private int navIndex;
        private float phaseTime;

        private void Start()
        {
            if (playOnStart)
            {
                ApplyPhase(ShowcasePhase.NavMesh);
            }
        }

        private void Update()
        {
            if (controller == null)
            {
                return;
            }

            phaseTime += Time.deltaTime;
            switch (phase)
            {
                case ShowcasePhase.NavMesh:
                    if (phaseTime >= navLegSeconds)
                    {
                        navIndex = (navIndex + 1) % Mathf.Max(1, navMarks != null ? navMarks.Length : 0);
                        ApplyPhase(ShowcasePhase.Spline);
                    }
                    break;
                case ShowcasePhase.Spline:
                    if (phaseTime >= splineLegSeconds)
                    {
                        ApplyPhase(ShowcasePhase.RootMotion);
                    }
                    break;
                case ShowcasePhase.RootMotion:
                    if (phaseTime >= rootMotionBeatSeconds)
                    {
                        ApplyPhase(ShowcasePhase.Ik);
                    }
                    break;
                case ShowcasePhase.Ik:
                    controller.handIkWeight = Mathf.PingPong(phaseTime, 1f);
                    if (phaseTime >= ikHoldSeconds)
                    {
                        ApplyPhase(ShowcasePhase.NavMesh);
                    }
                    break;
            }
        }

        public void PlayNavMeshPhase()
        {
            ApplyPhase(ShowcasePhase.NavMesh);
        }

        public void PlaySplinePhase()
        {
            ApplyPhase(ShowcasePhase.Spline);
        }

        public void PlayRootMotionPhase()
        {
            ApplyPhase(ShowcasePhase.RootMotion);
        }

        public void PlayIkPhase()
        {
            ApplyPhase(ShowcasePhase.Ik);
        }

        public void ApplyPhase(ShowcasePhase nextPhase)
        {
            phase = nextPhase;
            phaseTime = 0f;

            controller.ApplyProfile(profile);
            controller.SetLookTarget(lookTarget, profile != null ? profile.lookWeight : controller.lookWeight);
            controller.SetHandTargets(leftHandTarget, rightHandTarget, 0f);

            switch (phase)
            {
                case ShowcasePhase.NavMesh:
                    controller.SetNavDestination(GetNavMarkPosition());
                    break;
                case ShowcasePhase.Spline:
                    if (spline != null)
                    {
                        controller.FollowSpline(spline, 0f);
                    }
                    break;
                case ShowcasePhase.RootMotion:
                    controller.PlayRootMotionBeat(rootMotionTrigger, rootMotionBeatSeconds);
                    break;
                case ShowcasePhase.Ik:
                    controller.SetMotionDriver(JoeCinematicController.MotionDriver.External);
                    controller.SetHandTargets(leftHandTarget, rightHandTarget, 1f);
                    break;
            }
        }

        private Vector3 GetNavMarkPosition()
        {
            if (navMarks == null || navMarks.Length == 0 || navMarks[navIndex] == null)
            {
                return controller.transform.position;
            }

            return navMarks[navIndex].position;
        }
    }
}
