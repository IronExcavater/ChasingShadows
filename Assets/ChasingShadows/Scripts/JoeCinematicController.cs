using RootMotion;
using RootMotion.FinalIK;
using UnityEngine;

namespace ChasingShadows.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class JoeCinematicController : MonoBehaviour
    {
        [Header("References")]
        public Animator animator;
        public Transform lookTarget;
        public Transform leftHandTarget;
        public Transform rightHandTarget;
        public BipedIK bipedIk;
        public LookAtIK lookAtIk;
        public GrounderBipedIK grounder;

        [Header("Timeline Motion")]
        public bool applyRootMotion;
        public bool projectTimelineMotionToGround = true;
        public LayerMask groundMask = ~0;
        public float groundRayHeight = 1.4f;
        public float groundRayDistance = 3f;
        public float rootHeightOffset = 0f;
        public float maxRootMotionStep = 1.25f;

        [Header("Animator Parameters")]
        public string moveSpeedParameter = "MoveSpeed";
        public string moveForwardParameter = "MoveForward";
        public string moveRightParameter = "MoveRight";
        public string turnSpeedParameter = "TurnSpeed";
        public string groundedParameter = "Grounded";
        public string leanForwardParameter = "LeanForward";
        public string leanRightParameter = "LeanRight";
        [Range(0.01f, 0.5f)] public float animatorDampTime = 0.16f;

        [Header("Lean")]
        public float leanForwardScale = 0.08f;
        public float leanRightScale = 0.035f;
        public float leanSharpness = 8f;

        [Header("Final IK")]
        public bool finalIkEnabled = true;
        [Range(0f, 1f)] public float lookWeight = 0.75f;
        [Range(0f, 1f)] public float handIkWeight = 0f;
        [Range(0f, 1f)] public float footIkWeight = 0.85f;
        public float ikWeightSharpness = 8f;
        public float grounderMaxStep = 0.5f;
        public float grounderHeightOffset = 0.02f;

        private bool finalIkReady;
        private float lookWeightCurrent;
        private float handWeightCurrent;
        private float footWeightCurrent;
        private float leanForward;
        private float leanRight;
        private Vector3 previousPosition;
        private Vector3 previousVelocity;

        private void Reset()
        {
            animator = GetComponent<Animator>();
            bipedIk = GetComponent<BipedIK>();
            lookAtIk = GetComponent<LookAtIK>();
            grounder = GetComponent<GrounderBipedIK>();
        }

        private void Awake()
        {
            CacheReferences();
            EnsureFinalIkComponents();
            previousPosition = transform.position;
            SyncAnimatorRootMotion();
        }

        private void OnEnable()
        {
            CacheReferences();
            EnsureFinalIkComponents();
            previousPosition = transform.position;
            SyncAnimatorRootMotion();
        }

        private void OnValidate()
        {
            groundRayHeight = Mathf.Max(0f, groundRayHeight);
            groundRayDistance = Mathf.Max(0f, groundRayDistance);
            maxRootMotionStep = Mathf.Max(0.01f, maxRootMotionStep);
            grounderMaxStep = Mathf.Max(0.01f, grounderMaxStep);
            ikWeightSharpness = Mathf.Max(0.01f, ikWeightSharpness);
            CacheReferences();
            SyncAnimatorRootMotion();
        }

        private void Update()
        {
            UpdateAnimatorState();
            UpdateFinalIk();
            previousPosition = transform.position;
        }

        private void OnAnimatorMove()
        {
            if (animator == null || !applyRootMotion)
            {
                return;
            }

            var delta = SanitizeRootMotionDelta(animator.deltaPosition);
            var nextPosition = transform.position + delta;
            if (projectTimelineMotionToGround)
            {
                nextPosition = ProjectToGround(nextPosition);
            }

            transform.SetPositionAndRotation(nextPosition, animator.deltaRotation * transform.rotation);
        }

        public void SetTimelinePose(Vector3 position, Quaternion rotation, bool useRotation, bool projectToGround)
        {
            if (projectToGround)
            {
                position = ProjectToGround(position);
            }

            if (useRotation)
            {
                transform.SetPositionAndRotation(position, rotation);
            }
            else
            {
                transform.position = position;
            }
        }

        public void SetRootMotionEnabled(bool enabled)
        {
            applyRootMotion = enabled;
            SyncAnimatorRootMotion();
        }

        public void SetLookTarget(Transform target, float weight)
        {
            lookTarget = target;
            lookWeight = Mathf.Clamp01(weight);
        }

        public void SetHandTargets(Transform leftTarget, Transform rightTarget, float weight)
        {
            leftHandTarget = leftTarget;
            rightHandTarget = rightTarget;
            handIkWeight = Mathf.Clamp01(weight);
        }

        public void SetIkWeights(float look, float hands, float feet)
        {
            lookWeight = Mathf.Clamp01(look);
            handIkWeight = Mathf.Clamp01(hands);
            footIkWeight = Mathf.Clamp01(feet);
        }

        public void PlayActionTrigger(string triggerName)
        {
            FireTrigger(triggerName);
        }

        public void Warp(Vector3 worldPosition)
        {
            SetTimelinePose(worldPosition, transform.rotation, false, projectTimelineMotionToGround);
            previousPosition = transform.position;
            previousVelocity = Vector3.zero;
        }

        public void WarpTo(Transform mark)
        {
            if (mark != null)
            {
                Warp(mark.position);
            }
        }

        public void Stop()
        {
            SetRootMotionEnabled(false);
            SetHandTargets(null, null, 0f);
            SetLookTarget(null, 0f);
        }

        private Vector3 ProjectToGround(Vector3 targetPosition)
        {
            var rayStart = targetPosition + Vector3.up * groundRayHeight;
            if (Physics.Raycast(rayStart, Vector3.down, out var groundHit, groundRayHeight + groundRayDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                targetPosition.y = groundHit.point.y + rootHeightOffset;
            }

            return targetPosition;
        }

        private void UpdateAnimatorState()
        {
            if (animator == null)
            {
                return;
            }

            var velocity = Time.deltaTime > 0f ? (transform.position - previousPosition) / Time.deltaTime : Vector3.zero;
            var localVelocity = transform.InverseTransformDirection(velocity);
            var planarSpeed = new Vector2(localVelocity.x, localVelocity.z).magnitude;
            var accelerationVector = Time.deltaTime > 0f ? (velocity - previousVelocity) / Time.deltaTime : Vector3.zero;
            var localAcceleration = transform.InverseTransformDirection(accelerationVector);

            leanForward = Mathf.Lerp(leanForward, Mathf.Clamp(localAcceleration.z * leanForwardScale, -1f, 1f), 1f - Mathf.Exp(-leanSharpness * Time.deltaTime));
            leanRight = Mathf.Lerp(leanRight, Mathf.Clamp((localAcceleration.x * leanRightScale) + (localVelocity.x * leanRightScale), -1f, 1f), 1f - Mathf.Exp(-leanSharpness * Time.deltaTime));

            SetAnimatorFloat(moveSpeedParameter, planarSpeed);
            SetAnimatorFloat(moveForwardParameter, localVelocity.z);
            SetAnimatorFloat(moveRightParameter, localVelocity.x);

            var turn = velocity.sqrMagnitude > 0.0001f
                ? Vector3.SignedAngle(transform.forward, velocity.normalized, Vector3.up)
                : 0f;
            SetAnimatorFloat(turnSpeedParameter, turn);
            SetAnimatorFloat(leanForwardParameter, leanForward);
            SetAnimatorFloat(leanRightParameter, leanRight);
            SetAnimatorBool(groundedParameter, IsGrounded());

            previousVelocity = velocity;
        }

        private void UpdateFinalIk()
        {
            EnsureFinalIkComponents();

            var targetLook = finalIkEnabled && lookTarget != null ? lookWeight : 0f;
            var targetHands = finalIkEnabled ? handIkWeight : 0f;
            var targetFeet = finalIkEnabled ? footIkWeight : 0f;
            var blend = 1f - Mathf.Exp(-ikWeightSharpness * Time.deltaTime);

            lookWeightCurrent = Mathf.Lerp(lookWeightCurrent, targetLook, blend);
            handWeightCurrent = Mathf.Lerp(handWeightCurrent, targetHands, blend);
            footWeightCurrent = Mathf.Lerp(footWeightCurrent, targetFeet, blend);

            if (bipedIk != null && finalIkReady)
            {
                bipedIk.SetLookAtWeight(0f, 0f, 0f, 0f, 0.5f, 0.7f, 0.5f);
                ApplyBipedHand(AvatarIKGoal.LeftHand, leftHandTarget);
                ApplyBipedHand(AvatarIKGoal.RightHand, rightHandTarget);
                bipedIk.SetIKPositionWeight(AvatarIKGoal.LeftFoot, footWeightCurrent);
                bipedIk.SetIKRotationWeight(AvatarIKGoal.LeftFoot, footWeightCurrent);
                bipedIk.SetIKPositionWeight(AvatarIKGoal.RightFoot, footWeightCurrent);
                bipedIk.SetIKRotationWeight(AvatarIKGoal.RightFoot, footWeightCurrent);
            }

            if (lookAtIk != null && finalIkReady)
            {
                lookAtIk.solver.target = lookTarget;
                lookAtIk.solver.SetLookAtWeight(lookWeightCurrent, 0.25f, 0.85f, 0f, 0.45f, 0.7f, 0.3f);
            }

            if (grounder != null)
            {
                grounder.weight = footWeightCurrent;
                grounder.ik = bipedIk;
                grounder.solver.layers = groundMask;
                grounder.solver.maxStep = grounderMaxStep;
                grounder.solver.heightOffset = grounderHeightOffset;
            }
        }

        private void ApplyBipedHand(AvatarIKGoal goal, Transform target)
        {
            var weight = target != null ? handWeightCurrent : 0f;
            bipedIk.SetIKPositionWeight(goal, weight);
            bipedIk.SetIKRotationWeight(goal, weight);

            if (target == null)
            {
                return;
            }

            bipedIk.SetIKPosition(goal, target.position);
            bipedIk.SetIKRotation(goal, target.rotation);
        }

        private void EnsureFinalIkComponents()
        {
            if (bipedIk == null)
            {
                bipedIk = GetComponent<BipedIK>();
            }

            if (lookAtIk == null)
            {
                lookAtIk = GetComponent<LookAtIK>();
            }

            if (grounder == null)
            {
                grounder = GetComponent<GrounderBipedIK>();
            }

            if (!Application.isPlaying)
            {
                return;
            }

            if (bipedIk == null)
            {
                bipedIk = gameObject.AddComponent<BipedIK>();
            }

            if (lookAtIk == null)
            {
                lookAtIk = gameObject.AddComponent<LookAtIK>();
            }

            if (grounder == null)
            {
                grounder = gameObject.AddComponent<GrounderBipedIK>();
            }

            ConfigureFinalIkReferences();
        }

        private void ConfigureFinalIkReferences()
        {
            if (animator == null || bipedIk == null)
            {
                finalIkReady = false;
                return;
            }

            if (bipedIk.references == null)
            {
                bipedIk.references = new BipedReferences();
            }

            if (bipedIk.references.isEmpty)
            {
                BipedReferences.AutoDetectReferences(ref bipedIk.references, transform, BipedReferences.AutoDetectParams.Default);
                bipedIk.SetToDefaults();
            }

            var setupError = string.Empty;
            finalIkReady = !BipedReferences.SetupError(bipedIk.references, ref setupError);
            if (!finalIkReady)
            {
                return;
            }

            if (grounder != null)
            {
                grounder.ik = bipedIk;
                grounder.solver.layers = groundMask;
                grounder.solver.maxStep = grounderMaxStep;
                grounder.solver.heightOffset = grounderHeightOffset;
            }

            if (lookAtIk != null)
            {
                lookAtIk.solver.SetChain(bipedIk.references.spine, bipedIk.references.head, bipedIk.references.eyes, transform);
            }
        }

        private bool IsGrounded()
        {
            return Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, 0.45f + Mathf.Abs(rootHeightOffset), groundMask, QueryTriggerInteraction.Ignore);
        }

        private Vector3 SanitizeRootMotionDelta(Vector3 delta)
        {
            if (float.IsNaN(delta.x) || float.IsNaN(delta.y) || float.IsNaN(delta.z))
            {
                return Vector3.zero;
            }

            delta.y = 0f;
            return delta.sqrMagnitude > maxRootMotionStep * maxRootMotionStep ? delta.normalized * maxRootMotionStep : delta;
        }

        private void CacheReferences()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }

        private void SyncAnimatorRootMotion()
        {
            if (animator != null)
            {
                animator.applyRootMotion = applyRootMotion;
            }
        }

        private void FireTrigger(string triggerName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(triggerName) ||
                !HasAnimatorParameter(triggerName, AnimatorControllerParameterType.Trigger))
            {
                return;
            }

            animator.ResetTrigger(triggerName);
            animator.SetTrigger(triggerName);
        }

        private void SetAnimatorFloat(string parameterName, float value)
        {
            if (!string.IsNullOrWhiteSpace(parameterName) &&
                HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Float))
            {
                animator.SetFloat(parameterName, value, animatorDampTime, Time.deltaTime);
            }
        }

        private void SetAnimatorBool(string parameterName, bool value)
        {
            if (!string.IsNullOrWhiteSpace(parameterName) &&
                HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool))
            {
                animator.SetBool(parameterName, value);
            }
        }

        private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType type)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return false;
            }

            foreach (var parameter in animator.parameters)
            {
                if (parameter.type == type && parameter.name == parameterName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
