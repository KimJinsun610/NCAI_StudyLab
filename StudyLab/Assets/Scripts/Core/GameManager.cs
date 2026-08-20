using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VARCO_Workshop
{
    public enum GameState
    {
        Ready,
        Playing,
        Clear,
        GameOver
    }

    /// <summary>Global workshop game state: Ready, Playing, Clear, GameOver.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Optional Profile")]
        public GameProfile profile;

        [Header("Optional Result Scenes")]
        public string clearSceneName = "Clear";
        public string gameOverSceneName = "GameOver";
        [Tooltip("Load Clear/GameOver scenes only if they are available in the active Build Profile or shared scene list.")]
        public bool loadResultScenes = true;
        [Tooltip("Keep the game result state available after loading Clear/GameOver scenes.")]
        public bool persistAcrossScenes = true;

        [SerializeField] GameState state = GameState.Ready;

        public GameState State => state;
        public event Action<GameState> OnStateChanged;
        public event Action OnClear;
        public event Action OnGameOver;

        int killCount;
        public int KillCount => killCount;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (persistAcrossScenes)
                DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (state == GameState.Ready)
                SetState(GameState.Playing);
        }

        public void SetState(GameState s)
        {
            state = s;
            OnStateChanged?.Invoke(s);
        }

        public void TriggerClear()
        {
            if (state == GameState.Clear || state == GameState.GameOver)
                return;

            SetState(GameState.Clear);
            OnClear?.Invoke();
            TryLoadResultScene(clearSceneName, "Clear");
        }

        public void TriggerGameOver()
        {
            if (state == GameState.Clear || state == GameState.GameOver)
                return;

            SetState(GameState.GameOver);
            OnGameOver?.Invoke();
            TryLoadResultScene(gameOverSceneName, "GameOver");
        }

        public void RegisterKill()
        {
            if (state != GameState.Playing)
                return;

            killCount++;
        }

        void TryLoadResultScene(string sceneName, string stateName)
        {
            if (!loadResultScenes || string.IsNullOrWhiteSpace(sceneName))
                return;

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning(
                    $"[VARCO GameManager] {stateName} state reached, but scene '{sceneName}' is not in the active Build Profile/shared scene list. Keeping the current scene.");
                return;
            }

            SceneManager.LoadScene(sceneName);
        }
    }
}
