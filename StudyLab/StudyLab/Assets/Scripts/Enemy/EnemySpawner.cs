using UnityEngine;
using UnityEngine.AI;

namespace VARCO_Workshop
{
    /// <summary>단일 지점/간단 반복용 스포너(수업 데모).</summary>
    public class EnemySpawner : MonoBehaviour
    {
        public GameObject enemyPrefab;
        public Transform  spawnPoint;
        [Header("랜덤 스폰 영역 (없으면 현재 오브젝트 위치 기준)")]
        public BoxCollider spawnArea;
        [Min(1)] public int randomPointTryCount = 12;
        [Min(0.1f)] public float interval = 2f;
        [Min(0)] public int    maxEnemies  = 5;
        int spawned;

        float next;
        void Update()
        {
            if (spawned >= maxEnemies) return;
            if (Time.time < next) return;
            next = Time.time + interval;
            var spawnPosition = GetSpawnPosition();
            if (enemyPrefab) Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
            spawned++;
        }

        Vector3 GetSpawnPosition()
        {
            if (spawnArea == null)
            {
                var pt = spawnPoint ? spawnPoint : transform;
                return pt.position;
            }

            for (int i = 0; i < randomPointTryCount; i++)
            {
                var randomPos = GetRandomPointInBounds(spawnArea.bounds);
                if (TrySampleNavMesh(randomPos, out var navPos))
                    return navPos;
            }

            var fallback = GetRandomPointInBounds(spawnArea.bounds);
            return TrySampleNavMesh(fallback, out var navPosFallback) ? navPosFallback : fallback;
        }

        static Vector3 GetRandomPointInBounds(Bounds bounds)
        {
            return new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                bounds.center.y,
                Random.Range(bounds.min.z, bounds.max.z)
            );
        }

        static bool TrySampleNavMesh(Vector3 source, out Vector3 result)
        {
            if (NavMesh.SamplePosition(source, out var hit, 2.0f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
            result = source;
            return false;
        }
    }
}
