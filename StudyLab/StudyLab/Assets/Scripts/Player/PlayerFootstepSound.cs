using UnityEngine;

namespace VARCO_Workshop
{
    public class PlayerFootstepSound : MonoBehaviour
    {
        [Header("Sound")]
        public SoundEventRegistry soundRegistry;
        public string footstepSoundId = "sfx_player_footstep";
        public AudioClip fallbackFootstepSound;
        [Range(0f, 1f)] public float volume = 0.75f;

        [Header("Timing")]
        [Min(0.05f)] public float stepInterval = 0.38f;
        [Min(0.01f)] public float minMoveSpeed = 0.25f;
        public LayerMask groundMask = ~0;
        public float groundCheckExtra = 0.12f;

        Rigidbody rb;
        CharacterController characterController;
        CapsuleCollider capsule;
        AudioSource audioSource;
        SoundEventEmitter soundEmitter;
        PlayerHealth health;
        float nextStepTime;
        Vector3 lastPosition;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            characterController = GetComponent<CharacterController>();
            capsule = GetComponent<CapsuleCollider>();
            audioSource = GetComponent<AudioSource>();
            soundEmitter = GetComponent<SoundEventEmitter>();
            health = GetComponent<PlayerHealth>();
            lastPosition = transform.position;
        }

        void Update()
        {
            if (health != null && !health.IsAlive)
            {
                lastPosition = transform.position;
                return;
            }

            var horizontalSpeed = GetHorizontalSpeed();
            if (horizontalSpeed >= minMoveSpeed && IsGrounded() && Time.time >= nextStepTime)
            {
                PlayFootstep();
                nextStepTime = Time.time + stepInterval;
            }

            lastPosition = transform.position;
        }

        float GetHorizontalSpeed()
        {
            if (characterController != null)
            {
                var velocity = characterController.velocity;
                velocity.y = 0f;
                return velocity.magnitude;
            }

            if (rb != null)
            {
#if UNITY_6000_0_OR_NEWER
                var velocity = rb.linearVelocity;
#else
                var velocity = rb.velocity;
#endif
                velocity.y = 0f;
                return velocity.magnitude;
            }

            var delta = transform.position - lastPosition;
            delta.y = 0f;
            return delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        }

        bool IsGrounded()
        {
            if (characterController != null)
                return characterController.isGrounded;

            if (capsule != null)
            {
                var worldCenter = transform.TransformPoint(capsule.center);
                var halfLine = Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);
                var footCenter = worldCenter - transform.up * halfLine;
                var rayLength = capsule.radius + groundCheckExtra;
                return Physics.Raycast(footCenter + transform.up * 0.03f, -transform.up, rayLength, groundMask, QueryTriggerInteraction.Ignore);
            }

            return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.3f + groundCheckExtra, groundMask, QueryTriggerInteraction.Ignore);
        }

        void PlayFootstep()
        {
            if (soundEmitter != null && !string.IsNullOrEmpty(footstepSoundId) && soundEmitter.Play(footstepSoundId))
                return;

            if (audioSource == null && (fallbackFootstepSound != null || soundRegistry != null))
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
            }

            if (audioSource == null)
                return;

            if (soundRegistry != null && soundRegistry.TryGet(footstepSoundId, out var clip, out var registryVolume) && clip != null)
            {
                audioSource.PlayOneShot(clip, registryVolume * volume);
                return;
            }

            if (fallbackFootstepSound != null)
                audioSource.PlayOneShot(fallbackFootstepSound, volume);
        }
    }
}
