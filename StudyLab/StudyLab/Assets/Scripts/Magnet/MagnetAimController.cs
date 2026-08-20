using UnityEngine;

namespace VARCO_Workshop
{
    /// <summary>화면 중앙 조준으로 <see cref="MagnetTarget"/>을 찾아 강조 표시하고, 좌클릭으로 선택/릴리즈합니다.
    /// 선택 중에는 플레이어 이동이 잠기고, WASD/ZX/QE로 선택된 오브젝트를 염동력처럼 직접 조작합니다.
    /// W/S=위/아래, A/D=좌우(플레이어 화면 기준), Z=당기기/X=밀기(플레이어 화면 기준 앞뒤), Q=X축 회전/E=Z축 회전(시계방향).</summary>
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
        [Tooltip("선택된 오브젝트의 이동 속도(m/s). W/S=위/아래, A/D=좌우, Z=당기기, X=밀기")]
        public float moveSpeed = 4f;
        [Tooltip("Q(X축)/E(Z축) 회전 속도(도/초, 시계방향)")]
        public float rotateSpeed = 90f;

        [Header("범위")]
        [Tooltip("선택된 오브젝트가 이 거리를 넘어서면 안전을 위해 자동으로 릴리즈됩니다")]
        public float autoDetachRange = 12f;

        public MagnetTarget CurrentAimTarget { get; private set; }
        public MagnetTarget AttachedTarget { get; private set; }

        Camera cam;
        PlayerController_ThirdPerson playerMover;
        bool attachedGravityWasEnabled;

        void Awake()
        {
            cam = Camera.main;
            playerMover = GetComponent<PlayerController_ThirdPerson>();
        }

        void Update()
        {
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

            if (playerMover)
                playerMover.SetQaMoveInput(Vector2.zero, false);
        }

        void Release()
        {
            if (!AttachedTarget)
                return;

            var rb = AttachedTarget.Body;
            rb.useGravity = attachedGravityWasEnabled;

            AttachedTarget.SetHighlightState(MagnetTarget.HighlightState.None);
            AttachedTarget = null;

            if (playerMover)
                playerMover.ClearQaMoveInput();
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

            var goUp = Input.GetKey(KeyCode.W) ? 1f : 0f;
            var goDown = Input.GetKey(KeyCode.S) ? 1f : 0f;
            var goLeft = Input.GetKey(KeyCode.A) ? 1f : 0f;
            var goRight = Input.GetKey(KeyCode.D) ? 1f : 0f;
            var pull = Input.GetKey(KeyCode.Z) ? 1f : 0f;
            var push = Input.GetKey(KeyCode.X) ? 1f : 0f;

            var moveDir = Vector3.up * (goUp - goDown)
                        + right * (goRight - goLeft)
                        + forward * (push - pull);
            if (moveDir.sqrMagnitude > 1f)
                moveDir.Normalize();

            SetBodyVelocity(rb, moveDir * moveSpeed);

            // 시계방향 회전: Q = X축, E = Z축 (월드 기준, 각각 양의 오일러각이 시계방향)
            var rotateX = Input.GetKey(KeyCode.Q) ? rotateSpeed * Time.fixedDeltaTime : 0f;
            var rotateZ = Input.GetKey(KeyCode.E) ? rotateSpeed * Time.fixedDeltaTime : 0f;
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
