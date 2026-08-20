using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VARCO_Workshop
{
    /// <summary>
    /// 규칙 전체가 공유하는 이름표 기반 변수 저장소입니다.
    /// "score", "combo", "keys" 처럼 이름을 정해 값을 저장/증가시키고,
    /// 그 값을 조건이나 트리거에서 다시 검사할 수 있습니다. 씬을 새로 시작하면 초기화됩니다.
    /// </summary>
    public static class VARCOBlockVariableStore
    {
        static readonly Dictionary<string, float> values = new Dictionary<string, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad() => values.Clear();

        public static float Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0f;
            return values.TryGetValue(name, out var v) ? v : 0f;
        }

        public static void Set(string name, float value)
        {
            if (string.IsNullOrEmpty(name)) return;
            values[name] = value;
        }

        public static void Add(string name, float delta)
        {
            if (string.IsNullOrEmpty(name)) return;
            values[name] = Get(name) + delta;
        }
    }

    /// <summary>조건 블록 하나 — "무엇을 만족하면"에 해당합니다. 여러 개를 쌓으면 AND로 검사됩니다.</summary>
    [Serializable]
    public class BlockConditionEntry
    {
        public BlockConditionType type = BlockConditionType.RequireTag;
        [Tooltip("체크하면 이 조건의 결과를 반대로 뒤집습니다 (예: '태그가 아니면')")]
        public bool invert;
        public string stringValue = "Player";
        public int intValue;
        public float floatValue = 50f;
        public GameState gameStateValue = GameState.Playing;
        public CollectibleCounter targetCounter;
        public PlayerHealth targetPlayerHealth;
        public CountdownTimer targetTimer;
        public GameObject targetObject;
        public Transform targetPoint;
        public bool boolValue = true;
    }

    /// <summary>액션 블록 하나 — "무엇을 할지"에 해당합니다. 여러 개를 쌓으면 순서대로 실행됩니다.</summary>
    [Serializable]
    public class BlockActionEntry
    {
        public BlockActionType type = BlockActionType.ShowHudMessage;

        public string stringValue = "";
        public string stringValue2 = "";
        public int intValue = 1;
        public float floatValue = 1f;
        public float floatValue2 = 1f;
        public bool boolValue = true;
        public Vector3 vectorValue = Vector3.zero;
        public Color colorValue = Color.white;

        public AudioClip clip;
        public Material materialValue;
        public GameObject prefabToSpawn;

        public GameObject targetObject;
        public Transform targetTransform;
        public Transform spawnPoint;
        public Rigidbody targetRigidbody;
        public Renderer targetRenderer;
        public Light targetLight;
        public AudioSource targetAudio;
        public Animator targetAnimator;
        public Collider targetCollider;
        public ParticleSystem targetParticle;
        public TrailRenderer targetTrail;

        public DoorController targetDoor;
        public EnemyHealth targetEnemy;
        public PlayerHealth targetPlayer;
        public CollectibleCounter targetCounter;
        public CountdownTimer targetTimer;
    }

    /// <summary>규칙 하나 = 트리거(언제) + 조건 목록(선택) + 액션 목록(무엇을 할지).</summary>
    [Serializable]
    public class BlockRule
    {
        public string ruleName = "새 규칙";
        public bool enabledRule = true;
        public BlockTriggerType trigger = BlockTriggerType.OnPlayerTriggerEnter;

        public KeyCode key = KeyCode.None;
        public float seconds = 3f;
        public CollectibleCounter watchedCounter;
        public int requiredCount = 1;
        public PlayerHealth watchedPlayerHealth;
        public int hpThreshold = 30;
        public EnemyHealth watchedEnemy;
        public string variableName = "score";
        public float variableThreshold = 10f;

        [Tooltip("한 번만 실행하고 이후 무시할지 여부")]
        public bool once = true;
        [Tooltip("재실행 최소 간격(초). 0이면 제한 없음")]
        public float cooldown;

        public List<BlockConditionEntry> conditions = new List<BlockConditionEntry>();
        public List<BlockActionEntry> actions = new List<BlockActionEntry>();

        [NonSerialized] public bool hasFired;
        [NonSerialized] public float lastFireTime = -999f;
        [NonSerialized] public float timerElapsed;
    }

    /// <summary>
    /// VARCO 블록코딩 — 트리거(언제) + 조건(무엇을 만족하면) + 액션(무엇을 할지)을
    /// 인스펙터에서 카드처럼 쌓아 조합하는 규칙 실행기입니다.
    /// </summary>
    public class VARCOBlockRule : MonoBehaviour
    {
        public List<BlockRule> rules = new List<BlockRule>();
        [Tooltip("실행 로그를 Console에 출력합니다 (수업 중 디버그용).")]
        public bool verboseLog = true;

        static readonly Dictionary<int, bool> doorOpenState = new Dictionary<int, bool>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => doorOpenState.Clear();

        // ==============================================================
        // 트리거 감지
        // ==============================================================
        void Start()
        {
            foreach (var rule in rules)
            {
                if (rule.trigger == BlockTriggerType.OnStart)
                    TryFire(rule, null);

                if (rule.trigger == BlockTriggerType.OnPlayerDeath && rule.watchedPlayerHealth)
                {
                    var r = rule;
                    r.watchedPlayerHealth.OnDeath += () => TryFire(r, null);
                }
                else if (rule.trigger == BlockTriggerType.OnEnemyDefeated && rule.watchedEnemy)
                {
                    var r = rule;
                    r.watchedEnemy.OnDeath += () => TryFire(r, null);
                }
                else if (rule.trigger == BlockTriggerType.OnGameClear && GameManager.Instance)
                {
                    var r = rule;
                    GameManager.Instance.OnClear += () => TryFire(r, null);
                }
                else if (rule.trigger == BlockTriggerType.OnGameOver && GameManager.Instance)
                {
                    var r = rule;
                    GameManager.Instance.OnGameOver += () => TryFire(r, null);
                }
            }
        }

        void Update()
        {
            foreach (var rule in rules)
            {
                if (!rule.enabledRule) continue;

                switch (rule.trigger)
                {
                    case BlockTriggerType.OnKeyDown:
                        if (rule.key != KeyCode.None && Input.GetKeyDown(rule.key)) TryFire(rule, null);
                        break;

                    case BlockTriggerType.OnKeyHold:
                        if (rule.key != KeyCode.None && Input.GetKey(rule.key)) TryFire(rule, null);
                        break;

                    case BlockTriggerType.OnTimerElapsed:
                        rule.timerElapsed += Time.deltaTime;
                        if (rule.timerElapsed >= rule.seconds)
                        {
                            rule.timerElapsed = 0f;
                            TryFire(rule, null);
                        }
                        break;

                    case BlockTriggerType.OnCollectibleCountReached:
                        if (rule.watchedCounter && rule.watchedCounter.Count >= rule.requiredCount) TryFire(rule, null);
                        break;

                    case BlockTriggerType.OnHealthBelow:
                        if (rule.watchedPlayerHealth && rule.watchedPlayerHealth.CurrentHP <= rule.hpThreshold) TryFire(rule, null);
                        break;

                    case BlockTriggerType.OnHealthAbove:
                        if (rule.watchedPlayerHealth && rule.watchedPlayerHealth.CurrentHP >= rule.hpThreshold) TryFire(rule, null);
                        break;

                    case BlockTriggerType.OnVariableReaches:
                        if (VARCOBlockVariableStore.Get(rule.variableName) >= rule.variableThreshold) TryFire(rule, null);
                        break;
                }
            }
        }

        void OnMouseDown()
        {
            foreach (var rule in rules)
                if (rule.enabledRule && rule.trigger == BlockTriggerType.OnMouseClick)
                    TryFire(rule, null);
        }

        void OnTriggerEnter(Collider other)
        {
            foreach (var rule in rules)
            {
                if (!rule.enabledRule) continue;
                if (rule.trigger == BlockTriggerType.OnAnyTriggerEnter) { TryFire(rule, other.gameObject); continue; }
                if (rule.trigger != BlockTriggerType.OnPlayerTriggerEnter) continue;
                if (!IsPlayer(other.gameObject)) continue;
                TryFire(rule, other.gameObject);
            }
        }

        void OnTriggerExit(Collider other)
        {
            foreach (var rule in rules)
            {
                if (!rule.enabledRule) continue;
                if (rule.trigger != BlockTriggerType.OnPlayerTriggerExit) continue;
                if (!IsPlayer(other.gameObject)) continue;
                TryFire(rule, other.gameObject);
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            foreach (var rule in rules)
            {
                if (!rule.enabledRule) continue;
                if (rule.trigger != BlockTriggerType.OnPlayerCollisionEnter) continue;
                if (!IsPlayer(collision.collider.gameObject)) continue;
                TryFire(rule, collision.collider.gameObject);
            }
        }

        static bool IsPlayer(GameObject go)
        {
            if (go.CompareTag("Player")) return true;
            var health = go.GetComponent<PlayerHealth>() ?? go.GetComponentInParent<PlayerHealth>();
            return health != null;
        }

        /// <summary>다른 스크립트(버튼, 압력판 등)에서 이름으로 규칙을 수동 호출할 때 사용합니다.</summary>
        public void TriggerRuleByName(string ruleName)
        {
            foreach (var rule in rules)
                if (rule.ruleName == ruleName) TryFire(rule, null);
        }

        public void TriggerRuleByIndex(int index)
        {
            if (index < 0 || index >= rules.Count) return;
            TryFire(rules[index], null);
        }

        void TryFire(BlockRule rule, GameObject actor)
        {
            if (rule.once && rule.hasFired) return;
            if (rule.cooldown > 0f && Time.time - rule.lastFireTime < rule.cooldown) return;
            if (!CheckConditions(rule, actor)) return;

            rule.hasFired = true;
            rule.lastFireTime = Time.time;

            if (verboseLog)
                Debug.Log("[VARCO 블록 규칙] '" + rule.ruleName + "' 실행 (" + gameObject.name + ")");

            StartCoroutine(RunActions(rule));
        }

        // ==============================================================
        // 조건 검사
        // ==============================================================
        bool CheckConditions(BlockRule rule, GameObject actor)
        {
            foreach (var c in rule.conditions)
            {
                bool passed;
                switch (c.type)
                {
                    case BlockConditionType.RequireTag:
                        passed = actor != null && actor.CompareTag(c.stringValue);
                        break;
                    case BlockConditionType.RequireCollectibleAtLeast:
                        passed = c.targetCounter && c.targetCounter.Count >= c.intValue;
                        break;
                    case BlockConditionType.RequireGameState:
                        passed = GameManager.Instance && GameManager.Instance.State == c.gameStateValue;
                        break;
                    case BlockConditionType.RequirePlayerHPAtLeast:
                        passed = c.targetPlayerHealth && c.targetPlayerHealth.CurrentHP >= c.intValue;
                        break;
                    case BlockConditionType.RequirePlayerHPBelow:
                        passed = c.targetPlayerHealth && c.targetPlayerHealth.CurrentHP < c.intValue;
                        break;
                    case BlockConditionType.RequireObjectActive:
                        passed = c.targetObject && c.targetObject.activeInHierarchy == c.boolValue;
                        break;
                    case BlockConditionType.RequireRandomChance:
                        passed = UnityEngine.Random.Range(0f, 100f) <= c.floatValue;
                        break;
                    case BlockConditionType.RequireVariableAtLeast:
                        passed = VARCOBlockVariableStore.Get(c.stringValue) >= c.floatValue;
                        break;
                    case BlockConditionType.RequireVariableEquals:
                        passed = Mathf.Approximately(VARCOBlockVariableStore.Get(c.stringValue), c.floatValue);
                        break;
                    case BlockConditionType.RequireKillCountAtLeast:
                        passed = GameManager.Instance && GameManager.Instance.KillCount >= c.intValue;
                        break;
                    case BlockConditionType.RequireTimerBelow:
                        passed = c.targetTimer && c.targetTimer.Remaining <= c.floatValue;
                        break;
                    case BlockConditionType.RequireDistanceBelow:
                        passed = c.targetPoint && Vector3.Distance(transform.position, c.targetPoint.position) <= c.floatValue;
                        break;
                    default:
                        passed = true;
                        break;
                }

                if (c.invert) passed = !passed;
                if (!passed) return false;
            }
            return true;
        }

        // ==============================================================
        // 액션 실행
        // ==============================================================
        IEnumerator RunActions(BlockRule rule)
        {
            foreach (var a in rule.actions)
            {
                // 대기 계열은 코루틴을 직접 멈춰야 하므로 먼저 처리합니다.
                if (a.type == BlockActionType.Wait)
                {
                    yield return new WaitForSeconds(Mathf.Max(0f, a.floatValue));
                    continue;
                }
                if (a.type == BlockActionType.WaitRandom)
                {
                    yield return new WaitForSeconds(Mathf.Max(0f, UnityEngine.Random.Range(a.floatValue, a.floatValue2)));
                    continue;
                }

                Execute(a);
            }
        }

        void Execute(BlockActionEntry a)
        {
            switch (a.type)
            {
                // ---------------- 이동/변형 ----------------
                case BlockActionType.MoveBy: if (Tr(a)) Tr(a).position += a.vectorValue; break;
                case BlockActionType.MoveTo: if (Tr(a)) Tr(a).position = a.vectorValue; break;
                case BlockActionType.MoveToPoint: if (Tr(a) && a.spawnPoint) Tr(a).position = a.spawnPoint.position; break;
                case BlockActionType.MoveForward: if (Tr(a)) Tr(a).position += Tr(a).forward * a.floatValue; break;
                case BlockActionType.MoveUp: if (Tr(a)) Tr(a).position += Vector3.up * a.floatValue; break;
                case BlockActionType.SmoothMoveToPoint: if (Tr(a) && a.spawnPoint) StartCoroutine(CoMove(Tr(a), a.spawnPoint.position, a.floatValue)); break;
                case BlockActionType.SetRotation: if (Tr(a)) Tr(a).eulerAngles = a.vectorValue; break;
                case BlockActionType.RotateBy: if (Tr(a)) Tr(a).Rotate(a.vectorValue); break;
                case BlockActionType.LookAtPoint: if (Tr(a) && a.spawnPoint) Tr(a).LookAt(a.spawnPoint); break;
                case BlockActionType.LookAtPlayer: { var p = FindPlayer(); if (Tr(a) && p) Tr(a).LookAt(p.transform); break; }
                case BlockActionType.SetScale: if (Tr(a)) Tr(a).localScale = a.vectorValue; break;
                case BlockActionType.ScaleBy: if (Tr(a)) Tr(a).localScale *= a.floatValue; break;
                case BlockActionType.PulseScale: if (Tr(a)) StartCoroutine(CoPulse(Tr(a), a.floatValue, a.floatValue2)); break;
                case BlockActionType.SetParent: if (Tr(a)) Tr(a).SetParent(a.spawnPoint); break;
                case BlockActionType.DetachParent: if (Tr(a)) Tr(a).SetParent(null); break;

                // ---------------- 물리 ----------------
                case BlockActionType.AddForce: if (a.targetRigidbody) a.targetRigidbody.AddForce(a.vectorValue, ForceMode.Impulse); break;
                case BlockActionType.AddForceUp: if (a.targetRigidbody) a.targetRigidbody.AddForce(Vector3.up * a.floatValue, ForceMode.Impulse); break;
                case BlockActionType.AddForceForward: if (a.targetRigidbody) a.targetRigidbody.AddForce(a.targetRigidbody.transform.forward * a.floatValue, ForceMode.Impulse); break;
                case BlockActionType.SetVelocity: if (a.targetRigidbody) SetVel(a.targetRigidbody, a.vectorValue); break;
                case BlockActionType.StopMotion: if (a.targetRigidbody) { SetVel(a.targetRigidbody, Vector3.zero); a.targetRigidbody.angularVelocity = Vector3.zero; } break;
                case BlockActionType.AddTorque: if (a.targetRigidbody) a.targetRigidbody.AddTorque(a.vectorValue, ForceMode.Impulse); break;
                case BlockActionType.SetGravityEnabled: if (a.targetRigidbody) a.targetRigidbody.useGravity = a.boolValue; break;
                case BlockActionType.SetKinematic: if (a.targetRigidbody) a.targetRigidbody.isKinematic = a.boolValue; break;
                case BlockActionType.SetMass: if (a.targetRigidbody) a.targetRigidbody.mass = Mathf.Max(0.001f, a.floatValue); break;
                case BlockActionType.SetDrag: if (a.targetRigidbody) SetDrag(a.targetRigidbody, Mathf.Max(0f, a.floatValue)); break;
                case BlockActionType.ExplodeFromHere: if (a.targetRigidbody) a.targetRigidbody.AddExplosionForce(a.floatValue, transform.position, a.floatValue2, 0.5f, ForceMode.Impulse); break;
                case BlockActionType.PushTowardPoint:
                    if (a.targetRigidbody && a.spawnPoint)
                    {
                        var dir = (a.spawnPoint.position - a.targetRigidbody.transform.position).normalized;
                        a.targetRigidbody.AddForce(dir * a.floatValue, ForceMode.Impulse);
                    }
                    break;

                // ---------------- 플레이어 체력 ----------------
                case BlockActionType.DamagePlayer: if (a.targetPlayer) a.targetPlayer.TakeDamage(a.intValue); break;
                case BlockActionType.HealPlayer: if (a.targetPlayer) a.targetPlayer.Heal(a.intValue); break;
                case BlockActionType.FullHealPlayer: if (a.targetPlayer) a.targetPlayer.Heal(a.targetPlayer.maxHP); break;
                case BlockActionType.KillPlayer: if (a.targetPlayer) a.targetPlayer.TakeDamage(a.targetPlayer.CurrentHP); break;
                case BlockActionType.SetPlayerMaxHP: if (a.targetPlayer) a.targetPlayer.maxHP = Mathf.Max(1, a.intValue); break;
                case BlockActionType.DamagePlayerPercent: if (a.targetPlayer) a.targetPlayer.TakeDamage(Mathf.RoundToInt(a.targetPlayer.maxHP * a.floatValue / 100f)); break;
                case BlockActionType.HealPlayerPercent: if (a.targetPlayer) a.targetPlayer.Heal(Mathf.RoundToInt(a.targetPlayer.maxHP * a.floatValue / 100f)); break;
                case BlockActionType.RespawnPlayerAtCheckpoint:
                    { var pc = Comp<PlayerController_Platform>(a.targetObject); if (pc) pc.RespawnAtCheckpoint(); break; }
                case BlockActionType.SetPlayerRespawnHere:
                    {
                        Checkpoint.SetLastPosition(transform.position);
                        var pc = Comp<PlayerController_Platform>(a.targetObject);
                        if (pc) pc.SetRespawnPoint(transform.position, pc.transform.rotation);
                        break;
                    }
                case BlockActionType.TeleportPlayerToPoint:
                    {
                        var go = a.targetObject ? a.targetObject : FindPlayer();
                        if (go && a.spawnPoint)
                        {
                            var cc = go.GetComponent<CharacterController>();
                            if (cc) cc.enabled = false;
                            go.transform.position = a.spawnPoint.position;
                            if (cc) cc.enabled = true;
                        }
                        break;
                    }
                case BlockActionType.BouncePlayer:
                    { var pc = Comp<PlayerController_Platform>(a.targetObject); if (pc) pc.Bounce(a.floatValue); break; }
                case BlockActionType.PushPlayer:
                    { var pc = Comp<PlayerController_Platform>(a.targetObject); if (pc) pc.AddExternalVelocity(a.vectorValue); break; }

                // ---------------- 적/전투 ----------------
                case BlockActionType.DamageTarget: if (a.targetEnemy) a.targetEnemy.TakeDamage(a.intValue); break;
                case BlockActionType.KillTarget: if (a.targetEnemy) a.targetEnemy.TakeDamage(a.targetEnemy.CurrentHP); break;
                case BlockActionType.DamageAllEnemies:
                    foreach (var e in FindAll<EnemyHealth>()) e.TakeDamage(a.intValue);
                    break;
                case BlockActionType.KillAllEnemies:
                    foreach (var e in FindAll<EnemyHealth>()) e.TakeDamage(e.CurrentHP);
                    break;
                case BlockActionType.SetEnemyDetectionRange: { var ai = Comp<EnemyAI_NavMesh>(a.targetObject); if (ai) ai.detectionRange = a.floatValue; break; }
                case BlockActionType.SetEnemyContactDamage: { var ai = Comp<EnemyAI_NavMesh>(a.targetObject); if (ai) ai.contactDamage = a.intValue; break; }
                case BlockActionType.SetEnemyAttackSpeed: { var ai = Comp<EnemyAI_NavMesh>(a.targetObject); if (ai) ai.attackSpeed = a.floatValue; break; }
                case BlockActionType.SetEnemyStopDistance: { var ai = Comp<EnemyAI_NavMesh>(a.targetObject); if (ai) ai.stopDistance = a.floatValue; break; }
                case BlockActionType.SpawnEnemyPrefab: Spawn(a.prefabToSpawn, a.spawnPoint); break;
                case BlockActionType.RegisterKill: if (GameManager.Instance) GameManager.Instance.RegisterKill(); break;
                case BlockActionType.DisableEnemyAI: { var ai = Comp<EnemyAI_NavMesh>(a.targetObject); if (ai) ai.enabled = false; break; }
                case BlockActionType.EnableEnemyAI: { var ai = Comp<EnemyAI_NavMesh>(a.targetObject); if (ai) ai.enabled = true; break; }
                case BlockActionType.SetPlayerAttackDamage: { var pa = Comp<PlayerAttack>(a.targetObject); if (pa) pa.damage = a.intValue; break; }
                case BlockActionType.SetPlayerAttackCooldown: { var pa = Comp<PlayerAttack>(a.targetObject); if (pa) pa.cooldown = a.floatValue; break; }

                // ---------------- 아이템/점수 ----------------
                case BlockActionType.AddCollectible: if (a.targetCounter) a.targetCounter.Add(a.intValue == 0 ? 1 : a.intValue); break;
                case BlockActionType.RemoveCollectible: if (a.targetCounter) a.targetCounter.Add(-Mathf.Abs(a.intValue)); break;
                case BlockActionType.ResetCollectible: if (a.targetCounter) a.targetCounter.Add(-a.targetCounter.Count); break;
                case BlockActionType.SpawnItemPrefab: Spawn(a.prefabToSpawn, a.spawnPoint); break;
                case BlockActionType.DropPrefabAtPoint: Spawn(a.prefabToSpawn, a.spawnPoint); break;
                case BlockActionType.AddScore: VARCOBlockVariableStore.Add("score", a.floatValue); break;
                case BlockActionType.SetScore: VARCOBlockVariableStore.Set("score", a.floatValue); break;
                case BlockActionType.ResetScore: VARCOBlockVariableStore.Set("score", 0f); break;
                case BlockActionType.AddKillScore:
                    if (GameManager.Instance) GameManager.Instance.RegisterKill();
                    VARCOBlockVariableStore.Add("score", a.floatValue);
                    break;
                case BlockActionType.SpawnPrefabRandomNearby:
                    if (a.prefabToSpawn)
                    {
                        var off = UnityEngine.Random.insideUnitCircle * Mathf.Max(0.01f, a.floatValue);
                        Instantiate(a.prefabToSpawn, transform.position + new Vector3(off.x, 0f, off.y), Quaternion.identity);
                    }
                    break;

                // ---------------- 문/장치 ----------------
                case BlockActionType.OpenDoor: SetDoor(a.targetDoor, true); break;
                case BlockActionType.CloseDoor: SetDoor(a.targetDoor, false); break;
                case BlockActionType.ToggleDoor:
                    if (a.targetDoor)
                    {
                        int id = a.targetDoor.GetInstanceID();
                        doorOpenState.TryGetValue(id, out var cur);
                        SetDoor(a.targetDoor, !cur);
                    }
                    break;
                case BlockActionType.SetDoorOffset: if (a.targetDoor) a.targetDoor.openOffset = a.vectorValue; break;
                case BlockActionType.SetPlatformSpeed: { var mp = Comp<MovingPlatform>(a.targetObject); if (mp) mp.speed = a.floatValue; break; }
                case BlockActionType.StopPlatform: { var mp = Comp<MovingPlatform>(a.targetObject); if (mp) mp.speed = 0f; break; }
                case BlockActionType.StartPlatform: { var mp = Comp<MovingPlatform>(a.targetObject); if (mp) mp.speed = a.floatValue <= 0f ? 1.2f : a.floatValue; break; }
                case BlockActionType.BreakPlatformNow: { var bp = Comp<BreakawayPlatform>(a.targetObject); if (bp) bp.BreakNow(); break; }
                case BlockActionType.ResetPlatform: { var bp = Comp<BreakawayPlatform>(a.targetObject); if (bp) bp.ResetPlatform(); break; }
                case BlockActionType.SaveCheckpointHere:
                    Checkpoint.SetLastPosition(a.targetObject ? a.targetObject.transform.position : transform.position);
                    break;
                case BlockActionType.EnableHazard: if (a.targetObject) a.targetObject.SetActive(true); break;
                case BlockActionType.DisableHazard: if (a.targetObject) a.targetObject.SetActive(false); break;
                case BlockActionType.SetHazardDamage: { var hz = Comp<HazardZone>(a.targetObject); if (hz) hz.damagePerSecond = Mathf.Max(1, a.intValue); break; }

                // ---------------- 사운드 ----------------
                case BlockActionType.PlaySoundClip: if (a.clip) AudioSource.PlayClipAtPoint(a.clip, transform.position); break;
                case BlockActionType.PlaySoundEvent:
                    { var em = GetComponent<SoundEventEmitter>(); if (em && !string.IsNullOrEmpty(a.stringValue)) em.Play(a.stringValue); break; }
                case BlockActionType.PlayClipAtPoint: if (a.clip) AudioSource.PlayClipAtPoint(a.clip, a.spawnPoint ? a.spawnPoint.position : transform.position); break;
                case BlockActionType.PlayBGM:
                    if (a.targetAudio && a.clip) { a.targetAudio.clip = a.clip; a.targetAudio.loop = true; a.targetAudio.Play(); }
                    break;
                case BlockActionType.StopBGM: if (a.targetAudio) a.targetAudio.Stop(); break;
                case BlockActionType.PauseAudio: if (a.targetAudio) a.targetAudio.Pause(); break;
                case BlockActionType.ResumeAudio: if (a.targetAudio) a.targetAudio.UnPause(); break;
                case BlockActionType.SetVolume: if (a.targetAudio) a.targetAudio.volume = Mathf.Clamp01(a.floatValue); break;
                case BlockActionType.SetPitch: if (a.targetAudio) a.targetAudio.pitch = a.floatValue; break;
                case BlockActionType.FadeInAudio: if (a.targetAudio) StartCoroutine(CoFadeAudio(a.targetAudio, 1f, a.floatValue)); break;
                case BlockActionType.FadeOutAudio: if (a.targetAudio) StartCoroutine(CoFadeAudio(a.targetAudio, 0f, a.floatValue)); break;
                case BlockActionType.MuteAudio: if (a.targetAudio) a.targetAudio.mute = true; break;
                case BlockActionType.UnmuteAudio: if (a.targetAudio) a.targetAudio.mute = false; break;
                case BlockActionType.PlayOneShotOnTarget: if (a.targetAudio && a.clip) a.targetAudio.PlayOneShot(a.clip); break;
                case BlockActionType.SetSpatialBlend: if (a.targetAudio) a.targetAudio.spatialBlend = Mathf.Clamp01(a.floatValue); break;

                // ---------------- 시각효과 ----------------
                case BlockActionType.ChangeColor: if (a.targetRenderer) a.targetRenderer.material.color = a.colorValue; break;
                case BlockActionType.FlashColor: if (a.targetRenderer) StartCoroutine(CoFlash(a.targetRenderer, a.colorValue, a.floatValue)); break;
                case BlockActionType.SetTransparency: if (a.targetRenderer) SetAlpha(a.targetRenderer, Mathf.Clamp01(a.floatValue)); break;
                case BlockActionType.FadeOutRenderer: if (a.targetRenderer) StartCoroutine(CoFadeRenderer(a.targetRenderer, 0f, a.floatValue)); break;
                case BlockActionType.FadeInRenderer: if (a.targetRenderer) StartCoroutine(CoFadeRenderer(a.targetRenderer, 1f, a.floatValue)); break;
                case BlockActionType.SetEmissionColor:
                    if (a.targetRenderer) { a.targetRenderer.material.EnableKeyword("_EMISSION"); a.targetRenderer.material.SetColor("_EmissionColor", a.colorValue); }
                    break;
                case BlockActionType.PlayParticle: if (a.targetParticle) a.targetParticle.Play(); break;
                case BlockActionType.StopParticle: if (a.targetParticle) a.targetParticle.Stop(); break;
                case BlockActionType.SpawnParticlePrefab: Spawn(a.prefabToSpawn, a.spawnPoint); break;
                case BlockActionType.BlinkRenderer: if (a.targetRenderer) StartCoroutine(CoBlink(a.targetRenderer, Mathf.Max(1, a.intValue), Mathf.Max(0.02f, a.floatValue))); break;
                case BlockActionType.SetMaterial: if (a.targetRenderer && a.materialValue) a.targetRenderer.material = a.materialValue; break;
                case BlockActionType.HideRenderer: if (a.targetRenderer) a.targetRenderer.enabled = false; break;
                case BlockActionType.ShowRenderer: if (a.targetRenderer) a.targetRenderer.enabled = true; break;
                case BlockActionType.EnableTrail: if (a.targetTrail) a.targetTrail.emitting = true; break;
                case BlockActionType.DisableTrail: if (a.targetTrail) a.targetTrail.emitting = false; break;
                case BlockActionType.SetCastShadows:
                    if (a.targetRenderer)
                        a.targetRenderer.shadowCastingMode = a.boolValue
                            ? UnityEngine.Rendering.ShadowCastingMode.On
                            : UnityEngine.Rendering.ShadowCastingMode.Off;
                    break;
                case BlockActionType.ShakeObject: if (Tr(a)) StartCoroutine(CoShake(Tr(a), a.floatValue, a.floatValue2)); break;
                case BlockActionType.SetTextureOffset: if (a.targetRenderer) a.targetRenderer.material.mainTextureOffset = new Vector2(a.vectorValue.x, a.vectorValue.y); break;

                // ---------------- 조명 ----------------
                case BlockActionType.SetLightColor: if (a.targetLight) a.targetLight.color = a.colorValue; break;
                case BlockActionType.SetLightIntensity: if (a.targetLight) a.targetLight.intensity = a.floatValue; break;
                case BlockActionType.ToggleLight: if (a.targetLight) a.targetLight.enabled = !a.targetLight.enabled; break;
                case BlockActionType.LightOn: if (a.targetLight) a.targetLight.enabled = true; break;
                case BlockActionType.LightOff: if (a.targetLight) a.targetLight.enabled = false; break;
                case BlockActionType.FlickerLight: if (a.targetLight) StartCoroutine(CoFlicker(a.targetLight, Mathf.Max(1, a.intValue), Mathf.Max(0.02f, a.floatValue))); break;
                case BlockActionType.SetLightRange: if (a.targetLight) a.targetLight.range = a.floatValue; break;
                case BlockActionType.FadeLightIntensity: if (a.targetLight) StartCoroutine(CoFadeLight(a.targetLight, a.floatValue, a.floatValue2)); break;

                // ---------------- 카메라 ----------------
                case BlockActionType.ShakeCamera:
                    if (Camera.main) StartCoroutine(CoShake(Camera.main.transform, a.floatValue, a.floatValue2));
                    break;
                case BlockActionType.SetCameraDistance: { var c = Cam(); if (c) c.distance = a.floatValue; break; }
                case BlockActionType.SetCameraTarget: { var c = Cam(); if (c && a.spawnPoint) c.target = a.spawnPoint; break; }
                case BlockActionType.SetCameraHeight: { var c = Cam(); if (c) c.pivotOffset = new Vector3(c.pivotOffset.x, a.floatValue, c.pivotOffset.z); break; }
                case BlockActionType.SetCameraSensitivity: { var c = Cam(); if (c) { c.sensX = a.floatValue; c.sensY = a.floatValue; } break; }
                case BlockActionType.ZoomCameraFOV: if (Camera.main) Camera.main.fieldOfView = Mathf.Clamp(a.floatValue, 5f, 170f); break;
                case BlockActionType.ResetCameraFOV: if (Camera.main) Camera.main.fieldOfView = 60f; break;
                case BlockActionType.SetCameraBackground: if (Camera.main) Camera.main.backgroundColor = a.colorValue; break;
                case BlockActionType.SetCameraPitchLimits: { var c = Cam(); if (c) { c.minPitch = a.floatValue; c.maxPitch = a.floatValue2; } break; }
                case BlockActionType.SetCameraMinDistance: { var c = Cam(); if (c) c.minDistance = a.floatValue; break; }

                // ---------------- UI/HUD ----------------
                case BlockActionType.ShowHudMessage: VARCOGameHUD.ShowNotice(a.stringValue); break;
                case BlockActionType.ShowHudMessageLong: VARCOGameHUD.ShowNotice(a.stringValue, Mathf.Max(0.2f, a.floatValue)); break;
                case BlockActionType.ShowVariableInHud: VARCOGameHUD.ShowNotice(a.stringValue + ": " + VARCOBlockVariableStore.Get(a.stringValue)); break;
                case BlockActionType.ShowCollectibleCount: if (a.targetCounter) VARCOGameHUD.ShowNotice("수집: " + a.targetCounter.Count); break;
                case BlockActionType.ShowPlayerHP: if (a.targetPlayer) VARCOGameHUD.ShowNotice("체력: " + a.targetPlayer.CurrentHP + " / " + a.targetPlayer.maxHP); break;
                case BlockActionType.ShowTimerRemaining: if (a.targetTimer) VARCOGameHUD.ShowNotice("남은 시간: " + Mathf.CeilToInt(a.targetTimer.Remaining) + "초"); break;
                case BlockActionType.ShowKillCount: if (GameManager.Instance) VARCOGameHUD.ShowNotice("처치: " + GameManager.Instance.KillCount); break;
                case BlockActionType.ShowWarningMessage: VARCOGameHUD.ShowNotice("[경고] " + a.stringValue); break;
                case BlockActionType.ShowClearHint: VARCOGameHUD.ShowNotice("목표: " + a.stringValue); break;
                case BlockActionType.ShowValueWithLabel: VARCOGameHUD.ShowNotice(a.stringValue + ": " + VARCOBlockVariableStore.Get(a.stringValue2)); break;

                // ---------------- 게임 흐름 ----------------
                case BlockActionType.TriggerGameClear: if (GameManager.Instance) GameManager.Instance.TriggerClear(); break;
                case BlockActionType.TriggerGameOver: if (GameManager.Instance) GameManager.Instance.TriggerGameOver(); break;
                case BlockActionType.RestartScene: Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().name); break;
                case BlockActionType.LoadScene:
                    if (!string.IsNullOrEmpty(a.stringValue) && Application.CanStreamedLevelBeLoaded(a.stringValue))
                    { Time.timeScale = 1f; SceneManager.LoadScene(a.stringValue); }
                    else Debug.LogWarning("[VARCO 블록] 씬 '" + a.stringValue + "' 을(를) 찾을 수 없습니다. Build Settings를 확인하세요.");
                    break;
                case BlockActionType.PauseGame: Time.timeScale = 0f; break;
                case BlockActionType.ResumeGame: Time.timeScale = 1f; break;
                case BlockActionType.QuitGame: Application.Quit(); break;
                case BlockActionType.SetGameStatePlaying: if (GameManager.Instance) GameManager.Instance.SetState(GameState.Playing); break;
                case BlockActionType.SetGameStateReady: if (GameManager.Instance) GameManager.Instance.SetState(GameState.Ready); break;
                case BlockActionType.ClearAfterDelay: StartCoroutine(CoClearAfter(a.floatValue)); break;

                // ---------------- 변수/로직 ----------------
                case BlockActionType.SetVariable: VARCOBlockVariableStore.Set(a.stringValue, a.floatValue); break;
                case BlockActionType.AddVariable: VARCOBlockVariableStore.Add(a.stringValue, a.floatValue); break;
                case BlockActionType.SubVariable: VARCOBlockVariableStore.Add(a.stringValue, -a.floatValue); break;
                case BlockActionType.MultiplyVariable: VARCOBlockVariableStore.Set(a.stringValue, VARCOBlockVariableStore.Get(a.stringValue) * a.floatValue); break;
                case BlockActionType.DivideVariable:
                    if (Mathf.Abs(a.floatValue) > 0.0001f)
                        VARCOBlockVariableStore.Set(a.stringValue, VARCOBlockVariableStore.Get(a.stringValue) / a.floatValue);
                    break;
                case BlockActionType.ResetVariable: VARCOBlockVariableStore.Set(a.stringValue, 0f); break;
                case BlockActionType.RandomVariable: VARCOBlockVariableStore.Set(a.stringValue, UnityEngine.Random.Range(a.floatValue, a.floatValue2)); break;
                case BlockActionType.CopyVariable: VARCOBlockVariableStore.Set(a.stringValue2, VARCOBlockVariableStore.Get(a.stringValue)); break;
                case BlockActionType.ClampVariable: VARCOBlockVariableStore.Set(a.stringValue, Mathf.Clamp(VARCOBlockVariableStore.Get(a.stringValue), a.floatValue, a.floatValue2)); break;
                case BlockActionType.ToggleVariable: VARCOBlockVariableStore.Set(a.stringValue, VARCOBlockVariableStore.Get(a.stringValue) > 0.5f ? 0f : 1f); break;
                case BlockActionType.SetVarFromPlayerHP: if (a.targetPlayer) VARCOBlockVariableStore.Set(a.stringValue, a.targetPlayer.CurrentHP); break;
                case BlockActionType.SetVarFromCollectible: if (a.targetCounter) VARCOBlockVariableStore.Set(a.stringValue, a.targetCounter.Count); break;
                case BlockActionType.SetVarFromKillCount: if (GameManager.Instance) VARCOBlockVariableStore.Set(a.stringValue, GameManager.Instance.KillCount); break;
                case BlockActionType.LogVariable: Debug.Log("[VARCO 변수] " + a.stringValue + " = " + VARCOBlockVariableStore.Get(a.stringValue)); break;

                // ---------------- 타이머/시간 ----------------
                case BlockActionType.AddTimerSeconds: if (a.targetTimer) a.targetTimer.AddTime(a.floatValue); break;
                case BlockActionType.SubTimerSeconds: if (a.targetTimer) a.targetTimer.AddTime(-a.floatValue); break;
                case BlockActionType.SetTimeScale: Time.timeScale = Mathf.Max(0f, a.floatValue); break;
                case BlockActionType.SlowMotion: StartCoroutine(CoSlowMo(Mathf.Max(0.01f, a.floatValue), a.floatValue2)); break;
                case BlockActionType.NormalSpeed: Time.timeScale = 1f; break;
                case BlockActionType.SetTimerTotal: if (a.targetTimer) a.targetTimer.totalSeconds = Mathf.Max(1f, a.floatValue); break;
                case BlockActionType.ResetTimerToFull: if (a.targetTimer) a.targetTimer.AddTime(a.targetTimer.totalSeconds); break;
                case BlockActionType.PauseTimer: if (a.targetTimer) a.targetTimer.enabled = !a.boolValue; break;

                // ---------------- 오브젝트 ----------------
                case BlockActionType.ActivateObject: if (a.targetObject) a.targetObject.SetActive(true); break;
                case BlockActionType.DeactivateObject: if (a.targetObject) a.targetObject.SetActive(false); break;
                case BlockActionType.ToggleObject: if (a.targetObject) a.targetObject.SetActive(!a.targetObject.activeSelf); break;
                case BlockActionType.DestroyTarget: if (a.targetObject) Destroy(a.targetObject); break;
                case BlockActionType.DestroyAfterDelay: if (a.targetObject) Destroy(a.targetObject, Mathf.Max(0f, a.floatValue)); break;
                case BlockActionType.DestroySelf: Destroy(gameObject); break;
                case BlockActionType.SpawnPrefab: Spawn(a.prefabToSpawn, null); break;
                case BlockActionType.SpawnPrefabAtPoint: Spawn(a.prefabToSpawn, a.spawnPoint); break;
                case BlockActionType.CloneTarget:
                    if (a.targetObject) Instantiate(a.targetObject, a.targetObject.transform.position, a.targetObject.transform.rotation);
                    break;
                case BlockActionType.EnableCollider: if (a.targetCollider) a.targetCollider.enabled = true; break;
                case BlockActionType.DisableCollider: if (a.targetCollider) a.targetCollider.enabled = false; break;
                case BlockActionType.SetColliderTrigger: if (a.targetCollider) a.targetCollider.isTrigger = a.boolValue; break;
                case BlockActionType.ActivateChildren:
                    if (a.targetObject) foreach (Transform c in a.targetObject.transform) c.gameObject.SetActive(true);
                    break;
                case BlockActionType.DeactivateChildren:
                    if (a.targetObject) foreach (Transform c in a.targetObject.transform) c.gameObject.SetActive(false);
                    break;

                // ---------------- 애니메이션 ----------------
                case BlockActionType.SetAnimatorTrigger: if (a.targetAnimator && !string.IsNullOrEmpty(a.stringValue)) a.targetAnimator.SetTrigger(a.stringValue); break;
                case BlockActionType.SetAnimatorBool: if (a.targetAnimator && !string.IsNullOrEmpty(a.stringValue)) a.targetAnimator.SetBool(a.stringValue, a.boolValue); break;
                case BlockActionType.SetAnimatorFloat: if (a.targetAnimator && !string.IsNullOrEmpty(a.stringValue)) a.targetAnimator.SetFloat(a.stringValue, a.floatValue); break;
                case BlockActionType.SetAnimatorInt: if (a.targetAnimator && !string.IsNullOrEmpty(a.stringValue)) a.targetAnimator.SetInteger(a.stringValue, a.intValue); break;
                case BlockActionType.PlayAnimationState: if (a.targetAnimator && !string.IsNullOrEmpty(a.stringValue)) a.targetAnimator.Play(a.stringValue); break;
                case BlockActionType.SetAnimatorSpeed: if (a.targetAnimator) a.targetAnimator.speed = a.floatValue; break;
                case BlockActionType.ResetAnimatorTrigger: if (a.targetAnimator && !string.IsNullOrEmpty(a.stringValue)) a.targetAnimator.ResetTrigger(a.stringValue); break;
                case BlockActionType.EnableAnimator: if (a.targetAnimator) a.targetAnimator.enabled = true; break;
                case BlockActionType.DisableAnimator: if (a.targetAnimator) a.targetAnimator.enabled = false; break;
                case BlockActionType.RebindAnimator: if (a.targetAnimator) a.targetAnimator.Rebind(); break;

                // ---------------- 플레이어 제어 ----------------
                case BlockActionType.SetPlayerMoveSpeed:
                    {
                        var tp = Comp<PlayerController_ThirdPerson>(a.targetObject); if (tp) tp.moveSpeed = a.floatValue;
                        var pp = Comp<PlayerController_Platform>(a.targetObject); if (pp) pp.moveSpeed = a.floatValue;
                        break;
                    }
                case BlockActionType.SetPlayerJumpForce: { var pp = Comp<PlayerController_Platform>(a.targetObject); if (pp) pp.jumpForce = a.floatValue; break; }
                case BlockActionType.SetPlayerGravity:
                    {
                        var tp = Comp<PlayerController_ThirdPerson>(a.targetObject); if (tp) tp.gravity = a.floatValue;
                        var pp = Comp<PlayerController_Platform>(a.targetObject); if (pp) pp.gravity = a.floatValue;
                        break;
                    }
                case BlockActionType.SetPlayerRunMultiplier:
                    {
                        var tp = Comp<PlayerController_ThirdPerson>(a.targetObject); if (tp) tp.runMultiplier = a.floatValue;
                        var pp = Comp<PlayerController_Platform>(a.targetObject); if (pp) pp.runMultiplier = a.floatValue;
                        break;
                    }
                case BlockActionType.FreezePlayer: SetPlayerControl(a.targetObject, false); break;
                case BlockActionType.UnfreezePlayer: SetPlayerControl(a.targetObject, true); break;
                case BlockActionType.DisablePlayerAttack: { var pa = Comp<PlayerAttack>(a.targetObject); if (pa) pa.enabled = false; break; }
                case BlockActionType.EnablePlayerAttack: { var pa = Comp<PlayerAttack>(a.targetObject); if (pa) pa.enabled = true; break; }
                case BlockActionType.SetPlayerTurnSpeed:
                    {
                        var tp = Comp<PlayerController_ThirdPerson>(a.targetObject); if (tp) tp.turnSpeed = a.floatValue;
                        var pp = Comp<PlayerController_Platform>(a.targetObject); if (pp) pp.turnSpeed = a.floatValue;
                        break;
                    }
                case BlockActionType.ForcePlayerJump: { var pp = Comp<PlayerController_Platform>(a.targetObject); if (pp) pp.Bounce(a.floatValue <= 0f ? 8f : a.floatValue); break; }
            }
        }

        // ==============================================================
        // 헬퍼
        // ==============================================================
        Transform Tr(BlockActionEntry a)
        {
            if (a.targetTransform) return a.targetTransform;
            if (a.targetObject) return a.targetObject.transform;
            return transform;
        }

        static T Comp<T>(GameObject go) where T : Component
        {
            if (!go) return null;
            return go.GetComponent<T>() ?? go.GetComponentInParent<T>();
        }

        static List<T> FindAll<T>() where T : Component
        {
#if UNITY_2023_1_OR_NEWER
            return new List<T>(UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None));
#else
            return new List<T>(UnityEngine.Object.FindObjectsOfType<T>());
#endif
        }

        static GameObject FindPlayer()
        {
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged) return tagged;
#if UNITY_2023_1_OR_NEWER
            var ph = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
#else
            var ph = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
#endif
            return ph ? ph.gameObject : null;
        }

        static ThirdPersonCamera Cam()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<ThirdPersonCamera>();
#else
            return UnityEngine.Object.FindObjectOfType<ThirdPersonCamera>();
#endif
        }

        static void SetVel(Rigidbody rb, Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        static void SetDrag(Rigidbody rb, float d)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = d;
#else
            rb.drag = d;
#endif
        }

        static void SetDoor(DoorController door, bool open)
        {
            if (!door) return;
            door.SetOpen(open);
            doorOpenState[door.GetInstanceID()] = open;
        }

        void Spawn(GameObject prefab, Transform point)
        {
            if (!prefab) return;
            var pos = point ? point.position : transform.position;
            var rot = point ? point.rotation : transform.rotation;
            Instantiate(prefab, pos, rot);
        }

        static void SetPlayerControl(GameObject go, bool enabled)
        {
            var tp = Comp<PlayerController_ThirdPerson>(go); if (tp) tp.enabled = enabled;
            var pp = Comp<PlayerController_Platform>(go); if (pp) pp.enabled = enabled;
        }

        static void SetAlpha(Renderer r, float alpha)
        {
            var c = r.material.color;
            c.a = alpha;
            r.material.color = c;
        }

        // ==============================================================
        // 코루틴
        // ==============================================================
        IEnumerator CoMove(Transform t, Vector3 target, float duration)
        {
            if (duration <= 0f) { t.position = target; yield break; }
            var start = t.position;
            for (float e = 0f; e < duration && t; e += Time.deltaTime)
            {
                t.position = Vector3.Lerp(start, target, e / duration);
                yield return null;
            }
            if (t) t.position = target;
        }

        IEnumerator CoPulse(Transform t, float mult, float duration)
        {
            if (!t) yield break;
            var baseScale = t.localScale;
            var peak = baseScale * (mult <= 0f ? 1.2f : mult);
            var half = Mathf.Max(0.02f, duration) * 0.5f;
            for (float e = 0f; e < half && t; e += Time.deltaTime) { t.localScale = Vector3.Lerp(baseScale, peak, e / half); yield return null; }
            for (float e = 0f; e < half && t; e += Time.deltaTime) { t.localScale = Vector3.Lerp(peak, baseScale, e / half); yield return null; }
            if (t) t.localScale = baseScale;
        }

        IEnumerator CoShake(Transform t, float strength, float duration)
        {
            if (!t) yield break;
            var origin = t.localPosition;
            var dur = Mathf.Max(0.02f, duration);
            for (float e = 0f; e < dur && t; e += Time.deltaTime)
            {
                t.localPosition = origin + (Vector3)(UnityEngine.Random.insideUnitCircle * strength);
                yield return null;
            }
            if (t) t.localPosition = origin;
        }

        IEnumerator CoFlash(Renderer r, Color flash, float duration)
        {
            if (!r) yield break;
            var original = r.material.color;
            r.material.color = flash;
            yield return new WaitForSeconds(Mathf.Max(0.02f, duration));
            if (r) r.material.color = original;
        }

        IEnumerator CoBlink(Renderer r, int times, float interval)
        {
            for (int i = 0; i < times && r; i++)
            {
                r.enabled = false;
                yield return new WaitForSeconds(interval);
                if (!r) yield break;
                r.enabled = true;
                yield return new WaitForSeconds(interval);
            }
        }

        IEnumerator CoFadeRenderer(Renderer r, float targetAlpha, float duration)
        {
            if (!r) yield break;
            float startAlpha = r.material.color.a;
            var dur = Mathf.Max(0.02f, duration);
            for (float e = 0f; e < dur && r; e += Time.deltaTime)
            {
                SetAlpha(r, Mathf.Lerp(startAlpha, targetAlpha, e / dur));
                yield return null;
            }
            if (r) SetAlpha(r, targetAlpha);
        }

        IEnumerator CoFadeAudio(AudioSource src, float targetVolume, float duration)
        {
            if (!src) yield break;
            if (targetVolume > 0f && !src.isPlaying) src.Play();
            float startVolume = src.volume;
            var dur = Mathf.Max(0.02f, duration);
            for (float e = 0f; e < dur && src; e += Time.deltaTime)
            {
                src.volume = Mathf.Lerp(startVolume, targetVolume, e / dur);
                yield return null;
            }
            if (!src) yield break;
            src.volume = targetVolume;
            if (targetVolume <= 0f) src.Stop();
        }

        IEnumerator CoFlicker(Light l, int times, float interval)
        {
            for (int i = 0; i < times && l; i++)
            {
                l.enabled = false;
                yield return new WaitForSeconds(interval);
                if (!l) yield break;
                l.enabled = true;
                yield return new WaitForSeconds(interval);
            }
        }

        IEnumerator CoFadeLight(Light l, float targetIntensity, float duration)
        {
            if (!l) yield break;
            float start = l.intensity;
            var dur = Mathf.Max(0.02f, duration);
            for (float e = 0f; e < dur && l; e += Time.deltaTime)
            {
                l.intensity = Mathf.Lerp(start, targetIntensity, e / dur);
                yield return null;
            }
            if (l) l.intensity = targetIntensity;
        }

        IEnumerator CoSlowMo(float scale, float duration)
        {
            Time.timeScale = scale;
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, duration));
            Time.timeScale = 1f;
        }

        IEnumerator CoClearAfter(float delay)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, delay));
            if (GameManager.Instance) GameManager.Instance.TriggerClear();
        }
    }
}
