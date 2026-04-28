using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;
using UnityEngine.Splines;

namespace ChasingShadows.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class JoeCinematicController : MonoBehaviour
    {
        public enum MotionDriver
        {
            NavMesh,
            RootMotion,
            Spline,
            External
        }

        public enum NavMeshProjection
        {
            Disabled,
            WhenAvailable,
            Required
        }

        [Header("References")]
        public Animator animator;
        public NavMeshAgent agent;
        public Transform lookTarget;
        public Transform leftHandTarget;
        public Transform rightHandTarget;
        public Transform cameraTarget;
        public SplineContainer trackedSpline;

        [Header("Movement")]
        public MotionDriver driver = MotionDriver.NavMesh;
        public float maxSpeed = 2.8f;
        public float acceleration = 8f;
        public float rotationSharpness = 12f;
        public bool rootMotionLocomotion = false;
        public float navMeshSampleRadius = 1.5f;
        [Range(0.05f, 2f)] public float splineSpeed = 1f;

        [Header("System Ownership")]
        public bool navMeshEnabled = false;
        public bool rootMotionEnabled = true;
        public bool ikEnabled = true;
        public bool advancedLocomotionEnabled = true;
        public bool allowRootMotionOnSpline = false;
        public bool syncAgentEveryFrame = false;
        public NavMeshProjection splineNavMeshProjection = NavMeshProjection.WhenAvailable;
        public bool holdFinalSplinePose = true;

        [Header("Animator Parameters")]
        public string moveSpeedParameter = "MoveSpeed";
        public string moveForwardParameter = "MoveForward";
        public string moveRightParameter = "MoveRight";
        public string turnSpeedParameter = "TurnSpeed";
        public string groundedParameter = "Grounded";

        [Header("IK")]
        [Range(0f, 1f)] public float lookWeight = 0.75f;
        [Range(0f, 1f)] public float handIkWeight = 0f;
        [Range(0f, 1f)] public float footIkWeight = 0.75f;
        public LayerMask footIkMask = ~0;
        public float footRayHeight = 0.55f;
        public float footRayDistance = 1.2f;
        public float footOffset = 0.03f;

        private bool hasDestination;
        private float splineT;
        private float currentSpeed;
        private float rootMotionSecondsRemaining;
        private Vector3 destination;
        private Vector3 previousPosition;
        private Vector3 lastSplinePosition;
        private Quaternion lastSplineRotation = Quaternion.identity;
        private bool hasSplinePose;

        private void Reset()
        {
            animator = GetComponent<Animator>();
            agent = GetComponent<NavMeshAgent>();
        }

        private void Awake()
        {
            CacheReferences();
            ConfigureAgent();
            previousPosition = transform.position;
            destination = transform.position;
        }

        private void Start()
        {
            var rigBuilder = GetComponent<RigBuilder>();
            if (rigBuilder != null)
            {
                rigBuilder.Build();
            }
        }

        private void OnEnable()
        {
            CacheReferences();
            ConfigureAgent();
        }

        private void OnValidate()
        {
            maxSpeed = Mathf.Max(0f, maxSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            rotationSharpness = Mathf.Max(0f, rotationSharpness);
            navMeshSampleRadius = Mathf.Max(0.01f, navMeshSampleRadius);
            footRayHeight = Mathf.Max(0f, footRayHeight);
            footRayDistance = Mathf.Max(0f, footRayDistance);
            CacheReferences();
            ConfigureAgent();
        }

        private void Update()
        {
            switch (driver)
            {
                case MotionDriver.NavMesh:
                    TickDestinationMove();
                    break;
                case MotionDriver.RootMotion:
                    TickRootMotionBeat();
                    break;
                case MotionDriver.Spline:
                    TickSplineMove();
                    break;
                case MotionDriver.External:
                    currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, acceleration * Time.deltaTime);
                    break;
            }

            UpdateAnimatorVelocity();
            SyncAgentToTransform();
            previousPosition = transform.position;
        }

        private void OnAnimatorMove()
        {
            if (animator == null || !rootMotionEnabled || !animator.applyRootMotion)
            {
                return;
            }

            if (driver != MotionDriver.RootMotion || rootMotionSecondsRemaining <= 0f)
            {
                return;
            }

            var delta = SanitizePlanarDelta(animator.deltaPosition);
            MoveTo(transform.position + delta, NavMeshProjection.WhenAvailable);
            transform.rotation *= animator.deltaRotation;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || !ikEnabled)
            {
                return;
            }

            ApplyLookIk();
            ApplyHandIk(AvatarIKGoal.LeftHand, leftHandTarget);
            ApplyHandIk(AvatarIKGoal.RightHand, rightHandTarget);
            ApplyFootIk(AvatarIKGoal.LeftFoot);
            ApplyFootIk(AvatarIKGoal.RightFoot);
        }

        public void SetNavDestination(Vector3 worldPosition)
        {
            driver = MotionDriver.NavMesh;
            destination = worldPosition;
            hasDestination = true;
            rootMotionSecondsRemaining = 0f;
        }

        public void SetNavDestination(Transform target)
        {
            if (target != null)
            {
                SetNavDestination(target.position);
            }
        }

        public void SetNavTarget(Transform mark)
        {
            SetNavDestination(mark);
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

        public void FollowSpline(SplineContainer spline, float startT = 0f)
        {
            trackedSpline = spline;
            SetSplineNormalized(startT);
            driver = MotionDriver.Spline;
            hasDestination = false;
        }

        public void SetSplineNormalized(float normalizedTime, bool evaluateImmediately = true)
        {
            splineT = Mathf.Clamp01(normalizedTime);
            if (evaluateImmediately && trackedSpline != null)
            {
                ApplySplinePose(splineT, true);
            }
        }

        public void SetMotionDriver(MotionDriver motionDriver)
        {
            driver = motionDriver;
            if (motionDriver != MotionDriver.NavMesh)
            {
                hasDestination = false;
            }
        }

        public void ConfigureSystems(bool useNavMesh, bool useRootMotion, bool useIk, bool useAdvancedLocomotion)
        {
            navMeshEnabled = useNavMesh;
            rootMotionEnabled = useRootMotion;
            ikEnabled = useIk;
            advancedLocomotionEnabled = useAdvancedLocomotion;
            ConfigureAgent();
        }

        public void RefreshConfiguration()
        {
            ConfigureAgent();
            SyncAgentToTransform();
        }

        public void SetNavMeshEnabled(bool enabled)
        {
            navMeshEnabled = enabled;
            ConfigureAgent();
        }

        public void SetRootMotionEnabled(bool enabled)
        {
            rootMotionEnabled = enabled;
        }

        public void SetIkEnabled(bool enabled)
        {
            ikEnabled = enabled;
        }

        public void SetAdvancedLocomotionEnabled(bool enabled)
        {
            advancedLocomotionEnabled = enabled;
        }

        public void SetSplineProjection(NavMeshProjection projection)
        {
            splineNavMeshProjection = projection;
        }

        public void ApplyProfile(JoeMovementProfile profile)
        {
            if (profile != null)
            {
                profile.ApplyTo(this);
            }
        }

        public void PlayRootMotionBeat(string triggerName, float durationSeconds)
        {
            driver = MotionDriver.RootMotion;
            hasDestination = false;
            rootMotionSecondsRemaining = Mathf.Max(0.01f, durationSeconds);
            FireTrigger(triggerName);
        }

        public void PlayBeat(string triggerName, MotionDriver motionDriver, float durationSeconds = 1f)
        {
            if (motionDriver == MotionDriver.RootMotion)
            {
                PlayRootMotionBeat(triggerName, durationSeconds);
                return;
            }

            SetMotionDriver(motionDriver);
            FireTrigger(triggerName);
        }

        public void Warp(Vector3 worldPosition)
        {
            transform.position = worldPosition;
            destination = worldPosition;
            hasDestination = false;
            SyncAgentToTransform(true);
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
            currentSpeed = 0f;
            rootMotionSecondsRemaining = 0f;
            hasDestination = false;
            driver = MotionDriver.External;
            UpdateAnimatorVelocity();
        }

        private void TickDestinationMove()
        {
            if (!hasDestination)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, acceleration * Time.deltaTime);
                return;
            }

            var toTarget = destination - transform.position;
            toTarget.y = 0f;
            var distance = toTarget.magnitude;
            if (distance <= 0.03f)
            {
                hasDestination = false;
                currentSpeed = 0f;
                return;
            }

            currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.deltaTime);
            var step = Mathf.Min(distance, currentSpeed * Time.deltaTime);
            var direction = toTarget / distance;
            MoveTo(transform.position + direction * step, NavMeshProjection.WhenAvailable);
            RotateToward(direction);
        }

        private void TickRootMotionBeat()
        {
            rootMotionSecondsRemaining -= Time.deltaTime;
            if (rootMotionSecondsRemaining <= 0f)
            {
                rootMotionSecondsRemaining = 0f;
                driver = MotionDriver.External;
            }
        }

        private void TickSplineMove()
        {
            if (trackedSpline == null)
            {
                driver = MotionDriver.External;
                return;
            }

            var length = Mathf.Max(0.01f, trackedSpline.CalculateLength());
            splineT = Mathf.Clamp01(splineT + (splineSpeed / length) * Time.deltaTime);
            ApplySplinePose(splineT, false);

            if (splineT >= 1f)
            {
                driver = holdFinalSplinePose ? MotionDriver.External : MotionDriver.NavMesh;
            }
        }

        private void ApplySplinePose(float normalizedTime, bool snapRotation)
        {
            EvaluateSplinePose(normalizedTime, out lastSplinePosition, out lastSplineRotation);
            hasSplinePose = true;
            MoveTo(lastSplinePosition, splineNavMeshProjection);
            transform.rotation = snapRotation
                ? lastSplineRotation
                : Quaternion.Slerp(
                    transform.rotation,
                    lastSplineRotation,
                    1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
        }

        private void MoveTo(Vector3 targetPosition, NavMeshProjection projection)
        {
            if (navMeshEnabled && projection != NavMeshProjection.Disabled
                && NavMesh.SamplePosition(targetPosition, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                return;
            }

            if (navMeshEnabled && projection == NavMeshProjection.Required)
            {
                return;
            }

            transform.position = targetPosition;
        }

        private void EvaluateSplinePose(float normalizedTime, out Vector3 position, out Quaternion rotation)
        {
            var rawPosition = trackedSpline.EvaluatePosition(normalizedTime);
            var rawTangent = trackedSpline.EvaluateTangent(normalizedTime);
            position = new Vector3(rawPosition.x, rawPosition.y, rawPosition.z);

            var tangent = new Vector3(rawTangent.x, rawTangent.y, rawTangent.z);
            tangent.y = 0f;
            rotation = tangent.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(tangent.normalized, Vector3.up)
                : transform.rotation;
        }

        private void RotateToward(Vector3 worldDirection)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
        }

        private void UpdateAnimatorVelocity()
        {
            if (animator == null)
            {
                return;
            }

            var velocity = Time.deltaTime > 0f ? (transform.position - previousPosition) / Time.deltaTime : Vector3.zero;
            var localVelocity = transform.InverseTransformDirection(velocity);
            var planarSpeed = new Vector2(localVelocity.x, localVelocity.z).magnitude;

            currentSpeed = driver == MotionDriver.External
                ? Mathf.MoveTowards(currentSpeed, planarSpeed, acceleration * Time.deltaTime)
                : Mathf.Max(currentSpeed, planarSpeed);

            SetAnimatorFloat(moveSpeedParameter, planarSpeed);
            SetAnimatorFloat(moveForwardParameter, advancedLocomotionEnabled ? localVelocity.z : planarSpeed);
            SetAnimatorFloat(moveRightParameter, advancedLocomotionEnabled ? localVelocity.x : 0f);

            var turn = velocity.sqrMagnitude > 0.0001f
                ? Vector3.SignedAngle(transform.forward, velocity.normalized, Vector3.up)
                : 0f;
            SetAnimatorFloat(turnSpeedParameter, turn);
            SetAnimatorBool(groundedParameter, IsGrounded());
        }

        private void ApplyLookIk()
        {
            if (lookTarget == null || lookWeight <= 0f)
            {
                animator.SetLookAtWeight(0f);
                return;
            }

            animator.SetLookAtWeight(lookWeight, 0.25f, 0.75f, 0.35f, 0.5f);
            animator.SetLookAtPosition(lookTarget.position);
        }

        private void ApplyHandIk(AvatarIKGoal goal, Transform target)
        {
            if (target == null || handIkWeight <= 0f)
            {
                animator.SetIKPositionWeight(goal, 0f);
                animator.SetIKRotationWeight(goal, 0f);
                return;
            }

            animator.SetIKPositionWeight(goal, handIkWeight);
            animator.SetIKRotationWeight(goal, handIkWeight);
            animator.SetIKPosition(goal, target.position);
            animator.SetIKRotation(goal, target.rotation);
        }

        private void ApplyFootIk(AvatarIKGoal goal)
        {
            if (footIkWeight <= 0f)
            {
                animator.SetIKPositionWeight(goal, 0f);
                animator.SetIKRotationWeight(goal, 0f);
                return;
            }

            var footPosition = animator.GetIKPosition(goal);
            var rayStart = footPosition + Vector3.up * footRayHeight;
            if (!Physics.Raycast(rayStart, Vector3.down, out var hit, footRayDistance, footIkMask, QueryTriggerInteraction.Ignore))
            {
                animator.SetIKPositionWeight(goal, 0f);
                animator.SetIKRotationWeight(goal, 0f);
                return;
            }

            animator.SetIKPositionWeight(goal, footIkWeight);
            animator.SetIKRotationWeight(goal, footIkWeight);
            animator.SetIKPosition(goal, hit.point + Vector3.up * footOffset);
            animator.SetIKRotation(goal, Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, hit.normal), hit.normal));
        }

        private bool IsGrounded()
        {
            return Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, 0.45f, footIkMask, QueryTriggerInteraction.Ignore);
        }

        private Vector3 SanitizePlanarDelta(Vector3 delta)
        {
            if (float.IsNaN(delta.x) || float.IsNaN(delta.y) || float.IsNaN(delta.z))
            {
                return Vector3.zero;
            }

            delta.y = 0f;
            var maxMagnitude = Mathf.Max(0.1f, maxSpeed * Time.deltaTime * 3f);
            return delta.sqrMagnitude > maxMagnitude * maxMagnitude ? delta.normalized * maxMagnitude : delta;
        }

        private void ConfigureAgent()
        {
            if (agent == null)
            {
                return;
            }

            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.speed = maxSpeed;
            agent.acceleration = acceleration;
            agent.angularSpeed = 720f;
        }

        private void SyncAgentToTransform(bool warp = false)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return;
            }

            if (warp)
            {
                agent.Warp(transform.position);
                return;
            }

            if (syncAgentEveryFrame)
            {
                agent.nextPosition = transform.position;
            }
        }

        private void CacheReferences()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }
        }

        private void FireTrigger(string triggerName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(triggerName))
            {
                return;
            }

            animator.ResetTrigger(triggerName);
            animator.SetTrigger(triggerName);
        }

        private void SetAnimatorFloat(string parameterName, float value)
        {
            if (!string.IsNullOrWhiteSpace(parameterName))
            {
                animator.SetFloat(parameterName, value);
            }
        }

        private void SetAnimatorBool(string parameterName, bool value)
        {
            if (!string.IsNullOrWhiteSpace(parameterName))
            {
                animator.SetBool(parameterName, value);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            if (hasDestination)
            {
                Gizmos.DrawWireSphere(destination, 0.2f);
                Gizmos.DrawLine(transform.position, destination);
            }

            if (!hasSplinePose)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(lastSplinePosition, 0.18f);
            Gizmos.DrawLine(lastSplinePosition, lastSplinePosition + (lastSplineRotation * Vector3.forward));
        }
    }
}
