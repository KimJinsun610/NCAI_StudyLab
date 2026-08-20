using UnityEngine;
using UnityEngine.EventSystems;

namespace VARCO_Workshop
{
    public class PlayerAttack : MonoBehaviour
    {
        [Header("Combat")]
        public int   damage   = 15;
        public float range    = 1.5f;
        public float radius   = 0.4f;
        [Min(0.05f)]
        public float cooldown = 0.45f;

        [Header("Sound")]
        public SoundEventRegistry soundRegistry;
        public string attackSoundId = "sfx_player_attack";
        public AudioClip fallbackAttackSound;
        [Range(0f, 1f)] public float attackSoundVolume = 1f;

        [Header("Animation")]
        [Min(0.05f)]
        public float attackAnimationSpeed = 1f;
        [HideInInspector]
        [Tooltip("Base attack animation duration used to time the hit moment.")]
        [Min(0.05f)]
        public float animationDurationAtSpeedOne = 0.9f;
        [HideInInspector]
        public bool  lockMovementDuringAttack = true;
        [HideInInspector]
        [Tooltip("Movement lock duration at attack speed 1.0. The effective duration is adjusted by attackAnimationSpeed.")]
        public float movementLockTime = 0.45f;
        [HideInInspector]
        public bool consumeFirstMouseClickOnStart = true;
        [Tooltip("선택 키보드 공격 키입니다. 마우스 전용 공격은 None을 사용하세요.")]
        public KeyCode keyboardAttackKey = KeyCode.None;
        public LayerMask hitMask = ~0;

        float next;
        float lockUntil;
        float baseAnimatorSpeed = 1f;
        bool attackSpeedApplied;
        Animator anim;
        PlayerHealth health;
        PlayerController_ThirdPerson controller;
        AudioSource audioSource;
        SoundEventEmitter soundEmitter;
        SoundEventTrigger[] soundTriggers;
        bool attackImpactPending;
        bool attackImpactResolved;
        bool firstMouseClickConsumed;
        float attackImpactTime;
        Vector3 attackForward;

        const float AttackImpactNormalizedTime = 0.35f;

        public bool IsMovementLocked => lockMovementDuringAttack && Time.time < lockUntil;
        public float EffectiveAttackAnimationSpeed => Mathf.Max(0.05f, attackAnimationSpeed);

        void Awake()
        {
            health = GetComponent<PlayerHealth>();
            controller = GetComponent<PlayerController_ThirdPerson>();
            anim = RuntimeAnimatorResolver.FindBestAnimator(gameObject, controller ? controller.modelRoot : null);
            audioSource = GetComponent<AudioSource>();
            soundEmitter = GetComponent<SoundEventEmitter>();
            soundTriggers = GetComponents<SoundEventTrigger>();
            if (anim) baseAnimatorSpeed = anim.speed;
            if (attackAnimationSpeed <= 0f)
                attackAnimationSpeed = 1f;
        }

        void Update()
        {
            UpdateAttackAnimationSpeed();

            if (health != null && !health.IsAlive)
            {
                attackImpactPending = false;
                RestoreAnimatorSpeed();
                return;
            }

            ResolvePendingAttackImpact();

            if (Time.time < next) return;
            if (InAttackState()) return;
            if (WantsAttack())
            {
                StartAttackSpeed();
                if (anim) anim.SetTrigger(PlayerAnimParams.IsAttack);
                QueueAttackImpact();
                next = Time.time + cooldown;
                lockUntil = Time.time + GetScaledMovementLockTime();
            }
        }

        bool WantsAttack()
        {
            if (keyboardAttackKey != KeyCode.None && Input.GetKeyDown(keyboardAttackKey))
                return true;

            if (!Input.GetMouseButtonDown(0))
                return false;

            if (consumeFirstMouseClickOnStart && !firstMouseClickConsumed)
            {
                firstMouseClickConsumed = true;
                return false;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return false;

            return true;
        }

        void OnDisable()
        {
            attackImpactPending = false;
            RestoreAnimatorSpeed();
        }

        bool InAttackState()
        {
            if (!anim || !anim.isInitialized) return false;
            var s = anim.GetCurrentAnimatorStateInfo(0);
            return s.IsName("Attack") && s.normalizedTime < 1f;
        }

        float GetScaledMovementLockTime()
        {
            return Mathf.Min(cooldown, movementLockTime / EffectiveAttackAnimationSpeed);
        }

        float GetAttackImpactDelay()
        {
            var scaledDuration = animationDurationAtSpeedOne / EffectiveAttackAnimationSpeed;
            return Mathf.Clamp(scaledDuration * AttackImpactNormalizedTime, 0.04f, Mathf.Max(0.04f, cooldown * 0.65f));
        }

        void QueueAttackImpact()
        {
            attackForward = GetAttackForward();
            attackImpactTime = Time.time + GetAttackImpactDelay();
            attackImpactResolved = false;
            attackImpactPending = true;
        }

        void ResolvePendingAttackImpact()
        {
            if (!attackImpactPending || attackImpactResolved || Time.time < attackImpactTime)
                return;

            attackImpactResolved = true;
            attackImpactPending = false;
            PlayAttackSound();
            TryHit();
        }

        void StartAttackSpeed()
        {
            if (!anim) return;
            if (!attackSpeedApplied)
                baseAnimatorSpeed = anim.speed;
            anim.speed = EffectiveAttackAnimationSpeed;
            attackSpeedApplied = true;
        }

        void UpdateAttackAnimationSpeed()
        {
            if (!attackSpeedApplied) return;
            if (InAttackState() || Time.time < lockUntil)
            {
                if (anim) anim.speed = EffectiveAttackAnimationSpeed;
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

        void TryHit()
        {
            var origin = transform.position + Vector3.up * 0.85f;
            var forward = attackForward.sqrMagnitude > 0.0001f ? attackForward : GetAttackForward();
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            var center = origin + forward * (range * 0.5f);
            var searchRadius = Mathf.Max(radius, range * 0.55f);
            var hits = Physics.OverlapSphere(center, searchRadius, hitMask, QueryTriggerInteraction.Ignore);
            float bestScore = float.MaxValue;
            EnemyHealth best = null;

            foreach (var hit in hits)
            {
                if (!hit || hit.transform.root == transform.root) continue;
                var enemy = hit.GetComponentInParent<EnemyHealth>();
                if (!enemy || enemy.CurrentHP <= 0) continue;

                var targetPoint = hit.ClosestPoint(origin);
                var toTarget = targetPoint - origin;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > range * range) continue;

                var angle = Vector3.Angle(forward, toTarget.normalized);
                if (angle > 80f) continue;

                float score = toTarget.sqrMagnitude + angle * 0.01f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = enemy;
                }
            }

            if (best)
                best.TakeDamage(damage);
        }

        Vector3 GetAttackForward()
        {
            if (controller != null)
            {
                var controllerForward = controller.GetPlanarFacingForward();
                if (controllerForward.sqrMagnitude > 0.0001f)
                    return controllerForward;
            }

            Transform forwardSource = null;
            if (anim != null)
                forwardSource = anim.transform;
            else
                forwardSource = transform;

            var forward = Vector3.ProjectOnPlane(forwardSource.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude > 0.0001f)
                return forward;

            return Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        }

        void PlayAttackSound()
        {
            if (soundEmitter != null && !string.IsNullOrEmpty(attackSoundId) && soundEmitter.Play(attackSoundId))
                return;

            if (soundTriggers == null || soundTriggers.Length == 0)
                soundTriggers = GetComponents<SoundEventTrigger>();
            foreach (var trigger in soundTriggers)
            {
                if (trigger != null && trigger.eventId == attackSoundId)
                {
                    trigger.Play(ignorePlayOnce: true);
                    return;
                }
            }

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

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
    }
}
