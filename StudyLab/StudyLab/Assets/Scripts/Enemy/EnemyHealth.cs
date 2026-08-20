using System;
using System.Collections;
using UnityEngine;

namespace VARCO_Workshop
{
    public class EnemyHealth : MonoBehaviour
    {
        [Header("HP")]
        public int maxHP = 40;

        [Header("Sound")]
        public SoundEventRegistry soundRegistry;
        public string hitSoundId = "sfx_enemy_hit";
        public AudioClip fallbackHitSound;
        public string deathSoundId = "sfx_enemy_death";
        public AudioClip fallbackDeathSound;

        [Header("Death")]
        [Min(0.1f)]
        public float fallbackDestroyDelay = 2f;
        [Header("Drop")]
        public GameObject healthDropPrefab;
        [Range(0f, 1f)]
        public float healthDropChance = 1f;
        [Min(1)]
        public int healthDropHealAmount = 25;
        public Vector3 healthDropOffset = new Vector3(0f, 0.25f, 0f);

        int hp;
        bool dead;
        bool dropped;
        AudioSource audioSource;
        SoundEventEmitter soundEmitter;

        static readonly string[] DeathStateNames =
        {
            "Death", "Dead", "Die", "Dying",
            "death", "dead", "die", "dying"
        };

        public int CurrentHP => hp;
        public int MaxHP => maxHP;
        public event Action OnDeath;
        public event Action<int, int> OnHPChanged;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            soundEmitter = GetComponent<SoundEventEmitter>();
        }

        void Start()
        {
            CombatHealthUI.EnsureExists();
            hp = maxHP;
            OnHPChanged?.Invoke(hp, maxHP);
        }

        public void TakeDamage(int dmg)
        {
            if (dead) return;

            hp = Mathf.Max(0, hp - Mathf.Abs(dmg));
            OnHPChanged?.Invoke(hp, maxHP);
            if (hp == 0)
            {
                dead = true;
                PlaySound(deathSoundId, fallbackDeathSound);
                OnDeath?.Invoke();
                DropHealthPickup();
                if (WaveManager.Instance) WaveManager.Instance.RegisterEnemyDied(this);
                if (GameManager.Instance) GameManager.Instance.RegisterKill();
                StartCoroutine(DestroyAfterDeathAnimation());
            }
            else
            {
                PlaySound(hitSoundId, fallbackHitSound);
            }
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

        void DropHealthPickup()
        {
            if (dropped || healthDropChance <= 0f) return;
            if (UnityEngine.Random.value > healthDropChance) return;

            dropped = true;
            GameObject drop;
            var spawnPosition = transform.position + healthDropOffset;
            if (healthDropPrefab)
            {
                drop = Instantiate(healthDropPrefab, spawnPosition, Quaternion.identity);
                drop.name = $"{healthDropPrefab.name}_HealthPickup";
            }
            else
            {
                drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                drop.name = "Dropped_HealthPickup";
                drop.transform.position = spawnPosition;
                drop.transform.localScale = Vector3.one * 0.45f;
                var renderer = drop.GetComponent<Renderer>();
                if (renderer)
                    renderer.material.color = new Color(0.95f, 0.1f, 0.16f);
            }

            ConfigureDroppedHealthPickup(drop);
        }

        void ConfigureDroppedHealthPickup(GameObject drop)
        {
            if (!drop) return;
            RemoveAnimators(drop);

            var collider = drop.GetComponent<Collider>();
            if (!collider)
                collider = drop.AddComponent<SphereCollider>();
            if (!collider)
                return;
            collider.isTrigger = true;

            FitDropCollider(drop, collider);

            var pickup = drop.GetComponent<HealthPickup>() ?? drop.AddComponent<HealthPickup>();
            if (!pickup)
                return;

            pickup.healAmount = healthDropHealAmount;
            pickup.destroyOnPickup = true;
        }

        static void RemoveAnimators(GameObject drop)
        {
            var animators = drop.GetComponentsInChildren<Animator>(true);
            foreach (var animator in animators)
            {
                if (animator)
                    Destroy(animator);
            }
        }

        static void FitDropCollider(GameObject drop, Collider collider)
        {
            if (!drop || !collider) return;
            var renderers = drop.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            var localCenter = drop.transform.InverseTransformPoint(bounds.center);
            var localSize = drop.transform.InverseTransformVector(bounds.size);
            localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));

            if (collider is BoxCollider box)
            {
                box.center = localCenter;
                box.size = Vector3.Max(localSize, Vector3.one * 0.5f);
            }
            else if (collider is SphereCollider sphere)
            {
                sphere.center = localCenter;
                sphere.radius = Mathf.Max(localSize.x, localSize.y, localSize.z, 0.5f) * 0.5f;
            }
        }

        IEnumerator DestroyAfterDeathAnimation()
        {
            var animator = RuntimeAnimatorResolver.FindBestAnimator(gameObject);
            if (!animator || !animator.isActiveAndEnabled)
            {
                yield return new WaitForSeconds(fallbackDestroyDelay);
                Destroy(gameObject);
                yield break;
            }

            yield return null;

            float timeout = Mathf.Max(0.1f, fallbackDestroyDelay);
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (IsDeathState(state))
                {
                    while (state.normalizedTime < 1f && elapsed < timeout)
                    {
                        elapsed += Time.deltaTime;
                        yield return null;
                        state = animator.GetCurrentAnimatorStateInfo(0);
                    }

                    Destroy(gameObject);
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            Destroy(gameObject);
        }

        static bool IsDeathState(AnimatorStateInfo state)
        {
            foreach (var stateName in DeathStateNames)
            {
                if (state.IsName(stateName) || state.IsTag(stateName))
                    return true;
            }

            return false;
        }
    }
}
