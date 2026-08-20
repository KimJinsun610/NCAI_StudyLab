using UnityEngine;
using UnityEngine.AI;

namespace VARCO_Workshop
{
    [DefaultExecutionOrder(-2000)]
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class NavMeshEditPlayAlign : MonoBehaviour
    {
        [Tooltip("Max distance used by NavMesh.SamplePosition.")]
        public float sampleMaxDistance = 4f;

        [Tooltip("Align to the nearest NavMesh position when Play starts.")]
        public bool alignInPlayMode = true;

        [Tooltip("Align automatically in edit mode. Auto-build keeps this off to avoid dirtying scenes after save.")]
        public bool alignInEditMode;

        void Awake()
        {
            if (Application.isPlaying && alignInPlayMode)
                Align(logWarning: false);
        }

        void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && alignInEditMode)
                Align(logWarning: false);
#endif
        }

        [ContextMenu("Align To NavMesh (Sample + Warp)")]
        public void Align()
        {
            Align(logWarning: true);
        }

        public bool Align(bool logWarning)
        {
            if (!NavMesh.SamplePosition(transform.position, out var hit, sampleMaxDistance, NavMesh.AllAreas))
            {
                if (logWarning)
                    Debug.LogWarning("[NavMeshEditPlayAlign] NavMesh not found near " + name, this);
                return false;
            }

            var pos = new Vector3(hit.position.x, hit.position.y, hit.position.z);
            if (TryGetComponent<NavMeshAgent>(out var agent))
            {
                if (agent.enabled && agent.Warp(pos))
                    return true;

                var wasEnabled = agent.enabled;
                agent.enabled = false;
                transform.position = pos;
                agent.enabled = wasEnabled;
                if (wasEnabled)
                    agent.Warp(pos);
            }
            else
            {
                transform.position = pos;
            }

            return true;
        }
    }
}
