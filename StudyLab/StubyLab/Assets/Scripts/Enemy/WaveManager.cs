using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace VARCO_Workshop
{
    [System.Serializable]
    public class WaveDataVW
    {
        [Min(1)] public int   enemyCount    = 3;
        [Min(0.1f)] public float spawnInterval = 0.6f;
        public GameObject       enemyPrefab;
    }

    public class WaveManager : MonoBehaviour
    {
        public static WaveManager Instance { get; private set; }

        [Header("웨이브")]
        public WaveDataVW[] waves;
        public Transform[]  spawnPoints;
        [Header("랜덤 스폰")]
        [Tooltip("설정 시 spawnPoints 대신 이 영역 내부 랜덤 위치에서 스폰")]
        public BoxCollider randomSpawnArea;
        [Min(1)] public int randomPointTryCount = 12;
        [Min(0)] public float delayBetweenWaves = 1.5f;
        [Tooltip("마지막 웨이브 처치 후 클리어 씬")]
        public bool clearWhenAllWavesCleared = true;
        public bool disableWhenNavMeshMissing = true;

        int   alive;
        int   spawned;
        int   totalEnemyCount;
        readonly HashSet<EnemyHealth> liveWaveEnemies = new HashSet<EnemyHealth>();

        public int AliveCount => alive;
        public int SpawnedCount => spawned;
        public int TotalEnemyCount => totalEnemyCount > 0 ? totalEnemyCount : CalculateTotalEnemyCount();
        public bool HasRemainingEnemies => SpawnedCount < TotalEnemyCount || AliveCount > 0;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            if (waves == null || waves.Length == 0) yield break;
            if (disableWhenNavMeshMissing && HasNavMeshAgentsConfigured() && !HasAnyNavMesh())
            {
                Debug.LogWarning("[VARCO] NavMesh가 없어 적 웨이브 생성을 중단했습니다. 자동 제작 또는 현재 씬 자동 보정을 다시 실행하세요.", this);
                yield break;
            }

            totalEnemyCount = CalculateTotalEnemyCount();
            spawned = 0;
            for (int i = 0; i < waves.Length; i++)
            {
                alive = 0;
                liveWaveEnemies.Clear();
                var w = waves[i];
                for (int k = 0; k < w.enemyCount; k++)
                {
                    SpawnOne(w);
                    yield return new WaitForSeconds(w.spawnInterval);
                }
                while (alive > 0) yield return null;
                if (i < waves.Length - 1) yield return new WaitForSeconds(delayBetweenWaves);
            }
            while (HasLiveEnemiesInScene()) yield return null;
            if (clearWhenAllWavesCleared && GameManager.Instance)
                GameManager.Instance.TriggerClear();
        }

        void SpawnOne(WaveDataVW w)
        {
            if (w.enemyPrefab == null) return;
            var spawnPosition = GetSpawnPosition();
            var enemy = Instantiate(w.enemyPrefab, spawnPosition, Quaternion.identity);
            AlignSpawnedEnemyVisualToGround(enemy, spawnPosition.y);
            spawned++;
            alive++;
            var health = enemy.GetComponentInChildren<EnemyHealth>(true);
            if (health)
                liveWaveEnemies.Add(health);
        }

        static void AlignSpawnedEnemyVisualToGround(GameObject enemy, float fallbackGroundY)
        {
            if (!enemy || !TryGetRendererBounds(enemy, out var bounds))
                return;

            var groundY = TrySampleGroundY(enemy, out var sampledY) ? sampledY : fallbackGroundY;
            var deltaY = groundY + 0.025f - bounds.min.y;
            if (Mathf.Abs(deltaY) <= 0.025f)
                return;

            if (TryOffsetTopLevelVisualChildren(enemy, deltaY))
                return;

            if (!enemy.TryGetComponent<NavMeshAgent>(out var agent) || !agent.enabled)
                enemy.transform.position += Vector3.up * deltaY;
        }

        static bool TrySampleGroundY(GameObject ignoreRoot, out float groundY)
        {
            groundY = 0f;
            var origin = ignoreRoot.transform.position + Vector3.up * 12f;
            var hits = Physics.RaycastAll(origin, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
                return false;

            var found = false;
            var bestY = float.NegativeInfinity;
            foreach (var hit in hits)
            {
                if (!hit.collider || hit.normal.y < 0.45f)
                    continue;
                if (hit.collider.transform.IsChildOf(ignoreRoot.transform))
                    continue;
                if (hit.point.y > bestY)
                {
                    bestY = hit.point.y;
                    found = true;
                }
            }

            if (!found)
                return false;

            groundY = bestY;
            return true;
        }

        static bool TryOffsetTopLevelVisualChildren(GameObject root, float worldDeltaY)
        {
            if (!root || root.transform.childCount == 0)
                return false;

            var moved = false;
            var worldDelta = Vector3.up * worldDeltaY;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (!child || !ChildContainsVisibleRenderer(child))
                    continue;

                child.localPosition += child.parent.InverseTransformVector(worldDelta);
                moved = true;
            }

            return moved;
        }

        static bool ChildContainsVisibleRenderer(Transform child)
        {
            var renderers = child.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer && !(renderer is ParticleSystemRenderer))
                    return true;
            }
            return false;
        }

        static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var hasAny = false;
            foreach (var renderer in renderers)
            {
                if (!renderer || renderer is ParticleSystemRenderer)
                    continue;

                var rendererBounds = renderer.bounds;
                if (rendererBounds.size.sqrMagnitude <= 0.0001f)
                    continue;

                if (!hasAny)
                {
                    bounds = rendererBounds;
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }

            return hasAny;
        }

        Vector3 GetSpawnPosition()
        {
            if (randomSpawnArea != null)
            {
                for (int i = 0; i < randomPointTryCount; i++)
                {
                    var randomPos = GetRandomPointInBounds(randomSpawnArea.bounds);
                    if (TrySampleNavMesh(randomPos, out var navPos))
                        return navPos;
                }

                var fallback = GetRandomPointInBounds(randomSpawnArea.bounds);
                return TrySampleNavMesh(fallback, out var navPosFallback) ? navPosFallback : fallback;
            }

            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var pt = spawnPoints[Random.Range(0, spawnPoints.Length)];
                return pt.position;
            }

            return transform.position;
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

        public void RegisterEnemyDied(EnemyHealth enemy = null)
        {
            if (enemy != null && liveWaveEnemies.Count > 0 && !liveWaveEnemies.Remove(enemy))
                return;

            alive = Mathf.Max(0, alive - 1);
        }

        static bool HasAnyNavMesh()
        {
            var triangulation = NavMesh.CalculateTriangulation();
            return triangulation.vertices != null && triangulation.vertices.Length > 0;
        }

        bool HasNavMeshAgentsConfigured()
        {
            if (waves != null)
            {
                foreach (var wave in waves)
                {
                    if (wave != null && wave.enemyPrefab && wave.enemyPrefab.GetComponentInChildren<NavMeshAgent>(true))
                        return true;
                }
            }

            return FindFirstObjectByType<NavMeshAgent>() != null;
        }

        int CalculateTotalEnemyCount()
        {
            if (waves == null)
                return 0;

            var total = 0;
            foreach (var wave in waves)
            {
                if (wave != null && wave.enemyPrefab != null)
                    total += Mathf.Max(0, wave.enemyCount);
            }

            return total;
        }

        static bool HasLiveEnemiesInScene()
        {
            var enemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (enemy != null && enemy.CurrentHP > 0)
                    return true;
            }

            return false;
        }
    }
}
