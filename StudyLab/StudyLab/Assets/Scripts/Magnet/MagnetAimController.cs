using UnityEngine;

namespace VARCO_Workshop
{
    /// <summary>화면 중앙 조준으로 <see cref="MagnetTarget"/>을 찾아 강조 표시하고, 좌클릭으로 선택/릴리즈합니다.
    /// 선택 중에는 플레이어 이동이 잠기고, WASD/ZX/QE로 선택된 오브젝트를 염동력처럼 직접 조작합니다.
    /// W=밀기/S=당기기(플레이어 화면 기준 앞뒤), A/D=좌우(플레이어 화면 기준), Z/X=위/아래, Q=X축 회전/E=Z축 회전(시계방향).</summary>
    [DisallowMultipleComponent]
    public class MagnetAimController : MonoBehaviour
    {
        [Header("조준")]
        [Tooltip("화면 중앙 기준 최대 조준 거리")]
        public float aimRange = 10f;
        [Tooltip("조준 레이캐스트가 검사할 레이어")]
        public LayerMask aimMask = ~0;

        [Header("입력")]
        [Tooltip("선택/릴리즈에 쓰일 마우스 버튼(0 = 좌클릭)")]
        public int selectMouseButton = 0;
        [Tooltip("선택된 오브젝트의 이동 속도(m/s). W=밀기, S=당기기, A/D=좌우, Z/X=위/아래")]
        public float moveSpeed = 4f;
        [Tooltip("Q(X축)/E(Z축) 회전 속도(도/초, 시계방향)")]
        public float rotateSpeed = 90f;

        [Header("범위")]
        [Tooltip("선택된 오브젝트가 이 거리를 넘어서면 안전을 위해 자동으로 릴리즈됩니다")]
        public float autoDetachRange = 12f;

        [Header("자석 능력 (아이템 습득)")]
        [Tooltip("꺼져 있으면 조준·강조·선택 등 자석 기능이 전혀 동작하지 않습니다. 아이템 습득 시 GrantMagnetAbility()가 이 값을 켭니다.")]
        public bool hasMagnetAbility = false;
        [Tooltip("능력 습득 시 장착할 자석 모델(선택). 비워두면 시각적 변화 없이 기능만 켜집니다.")]
        public GameObject magnetMeshPrefab;
        [Tooltip("자석 모델을 붙일 위치(예: 손 뼈대). 비워두면 플레이어 자기 자신에 붙입니다.")]
        public Transform magnetMeshAttachPoint;
        public Vector3 magnetMeshLocalPosition;
        public Vector3 magnetMeshLocalEulerAngles;

        public MagnetTarget CurrentAimTarget { get; private set; }
        public MagnetTarget AttachedTarget { get; private set; }

        Camera cam;
        IExternalMoveOverride playerMover;
        Animator anim;
        bool attachedGravityWasEnabled;
        GameObject equippedMagnetMeshInstance;

        void Awake()
        {
            cam = Camera.main;
            playerMover = GetComponent<IExternalMoveOverride>();
            anim = RuntimeAnimatorResolver.FindBestAnimator(gameObject);
            CrosshairUI.EnsureExists();
        }

        /// <summary>아이템 픽업 등에서 호출 — 자석 기능을 켜고, 설정된 모델이 있으면 장착합니다.</summary>
        public void GrantMagnetAbility()
        {
            hasMagnetAbility = true;
            EquipMagnetMeshIfNeeded();
        }

        void EquipMagnetMeshIfNeeded()
        {
            if (!magnetMeshPrefab || equippedMagnetMeshInstance)
                return;

            var parent = magnetMeshAttachPoint ? magnetMeshAttachPoint : transform;
            equippedMagnetMeshInstance = Instantiate(magnetMeshPrefab, parent);
            equippedMagnetMeshInstance.transform.localPosition = magnetMeshLocalPosition;
            equippedMagnetMeshInstance.transform.localEulerAngles = magnetMeshLocalEulerAngles;
        }

        void Update()
        {
            if (!hasMagnetAbility)
                return;

            if (!cam)
                cam = Camera.main;
            if (!cam)
                return;

            UpdateAimTarget();

            if (Input.GetMouseButtonDown(selectMouseButton))
                HandleSelectClick();
        }

        void UpdateAimTarget()
        {
            // 오브젝트를 선택 중일 때는 입력이 그 오브젝트 조작으로 전환되므로 조준을 다시 스캔하지 않습니다.
            if (AttachedTarget)
                return;

            MagnetTarget hitTarget = null;
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out var hit, aimRange, aimMask, QueryTriggerInteraction.Ignore))
            {
                var candidate = hit.collider.GetComponentInParent<MagnetTarget>();
                if (candidate && Vector3.Distance(transform.position, candidate.transform.position) <= candidate.maxRange)
                    hitTarget = candidate;
            }

            if (hitTarget == CurrentAimTarget)
                return;

            if (CurrentAimTarget)
                CurrentAimTarget.SetHighlightState(MagnetTarget.HighlightState.None);

            CurrentAimTarget = hitTarget;

            if (CurrentAimTarget)
                CurrentAimTarget.SetHighlightState(MagnetTarget.HighlightState.Aimable);
        }

        void HandleSelectClick()
        {
            if (AttachedTarget)
            {
                Release();
                return;
            }

            if (!CurrentAimTarget)
                return;

            AttachedTarget = CurrentAimTarget;
            AttachedTarget.SetHighlightState(MagnetTarget.HighlightState.Selected);

            var rb = AttachedTarget.Body;
            attachedGravityWasEnabled = rb.useGravity;
            rb.useGravity = false;
            SetBodyVelocity(rb, Vector3.zero);

            if (playerMover != null)
                playerMover.SetQaMoveInput(Vector2.zero, false);

            if (anim) anim.SetBool(PlayerAnimParams.IsAttack, true);
        }

        void Release()
        {
            if (!AttachedTarget)
                return;

            var rb = AttachedTarget.Body;
            rb.useGravity = attachedGravityWasEnabled;

            AttachedTarget.SetHighlightState(MagnetTarget.HighlightState.None);
            AttachedTarget = null;

            if (playerMover != null)
                playerMover.ClearQaMoveInput();

            if (anim) anim.SetBool(PlayerAnimParams.IsAttack, false);
        }

        void FixedUpdate()
        {
            if (!AttachedTarget)
                return;

            var rb = AttachedTarget.Body;
            var toTarget = rb.position - transform.position;
            var dist = toTarget.magnitude;
            if (dist > autoDetachRange || dist > AttachedTarget.maxRange)
            {
                Release();
                return;
            }

            var camT = cam.transform;
            var forward = Vector3.ProjectOnPlane(camT.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(camT.right, Vector3.up).normalized;

            var push = Input.GetKey(KeyCode.W) ? 1f : 0f;
            var pull = Input.GetKey(KeyCode.S) ? 1f : 0f;
            var goLeft = Input.GetKey(KeyCode.A) ? 1f : 0f;
            var goRight = Input.GetKey(KeyCode.D) ? 1f : 0f;
            var goUp = Input.GetKey(KeyCode.Z) ? 1f : 0f;
            var goDown = Input.GetKey(KeyCode.X) ? 1f : 0f;

            var moveDir = Vector3.up * (goUp - goDown)
                        + right * (goRight - goLeft)
                        + forward * (push - pull);
            if (moveDir.sqrMagnitude > 1f)
                moveDir.Normalize();

            // 무거울수록(weight) 이동·회전이 느려짐 — 완전히 굼떠지지 않게 최소치는 남겨둡니다.
            var weightScale = Mathf.Clamp(1f / AttachedTarget.weight, 0.2f, 1f);

            DriveTowardVelocity(rb, moveDir * moveSpeed * weightScale);

            // 시계방향 회전: Q = X축, E = Z축 (월드 기준, 각각 양의 오일러각이 시계방향)
            var rotateX = Input.GetKey(KeyCode.Q) ? rotateSpeed * weightScale * Time.fixedDeltaTime : 0f;
            var rotateZ = Input.GetKey(KeyCode.E) ? rotateSpeed * weightScale * Time.fixedDeltaTime : 0f;
            if (rotateX != 0f || rotateZ != 0f)
            {
                var delta = Quaternion.Euler(rotateX, 0f, rotateZ);
                rb.MoveRotation(delta * rb.rotation);
            }
        }

        static void SetBodyVelocity(Rigidbody rb, Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        static Vector3 GetBodyVelocity(Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        /// <summary>속도를 직접 덮어쓰지 않고, 목표 속도에 도달하는 데 필요한 힘(F = m·Δv/Δt)을 매 스텝 가합니다.
        /// 자유롭게 움직일 땐 속도를 즉시 덮어쓰는 것과 체감상 동일하지만, 같은 스텝에 다른 Rigidbody와
        /// 충돌 반작용력이 발생하면 그 힘과 정상적으로 합산되므로 더 무거운 물체를 그대로 뚫고 밀지 못합니다
        /// (기존에는 velocity를 매 프레임 강제로 덮어써서 무게와 무관하게 밀고 들어가는 문제가 있었습니다).</summary>
        static void DriveTowardVelocity(Rigidbody rb, Vector3 targetVelocity)
        {
            var velocityError = targetVelocity - GetBodyVelocity(rb);
            rb.AddForce(velocityError * rb.mass / Time.fixedDeltaTime, ForceMode.Force);
        }

        void OnDisable()
        {
            if (CurrentAimTarget && CurrentAimTarget != AttachedTarget)
                CurrentAimTarget.SetHighlightState(MagnetTarget.HighlightState.None);
            if (AttachedTarget)
                Release();

            CurrentAimTarget = null;
        }
    }
}
