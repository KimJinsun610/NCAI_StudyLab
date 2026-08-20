using UnityEngine;

namespace VARCO_Workshop
{
    public enum SoundTriggerMode
    {
        OnStart,
        OnEnable,
        OnTriggerEnter,
        OnCollisionEnter,
        OnKeyDown,
        OnMouseDown,
        OnDisable
    }

    [RequireComponent(typeof(AudioSource))]
    public class SoundEventTrigger : MonoBehaviour
    {
        public SoundEventRegistry registry;
        public string eventId = "sfx_event";
        public AudioClip fallbackClip;
        public SoundTriggerMode triggerMode = SoundTriggerMode.OnStart;
        public bool onlyPlayer = true;
        public bool playOnce;
        public KeyCode key = KeyCode.None;
        [Range(0f, 1f)] public float volumeMultiplier = 1f;
        public float cooldown = 0.05f;

        AudioSource source;
        bool played;
        float lastPlayTime = -999f;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
        }

        void OnEnable()
        {
            if (triggerMode == SoundTriggerMode.OnEnable)
                Play();
        }

        void Start()
        {
            if (triggerMode == SoundTriggerMode.OnStart)
                Play();
        }

        void Update()
        {
            if (triggerMode == SoundTriggerMode.OnKeyDown && key != KeyCode.None && Input.GetKeyDown(key))
                Play();
        }

        void OnMouseDown()
        {
            if (triggerMode == SoundTriggerMode.OnMouseDown)
                Play();
        }

        void OnDisable()
        {
            if (triggerMode == SoundTriggerMode.OnDisable)
                Play();
        }

        void OnTriggerEnter(Collider other)
        {
            if (triggerMode != SoundTriggerMode.OnTriggerEnter)
                return;
            if (onlyPlayer && !other.CompareTag("Player"))
                return;
            Play();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (triggerMode != SoundTriggerMode.OnCollisionEnter)
                return;
            if (onlyPlayer && !collision.collider.CompareTag("Player"))
                return;
            Play();
        }

        public void Play()
        {
            Play(ignorePlayOnce: false);
        }

        public void Play(bool ignorePlayOnce)
        {
            if (!ignorePlayOnce && playOnce && played)
                return;
            if (Time.time - lastPlayTime < cooldown)
                return;

            played = true;
            lastPlayTime = Time.time;

            if (registry != null && registry.TryGet(eventId, out var clip, out var volume) && clip != null)
            {
                source.PlayOneShot(clip, volume * volumeMultiplier);
                return;
            }

            if (fallbackClip != null)
                source.PlayOneShot(fallbackClip, volumeMultiplier);
        }
    }
}
