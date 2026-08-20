using UnityEngine;

namespace VARCO_Workshop
{
    /// <summary>자석 상호작용이 가능한 오브젝트 마커. 힘은 이 컴포넌트가 아니라 <see cref="MagnetAimController"/>가 가합니다.</summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class MagnetTarget : MonoBehaviour
    {
        [Header("자석 반응")]
        [Tooltip("당기기(Pull) 힘을 받을 수 있는지")]
        public bool acceptsPull = true;
        [Tooltip("밀어내기(Push) 힘을 받을 수 있는지")]
        public bool acceptsPush = true;

        [Header("범위 / 세기")]
        [Tooltip("이 거리 이내에서만 조준·부착 가능(플레이어 기준)")]
        public float maxRange = 8f;
        [Tooltip("매 FixedUpdate 가해지는 힘의 세기(질량이 클수록 체감 가속은 작아짐)")]
        public float forceStrength = 40f;
        [Tooltip("켜면 forceStrength를 그대로 가속도로 사용(질량 무시), 끄면 AddForce로 질량이 반영됨")]
        public bool ignoreMassForForce = false;

        [Header("하이라이트")]
        [Tooltip("강조 시 원래 색상에 곱해줄 틴트")]
        public Color highlightTint = new Color(0.4f, 0.9f, 1f, 1f);
        [Tooltip("자기 자신뿐 아니라 자식의 Renderer도 모두 강조 대상에 포함")]
        public bool includeChildRenderers = true;

        public Rigidbody Body { get; private set; }
        public bool IsHighlighted { get; private set; }

        Renderer[] renderers;
        MaterialPropertyBlock mpb;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            renderers = includeChildRenderers
                ? GetComponentsInChildren<Renderer>(true)
                : new[] { GetComponent<Renderer>() };
            mpb = new MaterialPropertyBlock();
        }

        public void SetHighlighted(bool on)
        {
            if (IsHighlighted == on)
                return;

            IsHighlighted = on;
            if (renderers == null)
                return;

            foreach (var r in renderers)
            {
                if (!r) continue;

                r.GetPropertyBlock(mpb);
                if (on)
                {
                    var baseColor = Color.white;
                    if (r.sharedMaterial)
                    {
                        if (r.sharedMaterial.HasProperty(BaseColorId))
                            baseColor = r.sharedMaterial.GetColor(BaseColorId);
                        else if (r.sharedMaterial.HasProperty(ColorId))
                            baseColor = r.sharedMaterial.GetColor(ColorId);
                    }

                    var tinted = baseColor * highlightTint;
                    mpb.SetColor(BaseColorId, tinted);
                    mpb.SetColor(ColorId, tinted);
                }
                else
                {
                    mpb.Clear();
                }

                r.SetPropertyBlock(mpb);
            }
        }

        void OnDisable()
        {
            if (IsHighlighted)
                SetHighlighted(false);
        }
    }
}
