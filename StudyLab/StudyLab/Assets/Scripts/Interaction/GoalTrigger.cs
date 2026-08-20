using UnityEngine;

namespace VARCO_Workshop
{
    [RequireComponent(typeof(Collider))]
    public class GoalTrigger : MonoBehaviour
    {
        [Tooltip("0이면 즉시 클리어, &gt;0이면 ItemPickup 카운트와 맞을 때(탐험)")]
        public int requiredItems;
        public AudioClip clearClip;

        bool done;

        void Reset() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider o)
        {
            if (done) return;
            var c = o.GetComponent<CollectibleCounter>() ?? o.GetComponentInParent<CollectibleCounter>();
            var health = o.GetComponent<PlayerHealth>() ?? o.GetComponentInParent<PlayerHealth>();
            var isPlayer = o.CompareTag("Player")
                || (c != null && c.CompareTag("Player"))
                || (health != null && health.CompareTag("Player"));

            if (!isPlayer) return;

            if (requiredItems > 0)
            {
                if (c == null || c.Count < requiredItems) return;
            }
            done = true;
            Debug.Log($"[VARCO] '{o.name}'이(가) '{name}'에 닿아 클리어되었습니다.", this);
            if (clearClip) AudioSource.PlayClipAtPoint(clearClip, transform.position);
            if (GameManager.Instance) GameManager.Instance.TriggerClear();
        }
    }
}
