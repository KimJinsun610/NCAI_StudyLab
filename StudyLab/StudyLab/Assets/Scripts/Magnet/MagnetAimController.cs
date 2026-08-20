using UnityEngine;

namespace VARCO_Workshop
{
    /// <summary>화면 중앙 조준으로 <see cref="MagnetTarget"/>을 찾아 강조 표시하고,
    /// 좌클릭 1회로 당기기/밀기 힘을 토글 부착합니다. 플레이어 이동 컨트롤러와는 독립적으로 동작합니다.</summary>
    [DisallowMultipleComponent]
    public class MagnetAimController : MonoBehaviour
    {
        public enum Polarity { Pull, Push }

        [Header("조준")]
        [Tooltip("화면 중앙 기준 최대 조준 거리")]
        public float aimRange = 10f;
        [Tooltip("조준 레이캐스트가 검사할 레이어")]
        public LayerMask aimMask = ~0;

        [Header("입력")]
        [Tooltip("당기기/밀기 극성 전환 키")]
        public KeyCode togglePolarityKey = KeyCode.Q;
        [Tooltip("부착/해제에 쓰일 마우스 버튼(0 = 좌클릭)")]
        public int attachMouseButton = 0;

        [Header("부착 상태")]
        public Polarity currentPolarity = Polarity.Pull;
        [Tooltip("부착된 대상이 이 거리를 벗어나면 자동 해제")]
        public float autoDetachRange = 12f;
        [Tooltip("당기는 중 이 거리보다 가까워지면 자동 해제(끼임 방지)")]
        public float minPullDistance = 1.2f;

        public MagnetTarget CurrentAimTarget { get; private set; }
        public MagnetTarget AttachedTarget { get; private set; }
        public Polarity AttachedPolarity { get; private set; }

        Camera cam;

        void Awake()
        {
            cam = Camera.main;
        }

        void Update()
        {
            if (!cam)
                cam = Camera.main;
            if (!cam)
                return;

            UpdateAimTarget();

            if (Input.GetKeyDown(togglePolarityKey))
                currentPolarity = currentPolarity == Polarity.Pull ? Polarity.Push : Polarity.Pull;

            if (Input.GetMouseButtonDown(attachMouseButton))
                HandleAttachClick();
        }

        void UpdateAimTarget()
        {
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

            // 부착 중인 대상의 강조는 조준이 벗어나도 계속 유지합니다.
            if (CurrentAimTarget && CurrentAimTarget != AttachedTarget)
                CurrentAimTarget.SetHighlighted(false);

            CurrentAimTarget = hitTarget;

            if (CurrentAimTarget && CurrentAimTarget != AttachedTarget)
                CurrentAimTarget.SetHighlighted(true);
        }

        void HandleAttachClick()
        {
            if (AttachedTarget)
            {
                // 이미 부착 중: 같은 대상을 다시 조준 중일 때만 해제. 다른 대상을 조준 중이면 먼저 해제해야 함.
                if (AttachedTarget == CurrentAimTarget)
                    Detach();
                return;
            }

            if (!CurrentAimTarget)
                return;

            var target = CurrentAimTarget;
            var polarityAccepted = currentPolarity == Polarity.Pull ? target.acceptsPull : target.acceptsPush;
            if (!polarityAccepted)
                return;

            AttachedTarget = target;
            AttachedPolarity = currentPolarity;
            AttachedTarget.SetHighlighted(true);
        }

        void Detach()
        {
            if (!AttachedTarget)
                return;

            var stillAimed = AttachedTarget == CurrentAimTarget;
            AttachedTarget.SetHighlighted(stillAimed);
            AttachedTarget = null;
        }

        void FixedUpdate()
        {
            if (!AttachedTarget)
                return;

            var toTarget = AttachedTarget.transform.position - transform.position;
            var dist = toTarget.magnitude;

            if (dist > autoDetachRange || dist > AttachedTarget.maxRange
                || (AttachedPolarity == Polarity.Pull && dist < minPullDistance))
            {
                Detach();
                return;
            }

            if (dist < 0.001f)
                return;

            var towardPlayer = -toTarget / dist;
            var awayFromPlayer = toTarget / dist;
            var forceDir = AttachedPolarity == Polarity.Pull ? towardPlayer : awayFromPlayer;

            var rb = AttachedTarget.Body;
            var strength = AttachedTarget.forceStrength;
            if (AttachedTarget.ignoreMassForForce)
                rb.AddForce(forceDir * strength, ForceMode.Acceleration);
            else
                rb.AddForce(forceDir * strength, ForceMode.Force);
        }

        void OnDisable()
        {
            if (CurrentAimTarget && CurrentAimTarget != AttachedTarget)
                CurrentAimTarget.SetHighlighted(false);
            if (AttachedTarget)
                AttachedTarget.SetHighlighted(false);

            AttachedTarget = null;
            CurrentAimTarget = null;
        }
    }
}
