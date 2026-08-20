#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using VWS = VARCO_Workshop;
using Object = UnityEngine.Object;

namespace VARCO_Workshop.Editor
{
    public enum VARCOCharacterPrefabKind
    {
        AdventurePlayer,
        PlatformPlayer,
        ChaserEnemy,
        BossEnemy,
        ZombieEnemy
    }

    public sealed class VARCOCharacterPrefabBuildSettings
    {
        public VARCOCharacterPrefabKind characterKind = VARCOCharacterPrefabKind.AdventurePlayer;
        public GameObject modelAsset;
        public Object idleAnimationSource;
        public Object walkAnimationSource;
        public Object runAnimationSource;
        public Object jumpAnimationSource;
        public Object attackAnimationSource;
        public Object deadAnimationSource;
        public string characterName = "VARCO_Character";
        public readonly List<Object> animationSources = new List<Object>();
        public string outputRoot = "Assets/Prefabs/VARCO_Characters";
        public string animationOutputRoot = "Assets/Animations/Generated";
        public bool overwriteExisting = true;
        public bool normalizeGenericDirection = true;
        public bool removeGenericRootXZMotion = true;
        public bool useVarcoImportsAsFallback = false;
    }

    public sealed class VARCOCharacterPrefabBuildResult
    {
        public GameObject prefab;
        public AnimatorController controller;
        public readonly List<string> logs = new List<string>();
        public readonly Dictionary<string, AnimationClip> roleClips = new Dictionary<string, AnimationClip>();

        public string Report => string.Join("\n", logs.ToArray());
    }

    public static class VARCOCharacterPrefabBuildUtility
    {
        const string VarcoImportsFolder = "Assets/VARCO3DImports";
        const string SampledBodyYawReferenceKey = "__VARCO_SampledBodyYaw__";

        static readonly string[] RoleOrder =
        {
            "Idle", "Walk", "Run", "Jump", "Attack", "Dead"
        };

        const float LocomotionTransitionSeconds = 0.16f;
        const float ActionTransitionSeconds = 0.12f;

        public static VARCOCharacterPrefabBuildResult Build(VARCOCharacterPrefabBuildSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var result = new VARCOCharacterPrefabBuildResult();
            var modelAsset = settings.modelAsset;
            var manualObjects = EnumerateManualAnimationSources(settings).Where(o => o).ToList();
            var sourceObjects = new List<Object>(settings.animationSources.Where(o => o));
            var modelSearchObjects = new List<Object>();
            foreach (var source in sourceObjects)
                AddUniqueObject(modelSearchObjects, source);
            foreach (var source in manualObjects)
                AddUniqueObject(modelSearchObjects, source);

            if (!modelAsset && modelSearchObjects.Count > 0)
                modelAsset = FindFirstModelAsset(modelSearchObjects);

            if (!modelAsset && settings.useVarcoImportsAsFallback)
            {
                var fallbackSources = GatherAssetPathsFromFolder(VarcoImportsFolder)
                    .Select(AssetDatabase.LoadAssetAtPath<Object>)
                    .Where(o => o)
                    .ToList();
                modelAsset = FindFirstModelAsset(fallbackSources);
                foreach (var source in fallbackSources)
                    AddUniqueObject(sourceObjects, source);
            }

            if (!modelAsset)
            {
                result.logs.Add("ERROR: 모델 에셋을 찾지 못했습니다. USDZ/FBX/GLB 프리팹 또는 모델 에셋을 지정하세요.");
                return result;
            }

            settings.characterName = SafeName(string.IsNullOrWhiteSpace(settings.characterName)
                ? modelAsset.name
                : settings.characterName.Trim());

            var sources = LoadSources(sourceObjects, settings.useVarcoImportsAsFallback);
            if (sources.Count == 0 && !HasManualAnimationSources(settings))
            {
                result.logs.Add("ERROR: 애니메이션 소스를 찾지 못했습니다.");
                return result;
            }

            var modelSource = FindSourceForModel(modelAsset, sources) ?? BuildModelOnlySource(modelAsset);
            var usableSources = FilterSourcesForModel(modelSource, sources);
            var plan = BuildAnimationPlan(usableSources, settings);

            result.logs.Add($"Character: {settings.characterName}");
            result.logs.Add($"Preset: {settings.characterKind}");
            result.logs.Add($"Model: {AssetDatabase.GetAssetPath(modelAsset)}");
            result.logs.Add($"Sources scanned: {sources.Count}, matched: {usableSources.Count}");

            foreach (var role in RoleOrder)
            {
                if (plan.roleClips.TryGetValue(role, out var sourceClip))
                    result.logs.Add($"{role} <= {Path.GetFileNameWithoutExtension(sourceClip.assetPath)} ({sourceClip.clip.length:0.###}s, {sourceClip.reason})");
                else
                    result.logs.Add($"{role} <= <missing>");
            }

            foreach (var warning in plan.warnings)
                result.logs.Add("WARN: " + warning);

            if (!plan.roleClips.ContainsKey("Idle"))
            {
                result.logs.Add("ERROR: Idle 클립이 필요합니다.");
                return result;
            }

            EnsureFolder(settings.outputRoot);
            EnsureFolder(settings.animationOutputRoot);

            string characterRoot = $"{settings.outputRoot}/{settings.characterName}";
            string prefabFolder = $"{characterRoot}/Prefabs";
            string clipFolder = $"{settings.animationOutputRoot}/{settings.characterName}";
            EnsureFolder(characterRoot);
            EnsureFolder(prefabFolder);
            EnsureFolder(clipFolder);

            var referenceYaw = ResolveReferenceYaw(plan, modelAsset);
            var generatedClips = CopyRoleClips(plan, clipFolder, settings, referenceYaw, modelAsset, result);
            var controller = CreateController($"{characterRoot}/{settings.characterName}_Controller.controller", generatedClips, settings, result);
            if (!controller)
                return result;

            var prefab = CreateCharacterPrefab(modelAsset, controller, settings, $"{prefabFolder}/{settings.characterName}.prefab", result);
            result.prefab = prefab;
            result.controller = controller;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return result;
        }

        public static string BuildScanReport(IEnumerable<Object> roots, bool includeVarcoImports)
        {
            var sources = LoadSources(roots != null ? roots.Where(o => o).ToList() : new List<Object>(), includeVarcoImports);
            var sb = new StringBuilder();
            sb.AppendLine("VARCO Character Source Scan");
            sb.AppendLine($"sources={sources.Count}");

            foreach (var group in sources.GroupBy(s => !string.IsNullOrEmpty(s.modelSignature) ? s.modelSignature : s.skeletonSignature))
            {
                sb.AppendLine();
                sb.AppendLine($"Group {ShortHash(group.Key)} files={group.Count()}");
                foreach (var source in group.OrderBy(s => s.assetPath))
                {
                    sb.AppendLine($"  {source.assetPath}");
                    sb.AppendLine($"    model={source.mainAsset?.name ?? "<none>"} clips={source.clips.Count} skinned={source.skinnedRendererCount} bones={source.boneCount}");
                    foreach (var clip in source.clips)
                        sb.AppendLine($"    clip={clip.clip.name} length={clip.clip.length:0.###} hint={ClassifyByText(clip)} human={clip.clip.humanMotion}");
                }
            }

            return sb.ToString();
        }

        // [메뉴 일원화로 제거됨] [MenuItem("VARCO/선택 캐릭터 애니메이션 방향 통일", priority = -97)]
        public static void NormalizeSelectedCharacterAnimationsMenu()
        {
            var selected = Selection.activeGameObject;
            if (!selected)
            {
                EditorUtility.DisplayDialog("VARCO 애니메이션 방향 통일", "Hierarchy에서 캐릭터 또는 ModelRoot를 선택하세요.", "확인");
                return;
            }

            var animator = selected.GetComponent<Animator>()
                ?? selected.GetComponentInChildren<Animator>(true)
                ?? selected.GetComponentInParent<Animator>();

            if (!animator || !animator.runtimeAnimatorController)
            {
                EditorUtility.DisplayDialog("VARCO 애니메이션 방향 통일", "선택한 오브젝트에서 AnimatorController를 찾지 못했습니다.", "확인");
                return;
            }

            var report = NormalizeGenericAnimatorControllerClips(animator.gameObject, animator.runtimeAnimatorController, true);
            Debug.Log("[VARCO 애니메이션 방향 통일]\n" + report);
            EditorUtility.DisplayDialog("VARCO 애니메이션 방향 통일", report, "확인");
        }

        public static string NormalizeGenericAnimatorControllerClips(
            GameObject sampleModel,
            RuntimeAnimatorController runtimeController,
            bool removeRootXZMotion)
        {
            var logs = new List<string>();
            if (!sampleModel)
                return "ERROR: 샘플 모델이 없습니다.";

            var controller = runtimeController as AnimatorController;
            if (!controller)
                return "ERROR: AnimatorController가 아닙니다: " + (runtimeController ? runtimeController.name : "<none>");

            var clips = controller.layers
                .SelectMany(layer => layer.stateMachine.states.Select(s => new { state = s.state, clip = s.state.motion as AnimationClip }))
                .Where(x => x.clip)
                .GroupBy(x => x.clip)
                .Select(g => g.First())
                .ToList();

            if (clips.Count == 0)
                return "ERROR: 컨트롤러에서 AnimationClip을 찾지 못했습니다.";

            var idle = clips.FirstOrDefault(x => ContainsAny(x.state.name.ToLowerInvariant(), "idle", "대기"))
                ?? clips.FirstOrDefault(x => ContainsAny(x.clip.name.ToLowerInvariant(), "idle", "대기"))
                ?? clips.FirstOrDefault(x => x.state == controller.layers[0].stateMachine.defaultState)
                ?? clips[0];

            var referenceYaw = new Dictionary<string, float>();
            if (TryEvaluateSampledBodyYaw(sampleModel, idle.clip, out var bodyYaw))
                referenceYaw[SampledBodyYawReferenceKey] = 0f;

            var root = FindTopRotationPath(idle.clip);
            if (!string.IsNullOrEmpty(root))
                referenceYaw[root] = 0f;

            logs.Add($"Reference <= {idle.clip.name}, targetBodyYaw=0, sourceBodyYaw={(referenceYaw.ContainsKey(SampledBodyYawReferenceKey) ? bodyYaw.ToString("0.##") : "<sample failed>")}");

            foreach (var item in clips)
            {
                var clip = item.clip;
                if (clip.humanMotion)
                {
                    logs.Add($"SKIP: {clip.name} is Humanoid.");
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(clip);
                if (!path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                {
                    logs.Add($"SKIP: {clip.name} is an imported sub-clip. Generate editable .anim clips first.");
                    continue;
                }

                NormalizeGenericClip(clip, clip, referenceYaw, sampleModel, removeRootXZMotion, logs);
                EditorUtility.SetDirty(clip);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return string.Join("\n", logs.ToArray());
        }

        static IEnumerable<Object> EnumerateManualAnimationSources(VARCOCharacterPrefabBuildSettings settings)
        {
            if (settings == null)
                yield break;

            yield return settings.idleAnimationSource;
            yield return settings.walkAnimationSource;
            yield return settings.runAnimationSource;
            yield return settings.jumpAnimationSource;
            yield return settings.attackAnimationSource;
            yield return settings.deadAnimationSource;
        }

        static bool HasManualAnimationSources(VARCOCharacterPrefabBuildSettings settings)
        {
            return EnumerateManualAnimationSources(settings).Any(source => source);
        }

        static void AddUniqueObject(List<Object> list, Object obj)
        {
            if (!obj || list.Contains(obj))
                return;

            list.Add(obj);
        }

        static Dictionary<string, AnimationClip> CopyRoleClips(
            AnimationPlan plan,
            string clipFolder,
            VARCOCharacterPrefabBuildSettings settings,
            Dictionary<string, float> referenceYaw,
            GameObject modelAsset,
            VARCOCharacterPrefabBuildResult result)
        {
            var generated = new Dictionary<string, AnimationClip>();

            foreach (var role in RoleOrder)
            {
                if (!plan.roleClips.TryGetValue(role, out var sourceClip))
                    continue;

                string path = $"{clipFolder}/{settings.characterName}_{role}.anim";
                if (!PrepareAssetPath(path, settings.overwriteExisting, result.logs))
                    continue;

                var copy = new AnimationClip();
                EditorUtility.CopySerialized(sourceClip.clip, copy);
                copy.name = $"{settings.characterName}_{role}";
                ConfigureLoop(copy, role == "Idle" || role == "Walk" || role == "Run");

                if (settings.normalizeGenericDirection && !sourceClip.clip.humanMotion)
                    NormalizeGenericClip(copy, sourceClip.clip, referenceYaw, modelAsset, settings.removeGenericRootXZMotion, result.logs);

                AssetDatabase.CreateAsset(copy, path);
                generated[role] = copy;
                result.roleClips[role] = copy;
            }

            return generated;
        }

        static AnimatorController CreateController(
            string controllerPath,
            Dictionary<string, AnimationClip> clips,
            VARCOCharacterPrefabBuildSettings settings,
            VARCOCharacterPrefabBuildResult result)
        {
            if (!PrepareAssetPath(controllerPath, settings.overwriteExisting, result.logs))
                return null;

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("IsWalk", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsRun", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsJump", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsAttack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsDead", AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;
            var idle = AddState(sm, "Idle", clips, new Vector3(250, 100, 0));
            var walk = AddState(sm, "Walk", clips, new Vector3(520, 100, 0));
            var run = AddState(sm, "Run", clips, new Vector3(790, 100, 0));
            var jump = AddState(sm, "Jump", clips, new Vector3(250, 320, 0));
            var attack = AddState(sm, "Attack", clips, new Vector3(520, 320, 0));
            var dead = AddState(sm, "Death", clips, new Vector3(790, 320, 0), "Dead");

            sm.defaultState = idle;

            if (walk.motion)
            {
                AddBoolTransition(idle, walk, "IsWalk", true, "IsRun", false);
                AddBoolTransition(walk, idle, "IsWalk", false, null, false);
            }

            if (run.motion)
            {
                AddBoolTransition(idle, run, "IsRun", true, null, false);
                AddBoolTransition(run, idle, "IsRun", false, "IsWalk", false);
                if (walk.motion)
                {
                    AddBoolTransition(walk, run, "IsRun", true, null, false);
                    AddBoolTransition(run, walk, "IsRun", false, "IsWalk", true);
                }
            }

            if (jump.motion)
                AddBoolAction(sm, idle, jump, "IsJump");
            if (attack.motion)
                AddTriggeredAction(sm, idle, attack, "IsAttack");
            if (dead.motion)
                AddAnyTrigger(sm, dead, "IsDead");

            result.logs.Add("Controller: " + controllerPath);
            return controller;
        }

        static GameObject CreateCharacterPrefab(
            GameObject modelAsset,
            RuntimeAnimatorController controller,
            VARCOCharacterPrefabBuildSettings settings,
            string prefabPath,
            VARCOCharacterPrefabBuildResult result)
        {
            if (!PrepareAssetPath(prefabPath, settings.overwriteExisting, result.logs))
                return null;

            var root = new GameObject(settings.characterName);
            try
            {
                var modelRoot = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                if (!modelRoot)
                    modelRoot = Object.Instantiate(modelAsset);
                modelRoot.name = "ModelRoot";
                modelRoot.transform.SetParent(root.transform, false);
                modelRoot.transform.localPosition = Vector3.zero;
                modelRoot.transform.localRotation = Quaternion.identity;
                modelRoot.transform.localScale = Vector3.one;

                var animator = modelRoot.GetComponent<Animator>();
                if (!animator)
                    animator = modelRoot.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.avatar = ControllerUsesHumanMotion(controller) ? ResolveAvatar(modelAsset) : null;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                AlignModelBottomToGround(modelRoot.transform);
                ConfigureCharacterRoot(root, modelRoot.transform, settings.characterKind);
                MarkFeature(root, settings.characterKind);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab)
                {
                    result.logs.Add("Prefab: " + prefabPath);
                    return prefab;
                }

                result.logs.Add("ERROR: 프리팹 저장 실패: " + prefabPath);
                return null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void ConfigureCharacterRoot(GameObject root, Transform modelRoot, VARCOCharacterPrefabKind kind)
        {
            bool isPlayer = kind == VARCOCharacterPrefabKind.AdventurePlayer || kind == VARCOCharacterPrefabKind.PlatformPlayer;
            bool usesNavMesh = !isPlayer;
            bool platform = kind == VARCOCharacterPrefabKind.PlatformPlayer;

            if (isPlayer)
                EnsureTag(root, "Player");

            if (platform)
            {
                var cc = root.AddComponent<CharacterController>();
                ApplyCapsuleFromBounds(cc, modelRoot);
                var controller = root.AddComponent<VWS.PlayerController_Platform>();
                controller.moveSpeed = Mathf.Max(controller.moveSpeed, 6f);
                controller.runMultiplier = Mathf.Max(controller.runMultiplier, 1.3f);
                controller.jumpForce = Mathf.Max(controller.jumpForce, 8f);
                controller.respawnAtStartOnFall = true;
                root.AddComponent<VWS.PlayerHealth>();
                root.AddComponent<VWS.PlayerAttack>();
                root.AddComponent<VWS.CollectibleCounter>();
            }
            else if (isPlayer)
            {
                var rb = root.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.constraints = RigidbodyConstraints.FreezeRotation;

                var capsule = root.AddComponent<CapsuleCollider>();
                ApplyCapsuleFromBounds(capsule, modelRoot);

                var controller = root.AddComponent<VWS.PlayerController_ThirdPerson>();
                controller.modelRoot = modelRoot;
                controller.useCameraSpace = true;
                controller.moveInFacingDirection = false;
                controller.applyRootMotionFromAnimation = false;
                controller.preferTopLevelVisualRoot = true;
                controller.visualYawOffset = 0f;
                controller.moveSpeed = Mathf.Max(controller.moveSpeed, 5f);
                controller.runMultiplier = Mathf.Max(controller.runMultiplier, 1.35f);
                root.AddComponent<VWS.PlayerHealth>();
                root.AddComponent<VWS.PlayerAttack>();
            }
            else
            {
                var agent = root.AddComponent<NavMeshAgent>();
                agent.speed = kind == VARCOCharacterPrefabKind.BossEnemy ? 2.7f : 3.4f;
                agent.acceleration = kind == VARCOCharacterPrefabKind.BossEnemy ? 10f : 14f;
                agent.angularSpeed = 720f;
                agent.stoppingDistance = kind == VARCOCharacterPrefabKind.BossEnemy ? 1.8f : 1.25f;

                var capsule = root.AddComponent<CapsuleCollider>();
                ApplyCapsuleFromBounds(capsule, modelRoot);

                var health = root.AddComponent<VWS.EnemyHealth>();
                health.maxHP = kind == VARCOCharacterPrefabKind.BossEnemy ? 220 : 45;
                var ai = root.AddComponent<VWS.EnemyAI_NavMesh>();
                ai.contactDamage = kind == VARCOCharacterPrefabKind.BossEnemy ? 15 : 6;
                ai.detectionRange = kind == VARCOCharacterPrefabKind.BossEnemy ? 18f : 12f;
            }

            var audio = root.GetComponent<AudioSource>();
            if (!audio)
                audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;

            var anchor = root.GetComponent<VWS.CharacterInitialYAnchor>();
            if (!anchor)
                anchor = root.AddComponent<VWS.CharacterInitialYAnchor>();
            if (!anchor.HasStoredInitialY)
                anchor.CaptureCurrentYAsInitial();
            anchor.ConfigureForRole(isPlayer, usesNavMesh, platform);

            var align = root.GetComponent<VWS.RuntimeGroundAlign>();
            if (!align)
                align = root.AddComponent<VWS.RuntimeGroundAlign>();
            align.alignOnEnable = true;
            align.alignVisualChildrenOnly = true;
            align.continuous = false;
            align.useRootY = true;
            align.alignDuration = 0f;
            align.alignFramesAfterEnable = isPlayer ? 12 : 6;
            align.footClearance = isPlayer ? 0.08f : 0.05f;
        }

        static void MarkFeature(GameObject root, VARCOCharacterPrefabKind kind)
        {
            var marker = root.GetComponent<VWS.PrefabFeatureMarker>();
            if (!marker)
                marker = root.AddComponent<VWS.PrefabFeatureMarker>();
            marker.prefabPreset = kind.ToString();
            marker.role = kind == VARCOCharacterPrefabKind.AdventurePlayer || kind == VARCOCharacterPrefabKind.PlatformPlayer
                ? "Player"
                : "Enemy";
            marker.appliedFeatures = kind == VARCOCharacterPrefabKind.PlatformPlayer
                ? new[] { "CharacterPrefab", "PlatformMove", "AnimationController", "GenericDirectionNormalized" }
                : marker.role == "Player"
                    ? new[] { "CharacterPrefab", "PlayerCore", "AnimationController", "GenericDirectionNormalized" }
                    : new[] { "CharacterPrefab", "EnemyCore", "NavMesh", "AnimationController", "GenericDirectionNormalized" };
            marker.validationPassed = true;
            marker.validationSummary = "Generated by VARCO Character Prefab Generator.";
        }

        static AnimationPlan BuildAnimationPlan(List<SourceAsset> sources, VARCOCharacterPrefabBuildSettings settings)
        {
            var plan = new AnimationPlan();
            var clips = sources.SelectMany(s => s.clips).ToList();
            var used = new HashSet<SourceClip>();
            var manualRoles = new HashSet<string>();

            AssignManualRole(plan, used, manualRoles, "Idle", settings.idleAnimationSource);
            AssignManualRole(plan, used, manualRoles, "Walk", settings.walkAnimationSource);
            AssignManualRole(plan, used, manualRoles, "Run", settings.runAnimationSource);
            AssignManualRole(plan, used, manualRoles, "Jump", settings.jumpAnimationSource);
            AssignManualRole(plan, used, manualRoles, "Attack", settings.attackAnimationSource);
            AssignManualRole(plan, used, manualRoles, "Dead", settings.deadAnimationSource);

            foreach (var clip in clips)
            {
                var role = ClassifyByText(clip);
                if (string.IsNullOrEmpty(role))
                    continue;
                if (manualRoles.Contains(role))
                    continue;

                AssignBest(plan, used, role, clip, "name hint");
            }

            var remaining = clips.Where(c => !used.Contains(c)).ToList();
            AssignByReferenceClips(plan, used, remaining);
            remaining = clips.Where(c => !used.Contains(c)).ToList();
            AssignByHeuristics(plan, used, remaining);

            return plan;
        }

        static void AssignManualRole(
            AnimationPlan plan,
            HashSet<SourceClip> used,
            HashSet<string> manualRoles,
            string role,
            Object source)
        {
            if (!source)
                return;

            var clip = ResolveManualRoleClip(source, role);
            if (clip == null)
            {
                plan.warnings.Add($"{role} manual slot has no usable animation clip: {AssetDatabase.GetAssetPath(source)}");
                return;
            }

            AssignRole(plan, used, role, clip, "manual slot");
            manualRoles.Add(role);
        }

        static SourceClip ResolveManualRoleClip(Object source, string role)
        {
            if (!source)
                return null;

            if (source is AnimationClip directClip)
            {
                return new SourceClip
                {
                    assetPath = AssetDatabase.GetAssetPath(directClip),
                    clip = directClip
                };
            }

            var path = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var clips = new List<SourceClip>();
            if (AssetDatabase.IsValidFolder(path))
            {
                foreach (var child in GatherAssetPathsFromFolder(path))
                    AddManualClipsFromPath(child, clips);
            }
            else
            {
                AddManualClipsFromPath(path, clips);
            }

            return PickManualClip(clips, role);
        }

        static void AddManualClipsFromPath(string path, List<SourceClip> clips)
        {
            if (!IsSupportedModelPath(path))
                return;

            var source = BuildSource(path);
            if (source == null)
                return;

            clips.AddRange(source.clips);
        }

        static SourceClip PickManualClip(List<SourceClip> clips, string role)
        {
            if (clips == null || clips.Count == 0)
                return null;

            return clips
                .OrderByDescending(clip => ManualRoleScore(clip, role))
                .ThenBy(clip => clip.clip.length)
                .FirstOrDefault();
        }

        static int ManualRoleScore(SourceClip clip, string role)
        {
            var text = $"{clip.assetPath} {clip.clip.name}".ToLowerInvariant();
            var roleText = role.ToLowerInvariant();
            var score = 0;

            if (ClassifyByText(clip) == role)
                score += 100;
            if (text.Contains(roleText))
                score += 50;
            if (Path.GetFileNameWithoutExtension(clip.assetPath).ToLowerInvariant().Contains(roleText))
                score += 25;

            return score;
        }

        static void AssignByReferenceClips(AnimationPlan plan, HashSet<SourceClip> used, List<SourceClip> remaining)
        {
            foreach (var role in RoleOrder)
            {
                if (plan.roleClips.ContainsKey(role))
                    continue;

                var reference = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{VarcoImportsFolder}/Player_{role}.anim");
                if (!reference)
                    continue;

                SourceClip best = null;
                float bestScore = float.MaxValue;
                float secondScore = float.MaxValue;

                foreach (var candidate in remaining)
                {
                    var distance = ComputeClipDistance(candidate.clip, reference);
                    if (distance.matchedCurves < 30)
                        continue;

                    if (distance.distance < bestScore)
                    {
                        secondScore = bestScore;
                        bestScore = distance.distance;
                        best = candidate;
                    }
                    else if (distance.distance < secondScore)
                    {
                        secondScore = distance.distance;
                    }
                }

                bool confident = best != null && bestScore < 0.35f && (secondScore == float.MaxValue || bestScore + 0.01f < secondScore);
                if (confident)
                    AssignRole(plan, used, role, best, $"reference similarity {bestScore:0.###}");
            }
        }

        static void AssignByHeuristics(AnimationPlan plan, HashSet<SourceClip> used, List<SourceClip> remaining)
        {
            if (!plan.roleClips.ContainsKey("Idle"))
                AssignRole(plan, used, "Idle", remaining.OrderByDescending(c => c.clip.length).FirstOrDefault(), "longest fallback");

            if (!plan.roleClips.ContainsKey("Run"))
                AssignRole(plan, used, "Run", remaining.Where(c => !used.Contains(c)).OrderBy(c => c.clip.length).FirstOrDefault(c => c.clip.length <= 2.2f), "short locomotion fallback");

            if (!plan.roleClips.ContainsKey("Dead"))
                AssignRole(plan, used, "Dead", remaining.Where(c => !used.Contains(c)).OrderBy(c => Mathf.Abs(c.clip.length - 1.8f)).FirstOrDefault(c => c.clip.length <= 3.0f), "short ending fallback");

            if (!plan.roleClips.ContainsKey("Walk"))
                AssignRole(plan, used, "Walk", remaining.Where(c => !used.Contains(c)).OrderBy(c => Mathf.Abs(c.clip.length - 2.8f)).FirstOrDefault(), "medium locomotion fallback");

            if (!plan.roleClips.ContainsKey("Attack"))
            {
                var attack = remaining.Where(c => !used.Contains(c))
                    .Select(c => new { clip = c, metrics = ComputeMotionMetrics(c.clip) })
                    .OrderByDescending(x => x.metrics.armEnergy - x.metrics.legEnergy * 0.25f)
                    .Select(x => x.clip)
                    .FirstOrDefault();
                AssignRole(plan, used, "Attack", attack, "motion heuristic");
            }

            if (!plan.roleClips.ContainsKey("Jump"))
            {
                var jump = remaining.Where(c => !used.Contains(c))
                    .Select(c => new { clip = c, metrics = ComputeMotionMetrics(c.clip) })
                    .OrderByDescending(x => x.metrics.legEnergy + x.metrics.bodyPositionRange * 8f)
                    .Select(x => x.clip)
                    .FirstOrDefault();
                AssignRole(plan, used, "Jump", jump, "motion heuristic");
            }

            foreach (var role in RoleOrder)
            {
                if (!plan.roleClips.ContainsKey(role))
                    plan.warnings.Add($"{role} 클립을 찾지 못했습니다. 컨트롤러는 해당 상태에서 Idle로 대체됩니다.");
            }
        }

        static void AssignBest(AnimationPlan plan, HashSet<SourceClip> used, string role, SourceClip clip, string reason)
        {
            if (!plan.roleClips.TryGetValue(role, out var existing))
            {
                AssignRole(plan, used, role, clip, reason);
                return;
            }

            var better = PickBetterClipForRole(role, existing, clip);
            if (better == existing)
                return;

            used.Remove(existing);
            AssignRole(plan, used, role, better, reason);
            plan.warnings.Add($"{role} 후보가 여러 개라 더 적합한 클립을 선택했습니다.");
        }

        static void AssignRole(AnimationPlan plan, HashSet<SourceClip> used, string role, SourceClip clip, string reason)
        {
            if (clip == null || used.Contains(clip))
                return;

            clip.reason = reason;
            plan.roleClips[role] = clip;
            used.Add(clip);
        }

        static SourceClip PickBetterClipForRole(string role, SourceClip a, SourceClip b)
        {
            if (role == "Idle")
                return a.clip.length >= b.clip.length ? a : b;
            if (role == "Run")
                return a.clip.length <= b.clip.length ? a : b;
            if (role == "Walk")
                return Mathf.Abs(a.clip.length - 2.8f) <= Mathf.Abs(b.clip.length - 2.8f) ? a : b;
            if (role == "Dead")
                return Mathf.Abs(a.clip.length - 1.8f) <= Mathf.Abs(b.clip.length - 1.8f) ? a : b;
            return a;
        }

        static List<SourceAsset> LoadSources(List<Object> roots, bool includeVarcoImports)
        {
            var paths = new HashSet<string>();
            foreach (var root in roots)
                AddPathsFromObject(root, paths);

            if (includeVarcoImports)
            {
                foreach (var path in GatherAssetPathsFromFolder(VarcoImportsFolder))
                    paths.Add(path);
            }

            return paths
                .Where(IsSupportedModelPath)
                .Select(BuildSource)
                .Where(s => s != null)
                .ToList();
        }

        static void AddPathsFromObject(Object obj, HashSet<string> paths)
        {
            if (!obj)
                return;

            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (AssetDatabase.IsValidFolder(path))
            {
                foreach (var child in GatherAssetPathsFromFolder(path))
                    paths.Add(child);
                return;
            }

            paths.Add(path);
        }

        static IEnumerable<string> GatherAssetPathsFromFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                yield break;

            foreach (var guid in AssetDatabase.FindAssets("", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrWhiteSpace(path))
                    yield return path;
            }
        }

        static bool IsSupportedModelPath(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".usdz" || ext == ".fbx" || ext == ".glb" || ext == ".gltf" || ext == ".obj" || ext == ".prefab" || ext == ".anim";
        }

        static SourceAsset BuildSource(string path)
        {
            var main = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var clips = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal))
                .Select(c => new SourceClip { assetPath = path, clip = c })
                .ToList();

            if (!main && clips.Count == 0)
                return null;

            var source = new SourceAsset
            {
                assetPath = path,
                mainAsset = main,
                clips = clips
            };

            if (main)
            {
                source.modelSignature = BuildModelSignature(main);
                source.skeletonSignature = BuildSkeletonSignature(main);
                source.rendererCount = main.GetComponentsInChildren<Renderer>(true).Length;
                source.skinnedRendererCount = main.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                source.boneCount = main.GetComponentsInChildren<Transform>(true).Length;
            }

            foreach (var clip in clips)
                clip.source = source;

            return source;
        }

        static SourceAsset BuildModelOnlySource(GameObject modelAsset)
        {
            return new SourceAsset
            {
                assetPath = AssetDatabase.GetAssetPath(modelAsset),
                mainAsset = modelAsset,
                modelSignature = BuildModelSignature(modelAsset),
                skeletonSignature = BuildSkeletonSignature(modelAsset),
                rendererCount = modelAsset.GetComponentsInChildren<Renderer>(true).Length,
                skinnedRendererCount = modelAsset.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length,
                boneCount = modelAsset.GetComponentsInChildren<Transform>(true).Length
            };
        }

        static SourceAsset FindSourceForModel(GameObject modelAsset, List<SourceAsset> sources)
        {
            var path = AssetDatabase.GetAssetPath(modelAsset);
            return sources.FirstOrDefault(s => s.assetPath == path) ?? sources.FirstOrDefault(s => s.mainAsset == modelAsset);
        }

        static List<SourceAsset> FilterSourcesForModel(SourceAsset modelSource, List<SourceAsset> sources)
        {
            var matched = sources.Where(s => MatchesModelSource(modelSource, s)).ToList();
            return matched.Count > 0 ? matched : sources;
        }

        static bool MatchesModelSource(SourceAsset model, SourceAsset source)
        {
            if (source == model || source.mainAsset == model.mainAsset)
                return true;

            if (!string.IsNullOrEmpty(model.modelSignature) && model.modelSignature == source.modelSignature)
                return true;

            if (!string.IsNullOrEmpty(model.skeletonSignature) && model.skeletonSignature == source.skeletonSignature)
                return true;

            return string.IsNullOrEmpty(source.modelSignature) && string.IsNullOrEmpty(source.skeletonSignature);
        }

        static GameObject FindFirstModelAsset(List<Object> roots)
        {
            foreach (var root in roots)
            {
                var path = AssetDatabase.GetAssetPath(root);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                if (AssetDatabase.IsValidFolder(path))
                {
                    foreach (var child in GatherAssetPathsFromFolder(path))
                    {
                        if (!IsSupportedModelPath(child))
                            continue;
                        var model = AssetDatabase.LoadAssetAtPath<GameObject>(child);
                        if (model && model.GetComponentsInChildren<Renderer>(true).Length > 0)
                            return model;
                    }
                }
                else
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (model && model.GetComponentsInChildren<Renderer>(true).Length > 0)
                        return model;
                }
            }

            return null;
        }

        static string ClassifyByText(SourceClip clip)
        {
            var text = $"{clip.assetPath} {clip.clip.name}".ToLowerInvariant();
            if (ContainsAny(text, "idle", "wait", "stand", "breath", "대기"))
                return "Idle";
            if (ContainsAny(text, "walk", "walking", "걷"))
                return "Walk";
            if (ContainsAny(text, "run", "running", "sprint", "dash", "뛰"))
                return "Run";
            if (ContainsAny(text, "jump", "leap", "fall", "점프"))
                return "Jump";
            if (ContainsAny(text, "attack", "atk", "slash", "punch", "kick", "shoot", "hit", "공격"))
                return "Attack";
            if (ContainsAny(text, "dead", "death", "die", "dying", "죽"))
                return "Dead";
            return null;
        }

        static bool ContainsAny(string text, params string[] needles)
        {
            foreach (var needle in needles)
            {
                if (text.Contains(needle))
                    return true;
            }
            return false;
        }

        static Dictionary<string, float> ResolveReferenceYaw(AnimationPlan plan, GameObject modelAsset)
        {
            var reference = new Dictionary<string, float>();
            if (!plan.roleClips.TryGetValue("Idle", out var idle))
                return reference;

            if (modelAsset && TryEvaluateSampledBodyYaw(modelAsset, idle.clip, out var bodyYaw))
                reference[SampledBodyYawReferenceKey] = 0f;

            var root = FindTopRotationPath(idle.clip);
            if (string.IsNullOrEmpty(root))
                return reference;

            reference[root] = 0f;
            return reference;
        }

        static void NormalizeGenericClip(
            AnimationClip generatedClip,
            AnimationClip sourceClip,
            Dictionary<string, float> referenceYaw,
            GameObject modelAsset,
            bool removeRootXZMotion,
            List<string> logs)
        {
            var rootPath = FindTopRotationPath(sourceClip);
            if (string.IsNullOrEmpty(rootPath))
                return;

            float sourceYaw = 0f;
            float targetYaw = 0f;
            var usedSampledBodyYaw = modelAsset
                && referenceYaw.TryGetValue(SampledBodyYawReferenceKey, out targetYaw)
                && TryEvaluateSampledBodyYaw(modelAsset, sourceClip, out sourceYaw);

            if (!usedSampledBodyYaw)
            {
                sourceYaw = EvaluateRootYaw(sourceClip, rootPath, 0f);
                targetYaw = referenceYaw.TryGetValue(rootPath, out var yaw) ? yaw : 0f;
            }

            var correctionYaw = Mathf.DeltaAngle(sourceYaw, targetYaw);
            if (Mathf.Abs(correctionYaw) <= 0.01f && !removeRootXZMotion)
                return;

            ApplyRootCorrection(generatedClip, rootPath, Quaternion.Euler(0f, correctionYaw, 0f), removeRootXZMotion);
            var mode = usedSampledBodyYaw ? "sampled body" : "root rotation";
            logs.Add($"Generic direction normalized: {generatedClip.name}, mode={mode}, root={rootPath}, sourceYaw={sourceYaw:0.##}, targetYaw={targetYaw:0.##}, yawCorrection={correctionYaw:0.##}");

            if (usedSampledBodyYaw && modelAsset && TryEvaluateSampledBodyYaw(modelAsset, generatedClip, out var refinedYaw))
            {
                var residualYaw = Mathf.DeltaAngle(refinedYaw, targetYaw);
                if (Mathf.Abs(residualYaw) > 0.25f)
                {
                    ApplyRootCorrection(generatedClip, rootPath, Quaternion.Euler(0f, residualYaw, 0f), removeRootXZMotion);
                    logs.Add($"Generic direction refinement: {generatedClip.name}, refinedYaw={refinedYaw:0.##}, residualCorrection={residualYaw:0.##}");
                }
            }

            if (usedSampledBodyYaw && modelAsset)
                BakeRootRotationIntoDirectChildren(generatedClip, modelAsset, rootPath, logs);
        }

        static void ApplyRootCorrection(AnimationClip clip, string rootPath, Quaternion correction, bool removeRootXZMotion)
        {
            var bindings = AnimationUtility.GetCurveBindings(clip);
            var rotCurves = new Dictionary<string, AnimationCurve>();
            var posCurves = new Dictionary<string, AnimationCurve>();

            foreach (var binding in bindings)
            {
                if (binding.path != rootPath)
                    continue;

                if (binding.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal))
                    rotCurves[binding.propertyName] = AnimationUtility.GetEditorCurve(clip, binding);
                else if (binding.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal))
                    posCurves[binding.propertyName] = AnimationUtility.GetEditorCurve(clip, binding);
            }

            RewriteRootRotationCurves(clip, rootPath, rotCurves, correction);
            RewriteRootPositionCurves(clip, rootPath, posCurves, correction, removeRootXZMotion);
        }

        static void BakeRootRotationIntoDirectChildren(AnimationClip clip, GameObject modelAsset, string rootPath, List<string> logs)
        {
            var rootTransform = FindRelativeTransform(modelAsset.transform, rootPath);
            if (!rootTransform || rootTransform.childCount == 0)
                return;

            var rotCurves = new Dictionary<string, AnimationCurve>();
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.path == rootPath && binding.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal))
                    rotCurves[binding.propertyName] = AnimationUtility.GetEditorCurve(clip, binding);
            }

            if (!rotCurves.TryGetValue("m_LocalRotation.x", out var x)
                || !rotCurves.TryGetValue("m_LocalRotation.y", out var y)
                || !rotCurves.TryGetValue("m_LocalRotation.z", out var z)
                || !rotCurves.TryGetValue("m_LocalRotation.w", out var w))
                return;

            var times = CollectTimes(x, y, z, w);
            if (times.Count == 0)
                return;

            var needsBake = times.Any(time => Quaternion.Angle(EvaluateRootRotation(clip, rootPath, time), Quaternion.identity) > 0.01f);
            if (!needsBake)
            {
                SetRootRotationIdentity(clip, rootPath, times);
                return;
            }

            for (int childIndex = 0; childIndex < rootTransform.childCount; childIndex++)
            {
                var child = rootTransform.GetChild(childIndex);
                var childPath = string.IsNullOrEmpty(rootPath) ? child.name : rootPath + "/" + child.name;
                var defaultRotation = child.localRotation;
                var defaultPosition = child.localPosition;

                var rx = new AnimationCurve();
                var ry = new AnimationCurve();
                var rz = new AnimationCurve();
                var rw = new AnimationCurve();
                var px = new AnimationCurve();
                var py = new AnimationCurve();
                var pz = new AnimationCurve();
                var previous = Quaternion.identity;
                var hasPrevious = false;

                foreach (var time in times)
                {
                    var rootRotation = EvaluateRootRotation(clip, rootPath, time);
                    var childRotation = Normalize(rootRotation * defaultRotation);
                    childRotation = CanonicalizeQuaternion(childRotation, previous, hasPrevious);
                    var childPosition = rootRotation * defaultPosition;

                    rx.AddKey(time, childRotation.x);
                    ry.AddKey(time, childRotation.y);
                    rz.AddKey(time, childRotation.z);
                    rw.AddKey(time, childRotation.w);
                    px.AddKey(time, childPosition.x);
                    py.AddKey(time, childPosition.y);
                    pz.AddKey(time, childPosition.z);

                    previous = childRotation;
                    hasPrevious = true;
                }

                SetCurve(clip, childPath, "m_LocalRotation.x", rx);
                SetCurve(clip, childPath, "m_LocalRotation.y", ry);
                SetCurve(clip, childPath, "m_LocalRotation.z", rz);
                SetCurve(clip, childPath, "m_LocalRotation.w", rw);
                SetCurve(clip, childPath, "m_LocalPosition.x", px);
                SetCurve(clip, childPath, "m_LocalPosition.y", py);
                SetCurve(clip, childPath, "m_LocalPosition.z", pz);
            }

            SetRootRotationIdentity(clip, rootPath, times);
            clip.EnsureQuaternionContinuity();
            logs.Add($"Generic root rotation baked into children: {clip.name}, root={rootPath}, children={rootTransform.childCount}");
        }

        static void SetRootRotationIdentity(AnimationClip clip, string rootPath, List<float> times)
        {
            var identityX = new AnimationCurve();
            var identityY = new AnimationCurve();
            var identityZ = new AnimationCurve();
            var identityW = new AnimationCurve();
            foreach (var time in times)
            {
                identityX.AddKey(time, 0f);
                identityY.AddKey(time, 0f);
                identityZ.AddKey(time, 0f);
                identityW.AddKey(time, 1f);
            }

            SetCurve(clip, rootPath, "m_LocalRotation.x", identityX);
            SetCurve(clip, rootPath, "m_LocalRotation.y", identityY);
            SetCurve(clip, rootPath, "m_LocalRotation.z", identityZ);
            SetCurve(clip, rootPath, "m_LocalRotation.w", identityW);
        }

        static Transform FindRelativeTransform(Transform root, string path)
        {
            if (!root)
                return null;
            if (string.IsNullOrEmpty(path))
                return root;

            var current = root;
            var parts = path.Split('/');
            foreach (var part in parts)
            {
                current = current.Find(part);
                if (!current)
                    return null;
            }

            return current;
        }

        static bool TryEvaluateSampledBodyYaw(GameObject modelAsset, AnimationClip clip, out float yaw)
        {
            yaw = 0f;
            if (!modelAsset || !clip)
                return false;

            GameObject instance = null;
            try
            {
                instance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
                if (!instance)
                    instance = Object.Instantiate(modelAsset);
                if (!instance)
                    return false;

                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                var sum = Vector3.zero;
                var sampleCount = 0;
                var samples = clip.length > 0.001f
                    ? new[] { 0f, 0.25f, 0.5f, 0.75f }
                    : new[] { 0f };

                foreach (var normalizedTime in samples)
                {
                    clip.SampleAnimation(instance, clip.length * normalizedTime);
                    if (!TryEstimateBodyForward(instance.transform, out var forward))
                        continue;

                    sum += forward;
                    sampleCount++;
                }

                sum.y = 0f;
                if (sampleCount == 0 || sum.sqrMagnitude <= 0.0001f)
                    return false;

                sum.Normalize();
                yaw = Mathf.Atan2(sum.x, sum.z) * Mathf.Rad2Deg;
                return true;
            }
            finally
            {
                if (instance)
                    Object.DestroyImmediate(instance);
            }
        }

        static bool TryEstimateBodyForward(Transform root, out Vector3 forward)
        {
            forward = Vector3.zero;
            if (!root)
                return false;

            AddPairForward(root, IsShoulderBoneName, 2f, ref forward);
            AddPairForward(root, IsUpperArmBoneName, 1f, ref forward);
            AddPairForward(root, IsUpperLegBoneName, 2f, ref forward);

            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                return false;

            forward.Normalize();
            return true;
        }

        static void AddPairForward(Transform root, Func<string, bool> category, float weight, ref Vector3 sum)
        {
            if (!TryFindLeftRightBone(root, category, out var left, out var right))
                return;

            var rightAxis = right.position - left.position;
            rightAxis.y = 0f;
            if (rightAxis.sqrMagnitude <= 0.0001f)
                return;

            rightAxis.Normalize();
            var forward = Vector3.Cross(rightAxis, Vector3.up);
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                return;

            sum += forward.normalized * weight;
        }

        static bool TryFindLeftRightBone(Transform root, Func<string, bool> category, out Transform left, out Transform right)
        {
            left = null;
            right = null;
            var bestLeftScore = int.MinValue;
            var bestRightScore = int.MinValue;

            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform == root)
                    continue;

                var name = transform.name.ToLowerInvariant();
                if (!category(name))
                    continue;

                var score = BoneNameScore(name);
                if (IsLeftBoneName(name) && score > bestLeftScore)
                {
                    left = transform;
                    bestLeftScore = score;
                }
                else if (IsRightBoneName(name) && score > bestRightScore)
                {
                    right = transform;
                    bestRightScore = score;
                }
            }

            return left && right;
        }

        static int BoneNameScore(string name)
        {
            if (ContainsAny(name, "shoulder", "clavicle", "upper_arm", "upperarm", "thigh", "upper_leg", "upperleg"))
                return 3;
            if (ContainsAny(name, "arm", "leg"))
                return 2;
            return 1;
        }

        static bool IsShoulderBoneName(string name)
        {
            return ContainsAny(name, "shoulder", "clavicle");
        }

        static bool IsUpperArmBoneName(string name)
        {
            return ContainsAny(name, "upper_arm", "upperarm");
        }

        static bool IsUpperLegBoneName(string name)
        {
            return ContainsAny(name, "thigh", "upper_leg", "upperleg");
        }

        static bool IsLeftBoneName(string name)
        {
            return name.Contains("left")
                || name.StartsWith("l_", StringComparison.Ordinal)
                || name.EndsWith("_l", StringComparison.Ordinal)
                || name.Contains("_l_", StringComparison.Ordinal)
                || name.EndsWith(".l", StringComparison.Ordinal)
                || name.Contains(".l.", StringComparison.Ordinal)
                || name.EndsWith("-l", StringComparison.Ordinal);
        }

        static bool IsRightBoneName(string name)
        {
            return name.Contains("right")
                || name.StartsWith("r_", StringComparison.Ordinal)
                || name.EndsWith("_r", StringComparison.Ordinal)
                || name.Contains("_r_", StringComparison.Ordinal)
                || name.EndsWith(".r", StringComparison.Ordinal)
                || name.Contains(".r.", StringComparison.Ordinal)
                || name.EndsWith("-r", StringComparison.Ordinal);
        }

        static void RewriteRootRotationCurves(AnimationClip clip, string rootPath, Dictionary<string, AnimationCurve> curves, Quaternion correction)
        {
            if (!curves.TryGetValue("m_LocalRotation.x", out var x)
                || !curves.TryGetValue("m_LocalRotation.y", out var y)
                || !curves.TryGetValue("m_LocalRotation.z", out var z)
                || !curves.TryGetValue("m_LocalRotation.w", out var w))
                return;

            var times = CollectTimes(x, y, z, w);
            var nx = new AnimationCurve();
            var ny = new AnimationCurve();
            var nz = new AnimationCurve();
            var nw = new AnimationCurve();
            var previous = Quaternion.identity;
            var hasPrevious = false;

            foreach (var time in times)
            {
                var q = Normalize(new Quaternion(x.Evaluate(time), y.Evaluate(time), z.Evaluate(time), w.Evaluate(time)));
                q = Normalize(correction * q);
                q = CanonicalizeQuaternion(q, previous, hasPrevious);
                nx.AddKey(time, q.x);
                ny.AddKey(time, q.y);
                nz.AddKey(time, q.z);
                nw.AddKey(time, q.w);
                previous = q;
                hasPrevious = true;
            }

            SetCurve(clip, rootPath, "m_LocalRotation.x", nx);
            SetCurve(clip, rootPath, "m_LocalRotation.y", ny);
            SetCurve(clip, rootPath, "m_LocalRotation.z", nz);
            SetCurve(clip, rootPath, "m_LocalRotation.w", nw);
            clip.EnsureQuaternionContinuity();
        }

        static void RewriteRootPositionCurves(AnimationClip clip, string rootPath, Dictionary<string, AnimationCurve> curves, Quaternion correction, bool removeXZ)
        {
            if (!curves.TryGetValue("m_LocalPosition.x", out var x)
                || !curves.TryGetValue("m_LocalPosition.y", out var y)
                || !curves.TryGetValue("m_LocalPosition.z", out var z))
                return;

            var times = CollectTimes(x, y, z);
            var nx = new AnimationCurve();
            var ny = new AnimationCurve();
            var nz = new AnimationCurve();

            foreach (var time in times)
            {
                var p = new Vector3(x.Evaluate(time), y.Evaluate(time), z.Evaluate(time));
                p = correction * p;
                if (removeXZ)
                {
                    p.x = 0f;
                    p.z = 0f;
                }
                nx.AddKey(time, p.x);
                ny.AddKey(time, p.y);
                nz.AddKey(time, p.z);
            }

            SetCurve(clip, rootPath, "m_LocalPosition.x", nx);
            SetCurve(clip, rootPath, "m_LocalPosition.y", ny);
            SetCurve(clip, rootPath, "m_LocalPosition.z", nz);
        }

        static List<float> CollectTimes(params AnimationCurve[] curves)
        {
            return curves
                .Where(c => c != null)
                .SelectMany(c => c.keys.Select(k => k.time))
                .Distinct()
                .OrderBy(t => t)
                .ToList();
        }

        static void SetCurve(AnimationClip clip, string path, string property, AnimationCurve curve)
        {
            var binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), property);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        static string FindTopRotationPath(AnimationClip clip)
        {
            return AnimationUtility.GetCurveBindings(clip)
                .Where(b => b.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal) && !string.IsNullOrEmpty(b.path))
                .Select(b => b.path)
                .Distinct()
                .OrderBy(p => p.Count(c => c == '/'))
                .ThenBy(p => p.Length)
                .FirstOrDefault();
        }

        static float EvaluateRootYaw(AnimationClip clip, string rootPath, float time)
        {
            var q = EvaluateRootRotation(clip, rootPath, time);
            var forward = q * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                return 0f;
            forward.Normalize();
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        static Quaternion EvaluateRootRotation(AnimationClip clip, string rootPath, float time)
        {
            float x = 0f;
            float y = 0f;
            float z = 0f;
            float w = 1f;

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.path != rootPath || !binding.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal))
                    continue;

                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null)
                    continue;

                if (binding.propertyName.EndsWith(".x", StringComparison.Ordinal))
                    x = curve.Evaluate(time);
                else if (binding.propertyName.EndsWith(".y", StringComparison.Ordinal))
                    y = curve.Evaluate(time);
                else if (binding.propertyName.EndsWith(".z", StringComparison.Ordinal))
                    z = curve.Evaluate(time);
                else if (binding.propertyName.EndsWith(".w", StringComparison.Ordinal))
                    w = curve.Evaluate(time);
            }

            return Normalize(new Quaternion(x, y, z, w));
        }

        static Quaternion Normalize(Quaternion q)
        {
            var mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (mag <= 0.00001f)
                return Quaternion.identity;
            return new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag);
        }

        static Quaternion CanonicalizeQuaternion(Quaternion q, Quaternion previous, bool hasPrevious)
        {
            if (hasPrevious)
                return QuaternionDot(previous, q) < 0f ? FlipQuaternion(q) : q;

            var ax = Mathf.Abs(q.x);
            var ay = Mathf.Abs(q.y);
            var az = Mathf.Abs(q.z);
            var aw = Mathf.Abs(q.w);

            var sign = q.w;
            if (ax >= ay && ax >= az && ax >= aw)
                sign = q.x;
            else if (ay >= ax && ay >= az && ay >= aw)
                sign = q.y;
            else if (az >= ax && az >= ay && az >= aw)
                sign = q.z;

            return sign < 0f ? FlipQuaternion(q) : q;
        }

        static float QuaternionDot(Quaternion a, Quaternion b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
        }

        static Quaternion FlipQuaternion(Quaternion q)
        {
            return new Quaternion(-q.x, -q.y, -q.z, -q.w);
        }

        static MotionMetrics ComputeMotionMetrics(AnimationClip clip)
        {
            var metrics = new MotionMetrics();

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length < 2)
                    continue;

                float energy = 0f;
                float min = float.MaxValue;
                float max = float.MinValue;
                float previous = curve.Evaluate(0f);

                for (int i = 0; i <= 20; i++)
                {
                    var value = curve.Evaluate(clip.length * i / 20f);
                    min = Mathf.Min(min, value);
                    max = Mathf.Max(max, value);
                    energy += Mathf.Abs(value - previous);
                    previous = value;
                }

                var path = binding.path.ToLowerInvariant();
                if (ContainsAny(path, "shoulder", "upper_arm", "forearm", "hand", "arm"))
                    metrics.armEnergy += energy;
                if (ContainsAny(path, "thigh", "shin", "foot", "toe", "leg"))
                    metrics.legEnergy += energy;
                if (ContainsAny(path, "root", "spine", "hips", "pelvis"))
                {
                    metrics.bodyEnergy += energy;
                    if (binding.propertyName.Contains("m_LocalPosition"))
                        metrics.bodyPositionRange += max - min;
                }
            }

            return metrics;
        }

        static ClipDistance ComputeClipDistance(AnimationClip candidate, AnimationClip reference)
        {
            var referenceCurves = new Dictionary<string, AnimationCurve>();
            foreach (var binding in AnimationUtility.GetCurveBindings(reference))
            {
                var key = CurveKey(binding);
                if (!referenceCurves.ContainsKey(key))
                    referenceCurves.Add(key, AnimationUtility.GetEditorCurve(reference, binding));
            }

            double total = 0.0;
            int samples = 0;
            int matched = 0;

            foreach (var binding in AnimationUtility.GetCurveBindings(candidate))
            {
                if (!referenceCurves.TryGetValue(CurveKey(binding), out var referenceCurve))
                    continue;

                var candidateCurve = AnimationUtility.GetEditorCurve(candidate, binding);
                if (candidateCurve == null || referenceCurve == null)
                    continue;

                matched++;
                for (int i = 0; i <= 20; i++)
                {
                    float normalized = i / 20f;
                    double d = candidateCurve.Evaluate(candidate.length * normalized) - referenceCurve.Evaluate(reference.length * normalized);
                    total += d * d;
                    samples++;
                }
            }

            return samples == 0
                ? new ClipDistance { distance = float.MaxValue, matchedCurves = 0 }
                : new ClipDistance { distance = (float)Math.Sqrt(total / samples), matchedCurves = matched };
        }

        static string CurveKey(EditorCurveBinding binding)
        {
            return $"{binding.path}|{binding.type.FullName}|{binding.propertyName}";
        }

        static AnimatorState AddState(AnimatorStateMachine sm, string role, Dictionary<string, AnimationClip> clips, Vector3 position, string clipRole = null)
        {
            var state = sm.AddState(role, position);
            if (clips.TryGetValue(clipRole ?? role, out var clip))
                state.motion = clip;
            return state;
        }

        static void AddBoolTransition(AnimatorState from, AnimatorState to, string firstParam, bool firstValue, string secondParam, bool secondValue)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            ConfigureTransitionBlend(transition, LocomotionTransitionSeconds);
            transition.AddCondition(firstValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, firstParam);
            if (!string.IsNullOrEmpty(secondParam))
                transition.AddCondition(secondValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, secondParam);
        }

        static void AddBoolAction(AnimatorStateMachine sm, AnimatorState idle, AnimatorState action, string parameter)
        {
            var transition = sm.AddAnyStateTransition(action);
            transition.hasExitTime = false;
            ConfigureTransitionBlend(transition, ActionTransitionSeconds);
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, parameter);

            var back = action.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 0.98f;
            ConfigureTransitionBlend(back, ActionTransitionSeconds);
        }

        static void AddTriggeredAction(AnimatorStateMachine sm, AnimatorState idle, AnimatorState action, string parameter)
        {
            AddAnyTrigger(sm, action, parameter);
            var back = action.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 0.98f;
            ConfigureTransitionBlend(back, ActionTransitionSeconds);
        }

        static void AddAnyTrigger(AnimatorStateMachine sm, AnimatorState action, string parameter)
        {
            var transition = sm.AddAnyStateTransition(action);
            transition.hasExitTime = false;
            ConfigureTransitionBlend(transition, ActionTransitionSeconds);
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, parameter);
        }

        static void ConfigureTransitionBlend(AnimatorStateTransition transition, float seconds)
        {
            transition.hasFixedDuration = true;
            transition.duration = seconds;
            transition.offset = 0f;
        }

        static void ConfigureLoop(AnimationClip clip, bool loop)
        {
            clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.loopBlend = loop;
            settings.loopBlendOrientation = false;
            settings.loopBlendPositionY = false;
            settings.loopBlendPositionXZ = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        static Avatar ResolveAvatar(GameObject modelAsset)
        {
            var path = AssetDatabase.GetAssetPath(modelAsset);
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault(a => a && a.isValid);
        }

        static bool ControllerUsesHumanMotion(RuntimeAnimatorController controller)
        {
            if (!controller)
                return false;

            foreach (var clip in controller.animationClips)
            {
                if (clip && clip.humanMotion)
                    return true;
            }

            return false;
        }

        static void AlignModelBottomToGround(Transform modelRoot)
        {
            var renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            modelRoot.position -= new Vector3(0f, bounds.min.y, 0f);
        }

        static void ApplyCapsuleFromBounds(CapsuleCollider capsule, Transform modelRoot)
        {
            var bounds = CalculateWorldBounds(modelRoot);
            float height = Mathf.Max(0.8f, bounds.size.y);
            float radius = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.35f, 0.18f, 0.65f);
            capsule.center = new Vector3(bounds.center.x - modelRoot.root.position.x, bounds.min.y + height * 0.5f - modelRoot.root.position.y, bounds.center.z - modelRoot.root.position.z);
            capsule.height = height;
            capsule.radius = radius;
        }

        static void ApplyCapsuleFromBounds(CharacterController controller, Transform modelRoot)
        {
            var bounds = CalculateWorldBounds(modelRoot);
            float height = Mathf.Max(1.6f, bounds.size.y);
            float radius = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.35f, 0.25f, 0.7f);
            controller.center = new Vector3(bounds.center.x - modelRoot.root.position.x, bounds.min.y + height * 0.5f - modelRoot.root.position.y, bounds.center.z - modelRoot.root.position.z);
            controller.height = height;
            controller.radius = radius;
        }

        static Bounds CalculateWorldBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(root.position + Vector3.up * 0.8f, new Vector3(0.6f, 1.6f, 0.6f));

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        static void EnsureTag(GameObject go, string tag)
        {
            if (!UnityEditorInternal.InternalEditorUtility.tags.Contains(tag))
                UnityEditorInternal.InternalEditorUtility.AddTag(tag);
            go.tag = tag;
        }

        static bool PrepareAssetPath(string path, bool overwrite, List<string> logs)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (!existing)
                return true;

            if (!overwrite)
            {
                logs.Add("WARN: existing asset skipped: " + path);
                return false;
            }

            if (!AssetDatabase.DeleteAsset(path))
            {
                logs.Add("ERROR: cannot overwrite asset: " + path);
                return false;
            }

            return true;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static string BuildModelSignature(GameObject root)
        {
            var sb = new StringBuilder();
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(r => GetRelativePath(root.transform, r.transform)))
            {
                var mesh = renderer.sharedMesh;
                sb.Append("SMR|").Append(GetRelativePath(root.transform, renderer.transform)).Append('|');
                if (mesh)
                    sb.Append(mesh.vertexCount).Append('|').Append(mesh.subMeshCount).Append('|').Append(mesh.triangles.Length).Append('|').Append(mesh.bounds.size.ToString("F4"));
                sb.Append("|Bones=");
                foreach (var bone in renderer.bones)
                    if (bone) sb.Append(GetRelativePath(root.transform, bone)).Append(';');
                sb.AppendLine();
            }

            if (sb.Length == 0)
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true).OrderBy(r => GetRelativePath(root.transform, r.transform)))
                    sb.Append("Renderer|").Append(GetRelativePath(root.transform, renderer.transform)).Append('|').Append(renderer.GetType().Name).AppendLine();
            }

            return sb.Length == 0 ? null : Hash(sb.ToString());
        }

        static string BuildSkeletonSignature(GameObject root)
        {
            var names = root.GetComponentsInChildren<Transform>(true)
                .Select(t => t.name)
                .Where(n => IsBoneLikeName(n))
                .OrderBy(n => n)
                .ToArray();
            return names.Length == 0 ? null : Hash(string.Join("|", names));
        }

        static bool IsBoneLikeName(string name)
        {
            var lower = name.ToLowerInvariant();
            return ContainsAny(lower, "root", "hip", "pelvis", "spine", "neck", "head", "shoulder", "arm", "hand", "thigh", "leg", "shin", "foot", "toe", "metarig");
        }

        static string GetRelativePath(Transform root, Transform child)
        {
            if (child == root)
                return "";

            var stack = new Stack<string>();
            var current = child;
            while (current && current != root)
            {
                stack.Push(current.name);
                current = current.parent;
            }
            return string.Join("/", stack.ToArray());
        }

        static string SafeName(string value)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return string.IsNullOrWhiteSpace(value) ? "VARCO_Character" : value;
        }

        static string Hash(string value)
        {
            using (var sha1 = SHA1.Create())
            {
                return BitConverter.ToString(sha1.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
            }
        }

        static string ShortHash(string hash)
        {
            return string.IsNullOrEmpty(hash) ? "none" : hash.Substring(0, Mathf.Min(8, hash.Length));
        }

        sealed class SourceAsset
        {
            public string assetPath;
            public GameObject mainAsset;
            public List<SourceClip> clips = new List<SourceClip>();
            public string modelSignature;
            public string skeletonSignature;
            public int rendererCount;
            public int skinnedRendererCount;
            public int boneCount;
        }

        sealed class SourceClip
        {
            public SourceAsset source;
            public string assetPath;
            public AnimationClip clip;
            public string reason;
        }

        sealed class AnimationPlan
        {
            public readonly Dictionary<string, SourceClip> roleClips = new Dictionary<string, SourceClip>();
            public readonly List<string> warnings = new List<string>();
        }

        struct MotionMetrics
        {
            public float armEnergy;
            public float legEnergy;
            public float bodyEnergy;
            public float bodyPositionRange;
        }

        struct ClipDistance
        {
            public float distance;
            public int matchedCurves;
        }
    }
}
#endif
