using UnityEngine;

namespace VARCO_Workshop
{
    /// <summary>
    /// Optional countdown timer. When time reaches zero, GameManager.TriggerGameOver() is called.
    /// UI can read Remaining and NormalizedRemaining.
    /// </summary>
    public class CountdownTimer : MonoBehaviour
    {
        [Min(1f)] public float totalSeconds = 60f;
        [Tooltip("When true, count down only while GameManager is in Playing state.")]
        public bool pauseWhenNotPlaying = true;

        float _remaining;
        bool _fired;

        public float Remaining => _remaining;
        public float NormalizedRemaining => _remaining / totalSeconds;
        public bool IsExpired => _fired;

        void Start()
        {
            _remaining = totalSeconds;
        }

        void Update()
        {
            if (_fired) return;
            if (pauseWhenNotPlaying && GameManager.Instance?.State != GameState.Playing) return;

            _remaining -= Time.deltaTime;
            if (_remaining > 0) return;

            _remaining = 0f;
            _fired = true;
            if (GameManager.Instance) GameManager.Instance.TriggerGameOver();
        }

        /// <summary>Add time from bonuses or items.</summary>
        public void AddTime(float seconds) => _remaining = Mathf.Min(_remaining + seconds, totalSeconds);
    }
}
