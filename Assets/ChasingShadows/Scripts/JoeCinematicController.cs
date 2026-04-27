using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Splines;

namespace ChasingShadows.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Rigidbody))]
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
        public bool rootMotionLocomotion = true;
        public float navMeshSampleRadius = 1.5f;
        [Range(0.05f, 2f)] public float splineSpeed = 1f;

        [Header("System Ownership")]
        public bool navMeshEnabled = true;
        public bool rootMotionEnabled = true;
        public bool ikEnabled = true;
        public bool advancedLocomotionEnabled = true;
        public bool allowRootMotionOnSpline = false;
        public bool syncAgentEveryFrame = true;
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

        private float currentSpeed;
        private float rootMotionSecondsRemaining;
        private float splineT;
        private Vector3 previousPosition;
        private Vector3 currentVelocity;
        private Vector3 lastSplinePosition;
        private Quaternion lastSplineRotation;
        private bool hasSplinePose;

        private void Reset()
        {
            animator = GetComponent<Animator>();
            agent = GetComponent<NavMeshAgent>();
        }

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            ConfigureAgent();
            previousPosition = transform.position;
            lastSplinePosition = transform.position;
            lastSplineRotation = transform.rotation;
        }

        private void OnEnable()
        {
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

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            ConfigureAgent();
        }

        private void Update()
        {
            switch (driver)
            {
                case MotionDriver.NavMesh:
                    TickNavMesh();
                    break;
                case MotionDriver.RootMotion:
                    TickRootMotionTimer();
                    break;
                case MotionDriver.Spline:
                    TickSpline();
                    break;
                case MotionDriver.External:
                    currentSpeed = 0f;
                    break;
            }

            UpdateAnimatorVelocity();
            previousPosition = transform.position;
        }

        private void OnAnimatorMove()
        {
            if (animator == null)
            {
                return;
            }

            if (!ShouldConsumeRootMotion())
            {
                return;
            }

            if (driver == MotionDriver.RootMotion || driver == MotionDriver.Spline || (driver == MotionDriver.NavMesh && rootMotionLocomotion))
            {
                var delta = animator.deltaPosition;
                if (driver == MotionDriver.NavMesh && agent != null && agent.enabled && agent.hasPath)
                {
                    var desired = agent.desiredVelocity;
                    if (desired.sqrMagnitude > 0.01f)
                    {
                        delta = desired.normalized * Mathf.Min(delta.magnitude, maxSpeed * Time.deltaTime);
                    }
                }

                MoveWithCurrentAuthority(transform.position + delta);
                transform.rotation *= animator.deltaRotation;
                SyncAgentToTransform();
            }
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
            rootMotionSecondsRemaining = 0f;

            if (!navMeshEnabled || agent == null || !agent.enabled)
            {
                return;
            }

            EnsureAgentOnNavMesh();
            if (agent.isOnNavMesh)
            {
                agent.SetDestination(worldPosition);
            }
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
            SetSplineNormalized(startT, false);
            driver = MotionDriver.Spline;
            ClearNavPath();
        }

        public void SetSplineNormalized(float normalizedTime, bool evaluateImmediately = true)
        {
            splineT = Mathf.Clamp01(normalizedTime);
            if (evaluateImmediately && trackedSpline != null)
            {
                EvaluateSplinePose(splineT, out lastSplinePosition, out lastSplineRotation);
                hasSplinePose = true;
                MoveWithSplineProjection(lastSplinePosition);
                transform.rotation = lastSplineRotation;
                SyncAgentToTransform();
            }
        }

        public void SetMotionDriver(MotionDriver motionDriver)
        {
            driver = motionDriver;
            if (motionDriver != MotionDriver.NavMesh)
            {
                ClearNavPath();
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
            if (enabled)
            {
                EnsureAgentOnNavMesh();
                SyncAgentToTransform();
            }
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
            rootMotionSecondsRemaining = Mathf.Max(0.01f, durationSeconds);
            ClearNavPath();

            if (animator != null && !string.IsNullOrWhiteSpace(triggerName))
            {
                animator.ResetTrigger(triggerName);
                animator.SetTrigger(triggerName);
            }
        }

        public void PlayBeat(string triggerName, MotionDriver motionDriver, float durationSeconds = 1f)
        {
            if (motionDriver == MotionDriver.RootMotion)
            {
                PlayRootMotionBeat(triggerName, durationSeconds);
                return;
            }

            driver = motionDriver;
            if (animator != null && !string.IsNullOrWhiteSpace(triggerName))
            {
                animator.ResetTrigger(triggerName);
                animator.SetTrigger(triggerName);
            }
        }

        public void Warp(Vector3 worldPosition)
        {
            transform.position = worldPosition;
            EnsureAgentOnNavMesh();

            if (navMeshEnabled && agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.Warp(transform.position);
            }
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
            driver = MotionDriver.NavMesh;
            ClearNavPath();
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
            agent.enabled = navMeshEnabled;
        }

        private void TickNavMesh()
        {
            if (!navMeshEnabled || agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return;
            }

            var desiredVelocity = agent.desiredVelocity;
            currentSpeed = Mathf.MoveTowards(currentSpeed, desiredVelocity.magnitude, acceleration * Time.deltaTime);

            if (desiredVelocity.sqrMagnitude > 0.01f)
            {
                RotateToward(desiredVelocity);
            }

            if (!rootMotionLocomotion || !rootMotionEnabled)
            {
                MoveWithNavMeshProjection(transform.position + desiredVelocity * Time.deltaTime);
                SyncAgentToTransform();
            }
        }

        private void TickRootMotionTimer()
        {
            rootMotionSecondsRemaining -= Time.deltaTime;
            if (rootMotionSecondsRemaining <= 0f)
            {
                driver = MotionDriver.NavMesh;
                SyncAgentToTransform();
            }
        }

        private void TickSpline()
        {
            if (trackedSpline == null)
            {
                driver = MotionDriver.NavMesh;
                return;
            }

            var length = Mathf.Max(0.01f, trackedSpline.CalculateLength());
            splineT = Mathf.Clamp01(splineT + (splineSpeed / length) * Time.deltaTime);

            EvaluateSplinePose(splineT, out lastSplinePosition, out lastSplineRotation);
            hasSplinePose = true;
            MoveWithSplineProjection(lastSplinePosition);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                lastSplineRotation,
                1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));

            SyncAgentToTransform();
            if (splineT >= 1f)
            {
                if (holdFinalSplinePose)
                {
                    driver = MotionDriver.External;
                }
                else
                {
                    driver = MotionDriver.NavMesh;
                }
            }
        }

        private void MoveWithCurrentAuthority(Vector3 targetPosition)
        {
            if (driver == MotionDriver.Spline)
            {
                MoveWithSplineProjection(targetPosition);
                return;
            }

            MoveWithNavMeshProjection(targetPosition);
        }

        private void MoveWithNavMeshProjection(Vector3 targetPosition)
        {
            if (navMeshEnabled && NavMesh.SamplePosition(targetPosition, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                return;
            }

            transform.position = targetPosition;
        }

        private void MoveWithSplineProjection(Vector3 targetPosition)
        {
            if (!navMeshEnabled || splineNavMeshProjection == NavMeshProjection.Disabled)
            {
                transform.position = targetPosition;
                return;
            }

            if (NavMesh.SamplePosition(targetPosition, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                return;
            }

            if (splineNavMeshProjection == NavMeshProjection.Required)
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

            currentVelocity = Time.deltaTime > 0f ? (transform.position - previousPosition) / Time.deltaTime : Vector3.zero;
            var worldVelocity = currentVelocity;
            var localVelocity = transform.InverseTransformDirection(worldVelocity);
            var planarSpeed = new Vector2(localVelocity.x, localVelocity.z).magnitude;

            animator.SetFloat(moveSpeedParameter, planarSpeed);
            animator.SetFloat(moveForwardParameter, advancedLocomotionEnabled ? localVelocity.z : planarSpeed);
            animator.SetFloat(moveRightParameter, advancedLocomotionEnabled ? localVelocity.x : 0f);
            animator.SetFloat(turnSpeedParameter, Vector3.SignedAngle(transform.forward, worldVelocity.normalized, Vector3.up));
            animator.SetBool(groundedParameter, IsGrounded());
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
            return Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, 0.35f, footIkMask, QueryTriggerInteraction.Ignore);
        }

        private void ClearNavPath()
        {
            if (navMeshEnabled && agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }
        }

        private void SyncAgentToTransform()
        {
            if (!syncAgentEveryFrame)
            {
                return;
            }

            if (navMeshEnabled && agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.nextPosition = transform.position;
            }
        }

        private void EnsureAgentOnNavMesh()
        {
            if (!navMeshEnabled || agent == null || !agent.enabled || agent.isOnNavMesh)
            {
                return;
            }

            if (NavMesh.SamplePosition(transform.position, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

        private bool ShouldConsumeRootMotion()
        {
            if (!rootMotionEnabled || animator == null || !animator.applyRootMotion)
            {
                return false;
            }

            if (driver == MotionDriver.Spline)
            {
                return allowRootMotionOnSpline;
            }

            return driver == MotionDriver.RootMotion || (driver == MotionDriver.NavMesh && rootMotionLocomotion);
        }

        private void OnDrawGizmosSelected()
        {
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
