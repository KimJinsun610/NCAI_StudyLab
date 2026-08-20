using UnityEngine;
using UnityEngine.AI;

namespace VARCO_Workshop
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyAI_NavMesh : MonoBehaviour
    {
        [Header("Chase")]
        public float detectionRange = 12f;
        public float stopDistance   = 1.2f;
        public float attackReach    = 1.7f;
        public float turnSpeed      = 12f;

        [Header("Attack")]
        public int   contactDamage  = 8;
        [Min(0.05f)]
        public float attackSpeed = 2f;

        [Header("Sound")]
        public SoundEventRegistry soundRegistry;
        public string attackSoundId = "sfx_enemy_attack";
        public AudioClip fallbackAttackSound;
        [Range(0f, 1f)] public float attackSoundVolume = 1f;

        [Header("Animation")]
        [Min(0.05f)]
        public float attackAnimationSpeed = 2f;
        [HideInInspector]
        public float contactInterval = 0.5f;

        NavMeshAgent agent;
        Transform player;
        PlayerHealth playerHealth;
        Collider ownCollider;
        Collider playerCollider;
        float nextDmg;
        float attackSpeedHoldUntil;
        float baseAnimatorSpeed = 1f;
        EnemyHealth hp;
        Animator anim;
        AudioSource audioSource;
        SoundEventEmitter soundEmitter;
        bool dead;
        bool attackSpeedApplied;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            anim = RuntimeAnimatorResolver.FindBestAnimator(gameObject);
            RuntimeAnimatorResolver.DisableDuplicateRootAnimator(gameObject, anim);
            if (anim)
            {
                anim.applyRootMotion = false;
                baseAnimatorSpeed = anim.speed;
            }
            ConfigureRuntimeGroundAlignForStableY();
            audioSource = GetComponent<AudioSource>();
            soundEmitter = GetComponent<SoundEventEmitter>();
            hp = GetComponent<EnemyHealth>();
            ownCollider = GetComponentInChildren<Collider>();
            hp.OnDeath += HandleDeath;
        }

        void OnDestroy()
        {
            if (hp) hp.OnDeath -= HandleDeath;
        }

        void Start()
        {
            AcquirePlayer();
            if (attackReach <= 0f) attackReach = 1.7f;
            if (stopDistance < 1.2f) stopDistance = 1.2f;
            if (attackSpeed <= 0f)
                attackSpeed = contactInterval > 0f ? 1f / contactInterval : 2f;
            contactInterval = GetAttackInterval();
            if (agent)
                agent.stoppingDistance = Mathf.Max(stopDistance, attackReach * 0.85f);
        }

        void Update()
        {
            UpdateAttackAnimationSpeed();
            if (dead) return;
            if (!player) AcquirePlayer();
            if (!player) return;

            var centerDistance = Vector3.Distance(transform.position, player.position);
            if (centerDistance > detectionRange)
            {
                StopMoving();
                return;
            }

            var edgeDistance = GetDistanceToPlayer();
            bool inAttackRange = edgeDistance <= Mathf.Max(stopDistance, attackReach);
            if (agent && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = inAttackRange;
                if (inAttackRange)
                    agent.ResetPath();
                else
                    agent.SetDestination(player.position);
            }

            if (anim) anim.SetBool(EnemyAnimParams.IsWalk, !inAttackRange);
            FacePlayer();

            if (inAttackRange && Time.time >= nextDmg)
            {
                if (playerHealth) playerHealth.TakeDamage(contactDamage);
                PlayAttackSound();
                StartAttackSpeed();
                if (anim) anim.SetTrigger(EnemyAnimParams.IsAttack);
                nextDmg = Time.time + GetAttackInterval();
            }
        }

        void AcquirePlayer()
        {
            var p = GameObject.FindWithTag("Player");
            if (!p) return;
            player = p.transform;
            playerHealth = p.GetComponent<PlayerHealth>();
            playerCollider = p.GetComponentInChildren<Collider>();
        }

        void StopMoving()
        {
            if (agent && agent.enabled && agent.isOnNavMesh)
                agent.ResetPath();
            if (anim) anim.SetBool(EnemyAnimParams.IsWalk, false);
        }

        float GetDistanceToPlayer()
        {
            if (!ownCollider || !playerCollider)
                return Vector3.Distance(transform.position, player.position);

            var playerPoint = playerCollider.ClosestPoint(ownCollider.bounds.center);
            var enemyPoint = ownCollider.ClosestPoint(playerPoint);
            playerPoint.y = 0f;
            enemyPoint.y = 0f;
            return Vector3.Distance(enemyPoint, playerPoint);
        }

        void FacePlayer()
        {
            var dir = player.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude <= 0.0001f) return;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(dir.normalized, Vector3.up),
                turnSpeed * Time.deltaTime);
        }

        void HandleDeath()
        {
            dead = true;
            RestoreAnimatorSpeed();
            if (agent)
            {
                agent.isStopped = true;
                agent.enabled = false;
            }
            if (anim) anim.SetTrigger(EnemyAnimParams.IsDead);
        }

        float GetAttackInterval()
        {
            return 1f / Mathf.Max(0.05f, attackSpeed);
        }

        bool InAttackState()
        {
            if (!anim || !anim.isInitialized) return false;
            var state = anim.GetCurrentAnimatorStateInfo(0);
            return state.IsName("Attack") && state.normalizedTime < 1f;
        }

        void StartAttackSpeed()
        {
            if (!anim) return;
            if (!attackSpeedApplied)
                baseAnimatorSpeed = anim.speed;
            anim.speed = Mathf.Max(0.05f, attackAnimationSpeed);
            attackSpeedHoldUntil = Time.time + 0.2f;
            attackSpeedApplied = true;
        }

        void UpdateAttackAnimationSpeed()
        {
            if (!attackSpeedApplied) return;
            if (Time.time < attackSpeedHoldUntil || InAttackState())
            {
                if (anim) anim.speed = Mathf.Max(0.05f, attackAnimationSpeed);
                return;
            }

            RestoreAnimatorSpeed();
        }

        void RestoreAnimatorSpeed()
        {
            if (!attackSpeedApplied) return;
            if (anim) anim.speed = baseAnimatorSpeed;
            attackSpeedApplied = false;
        }

        void PlayAttackSound()
        {
            if (soundEmitter != null && !string.IsNullOrEmpty(attackSoundId) && soundEmitter.Play(attackSoundId))
                return;

            if (audioSource == null && (fallbackAttackSound != null || soundRegistry != null))
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
            }

            if (audioSource == null)
                return;

            if (soundRegistry != null && soundRegistry.TryGet(attackSoundId, out var clip, out var volume) && clip != null)
            {
                audioSource.PlayOneShot(clip, volume * attackSoundVolume);
                return;
            }

            if (fallbackAttackSound != null)
                audioSource.PlayOneShot(fallbackAttackSound, attackSoundVolume);
        }

        void ConfigureRuntimeGroundAlignForStableY()
        {
            var anchor = GetComponent<CharacterInitialYAnchor>();
            if (!anchor)
                anchor = gameObject.AddComponent<CharacterInitialYAnchor>();
            if (!anchor.HasStoredInitialY)
                anchor.CaptureCurrentYAsInitial();
            anchor.ConfigureForRole(isPlayer: false, usesNavMeshAgent: true, allowVerticalGameplayMotion: false);

            foreach (var align in GetComponentsInChildren<RuntimeGroundAlign>(true))
            {
                if (!align) continue;
                align.alignOnEnable = true;
                align.alignVisualChildrenOnly = true;
                align.continuous = false;
                align.useRootY = true;
                align.alignDuration = 0f;
                align.alignFramesAfterEnable = 6;
                align.footClearance = Mathf.Max(align.footClearance, 0.05f);
                align.maxCorrectionPerCall = Mathf.Max(align.maxCorrectionPerCall, 2.5f);
            }

            anchor.ApplyImmediate();
        }
    }
}
