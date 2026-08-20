using System;
using UnityEngine;

namespace VARCO_Workshop
{
    public class PlayerHealth : MonoBehaviour
    {
        [Header("HP")]
        public int maxHP = 100;

        [Header("Sound")]
        public SoundEventRegistry soundRegistry;
        public string hitSoundId = "sfx_player_hit";
        public AudioClip fallbackHitSound;
        public string deathSoundId = "sfx_game_over";
        public AudioClip fallbackDeathSound;

        int        hp;
        public  int  CurrentHP  => hp;
        public  bool IsAlive   => hp > 0;
        public event Action<int, int> OnHPChanged; // current, max
        public event Action         OnDeath;

        AudioSource audioSource;
        SoundEventEmitter soundEmitter;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            soundEmitter = GetComponent<SoundEventEmitter>();
        }

        void Start()
        {
            CombatHealthUI.EnsureExists();
            if (Application.isPlaying) hp = maxHP;
            OnHPChanged?.Invoke(hp, maxHP);
        }

        public void TakeDamage(int dmg)
        {
            if (hp <= 0) return;

            hp = Mathf.Max(0, hp - Mathf.Abs(dmg));
            OnHPChanged?.Invoke(hp, maxHP);
            if (hp == 0)
            {
                PlaySound(deathSoundId, fallbackDeathSound);
                var controller = GetComponent<PlayerController_ThirdPerson>();
                var a = RuntimeAnimatorResolver.FindBestAnimator(gameObject, controller ? controller.modelRoot : null);
                if (a) a.SetTrigger(PlayerAnimParams.IsDead);
                OnDeath?.Invoke();
                if (GameManager.Instance)
                    GameManager.Instance.TriggerGameOver();
            }
            else
            {
                PlaySound(hitSoundId, fallbackHitSound);
            }
        }

        public void Heal(int x)
        {
            hp = Mathf.Min(maxHP, hp + x);
            OnHPChanged?.Invoke(hp, maxHP);
        }

        void PlaySound(string id, AudioClip fallbackClip)
        {
            if (soundEmitter != null && !string.IsNullOrEmpty(id) && soundEmitter.Play(id))
                return;

            if (audioSource == null && (fallbackClip != null || soundRegistry != null))
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
            }

            if (audioSource == null)
                return;

            if (soundRegistry != null && soundRegistry.TryGet(id, out var clip, out var volume) && clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
                return;
            }

            if (fallbackClip != null)
                audioSource.PlayOneShot(fallbackClip);
        }
    }
}
