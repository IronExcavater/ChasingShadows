using RootMotion;
using RootMotion.FinalIK;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ChasingShadows.Characters
{
    [ExecuteAlways]
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

        [Header("Base Locomotion")]
        public bool enableBaseLocomotion = true;
        public bool cameraRelativeMovement = true;
        public bool faceMovementDirection;
        public Transform cameraReference;
        public float forwardSpeed = 3.5f;
        public float backwardSpeed = 2.1f;
        public float strafeSpeed = 2.6f;
        public float runMultiplier = 1.45f;
        public float inputAcceleration = 14f;
        public float inputDeceleration = 18f;
        public float rotationSpeed = 540f;
        public float turnInPlaceSpeed = 150f;
        public float turnDeadZone = 0.08f;
        public float movementDeadZone = 0.08f;

        [Header("Animator Parameters")]
        public string moveSpeedParameter = "MoveSpeed";
        public string moveForwardParameter = "MoveForward";
        public string moveRightParameter = "MoveRight";
        public string turnSpeedParameter = "TurnSpeed";
        public string groundedParameter = "Grounded";
        public string leanForwardParameter = "LeanForward";
        public string leanRightParameter = "LeanRight";
        [Range(0.01f, 0.5f)] public float animatorDampTime = 0.1f;

        [Header("Lean")]
        public float leanForwardScale = 0.08f;
        public float leanRightScale = 0.035f;
        public float turnLeanScale = 0.006f;
        public float maxLean = 0.65f;
        public float leanSharpness = 8f;

        [Header("Final IK")]
        public bool finalIkEnabled = true;
        public bool disableIkInEditMode = true;
        public bool forceBindPoseInEditMode = true;
        [Range(0f, 1f)] public float lookWeight = 0.75f;
        [Range(0f, 1f)] public float handIkWeight = 0f;
        [Range(0f, 1f)] public float footIkWeight = 0.85f;
        public float ikWeightSharpness = 8f;
        public float grounderMaxStep = 0.5f;
        public float grounderHeightOffset = 0.02f;
        [Range(0f, 1f)] public float kneeBendGoalWeight = 0.85f;
        public float kneeBendGoalForward = 0.42f;
        public float kneeBendGoalUp = 0.04f;
        public float kneeBendGoalSide = 0.06f;

        private bool finalIkReady;
        private bool editPoseApplied;
        private float lookWeightCurrent;
        private float handWeightCurrent;
        private float footWeightCurrent;
        private float leanForward;
        private float leanRight;
        private float smoothedForward;
        private float smoothedRight;
        private float smoothedTurn;
        private float currentYawSpeed;
        private Vector3 locomotionVelocity;
        private Vector3 previousPosition;
        private Vector3 previousVelocity;
        private Quaternion previousRotation;
        private Transform leftKneeBendGoal;
        private Transform rightKneeBendGoal;
#if UNITY_EDITOR
        private bool editPoseQueued;
#endif

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
            if (!Application.isPlaying)
            {
                QueueEditModePose();
                return;
            }

            RestorePlayModeComponents();
            EnsureFinalIkComponents();
            previousPosition = transform.position;
            previousRotation = transform.rotation;
            SyncAnimatorRootMotion();
        }

        private void OnEnable()
        {
            CacheReferences();
            if (!Application.isPlaying)
            {
                QueueEditModePose();
                return;
            }

            RestorePlayModeComponents();
            EnsureFinalIkComponents();
            previousPosition = transform.position;
            previousRotation = transform.rotation;
            SyncAnimatorRootMotion();
        }

        private void OnValidate()
        {
            groundRayHeight = Mathf.Max(0f, groundRayHeight);
            groundRayDistance = Mathf.Max(0f, groundRayDistance);
            maxRootMotionStep = Mathf.Max(0.01f, maxRootMotionStep);
            grounderMaxStep = Mathf.Max(0.01f, grounderMaxStep);
            ikWeightSharpness = Mathf.Max(0.01f, ikWeightSharpness);
            inputAcceleration = Mathf.Max(0.01f, inputAcceleration);
            inputDeceleration = Mathf.Max(0.01f, inputDeceleration);
            rotationSpeed = Mathf.Max(0f, rotationSpeed);
            turnInPlaceSpeed = Mathf.Max(0f, turnInPlaceSpeed);
            movementDeadZone = Mathf.Clamp01(movementDeadZone);
            turnDeadZone = Mathf.Clamp01(turnDeadZone);
            maxLean = Mathf.Max(0f, maxLean);
            kneeBendGoalForward = Mathf.Max(0.01f, kneeBendGoalForward);
            CacheReferences();
            if (!Application.isPlaying)
            {
                QueueEditModePose();
            }
            else
            {
                SyncAnimatorRootMotion();
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                ApplyEditModePose();
                return;
            }

            UpdateBaseLocomotion();
            UpdateAnimatorState();
            UpdateFinalIk();
            previousPosition = transform.position;
            previousRotation = transform.rotation;
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
            var rawForward = localVelocity.z;
            var rawRight = localVelocity.x;
            var planarSpeed = new Vector2(rawRight, rawForward).magnitude;
            var accelerationVector = Time.deltaTime > 0f ? (velocity - previousVelocity) / Time.deltaTime : Vector3.zero;
            var localAcceleration = transform.InverseTransformDirection(accelerationVector);
            var blendDamp = 1f - Mathf.Exp(-Mathf.Max(0.01f, 1f / animatorDampTime) * Time.deltaTime);
            // Lean uses a separate (slower) sharpness to avoid jitter
            var damp = blendDamp;

            smoothedForward = Mathf.Lerp(smoothedForward, rawForward, blendDamp);
            smoothedRight = Mathf.Lerp(smoothedRight, rawRight, blendDamp);
            var yawSpeed = Mathf.Abs(currentYawSpeed) > 0.001f ? currentYawSpeed : GetExternalYawSpeed();
            smoothedTurn = Mathf.Lerp(smoothedTurn, yawSpeed, damp);

            var targetLeanForward = Mathf.Clamp(localAcceleration.z * leanForwardScale, -maxLean, maxLean);
            var targetLeanRight = Mathf.Clamp((localAcceleration.x * leanRightScale) + (localVelocity.x * leanRightScale) + (yawSpeed * turnLeanScale), -maxLean, maxLean);
            var leanBlend = 1f - Mathf.Exp(-leanSharpness * Time.deltaTime);
            leanForward = Mathf.Lerp(leanForward, targetLeanForward, leanBlend);
            leanRight = Mathf.Lerp(leanRight, targetLeanRight, leanBlend);

            // Feed blend tree with single-smoothed values (smoothedForward/Right are explicit lerps)
            // Using Unity's built-in damp on top would double-filter and cause sluggish blend response
            SetAnimatorFloat(moveSpeedParameter, planarSpeed);
            SetAnimatorFloatDirect(moveForwardParameter, smoothedForward);
            SetAnimatorFloatDirect(moveRightParameter, smoothedRight);
            SetAnimatorFloatDirect(turnSpeedParameter, smoothedTurn);
            SetAnimatorFloat(leanForwardParameter, leanForward);
            SetAnimatorFloat(leanRightParameter, leanRight);
            SetAnimatorBool(groundedParameter, IsGrounded());

            previousVelocity = velocity;
        }

        private void UpdateBaseLocomotion()
        {
            currentYawSpeed = 0f;
            if (!enableBaseLocomotion || applyRootMotion)
            {
                locomotionVelocity = Vector3.zero;
                return;
            }

            var input = ReadMovementInput();
            var turnInput = ReadTurnInput();
            var targetVelocity = ResolveInputVelocity(input);
            var acceleration = targetVelocity.sqrMagnitude > locomotionVelocity.sqrMagnitude ? inputAcceleration : inputDeceleration;
            locomotionVelocity = Vector3.MoveTowards(locomotionVelocity, targetVelocity, acceleration * Time.deltaTime);

            if (locomotionVelocity.sqrMagnitude > 0.0001f)
            {
                var nextPosition = transform.position + (locomotionVelocity * Time.deltaTime);
                if (projectTimelineMotionToGround)
                {
                    nextPosition = ProjectToGround(nextPosition);
                }

                transform.position = nextPosition;
            }

            var yawDelta = ResolveYawDelta(input, turnInput);
            if (Mathf.Abs(yawDelta) > 0.001f)
            {
                transform.rotation = Quaternion.AngleAxis(yawDelta, Vector3.up) * transform.rotation;
                currentYawSpeed = yawDelta / Mathf.Max(Time.deltaTime, 0.0001f);
            }
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
                UpdateKneeBendGoals();
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
                grounder.enabled = finalIkEnabled;
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
                grounder.enabled = finalIkEnabled;
                grounder.ik = bipedIk;
                grounder.solver.layers = groundMask;
                grounder.solver.maxStep = grounderMaxStep;
                grounder.solver.heightOffset = grounderHeightOffset;
            }

            if (lookAtIk != null)
            {
                lookAtIk.enabled = finalIkEnabled;
                lookAtIk.solver.SetChain(bipedIk.references.spine, bipedIk.references.head, bipedIk.references.eyes, transform);
            }

            ConfigureKneeBendSolvers();
        }

        private Vector2 ReadMovementInput()
        {
            var x = Input.GetAxisRaw("Horizontal");
            var y = Input.GetAxisRaw("Vertical");
            var input = new Vector2(x, y);
            return input.magnitude > movementDeadZone ? Vector2.ClampMagnitude(input, 1f) : Vector2.zero;
        }

        private float ReadTurnInput()
        {
            var turn = 0f;
            if (Input.GetKey(KeyCode.Q))
            {
                turn -= 1f;
            }

            if (Input.GetKey(KeyCode.E))
            {
                turn += 1f;
            }

            return Mathf.Abs(turn) > turnDeadZone ? Mathf.Clamp(turn, -1f, 1f) : 0f;
        }

        private Vector3 ResolveInputVelocity(Vector2 input)
        {
            if (input == Vector2.zero)
            {
                return Vector3.zero;
            }

            var forward = transform.forward;
            var right = transform.right;
            if (cameraRelativeMovement)
            {
                var reference = cameraReference != null ? cameraReference : Camera.main != null ? Camera.main.transform : null;
                if (reference != null)
                {
                    forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
                    right = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;
                }
            }

            var forwardFactor = input.y >= 0f ? forwardSpeed : backwardSpeed;
            var speed = Mathf.Max(Mathf.Abs(input.x) * strafeSpeed, Mathf.Abs(input.y) * forwardFactor);
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                speed *= runMultiplier;
            }

            var direction = ((right * input.x) + (forward * input.y));
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized * speed : Vector3.zero;
        }

        private float ResolveYawDelta(Vector2 input, float turnInput)
        {
            if (turnInput != 0f)
            {
                return turnInput * turnInPlaceSpeed * Time.deltaTime;
            }

            if (locomotionVelocity.sqrMagnitude <= 0.0001f || input == Vector2.zero)
            {
                return 0f;
            }

            var desiredForward = faceMovementDirection ? locomotionVelocity : ResolveFacingDirection();
            desiredForward.y = 0f;
            if (desiredForward.sqrMagnitude <= 0.0001f)
            {
                return 0f;
            }

            var signedAngle = Vector3.SignedAngle(transform.forward, desiredForward.normalized, Vector3.up);
            return Mathf.Clamp(signedAngle, -rotationSpeed * Time.deltaTime, rotationSpeed * Time.deltaTime);
        }

        private Vector3 ResolveFacingDirection()
        {
            if (!cameraRelativeMovement)
            {
                return transform.forward;
            }

            var reference = cameraReference != null ? cameraReference : Camera.main != null ? Camera.main.transform : null;
            return reference != null ? Vector3.ProjectOnPlane(reference.forward, Vector3.up) : transform.forward;
        }

        private float GetExternalYawSpeed()
        {
            if (Time.deltaTime <= 0f)
            {
                return 0f;
            }

            return Vector3.SignedAngle(previousRotation * Vector3.forward, transform.forward, Vector3.up) / Time.deltaTime;
        }

        private void ConfigureKneeBendSolvers()
        {
            if (bipedIk?.solvers == null)
            {
                return;
            }

            EnsureKneeBendGoal(ref leftKneeBendGoal, "Joe_LeftKnee_BendGoal");
            EnsureKneeBendGoal(ref rightKneeBendGoal, "Joe_RightKnee_BendGoal");

            ConfigureLegBendSolver(bipedIk.solvers.leftFoot, leftKneeBendGoal);
            ConfigureLegBendSolver(bipedIk.solvers.rightFoot, rightKneeBendGoal);
        }

        private void ConfigureLegBendSolver(IKSolverLimb solver, Transform bendGoal)
        {
            if (solver == null)
            {
                return;
            }

            solver.bendModifier = IKSolverLimb.BendModifier.Goal;
            solver.bendModifierWeight = kneeBendGoalWeight;
            solver.bendGoal = bendGoal;
            solver.maintainRotationWeight = Mathf.Max(solver.maintainRotationWeight, 0.45f);
        }

        private void UpdateKneeBendGoals()
        {
            if (bipedIk?.references == null)
            {
                return;
            }

            PositionKneeBendGoal(leftKneeBendGoal, bipedIk.references.leftThigh, bipedIk.references.leftCalf, -1f);
            PositionKneeBendGoal(rightKneeBendGoal, bipedIk.references.rightThigh, bipedIk.references.rightCalf, 1f);
        }

        private void PositionKneeBendGoal(Transform bendGoal, Transform thigh, Transform calf, float sideSign)
        {
            if (bendGoal == null || thigh == null || calf == null)
            {
                return;
            }

            bendGoal.position = calf.position
                + (transform.forward * kneeBendGoalForward)
                + (transform.up * kneeBendGoalUp)
                + (transform.right * kneeBendGoalSide * sideSign);
        }

        private void EnsureKneeBendGoal(ref Transform bendGoal, string name)
        {
            if (bendGoal != null)
            {
                return;
            }

            var existing = transform.Find(name);
            if (existing != null)
            {
                bendGoal = existing;
                return;
            }

            var goal = new GameObject(name);
            goal.hideFlags = HideFlags.HideInHierarchy;
            goal.transform.SetParent(transform, false);
            bendGoal = goal.transform;
        }

        private void ApplyEditModePose()
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (disableIkInEditMode)
            {
                SetFinalIkComponentsEnabled(false);
            }

            if (animator == null || !forceBindPoseInEditMode || editPoseApplied)
            {
                return;
            }

            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            animator.applyRootMotion = false;
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);
            editPoseApplied = true;
        }

        private void QueueEditModePose()
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (disableIkInEditMode)
            {
                SetFinalIkComponentsEnabled(false);
            }

            editPoseApplied = false;
#if UNITY_EDITOR
            if (editPoseQueued)
            {
                return;
            }

            editPoseQueued = true;
            EditorApplication.delayCall += ApplyQueuedEditModePose;
#endif
        }

#if UNITY_EDITOR
        private void ApplyQueuedEditModePose()
        {
            editPoseQueued = false;
            if (this == null || Application.isPlaying)
            {
                return;
            }

            ApplyEditModePose();
        }
#endif

        private void RestorePlayModeComponents()
        {
            editPoseApplied = false;
            if (animator != null)
            {
                animator.enabled = true;
            }

            SetFinalIkComponentsEnabled(finalIkEnabled);
        }

        private void SetFinalIkComponentsEnabled(bool enabled)
        {
            if (bipedIk != null)
            {
                bipedIk.enabled = enabled;
            }

            if (lookAtIk != null)
            {
                lookAtIk.enabled = enabled;
            }

            if (grounder != null)
            {
                grounder.enabled = enabled;
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

        private void SetAnimatorFloatDirect(string parameterName, float value)
        {
            if (!string.IsNullOrWhiteSpace(parameterName) &&
                HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Float))
            {
                animator.SetFloat(parameterName, value);
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
