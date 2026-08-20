using UnityEngine;
using UnityEngine.AI;

namespace VARCO_Workshop
{
    [DefaultExecutionOrder(3000)]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class CharacterInitialYAnchor : MonoBehaviour
    {
        [Header("Scene Initial Y")]
        [Tooltip("Edit mode에서 Transform Y를 Initial Y로 계속 동기화합니다. 씬에서 캐릭터 Y를 옮기면 이 값이 플레이 기준 높이가 됩니다.")]
        public bool syncInitialYFromSceneTransform = true;
        public float initialY;
        [SerializeField] bool hasStoredInitialY;

        [Header("Runtime")]
        public bool lockRootYDuringMovement = true;
        public bool applyToNavMeshAgentBaseOffset = true;
        public bool zeroVerticalVelocity = true;
        [Min(0.1f)] public float navMeshSampleDistance = 20f;

        [Header("Visual")]
        public bool makeVisualAlignUseInitialY = true;
        public float visualFootClearance = 0.06f;

        Rigidbody rb;
        CharacterController characterController;
        NavMeshAgent agent;

        public bool HasStoredInitialY => hasStoredInitialY;
        public float InitialY => initialY;

        void Reset()
        {
            CaptureCurrentYAsInitial();
        }

        void Awake()
        {
            CacheComponents();
            if (!hasStoredInitialY)
                CaptureCurrentYAsInitial();
            ConfigureVisualAlignment();
            ApplyImmediate();
        }

        void OnEnable()
        {
            CacheComponents();
            if (Application.isPlaying)
            {
                if (!hasStoredInitialY)
                    CaptureCurrentYAsInitial();
                ConfigureVisualAlignment();
                ApplyImmediate();
            }
        }

        void Update()
        {
            if (Application.isPlaying)
                return;
            if (!syncInitialYFromSceneTransform)
                return;

            if (!hasStoredInitialY || Mathf.Abs(initialY - transform.position.y) > 0.0001f)
                CaptureCurrentYAsInitial();

            ConfigureVisualAlignment();
        }

        void FixedUpdate()
        {
            if (Application.isPlaying)
                ApplyImmediate();
        }

        void LateUpdate()
        {
            if (Application.isPlaying)
                ApplyImmediate();
        }

        [ContextMenu("Capture Current Transform Y")]
        public void CaptureCurrentYAsInitial()
        {
            initialY = transform.position.y;
            hasStoredInitialY = true;
        }

        public void ConfigureForRole(bool isPlayer, bool usesNavMeshAgent, bool allowVerticalGameplayMotion)
        {
            if (!hasStoredInitialY)
                CaptureCurrentYAsInitial();

            lockRootYDuringMovement = !allowVerticalGameplayMotion;
            applyToNavMeshAgentBaseOffset = usesNavMeshAgent;
            zeroVerticalVelocity = isPlayer && !allowVerticalGameplayMotion;
            makeVisualAlignUseInitialY = true;
            visualFootClearance = isPlayer ? Mathf.Max(visualFootClearance, 0.08f) : Mathf.Max(visualFootClearance, 0.05f);
            ConfigureVisualAlignment();
        }

        public void ApplyImmediate()
        {
            CacheComponents();
            ConfigureVisualAlignment();

            if (agent && applyToNavMeshAgentBaseOffset)
            {
                ApplyNavMeshAgentOffset();
                return;
            }

            if (!lockRootYDuringMovement)
                return;

            var position = transform.position;
            if (Mathf.Abs(position.y - initialY) <= 0.0001f)
                return;

            position.y = initialY;
            if (rb && Application.isPlaying)
            {
                rb.position = position;
                transform.position = position;
                if (zeroVerticalVelocity)
                    SetVerticalVelocity(0f);
            }
            else if (characterController && characterController.enabled)
            {
                characterController.enabled = false;
                transform.position = position;
                characterController.enabled = true;
            }
            else
            {
                transform.position = position;
            }
        }

        void ApplyNavMeshAgentOffset()
        {
            if (!agent)
                return;

            if (NavMesh.SamplePosition(transform.position, out var hit, navMeshSampleDistance, NavMesh.AllAreas))
                agent.baseOffset = initialY - hit.position.y;

            if (zeroVerticalVelocity)
                SetVerticalVelocity(0f);
        }

        void ConfigureVisualAlignment()
        {
            if (!makeVisualAlignUseInitialY)
                return;

            var aligns = GetComponentsInChildren<RuntimeGroundAlign>(true);
            foreach (var align in aligns)
            {
                if (!align)
                    continue;

                align.alignOnEnable = true;
                align.alignVisualChildrenOnly = true;
                align.continuous = false;
                align.useRootY = true;
                align.alignDuration = 0f;
                align.alignFramesAfterEnable = Mathf.Max(align.alignFramesAfterEnable, 6);
                align.footClearance = Mathf.Max(align.footClearance, visualFootClearance);
            }
        }

        void CacheComponents()
        {
            if (!rb)
                rb = GetComponent<Rigidbody>();
            if (!characterController)
                characterController = GetComponent<CharacterController>();
            if (!agent)
                agent = GetComponent<NavMeshAgent>();
        }

        void SetVerticalVelocity(float y)
        {
            if (!rb)
                return;

#if UNITY_6000_0_OR_NEWER
            var velocity = rb.linearVelocity;
            velocity.y = y;
            rb.linearVelocity = velocity;
#else
            var velocity = rb.velocity;
            velocity.y = y;
            rb.velocity = velocity;
#endif
        }
    }
}
