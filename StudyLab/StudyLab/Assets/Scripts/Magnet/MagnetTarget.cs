using UnityEngine;

namespace VARCO_Workshop
{
    /// <summary>염동력(자석)으로 조작 가능한 오브젝트 마커. 실제 이동/회전은 여기서 하지 않고
    /// <see cref="MagnetAimController"/>가 선택 중인 동안 이 오브젝트의 Rigidbody를 직접 조작합니다.</summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class MagnetTarget : MonoBehaviour
    {
        public enum HighlightState { None, Aimable, Selected }

        [Header("범위")]
        [Tooltip("이 거리 이내에서만 조준·선택 가능(플레이어 기준)")]
        public float maxRange = 8f;

        [Header("무게")]
        [Tooltip("무거울수록 자석으로 옮기고 돌릴 때 느려지고, 가벼운 물체(플레이어 포함)에 잘 안 밀립니다. " +
            "Rigidbody.mass에 그대로 반영됩니다.")]
        [Min(0.1f)] public float weight = 1f;

        [Header("하이라이트")]
        [Tooltip("조준만 하고 아직 선택 전일 때 표시할 색(원래 색상에 곱연산)")]
        public Color aimTint = new Color(0.4f, 0.9f, 1f, 1f);
        [Tooltip("선택되어 자석 기능이 적용 중일 때 표시할 색(원래 색상에 곱연산)")]
        public Color selectedTint = new Color(1f, 0.85f, 0.1f, 1f);
        [Tooltip("자기 자신뿐 아니라 자식의 Renderer도 모두 강조 대상에 포함")]
        public bool includeChildRenderers = true;

        public Rigidbody Body { get; private set; }
        public HighlightState CurrentHighlight { get; private set; } = HighlightState.None;

        Renderer[] renderers;
        MaterialPropertyBlock mpb;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.mass = weight; // 물리 충돌(밀림)도 무게에 맞게 반응하도록 Rigidbody.mass를 weight로 통일
            renderers = includeChildRenderers
                ? GetComponentsInChildren<Renderer>(true)
                : new[] { GetComponent<Renderer>() };
            mpb = new MaterialPropertyBlock();

            if (renderers == null || renderers.Length == 0 || System.Array.TrueForAll(renderers, r => !r))
            {
                Debug.LogWarning($"[VARCO 자석] '{name}'에서 강조 표시할 Renderer를 찾지 못했습니다. " +
                    "includeChildRenderers 설정과 자식 오브젝트의 MeshRenderer/SkinnedMeshRenderer 유무를 확인하세요.", this);
                return;
            }

            foreach (var r in renderers)
            {
                if (!r || !r.sharedMaterial) continue;
                if (!r.sharedMaterial.HasProperty(BaseColorId) && !r.sharedMaterial.HasProperty(ColorId))
                    Debug.LogWarning($"[VARCO 자석] '{r.name}'의 머티리얼('{r.sharedMaterial.shader.name}')에 " +
                        "_BaseColor/_Color 프로퍼티가 없어 하이라이트 색이 적용되지 않을 수 있습니다.", r);
            }

            if (!GetComponentInChildren<Collider>())
                Debug.LogWarning($"[VARCO 자석] '{name}'에 Collider가 없어 조준 레이캐스트에 걸리지 않습니다. " +
                    "임포트한 모델이라면 BoxCollider 등을 직접 추가해야 조준·강조·선택이 동작합니다.", this);
        }

        public void SetHighlightState(HighlightState state)
        {
            if (CurrentHighlight == state)
                return;

            CurrentHighlight = state;
            Debug.Log($"[VARCO 자석] '{name}' 하이라이트 상태 -> {state}", this);

            if (renderers == null)
                return;

            foreach (var r in renderers)
            {
                if (!r) continue;

                r.GetPropertyBlock(mpb);
                if (state == HighlightState.None)
                {
                    mpb.Clear();
                }
                else
                {
                    var tint = state == HighlightState.Selected ? selectedTint : aimTint;
                    var baseColor = Color.white;
                    if (r.sharedMaterial)
                    {
                        if (r.sharedMaterial.HasProperty(BaseColorId))
                            baseColor = r.sharedMaterial.GetColor(BaseColorId);
                        else if (r.sharedMaterial.HasProperty(ColorId))
                            baseColor = r.sharedMaterial.GetColor(ColorId);
                    }

                    var tinted = baseColor * tint;
                    mpb.SetColor(BaseColorId, tinted);
                    mpb.SetColor(ColorId, tinted);
                }

                r.SetPropertyBlock(mpb);
            }
        }

        void OnDisable()
        {
            if (CurrentHighlight != HighlightState.None)
                SetHighlightState(HighlightState.None);
        }
    }
}
