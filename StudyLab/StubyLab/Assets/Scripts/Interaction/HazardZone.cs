using System.Collections.Generic;
using UnityEngine;

namespace VARCO_Workshop
{
    /// <summary>
    /// 위험 구역 — 플레이어가 머무는 동안 초당 데미지.
    /// DeathZone(즉사)과 달리 DPS 방식으로 점진적 피해.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class HazardZone : MonoBehaviour
    {
        [Tooltip("초당 데미지")]
        [Min(1)] public int damagePerSecond = 15;

        [Tooltip("효과음 (반복 재생 안 함 — 입장 시 1회)")]
        public AudioClip enterClip;

        readonly Dictionary<PlayerHealth, float> damageRemainders = new Dictionary<PlayerHealth, float>();
        readonly Dictionary<PlayerHealth, int> processedFrames = new Dictionary<PlayerHealth, int>();

        void Reset()
        {
            var collider = GetComponent<Collider>();
            if (!collider)
                return;

            var mesh = collider as MeshCollider;
            if (mesh && !mesh.convex)
            {
                mesh.enabled = false;
                var box = GetComponent<BoxCollider>() ?? gameObject.AddComponent<BoxCollider>();
                box.enabled = true;
                box.isTrigger = true;
                if (box.size == Vector3.zero)
                    box.size = Vector3.one;
                return;
            }

            collider.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!TryGetPlayerHealth(other, out _)) return;
            if (enterClip) AudioSource.PlayClipAtPoint(enterClip, transform.position);
        }

        void OnTriggerStay(Collider other)
        {
            if (!TryGetPlayerHealth(other, out var ph)) return;
            if (processedFrames.TryGetValue(ph, out var frame) && frame == Time.frameCount)
                return;

            processedFrames[ph] = Time.frameCount;

            damageRemainders.TryGetValue(ph, out var remainder);
            var pendingDamage = remainder + damagePerSecond * Time.deltaTime;
            var damage = Mathf.FloorToInt(pendingDamage);
            damageRemainders[ph] = pendingDamage - damage;

            if (damage > 0)
                ph.TakeDamage(damage);
        }

        void OnTriggerExit(Collider other)
        {
            if (!TryGetPlayerHealth(other, out var ph)) return;
            damageRemainders.Remove(ph);
            processedFrames.Remove(ph);
        }

        static bool TryGetPlayerHealth(Collider other, out PlayerHealth health)
        {
            health = other.GetComponent<PlayerHealth>() ?? other.GetComponentInParent<PlayerHealth>();
            return health && (health.CompareTag("Player") || other.CompareTag("Player"));
        }
    }
}
