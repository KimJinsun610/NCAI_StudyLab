using UnityEngine;
using UnityEngine.SceneManagement;

namespace VARCO_Workshop
{
    /// <summary>탈출구 등에 닿아 GameManager.OnClear가 발생하면 자동으로 나타나는 게임클리어 패널.
    /// 씬에 활성 상태로 배치해두면(자기 자신을 Start에서 숨김) 됩니다. 버튼의 OnClick에 RestartScene()을 연결하세요.</summary>
    public class GameClearPanel : MonoBehaviour
    {
        void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnClear += Show;

            gameObject.SetActive(false);
        }

        void Show()
        {
            gameObject.SetActive(true);

            // 카메라가 커서를 잠가둔 상태일 수 있어(ThirdPersonCamera) 버튼을 누르려면 커서를 풀어줘야 합니다.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // 다시 플레이하기를 누르기 전까지 플레이어가 움직이지 못하게 고정합니다.
            PlayerLockUtility.FreezePlayer();
        }

        /// <summary>게임클리어 버튼에 연결 — 현재 씬을 처음부터 다시 시작합니다.</summary>
        public void RestartScene()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
