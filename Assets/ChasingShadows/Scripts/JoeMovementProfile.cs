using UnityEngine;

namespace ChasingShadows.Characters
{
    [CreateAssetMenu(menuName = "Chasing Shadows/Joe Movement Profile", fileName = "JoeMovementProfile")]
    public sealed class JoeMovementProfile : ScriptableObject
    {
        [Header("Movement")]
        public JoeCinematicController.MotionDriver driver = JoeCinematicController.MotionDriver.NavMesh;
        public float maxSpeed = 2.8f;
        public float acceleration = 8f;
        public float rotationSharpness = 12f;
        public float navMeshSampleRadius = 1.5f;
        [Range(0.05f, 2f)] public float splineSpeed = 1f;

        [Header("System Ownership")]
        public bool navMeshEnabled = true;
        public bool rootMotionEnabled = true;
        public bool ikEnabled = true;
        public bool advancedLocomotionEnabled = true;
        public bool rootMotionLocomotion = true;
        public bool allowRootMotionOnSpline = false;
        public bool syncAgentEveryFrame = true;
        public JoeCinematicController.NavMeshProjection splineNavMeshProjection = JoeCinematicController.NavMeshProjection.WhenAvailable;
        public bool holdFinalSplinePose = true;

        [Header("IK")]
        [Range(0f, 1f)] public float lookWeight = 0.75f;
        [Range(0f, 1f)] public float handIkWeight = 0f;
        [Range(0f, 1f)] public float footIkWeight = 0.75f;
        public LayerMask footIkMask = ~0;
        public float footRayHeight = 0.55f;
        public float footRayDistance = 1.2f;
        public float footOffset = 0.03f;

        public void ApplyTo(JoeCinematicController controller)
        {
            if (controller == null)
            {
                return;
            }

            controller.driver = driver;
            controller.maxSpeed = maxSpeed;
            controller.acceleration = acceleration;
            controller.rotationSharpness = rotationSharpness;
            controller.navMeshSampleRadius = navMeshSampleRadius;
            controller.splineSpeed = splineSpeed;

            controller.navMeshEnabled = navMeshEnabled;
            controller.rootMotionEnabled = rootMotionEnabled;
            controller.ikEnabled = ikEnabled;
            controller.advancedLocomotionEnabled = advancedLocomotionEnabled;
            controller.rootMotionLocomotion = rootMotionLocomotion;
            controller.allowRootMotionOnSpline = allowRootMotionOnSpline;
            controller.syncAgentEveryFrame = syncAgentEveryFrame;
            controller.splineNavMeshProjection = splineNavMeshProjection;
            controller.holdFinalSplinePose = holdFinalSplinePose;

            controller.lookWeight = lookWeight;
            controller.handIkWeight = handIkWeight;
            controller.footIkWeight = footIkWeight;
            controller.footIkMask = footIkMask;
            controller.footRayHeight = footRayHeight;
            controller.footRayDistance = footRayDistance;
            controller.footOffset = footOffset;

            controller.RefreshConfiguration();
        }
    }
}
