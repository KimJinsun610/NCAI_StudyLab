using UnityEngine;
using UnityEngine.SceneManagement;

namespace VARCO_Workshop
{
    /// <summary>플레이어 체력이 0이 되어 GameManager.OnGameOver가 발생하면 자동으로 나타나는 게임오버 패널.
    /// 씬에 활성 상태로 배치해두면(자기 자신을 Start에서 숨김) 됩니다. 버튼의 OnClick에 RestartScene()을 연결하세요.</summary>
    public class GameOverPanel : MonoBehaviour
    {
        void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver += Show;

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

        /// <summary>게임오버 버튼에 연결 — 현재 씬을 처음부터 다시 시작합니다.</summary>
        public void RestartScene()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
