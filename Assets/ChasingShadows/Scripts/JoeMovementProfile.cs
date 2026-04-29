using UnityEngine;

namespace ChasingShadows.Characters
{
    [CreateAssetMenu(menuName = "Chasing Shadows/Joe Movement Profile", fileName = "JoeMovementProfile")]
    public sealed class JoeMovementProfile : ScriptableObject
    {
        [Header("Movement")]
        public JoeCinematicController.MotionDriver driver = JoeCinematicController.MotionDriver.NavMesh;
        public float maxSpeed = 2.8f;
        public float acceleration = 6f;
        public float deceleration = 5f;
        public float rotationSharpness = 12f;
        public float stoppingDistance = 0.12f;
        public float navMeshSampleRadius = 1.5f;
        [Range(0.05f, 2f)] public float splineSpeed = 1f;

        [Header("Ground Projection")]
        public bool projectToNavMesh = true;
        public JoeCinematicController.NavMeshProjection splineNavMeshProjection = JoeCinematicController.NavMeshProjection.WhenAvailable;
        public LayerMask groundMask = ~0;
        public float groundRayHeight = 1.4f;
        public float groundRayDistance = 3f;
        public float rootHeightOffset = 0f;
        public bool holdFinalSplinePose = true;

        [Header("Lean")]
        public float leanForwardScale = 0.08f;
        public float leanRightScale = 0.035f;
        public float leanSharpness = 8f;

        [Header("IK")]
        public bool finalIkEnabled = true;
        [Range(0f, 1f)] public float lookWeight = 0.75f;
        [Range(0f, 1f)] public float handIkWeight = 0f;
        [Range(0f, 1f)] public float footIkWeight = 0.85f;
        public float ikWeightSharpness = 8f;
        public float grounderMaxStep = 0.5f;
        public float grounderHeightOffset = 0.02f;

        public void ApplyTo(JoeCinematicController controller)
        {
            if (controller == null)
            {
                return;
            }

            controller.driver = driver;
            controller.maxSpeed = maxSpeed;
            controller.acceleration = acceleration;
            controller.deceleration = deceleration;
            controller.rotationSharpness = rotationSharpness;
            controller.stoppingDistance = stoppingDistance;
            controller.navMeshSampleRadius = navMeshSampleRadius;
            controller.splineSpeed = splineSpeed;

            controller.projectToNavMesh = projectToNavMesh;
            controller.splineNavMeshProjection = splineNavMeshProjection;
            controller.groundMask = groundMask;
            controller.groundRayHeight = groundRayHeight;
            controller.groundRayDistance = groundRayDistance;
            controller.rootHeightOffset = rootHeightOffset;
            controller.holdFinalSplinePose = holdFinalSplinePose;

            controller.leanForwardScale = leanForwardScale;
            controller.leanRightScale = leanRightScale;
            controller.leanSharpness = leanSharpness;

            controller.finalIkEnabled = finalIkEnabled;
            controller.lookWeight = lookWeight;
            controller.handIkWeight = handIkWeight;
            controller.footIkWeight = footIkWeight;
            controller.ikWeightSharpness = ikWeightSharpness;
            controller.grounderMaxStep = grounderMaxStep;
            controller.grounderHeightOffset = grounderHeightOffset;

            controller.RefreshConfiguration();
        }
    }
}
