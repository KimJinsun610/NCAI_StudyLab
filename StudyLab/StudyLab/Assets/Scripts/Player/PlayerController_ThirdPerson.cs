using UnityEngine;

namespace VARCO_Workshop
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class PlayerController_ThirdPerson : MonoBehaviour
    {
        [Header("이동(입력)")]
        public float moveSpeed     = 5f;
        public float runMultiplier = 1.45f;
        public KeyCode runKey      = KeyCode.LeftShift;
        public float turnSpeed     = 10f;
        [Tooltip("아래로 당기려면 음수 (예: -20)")]
        public float gravity       = -20f;
        [Tooltip("카메라 기준 이동(메인 카메라 사용)")]
        public bool  useCameraSpace = true;
        public bool moveInFacingDirection = false;
        public float facingTurnSpeed = 180f;

        [Header("QA Input")]
        [HideInInspector] public bool qaInputOverrideActive;
        [HideInInspector] public Vector2 qaMoveInput;
        [HideInInspector] public bool qaRunInput;

        [Header("Visual Facing")]
        [Tooltip("Prefer the top renderer wrapper over the Animator transform, so facing correction is not overwritten by animation clips.")]
        public bool preferTopLevelVisualRoot = true;
        [Tooltip("Optional yaw correction for imported visuals after their mesh axis has been normalized.")]
        public float visualYawOffset = 0f;

        [Header("애니메이터(루트 모션)")]
        [Tooltip("Rigidbody로 이동할 때 루트 모션이 켜져 있으면 수직/이동이 겹쳐 끼일 수 있어 끄는 것을 권장합니다.")]
        public bool applyRootMotionFromAnimation;

        [Header("지면(물리)")]
        [Tooltip("접지 판정 레이(비트)")]
        public LayerMask groundMask = ~0;
        [Tooltip("발 위치에서 아래로 보내는 추가 거리")]
        public float groundCheckExtra = 0.1f;
        [Tooltip("지면에 붙일 때의 아래로 미세한 속도(떠다님 감소)")]
        public float groundedDownVel = -0.2f;

        [Header("점프")]
        [Tooltip("점프 시 위로 가해지는 초기 수직 속도")]
        public float jumpForce = 7f;

        Rigidbody       rb;
        CapsuleCollider cap;
        Vector3         moveInput;
        Vector3         moveDir;
        float           targetYaw;
        Quaternion      targetRotation;
        bool            hasTargetRotation;
        Quaternion      visualTargetRotation;
        bool            hasVisualTargetRotation;
        Vector3         lastFacingDirection;
        bool            isRunning;
        bool            jumpQueued;
        public Transform modelRoot;
        PhysicsMaterial noFrictionMaterial;

        PlayerHealth  health;
        PlayerAttack  attack;
        Animator      anim;
        CharacterInitialYAnchor yAnchor;

        const string VisualFacingRootName = "VARCO_VisualFacingRoot";

        void Awake()
        {
            rb  = GetComponent<Rigidbody>();
            cap = GetComponent<CapsuleCollider>();
            health = GetComponent<PlayerHealth>();
            attack = GetComponent<PlayerAttack>();
            var preferredModelRoot = FindRuntimeVisualRoot();
            if (preferTopLevelVisualRoot
                && preferredModelRoot
                && preferredModelRoot.parent == transform
                && preferredModelRoot.name != VisualFacingRootName
                && Mathf.Abs(visualYawOffset) > 0.01f)
            {
                preferredModelRoot = EnsureRuntimeVisualFacingRoot(preferredModelRoot);
            }
            if (!modelRoot || (preferTopLevelVisualRoot && preferredModelRoot && modelRoot != preferredModelRoot && modelRoot.IsChildOf(preferredModelRoot)))
                modelRoot = preferredModelRoot;
            anim = RuntimeAnimatorResolver.FindBestAnimator(gameObject, modelRoot);
            RuntimeAnimatorResolver.DisableDuplicateRootAnimator(gameObject, anim);
            if (anim) anim.applyRootMotion = applyRootMotionFromAnimation;
            lastFacingDirection = NormalizePlanarDirection(transform.forward, Vector3.forward);
            ConfigureRuntimeGroundAlignForStableY();

            rb.useGravity  = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints  = UsesVisualRotationOnly()
                ? RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ
                : RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            ApplyNoFrictionMaterial();
        }

        void Update()
        {
            if (qaInputOverrideActive)
            {
                moveInput.x = qaMoveInput.x;
                moveInput.z = qaMoveInput.y;
            }
            else
            {
                moveInput.x = Input.GetAxisRaw("Horizontal");
                moveInput.z = Input.GetAxisRaw("Vertical");
            }
            moveInput = Vector3.ClampMagnitude(moveInput, 1f);

            if (health != null && !health.IsAlive)
            {
                moveDir = Vector3.zero;
                SetAnimatorBoolIfExists(PlayerAnimParams.IsWalk, false);
                SetAnimatorBoolIfExists(PlayerAnimParams.IsRun, false);
                return;
            }

            bool movementLocked = attack != null && attack.IsMovementLocked;
            var runPressed = qaInputOverrideActive
                ? qaRunInput
                : (Input.GetKey(runKey) || Input.GetKey(KeyCode.RightShift));
            isRunning = !movementLocked
                && moveInput.sqrMagnitude > 0.0001f
                && runPressed;
            var resolvedMoveDir = movementLocked ? Vector3.zero : ResolveMoveDirection(moveInput);
            bool isMovingThisFrame = resolvedMoveDir.sqrMagnitude > 0.0001f;
            moveDir = isMovingThisFrame ? resolvedMoveDir : Vector3.zero;

            if (!moveInFacingDirection)
            {
                if (isMovingThisFrame)
                    SetFacingDirection(moveDir);
            }

            // 점프 입력은 여기서 큐에 담아두고(GetButtonDown은 한 프레임만 true), FixedUpdate에서 접지 상태일 때 소비합니다.
            if (!qaInputOverrideActive && Input.GetButtonDown("Jump"))
                jumpQueued = true;

            if (anim)
            {
                SetAnimatorBoolIfExists(PlayerAnimParams.IsWalk, !movementLocked && moveDir.sqrMagnitude > 0.0001f);
                SetAnimatorBoolIfExists(PlayerAnimParams.IsRun, !movementLocked && isRunning);
                SetAnimatorBoolIfExists(PlayerAnimParams.IsJump, !IsGrounded());
            }
        }

        public void SetQaMoveInput(Vector2 input, bool run)
        {
            qaInputOverrideActive = true;
            qaMoveInput = Vector2.ClampMagnitude(input, 1f);
            qaRunInput = run;
        }

        public void ClearQaMoveInput()
        {
            qaInputOverrideActive = false;
            qaMoveInput = Vector2.zero;
            qaRunInput = false;
        }

        void LateUpdate()
        {
            if (health != null && !health.IsAlive)
                return;

            RotateVisualRootIfNeeded();
        }

        void FixedUpdate()
        {
            bool grounded = IsGrounded();
            var v = GetLinearVelocity();
            bool lockInitialY = ShouldLockInitialY();

            if (health != null && !health.IsAlive)
            {
                jumpQueued = false;
                v.x = 0f;
                v.z = 0f;
                if (lockInitialY) v.y = 0f;
                else if (grounded) v.y = groundedDownVel;
                else v.y += gravity * Time.fixedDeltaTime;
                SetLinearVelocity(v);
                ApplyInitialYLockIfNeeded();
                return;
            }

            bool movementLocked = attack != null && attack.IsMovementLocked;
            Vector3 horiz = Vector3.zero;
            if (!movementLocked && moveDir.sqrMagnitude > 0.0001f)
                horiz = moveDir * moveSpeed * (isRunning ? Mathf.Max(1f, runMultiplier) : 1f);

            v.x = horiz.x;
            v.z = horiz.z;

            if (lockInitialY)
            {
                v.y = 0f;
                jumpQueued = false;
            }
            else if (grounded)
            {
                if (jumpQueued)
                {
                    v.y = jumpForce;
                    jumpQueued = false;
                }
                else if (v.y < 0f)
                {
                    v.y = groundedDownVel;
                }
            }
            else
                v.y += gravity * Time.fixedDeltaTime;

            RotatePhysicsRootIfNeeded();
            SetLinearVelocity(v);
            ApplyInitialYLockIfNeeded();
        }

        Vector3 ResolveMoveDirection(Vector3 input)
        {
            if (input.sqrMagnitude <= 0.0001f) return Vector3.zero;

            if (moveInFacingDirection)
            {
                var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                var right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
                if (forward.sqrMagnitude > 0.0001f && right.sqrMagnitude > 0.0001f)
                    return (forward * input.z + right * input.x).normalized;
                return Vector3.zero;
            }

            var camTransform = GetMoveCameraTransform();
            if (useCameraSpace && camTransform)
            {
                var forward = Vector3.ProjectOnPlane(camTransform.forward, Vector3.up).normalized;
                var right = Vector3.ProjectOnPlane(camTransform.right, Vector3.up).normalized;
                if (forward.sqrMagnitude > 0.0001f && right.sqrMagnitude > 0.0001f)
                    return (forward * input.z + right * input.x).normalized;
            }

            return new Vector3(input.x, 0f, input.z).normalized;
        }

        void SetFacingDirection(Vector3 direction)
        {
            direction = NormalizePlanarDirection(direction, lastFacingDirection);
            if (direction.sqrMagnitude <= 0.0001f) return;
            lastFacingDirection = direction;
            targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            var wanted = Quaternion.Euler(0f, targetYaw, 0f);

            if (UsesVisualRotationOnly())
            {
                visualTargetRotation = Quaternion.Euler(0f, targetYaw + visualYawOffset, 0f);
                hasVisualTargetRotation = true;
                return;
            }

            targetRotation = wanted;
            hasTargetRotation = true;
        }

        public Vector3 GetPlanarFacingForward()
        {
            if (moveInFacingDirection)
                return NormalizePlanarDirection(transform.forward, Vector3.forward);

            if (hasTargetRotation)
                return NormalizePlanarDirection(targetRotation * Vector3.forward, Vector3.forward);

            return NormalizePlanarDirection(lastFacingDirection, transform.forward);
        }

        void SnapVisualRootToFacing()
        {
            var direction = NormalizePlanarDirection(lastFacingDirection, transform.forward);
            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector3.forward;

            targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            visualTargetRotation = Quaternion.Euler(0f, targetYaw + visualYawOffset, 0f);
            hasVisualTargetRotation = true;
            modelRoot.rotation = visualTargetRotation;
        }

        static Vector3 NormalizePlanarDirection(Vector3 direction, Vector3 fallback)
        {
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (direction.sqrMagnitude > 0.0001f)
                return direction.normalized;

            fallback = Vector3.ProjectOnPlane(fallback, Vector3.up);
            if (fallback.sqrMagnitude > 0.0001f)
                return fallback.normalized;

            return Vector3.forward;
        }

        void RotatePhysicsRootIfNeeded()
        {
            if (UsesVisualRotationOnly() || !hasTargetRotation || moveDir.sqrMagnitude <= 0.0001f)
                return;

            var t = 1f - Mathf.Exp(-turnSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, t));
        }

        void RotateVisualRootIfNeeded()
        {
            if (!UsesVisualRotationOnly() || !hasVisualTargetRotation || moveDir.sqrMagnitude <= 0.0001f)
                return;

            var t = 1f - Mathf.Exp(-turnSpeed * Time.deltaTime);
            modelRoot.rotation = Quaternion.Slerp(modelRoot.rotation, visualTargetRotation, t);
            if (Quaternion.Angle(modelRoot.rotation, visualTargetRotation) <= 0.1f)
                modelRoot.rotation = visualTargetRotation;
        }

        bool UsesVisualRotationOnly()
        {
            return modelRoot && modelRoot != transform;
        }

        Transform FindRuntimeVisualRoot()
        {
            var wrapper = transform.Find(VisualFacingRootName);
            if (wrapper)
                return wrapper;

            Transform candidate = null;
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (!renderer || renderer.transform == transform) continue;

                var top = renderer.transform;
                while (top.parent && top.parent != transform)
                    top = top.parent;

                if (top == transform) continue;
                if (!candidate)
                {
                    candidate = top;
                    continue;
                }

                if (candidate != top)
                    return null;
            }

            if (candidate)
                return candidate;

            var childAnimator = GetComponentInChildren<Animator>(true);
            if (childAnimator && childAnimator.transform != transform)
                return childAnimator.transform;

            return null;
        }

        Transform EnsureRuntimeVisualFacingRoot(Transform visualRoot)
        {
            var wrapper = transform.Find(VisualFacingRootName);
            if (!wrapper)
            {
                var go = new GameObject(VisualFacingRootName);
                wrapper = go.transform;
                wrapper.SetParent(transform, false);
                wrapper.localPosition = Vector3.zero;
                wrapper.localRotation = Quaternion.identity;
                wrapper.localScale = Vector3.one;
            }

            if (visualRoot.parent != wrapper)
                visualRoot.SetParent(wrapper, false);

            return wrapper;
        }

        Transform GetMoveCameraTransform()
        {
            if (Camera.main) return Camera.main.transform;
            var followCamera = FindFirstObjectByType<ThirdPersonCamera>();
            return followCamera ? followCamera.transform : null;
        }

        bool IsGrounded()
        {
            if (!cap) return false;
            Vector3 worldCenter = transform.TransformPoint(cap.center);
            float halfLine = (cap.height * 0.5f) - cap.radius; // Y capsule
            Vector3 footCenter = worldCenter - transform.up * halfLine;
            float rayLen = cap.radius + groundCheckExtra;
            if (Physics.Raycast(footCenter + transform.up * 0.02f, -transform.up, out _, rayLen, groundMask, QueryTriggerInteraction.Ignore))
                return true;
            return false;
        }

        void SetLinearVelocity(Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        Vector3 GetLinearVelocity()
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        void ApplyNoFrictionMaterial()
        {
            if (!cap || cap.sharedMaterial) return;

            if (!noFrictionMaterial)
            {
                noFrictionMaterial = new PhysicsMaterial("VARCO_Player_NoFriction")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
            }

            cap.sharedMaterial = noFrictionMaterial;
        }

        void ConfigureRuntimeGroundAlignForStableY()
        {
            yAnchor = GetComponent<CharacterInitialYAnchor>();
            if (!yAnchor)
                yAnchor = gameObject.AddComponent<CharacterInitialYAnchor>();
            if (!yAnchor.HasStoredInitialY)
                yAnchor.CaptureCurrentYAsInitial();
            yAnchor.ConfigureForRole(isPlayer: true, usesNavMeshAgent: false, allowVerticalGameplayMotion: true);

            foreach (var align in GetComponentsInChildren<RuntimeGroundAlign>(true))
            {
                if (!align) continue;
                align.alignOnEnable = true;
                align.alignVisualChildrenOnly = true;
                align.continuous = false;
                align.useRootY = true;
                align.alignDuration = 0f;
                align.alignFramesAfterEnable = 12;
                align.footClearance = Mathf.Max(align.footClearance, 0.08f);
                align.maxCorrectionPerCall = Mathf.Max(align.maxCorrectionPerCall, 3f);
            }

            yAnchor.ApplyImmediate();
        }

        bool ShouldLockInitialY()
        {
            return yAnchor && yAnchor.lockRootYDuringMovement;
        }

        void ApplyInitialYLockIfNeeded()
        {
            if (!ShouldLockInitialY() || !rb)
                return;

            var position = rb.position;
            if (Mathf.Abs(position.y - yAnchor.InitialY) <= 0.001f)
                return;

            position.y = yAnchor.InitialY;
            rb.MovePosition(position);
        }

        void SetAnimatorBoolIfExists(int parameterHash, bool value)
        {
            if (!anim || !HasAnimatorBoolParameter(parameterHash))
                return;
            anim.SetBool(parameterHash, value);
        }

        bool HasAnimatorBoolParameter(int parameterHash)
        {
            if (!anim || !anim.runtimeAnimatorController)
                return false;

            foreach (var parameter in anim.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Bool
                    && Animator.StringToHash(parameter.name) == parameterHash)
                    return true;
            }

            return false;
        }
    }
}
