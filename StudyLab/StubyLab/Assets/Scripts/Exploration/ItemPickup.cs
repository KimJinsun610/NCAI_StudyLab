using UnityEngine;

namespace VARCO_Workshop
{
    [RequireComponent(typeof(Collider))]
    public class ItemPickup : MonoBehaviour
    {
        public AudioClip pickupClip;

        void Reset() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider o)
        {
            var c = o.GetComponent<CollectibleCounter>() ?? o.GetComponentInParent<CollectibleCounter>();
            var health = o.GetComponent<PlayerHealth>() ?? o.GetComponentInParent<PlayerHealth>();
            var isPlayer = o.CompareTag("Player")
                || (c != null && c.CompareTag("Player"))
                || (health != null && health.CompareTag("Player"));
            if (!isPlayer) return;

            if (c) c.Add(1);
            if (pickupClip) AudioSource.PlayClipAtPoint(pickupClip, transform.position);
            Destroy(gameObject);
        }
    }
}
