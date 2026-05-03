using RootMotion;
using RootMotion.FinalIK;
using UnityEngine;
using UnityEngine.AI;
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
        public BipedIK bipedIk;
        public LookAtIK lookAtIk;
        public GrounderBipedIK grounder;

        [Header("Movement")]
        public MotionDriver driver = MotionDriver.NavMesh;
        public float maxSpeed = 2.8f;
        public float acceleration = 6f;
        public float deceleration = 5f;
        public float rotationSharpness = 12f;
        public float stoppingDistance = 0.12f;
        [Range(0.05f, 4f)] public float splineSpeed = 1f;

        [Header("Ground Projection")]
        public bool projectToNavMesh = true;
        public NavMeshProjection splineNavMeshProjection = NavMeshProjection.WhenAvailable;
        public float navMeshSampleRadius = 1.5f;
        public LayerMask groundMask = ~0;
        public float groundRayHeight = 1.4f;
        public float groundRayDistance = 3f;
        public float rootHeightOffset = 0f;
        public bool holdFinalSplinePose = true;

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

        private bool hasDestination;
        private bool hasSplinePose;
        private bool finalIkReady;
        private float splineT;
        private float targetSpeed;
        private float currentSpeed;
        private float rootMotionSecondsRemaining;
        private float lookWeightCurrent;
        private float handWeightCurrent;
        private float footWeightCurrent;
        private float leanForward;
        private float leanRight;
        private Vector3 destination;
        private Vector3 desiredDirection;
        private Vector3 previousPosition;
        private Vector3 previousVelocity;
        private Vector3 rootMotionDelta;
        private Quaternion rootMotionRotationDelta = Quaternion.identity;
        private Vector3 lastSplinePosition;
        private Quaternion lastSplineRotation = Quaternion.identity;

        private void Reset()
        {
            animator = GetComponent<Animator>();
            agent = GetComponent<NavMeshAgent>();
            bipedIk = GetComponent<BipedIK>();
            lookAtIk = GetComponent<LookAtIK>();
            grounder = GetComponent<GrounderBipedIK>();
        }

        private void Awake()
        {
            CacheReferences();
            EnsureFinalIkComponents();
            ConfigureAgent();
            previousPosition = transform.position;
            destination = transform.position;
        }

        private void OnEnable()
        {
            CacheReferences();
            EnsureFinalIkComponents();
            ConfigureAgent();
            SyncAgentToTransform(true);
        }

        private void OnValidate()
        {
            maxSpeed = Mathf.Max(0f, maxSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            deceleration = Mathf.Max(0f, deceleration);
            rotationSharpness = Mathf.Max(0f, rotationSharpness);
            stoppingDistance = Mathf.Max(0f, stoppingDistance);
            navMeshSampleRadius = Mathf.Max(0.01f, navMeshSampleRadius);
            groundRayHeight = Mathf.Max(0f, groundRayHeight);
            groundRayDistance = Mathf.Max(0f, groundRayDistance);
            grounderMaxStep = Mathf.Max(0.01f, grounderMaxStep);
            ikWeightSharpness = Mathf.Max(0.01f, ikWeightSharpness);
            CacheReferences();
            ConfigureAgent();
        }

        private void Update()
        {
            switch (driver)
            {
                case MotionDriver.NavMesh:
                    ClearRootMotionDelta();
                    TickNavMesh();
                    break;
                case MotionDriver.Spline:
                    ClearRootMotionDelta();
                    TickSpline();
                    break;
                case MotionDriver.RootMotion:
                    TickRootMotion();
                    break;
                case MotionDriver.External:
                    ClearRootMotionDelta();
                    targetSpeed = 0f;
                    desiredDirection = transform.forward;
                    ApplyMotor(Vector3.zero, false, NavMeshProjection.WhenAvailable);
                    break;
            }

            UpdateAnimatorState();
            UpdateFinalIk();
            SyncAgentToTransform(false);
            previousPosition = transform.position;
        }

        private void OnAnimatorMove()
        {
            if (animator == null || driver != MotionDriver.RootMotion || rootMotionSecondsRemaining <= 0f)
            {
                return;
            }

            rootMotionDelta += SanitizeRootMotionDelta(animator.deltaPosition);
            rootMotionRotationDelta *= animator.deltaRotation;
        }

        public void SetNavDestination(Vector3 worldPosition)
        {
            driver = MotionDriver.NavMesh;
            destination = worldPosition;
            hasDestination = true;
            rootMotionSecondsRemaining = 0f;

            if (agent != null && agent.enabled && agent.isOnNavMesh)
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

        public void SetIkWeights(float look, float hands, float feet)
        {
            lookWeight = Mathf.Clamp01(look);
            handIkWeight = Mathf.Clamp01(hands);
            footIkWeight = Mathf.Clamp01(feet);
        }

        public void FollowSpline(SplineContainer spline, float startT = 0f)
        {
            trackedSpline = spline;
            SetSplineNormalized(startT);
            driver = MotionDriver.Spline;
            hasDestination = false;
            rootMotionSecondsRemaining = 0f;
        }

        public void SetSplineNormalized(float normalizedTime, bool evaluateImmediately = true)
        {
            splineT = Mathf.Clamp01(normalizedTime);
            if (evaluateImmediately && trackedSpline != null)
            {
                EvaluateSplinePose(splineT, out lastSplinePosition, out lastSplineRotation);
                hasSplinePose = true;
                transform.SetPositionAndRotation(
                    ProjectPosition(lastSplinePosition, splineNavMeshProjection, out _),
                    lastSplineRotation);
            }
        }

        public void SetMotionDriver(MotionDriver motionDriver)
        {
            driver = motionDriver;
            if (motionDriver != MotionDriver.NavMesh)
            {
                hasDestination = false;
            }

            if (motionDriver != MotionDriver.RootMotion)
            {
                rootMotionSecondsRemaining = 0f;
            }
        }

        public void ConfigureSystems(bool useNavMesh, bool useRootMotion, bool useIk, bool useAdvancedLocomotion)
        {
            projectToNavMesh = useNavMesh;
            finalIkEnabled = useIk;
            if (!useRootMotion && driver == MotionDriver.RootMotion)
            {
                SetMotionDriver(MotionDriver.External);
            }

            ConfigureAgent();
        }

        public void RefreshConfiguration()
        {
            ConfigureAgent();
            EnsureFinalIkComponents();
            SyncAgentToTransform(true);
        }

        public void SetNavMeshEnabled(bool enabled)
        {
            projectToNavMesh = enabled;
            ConfigureAgent();
        }

        public void SetRootMotionEnabled(bool enabled)
        {
            if (!enabled && driver == MotionDriver.RootMotion)
            {
                SetMotionDriver(MotionDriver.External);
            }
        }

        public void SetIkEnabled(bool enabled)
        {
            finalIkEnabled = enabled;
        }

        public void SetAdvancedLocomotionEnabled(bool enabled)
        {
            // Kept for Timeline/profile compatibility. Locomotion parameters are always updated.
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

        public void PlayActionTrigger(string triggerName)
        {
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
            var projected = ProjectPosition(worldPosition, NavMeshProjection.WhenAvailable, out _);
            transform.position = projected;
            destination = projected;
            hasDestination = false;
            currentSpeed = 0f;
            targetSpeed = 0f;
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
            targetSpeed = 0f;
            rootMotionSecondsRemaining = 0f;
            hasDestination = false;
            driver = MotionDriver.External;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }
        }

        private void TickNavMesh()
        {
            if (!hasDestination)
            {
                targetSpeed = 0f;
                ApplyMotor(Vector3.zero, false, NavMeshProjection.WhenAvailable);
                return;
            }

            var remaining = destination - transform.position;
            remaining.y = 0f;
            if (remaining.magnitude <= stoppingDistance)
            {
                hasDestination = false;
                targetSpeed = 0f;
                ApplyMotor(Vector3.zero, false, NavMeshProjection.WhenAvailable);
                return;
            }

            Vector3 velocity = Vector3.zero;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                if (!agent.hasPath || (agent.destination - destination).sqrMagnitude > 0.01f)
                {
                    agent.SetDestination(destination);
                }

                velocity = agent.desiredVelocity;
            }

            if (velocity.sqrMagnitude < 0.0001f)
            {
                velocity = remaining.normalized * maxSpeed;
            }

            desiredDirection = PlanarDirection(velocity, transform.forward);
            targetSpeed = Mathf.Min(maxSpeed, Mathf.Max(velocity.magnitude, maxSpeed * 0.35f));
            ApplyMotor(desiredDirection, true, NavMeshProjection.WhenAvailable);
        }

        private void TickSpline()
        {
            if (trackedSpline == null)
            {
                SetMotionDriver(MotionDriver.External);
                return;
            }

            EvaluateSplinePose(splineT, out var currentPose, out var currentRotation);
            var length = Mathf.Max(0.01f, trackedSpline.CalculateLength());
            splineT = Mathf.Clamp01(splineT + (splineSpeed / length) * Time.deltaTime);
            EvaluateSplinePose(splineT, out lastSplinePosition, out lastSplineRotation);
            hasSplinePose = true;

            var toNext = lastSplinePosition - currentPose;
            desiredDirection = PlanarDirection(toNext, currentRotation * Vector3.forward);
            targetSpeed = splineSpeed;
            ApplyMotor(desiredDirection, true, splineNavMeshProjection, lastSplinePosition);

            if (splineT >= 1f)
            {
                SetMotionDriver(holdFinalSplinePose ? MotionDriver.External : MotionDriver.NavMesh);
            }
        }

        private void TickRootMotion()
        {
            rootMotionSecondsRemaining -= Time.deltaTime;
            if (rootMotionDelta.sqrMagnitude > 0f)
            {
                desiredDirection = PlanarDirection(rootMotionDelta, transform.forward);
                targetSpeed = Mathf.Min(maxSpeed, rootMotionDelta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f));
                MoveRoot(rootMotionDelta, NavMeshProjection.WhenAvailable);
            }
            else
            {
                targetSpeed = 0f;
                ApplyMotor(Vector3.zero, false, NavMeshProjection.WhenAvailable);
            }

            if (rootMotionRotationDelta != Quaternion.identity)
            {
                transform.rotation = rootMotionRotationDelta * transform.rotation;
            }

            ClearRootMotionDelta();

            if (rootMotionSecondsRemaining <= 0f)
            {
                rootMotionSecondsRemaining = 0f;
                SetMotionDriver(MotionDriver.External);
            }
        }

        private void ApplyMotor(Vector3 direction, bool wantsMove, NavMeshProjection projection)
        {
            ApplyMotor(direction, wantsMove, projection, null);
        }

        private void ApplyMotor(Vector3 direction, bool wantsMove, NavMeshProjection projection, Vector3? explicitTarget)
        {
            var speedStep = (targetSpeed > currentSpeed ? acceleration : deceleration) * Time.deltaTime;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, speedStep);

            if (!wantsMove || currentSpeed <= 0.001f)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * Time.deltaTime);
                return;
            }

            var moveDirection = PlanarDirection(direction, transform.forward);
            var maxStep = currentSpeed * Time.deltaTime;
            var targetPosition = explicitTarget.HasValue
                ? Vector3.MoveTowards(transform.position, explicitTarget.Value, maxStep)
                : transform.position + moveDirection * maxStep;
            var projected = ProjectPosition(targetPosition, projection, out _);
            var delta = projected - transform.position;
            if (delta.sqrMagnitude > 0.000001f)
            {
                transform.position = projected;
            }

            RotateToward(moveDirection);
        }

        private void MoveRoot(Vector3 delta, NavMeshProjection projection)
        {
            var projected = ProjectPosition(transform.position + delta, projection, out _);
            transform.position = projected;
        }

        private Vector3 ProjectPosition(Vector3 targetPosition, NavMeshProjection projection, out bool grounded)
        {
            grounded = false;
            var projected = targetPosition;

            if (projectToNavMesh && projection != NavMeshProjection.Disabled)
            {
                if (NavMesh.SamplePosition(targetPosition, out var navHit, navMeshSampleRadius, NavMesh.AllAreas))
                {
                    projected = navHit.position;
                }
                else if (projection == NavMeshProjection.Required)
                {
                    return transform.position;
                }
            }

            var rayStart = projected + Vector3.up * groundRayHeight;
            if (Physics.Raycast(rayStart, Vector3.down, out var groundHit, groundRayHeight + groundRayDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                projected.y = groundHit.point.y + rootHeightOffset;
                grounded = true;
            }

            return projected;
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

            grounder.ik = bipedIk;
            grounder.solver.layers = groundMask;
            grounder.solver.maxStep = grounderMaxStep;
            grounder.solver.heightOffset = grounderHeightOffset;

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
            var maxMagnitude = Mathf.Max(0.1f, maxSpeed * Time.deltaTime * 3f);
            return delta.sqrMagnitude > maxMagnitude * maxMagnitude ? delta.normalized * maxMagnitude : delta;
        }

        private void ClearRootMotionDelta()
        {
            rootMotionDelta = Vector3.zero;
            rootMotionRotationDelta = Quaternion.identity;
        }

        private static Vector3 PlanarDirection(Vector3 value, Vector3 fallback)
        {
            value.y = 0f;
            if (value.sqrMagnitude > 0.0001f)
            {
                return value.normalized;
            }

            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
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
            agent.stoppingDistance = stoppingDistance;
        }

        private void SyncAgentToTransform(bool warp)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return;
            }

            if (warp)
            {
                agent.Warp(transform.position);
            }
            else
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
