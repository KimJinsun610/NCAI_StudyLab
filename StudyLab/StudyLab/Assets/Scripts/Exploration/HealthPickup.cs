using UnityEngine;

namespace VARCO_Workshop
{
    [RequireComponent(typeof(Collider))]
    public class HealthPickup : MonoBehaviour
    {
        [Min(1)] public int healAmount = 25;
        public bool destroyOnPickup = true;
        public AudioClip pickupClip;

        void Reset() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            var ph = other.GetComponent<PlayerHealth>() ?? other.GetComponentInParent<PlayerHealth>();
            if (ph == null) return;
            if (!ph.CompareTag("Player")) return;
            if (ph.CurrentHP >= ph.maxHP) return;

            ph.Heal(healAmount);
            VARCOGameHUD.ShowNotice("체력을 회복했습니다. +" + healAmount);
            if (pickupClip) AudioSource.PlayClipAtPoint(pickupClip, transform.position);
            if (destroyOnPickup) Destroy(gameObject);
        }
    }
}
