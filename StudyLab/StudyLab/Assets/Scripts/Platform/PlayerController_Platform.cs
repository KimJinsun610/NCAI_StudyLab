using UnityEngine;

namespace VARCO_Workshop
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController_Platform : MonoBehaviour, IExternalMoveOverride
    {
        public float moveSpeed  = 6f;
        public float runMultiplier = 1.35f;
        public KeyCode runKey = KeyCode.LeftShift;
        public float jumpForce  = 8f;
        public float gravity    = -25f;
        public bool lockZAxis = false;
        public bool useCameraSpace = true;
        public float turnSpeed = 12f;
        public Transform modelRoot;
        public bool respawnAtStartOnFall = true;
        public float fallRespawnY = -10f;

        [Header("QA Input")]
        [HideInInspector] public bool qaInputOverrideActive;
        [HideInInspector] public Vector2 qaMoveInput;
        [HideInInspector] public bool qaRunInput;

        CharacterController cc;
        Animator anim;
        Vector3 vel;
        Vector3 externalVelocity;
        Vector3 lastHorizontal;
        bool isRunning;
        Vector3 startPosition;
        Quaternion startRotation;
        Vector3 respawnPosition;
        Quaternion respawnRotation;
        bool hasRespawnPoint;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            anim = RuntimeAnimatorResolver.FindBestAnimator(gameObject, modelRoot);
            RuntimeAnimatorResolver.DisableDuplicateRootAnimator(gameObject, anim);
            if (!modelRoot && anim)
                modelRoot = anim.transform;
            if (anim)
                anim.applyRootMotion = false;
            ConfigureRuntimeGroundAlignForStableY();
        }

        void Start()
        {
            startPosition = transform.position;
            startRotation = transform.rotation;
            SetRespawnPoint(startPosition, startRotation);
        }

        void Update()
        {
            if (respawnAtStartOnFall && transform.position.y <= fallRespawnY)
            {
                RespawnAtCheckpoint();
                return;
            }

            var rawInput = qaInputOverrideActive
                ? new Vector3(qaMoveInput.x, 0f, qaMoveInput.y)
                : new Vector3(Input.GetAxisRaw("Horizontal"), 0f, lockZAxis ? 0f : Input.GetAxisRaw("Vertical"));
            var input = Vector3.ClampMagnitude(rawInput, 1f);

            var runPressed = qaInputOverrideActive
                ? qaRunInput
                : (Input.GetKey(runKey) || Input.GetKey(KeyCode.RightShift));
            isRunning = input.sqrMagnitude > 0.0001f && runPressed;
            var horizontal = ResolveMoveDirection(input) * moveSpeed * (isRunning ? Mathf.Max(1f, runMultiplier) : 1f);
            lastHorizontal = horizontal;
            if (cc.isGrounded && vel.y < 0f)
                vel.y = -2f;

            if (!qaInputOverrideActive && cc.isGrounded && Input.GetButtonDown("Jump"))
                vel.y = jumpForce;

            vel.y += gravity * Time.deltaTime;
            var move = (horizontal + externalVelocity + Vector3.up * vel.y) * Time.deltaTime;
            cc.Move(move);
            externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, 18f * Time.deltaTime);

            if (!lockZAxis && horizontal.sqrMagnitude > 0.0001f)
            {
                var look = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
                var root = modelRoot ? modelRoot : transform;
                root.rotation = Quaternion.Slerp(root.rotation, look, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
            }

            UpdateAnimator();
        }

        void UpdateAnimator()
        {
            if (!anim) return;
            SetAnimatorBoolIfExists(PlayerAnimParams.IsWalk, lastHorizontal.sqrMagnitude > 0.0001f && cc.isGrounded);
            SetAnimatorBoolIfExists(PlayerAnimParams.IsRun, isRunning && cc.isGrounded);
            SetAnimatorBoolIfExists(PlayerAnimParams.IsJump, !cc.isGrounded);
        }

        Vector3 ResolveMoveDirection(Vector3 input)
        {
            if (input.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            if (lockZAxis)
                return Vector3.right * input.x;

            var cam = Camera.main;
            if (!useCameraSpace || !cam)
                return input.normalized;

            var forward = cam.transform.forward;
            var right = cam.transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            return Vector3.ClampMagnitude(right * input.x + forward * input.z, 1f);
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

        public void SetRespawnPoint(Vector3 position, Quaternion rotation)
        {
            respawnPosition = position;
            respawnRotation = rotation;
            hasRespawnPoint = true;
            Checkpoint.SetLastPosition(position);
        }

        public void RespawnAtCheckpoint()
        {
            var targetPosition = hasRespawnPoint ? respawnPosition : startPosition;
            var targetRotation = hasRespawnPoint ? respawnRotation : startRotation;

            vel = Vector3.zero;
            externalVelocity = Vector3.zero;
            lastHorizontal = Vector3.zero;

            if (cc)
            {
                cc.enabled = false;
                transform.SetPositionAndRotation(targetPosition, targetRotation);
                cc.enabled = true;
            }
            else
            {
                transform.SetPositionAndRotation(targetPosition, targetRotation);
            }

            UpdateAnimator();
        }

        public void Bounce(float upwardVelocity)
        {
            vel.y = Mathf.Max(vel.y, Mathf.Abs(upwardVelocity));
        }

        public void AddExternalVelocity(Vector3 velocity)
        {
            velocity.y = 0f;
            externalVelocity += velocity;
        }

        public void Bounce(float upwardVelocity, Vector3 horizontalVelocity)
        {
            Bounce(upwardVelocity);
            AddExternalVelocity(horizontalVelocity);
        }

        void ConfigureRuntimeGroundAlignForStableY()
        {
            var anchor = GetComponent<CharacterInitialYAnchor>();
            if (!anchor)
                anchor = gameObject.AddComponent<CharacterInitialYAnchor>();
            if (!anchor.HasStoredInitialY)
                anchor.CaptureCurrentYAsInitial();
            anchor.ConfigureForRole(isPlayer: true, usesNavMeshAgent: false, allowVerticalGameplayMotion: true);

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

            anchor.ApplyImmediate();
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
