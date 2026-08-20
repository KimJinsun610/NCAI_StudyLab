using System.Collections;
using UnityEngine;

namespace VARCO_Workshop
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class BreakawayPlatform : MonoBehaviour
    {
        public float breakDelay = 0.5f;
        public float respawnDelay = 2.5f;
        public bool respawn = true;
        public bool reactToPlayerOnly = true;

        Renderer[] renderers;
        Collider[] colliders;
        Coroutine breakRoutine;

        void Awake()
        {
            CacheParts();
        }

        void Reset()
        {
            var collider = GetComponent<Collider>();
            if (collider)
                collider.isTrigger = false;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (collision != null)
                TryBreak(collision.collider);
        }

        void OnTriggerEnter(Collider other)
        {
            TryBreak(other);
        }

        public void BreakNow()
        {
            if (breakRoutine == null)
                breakRoutine = StartCoroutine(BreakRoutine());
        }

        public void ResetPlatform()
        {
            if (breakRoutine != null)
            {
                StopCoroutine(breakRoutine);
                breakRoutine = null;
            }

            SetPartsEnabled(true);
        }

        void TryBreak(Collider other)
        {
            if (breakRoutine != null || !other)
                return;

            if (reactToPlayerOnly && !IsPlayer(other))
                return;

            BreakNow();
        }

        bool IsPlayer(Collider other)
        {
            return other.CompareTag("Player") || other.GetComponentInParent<PlayerController_Platform>() || other.GetComponentInParent<PlayerHealth>();
        }

        IEnumerator BreakRoutine()
        {
            yield return new WaitForSeconds(Mathf.Max(0f, breakDelay));
            SetPartsEnabled(false);

            if (respawn)
            {
                yield return new WaitForSeconds(Mathf.Max(0f, respawnDelay));
                SetPartsEnabled(true);
            }

            breakRoutine = null;
        }

        void CacheParts()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
        }

        void SetPartsEnabled(bool enabled)
        {
            if (renderers == null || colliders == null)
                CacheParts();

            foreach (var renderer in renderers)
            {
                if (renderer)
                    renderer.enabled = enabled;
            }

            foreach (var collider in colliders)
            {
                if (collider)
                    collider.enabled = enabled;
            }
        }
    }
}
