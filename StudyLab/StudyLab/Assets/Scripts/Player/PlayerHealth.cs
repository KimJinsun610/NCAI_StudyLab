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

        [Header("사망 시 래그돌")]
        [Tooltip("켜져 있으면 사망 시 애니메이션/이동 컨트롤을 끄고 물리로 힘없이 쓰러지게 합니다.")]
        public bool ragdollOnDeath = true;
        [Tooltip("쓰러질 때 살짝 밀어줄 힘 — 매번 다른 방향으로 넘어지게 랜덤 적용")]
        public float ragdollToppleForce = 1.5f;
        public float ragdollToppleTorque = 3f;

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
                if (ragdollOnDeath)
                    ApplyRagdollFall();
                OnDeath?.Invoke();
                if (GameManager.Instance)
                    GameManager.Instance.TriggerGameOver();
            }
            else
            {
                PlaySound(hitSoundId, fallbackHitSound);
            }
        }

        /// <summary>완전한 본별(per-bone) 래그돌은 아니고, 애니메이터/이동 컨트롤을 끄고 몸통 전체를
        /// 물리에 맡겨서 힘없이 쓰러지는 것처럼 보이게 하는 간단한 방식입니다.</summary>
        void ApplyRagdollFall()
        {
            var anim = RuntimeAnimatorResolver.FindBestAnimator(gameObject);
            if (anim) anim.enabled = false;

            var thirdPerson = GetComponent<PlayerController_ThirdPerson>();
            if (thirdPerson) thirdPerson.enabled = false;
            var platform = GetComponent<PlayerController_Platform>();
            if (platform) platform.enabled = false;
            var magnet = GetComponent<MagnetAimController>();
            if (magnet) magnet.enabled = false;

            var cc = GetComponent<CharacterController>();
            if (cc)
            {
                // CharacterController는 그 자체로는 물리 충돌 상대가 안 되므로, 없으면 캡슐 콜라이더를 대신 붙여줍니다.
                if (!GetComponent<CapsuleCollider>())
                {
                    var capsule = gameObject.AddComponent<CapsuleCollider>();
                    capsule.height = cc.height;
                    capsule.radius = cc.radius;
                    capsule.center = cc.center;
                }
                cc.enabled = false;
            }

            var rb = GetComponent<Rigidbody>();
            if (!rb) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.None;

            var randomDir = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)).normalized;
            rb.AddForce(randomDir * ragdollToppleForce, ForceMode.VelocityChange);
            rb.AddTorque(new Vector3(
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-1f, 1f)) * ragdollToppleTorque, ForceMode.VelocityChange);
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
