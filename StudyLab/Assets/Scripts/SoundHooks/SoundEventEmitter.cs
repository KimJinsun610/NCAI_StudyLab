using UnityEngine;

namespace VARCO_Workshop
{
    [RequireComponent(typeof(AudioSource))]
    public class SoundEventEmitter : MonoBehaviour
    {
        public SoundEventRegistry registry;
        AudioSource src;

        void Awake() => src = GetComponent<AudioSource>();

        public bool Play(string id)
        {
            if (registry == null) return false;
            if (registry.TryGet(id, out var clip, out var vol) && clip)
            {
                src.PlayOneShot(clip, vol);
                return true;
            }

            return false;
        }
    }
}
