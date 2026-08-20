using UnityEngine;

namespace VARCO_Workshop
{
    /// <summary>씬 로드 직후 태그/이름으로 참조를 찾는 보조(선택). 인스펙터 연결이 어려울 때만 사용.</summary>
    public class SceneBootstrap : MonoBehaviour
    {
        [Header("자동 찾기(씬에 있을 때)")]
        public bool findMainCamera   = true;
        public bool findGameManager  = true;
        [Tooltip("Player 태그")]
        public bool findPlayer       = true;

        void Start()
        {
            if (findGameManager && GameManager.Instance == null)
                Debug.LogWarning("[SceneBootstrap] GameManager를 찾을 수 없습니다. 씬에 빈 오브젝트 + GameManager를 두세요.");
            if (findPlayer && GameObject.FindWithTag("Player") == null)
                Debug.LogWarning("[SceneBootstrap] 'Player' 태그를 가진 캐릭터를 배치하세요.");
            if (findMainCamera && Camera.main == null)
                Debug.LogWarning("[SceneBootstrap] MainCamera 태그가 메인 카메라에 없습니다.");
        }
    }
}
