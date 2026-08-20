#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using VWS = VARCO_Workshop;

namespace VARCO_Workshop.Editor
{
    public static class VARCOPrefabRepairUtility
    {
        const string AnimationOutputFolder = "Assets/Animations/Generated";

        public static bool RepairGameplayPrefab(GameObject target, string roleHint, string contextText, IList<string> log = null)
        {
            if (!target)
                return false;

            var changed = false;
            var roleText = Normalize(roleHint + " " + target.name + " " + contextText);
            var hasThirdPerson = target.GetComponent<VWS.PlayerController_ThirdPerson>();
            var hasPlatform = target.GetComponent<VWS.PlayerController_Platform>();
            var hasEnemy = target.GetComponent<VWS.EnemyAI_NavMesh>() || target.GetComponent<VWS.EnemyHealth>() || target.GetComponent<NavMeshAgent>();
            var isPlayer = hasThirdPerson || hasPlatform || roleText.Contains("player");
            var isEnemy = !isPlayer && (hasEnemy || roleText.Contains("enemy") || roleText.Contains("zombie") || roleText.Contains("boss"));

            if (isPlayer)
                changed |= RepairPlayerMotion(target, hasPlatform, log);
            if (isEnemy)
                changed |= RepairEnemyMotion(target, log);
            if (isPlayer || isEnemy)
                changed |= RepairAnimator(target, isPlayer ? "Player" : "Enemy", contextText, log);

            if (changed)
                EditorUtility.SetDirty(target);
            return changed;
        }

        static bool RepairPlayerMotion(GameObject target, bool platformPlayer, IList<string> log)
        {
            var changed = false;

            if (platformPlayer)
            {
                var platformRb = target.GetComponent<Rigidbody>();
                if (platformRb)
                {
                    UnityEngine.Object.DestroyImmediate(platformRb);
                    changed = true;
                    AddLog(log, "FIX: 플랫폼 플레이어의 Rigidbody를 제거했습니다.");
                }

                var controller = target.GetComponent<VWS.PlayerController_Platform>();
                if (controller && !controller.modelRoot)
                {
                    var visualRoot = FindVisualRoot(target);
                    if (visualRoot)
                    {
                        controller.modelRoot = visualRoot;
                        EditorUtility.SetDirty(controller);
                        changed = true;
                    }
                }

                changed |= ConfigureCharacterVisualSafety(target, isPlayer: true, usesNavMesh: false, allowVerticalMotion: true);
                return changed;
            }

            var thirdPerson = target.GetComponent<VWS.PlayerController_ThirdPerson>();
            if (!thirdPerson)
                return changed;

            var nav = target.GetComponent<NavMeshAgent>();
            if (nav)
            {
                UnityEngine.Object.DestroyImmediate(nav);
                changed = true;
                AddLog(log, "FIX: 플레이어의 NavMeshAgent를 제거했습니다.");
            }

            var cc = target.GetComponent<CharacterController>();
            if (cc)
            {
                UnityEngine.Object.DestroyImmediate(cc);
                changed = true;
                AddLog(log, "FIX: 3인칭 플레이어의 CharacterController를 제거했습니다.");
            }

            var rb = target.GetComponent<Rigidbody>();
            if (!rb)
            {
                rb = target.AddComponent<Rigidbody>();
                changed = true;
            }

            if (rb.useGravity)
            {
                rb.useGravity = false;
                changed = true;
            }
            if (rb.interpolation != RigidbodyInterpolation.Interpolate)
            {
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                changed = true;
            }
            if (rb.collisionDetectionMode != CollisionDetectionMode.Continuous)
            {
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                changed = true;
            }

            var visual = FindVisualRoot(target);
            if (visual && thirdPerson.modelRoot != visual)
            {
                thirdPerson.modelRoot = visual;
                EditorUtility.SetDirty(thirdPerson);
                changed = true;
            }

            var desiredConstraints = visual && visual != target.transform
                ? RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ
                : RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            if (rb.constraints != desiredConstraints)
            {
                rb.constraints = desiredConstraints;
                changed = true;
            }

            changed |= ConfigureCharacterVisualSafety(target, isPlayer: true, usesNavMesh: false, allowVerticalMotion: true);
            if (changed)
                AddLog(log, "FIX: 플레이어 물리/시각 보정 기본값을 안정화했습니다.");
            return changed;
        }

        static bool RepairEnemyMotion(GameObject target, IList<string> log)
        {
            var changed = false;
            var agent = target.GetComponent<NavMeshAgent>();
            if (!agent && (target.GetComponent<VWS.EnemyAI_NavMesh>() || target.GetComponent<VWS.EnemyHealth>()))
            {
                agent = target.AddComponent<NavMeshAgent>();
                changed = true;
            }

            if (agent)
            {
                if (agent.updateRotation != true)
                {
                    agent.updateRotation = true;
                    changed = true;
                }
                if (agent.speed <= 0f)
                {
                    agent.speed = 2.2f;
                    changed = true;
                }
            }

            changed |= ConfigureCharacterVisualSafety(target, isPlayer: false, usesNavMesh: true, allowVerticalMotion: false);
            if (changed)
                AddLog(log, "FIX: 적 NavMesh/시각 보정 기본값을 안정화했습니다.");
            return changed;
        }

        static bool ConfigureCharacterVisualSafety(GameObject target, bool isPlayer, bool usesNavMesh, bool allowVerticalMotion)
        {
            var changed = false;
            var anchor = target.GetComponent<VWS.CharacterInitialYAnchor>();
            if (!anchor)
            {
                anchor = target.AddComponent<VWS.CharacterInitialYAnchor>();
                changed = true;
            }

            if (!anchor.HasStoredInitialY)
            {
                anchor.CaptureCurrentYAsInitial();
                changed = true;
            }

            anchor.ConfigureForRole(isPlayer, usesNavMesh, allowVerticalMotion);
            anchor.lockRootYDuringMovement = !allowVerticalMotion;
            anchor.zeroVerticalVelocity = isPlayer && !allowVerticalMotion;
            anchor.makeVisualAlignUseInitialY = true;
            anchor.visualFootClearance = isPlayer ? Mathf.Max(anchor.visualFootClearance, 0.08f) : Mathf.Max(anchor.visualFootClearance, 0.05f);
            EditorUtility.SetDirty(anchor);

            var align = target.GetComponent<VWS.RuntimeGroundAlign>();
            if (!align)
            {
                align = target.AddComponent<VWS.RuntimeGroundAlign>();
                changed = true;
            }

            align.alignOnEnable = true;
            align.alignVisualChildrenOnly = true;
            align.continuous = false;
            align.useRootY = true;
            align.alignDuration = 0f;
            align.alignFramesAfterEnable = isPlayer ? 6 : 4;
            align.footClearance = isPlayer ? 0.08f : 0.05f;
            align.maxCorrectionPerCall = isPlayer ? 1.5f : 2.0f;
            EditorUtility.SetDirty(align);

            return changed;
        }

        static bool RepairAnimator(GameObject target, string role, string contextText, IList<string> log)
        {
            var changed = false;
            var animator = target.GetComponentInChildren<Animator>(true);
            if (!animator && target.GetComponentInChildren<SkinnedMeshRenderer>(true))
            {
                animator = target.AddComponent<Animator>();
                changed = true;
            }

            if (!animator)
                return changed;

            if (animator.applyRootMotion)
            {
                animator.applyRootMotion = false;
                changed = true;
            }

            if (animator.runtimeAnimatorController)
            {
                changed |= EnsureControllerParameters(animator.runtimeAnimatorController as AnimatorController);
                EditorUtility.SetDirty(animator);
                return changed;
            }

            var clips = LoadAnimationClips();
            var searchText = target.name + " " + role + " " + contextText;
            var idle = FindBestClip(clips, searchText, role, "idle", "stand", "wait", "대기");
            if (!idle)
            {
                AddLog(log, "확인 필요: " + target.name + " Animator Controller가 없지만 Idle 클립을 찾지 못했습니다.");
                return changed;
            }

            var walk = FindBestClip(clips, searchText, role, "walk", "move", "걷");
            var run = FindBestClip(clips, searchText, role, "run", "sprint", "dash", "뛰");
            var jump = FindBestClip(clips, searchText, role, "jump", "점프");
            var attack = FindBestClip(clips, searchText, role, "attack", "atk", "공격");
            var death = FindBestClip(clips, searchText, role, "death", "die", "dead", "죽");

            var controller = CreateController(target, role, searchText, idle, walk, run, jump, attack, death);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            EditorUtility.SetDirty(animator);
            AddLog(log, "FIX: 누락된 Animator Controller를 자동 생성/연결했습니다. " + AssetDatabase.GetAssetPath(controller));
            return true;
        }

        static bool EnsureControllerParameters(AnimatorController controller)
        {
            if (!controller)
                return false;

            var changed = false;
            changed |= EnsureAnimatorParameter(controller, "IsWalk", AnimatorControllerParameterType.Bool);
            changed |= EnsureAnimatorParameter(controller, "IsRun", AnimatorControllerParameterType.Bool);
            changed |= EnsureAnimatorParameter(controller, "IsJump", AnimatorControllerParameterType.Bool);
            changed |= EnsureAnimatorParameter(controller, "IsAttack", AnimatorControllerParameterType.Trigger);
            changed |= EnsureAnimatorParameter(controller, "IsDead", AnimatorControllerParameterType.Trigger);
            changed |= EnsureAnimatorParameter(controller, "IsPush", AnimatorControllerParameterType.Bool);
            if (changed)
                EditorUtility.SetDirty(controller);
            return changed;
        }

        static AnimatorController CreateController(GameObject target, string role, string contextText, AnimationClip idle, AnimationClip walk, AnimationClip run, AnimationClip jump, AnimationClip attack, AnimationClip death)
        {
            EnsureFolder(AnimationOutputFolder);
            var path = AnimationOutputFolder + "/" + SafeFileName(target.name + "_" + role + "_AutoRepair") + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!controller)
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            EnsureControllerParameters(controller);
            var sm = controller.layers[0].stateMachine;
            ClearStateMachine(sm);

            var idleState = AddState(sm, "Idle", idle, new Vector3(220f, 80f, 0f));
            sm.defaultState = idleState;

            AnimatorState walkState = null;
            if (walk)
            {
                walkState = AddState(sm, "Walk", walk, new Vector3(480f, 80f, 0f));
                AddBoolTransition(idleState, walkState, "IsWalk", true);
                AddBoolTransition(walkState, idleState, "IsWalk", false);
            }

            if (run)
            {
                var runState = AddState(sm, "Run", run, new Vector3(740f, 80f, 0f));
                AddBoolTransition(idleState, runState, "IsRun", true);
                AddBoolTransition(runState, idleState, "IsRun", false);
                if (walkState != null)
                {
                    AddBoolTransition(walkState, runState, "IsRun", true);
                    AddBoolTransition(runState, walkState, "IsRun", false);
                }
            }

            if (jump)
                AddBoolAction(sm, idleState, "Jump", jump, "IsJump", new Vector3(220f, 270f, 0f));
            if (attack)
                AddTriggeredAction(sm, idleState, "Attack", attack, "IsAttack", new Vector3(480f, 270f, 0f));
            if (death)
            {
                var deathState = AddState(sm, "Death", death, new Vector3(740f, 270f, 0f));
                var t = sm.AddAnyStateTransition(deathState);
                t.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
                t.hasExitTime = false;
                t.duration = 0.05f;
                t.canTransitionToSelf = false;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static Transform FindVisualRoot(GameObject target)
        {
            var animator = target.GetComponentInChildren<Animator>(true);
            if (animator && animator.transform != target.transform)
                return animator.transform;

            var skinned = target.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skinned)
            {
                var top = TopChildUnderRoot(target.transform, skinned.transform);
                if (top)
                    return top;
            }

            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer || renderer.transform == target.transform)
                    continue;
                var top = TopChildUnderRoot(target.transform, renderer.transform);
                if (top)
                    return top;
            }

            return null;
        }

        static Transform TopChildUnderRoot(Transform root, Transform child)
        {
            if (!root || !child || child == root)
                return null;
            var current = child;
            while (current.parent && current.parent != root)
                current = current.parent;
            return current.parent == root ? current : null;
        }

        static List<ClipCandidate> LoadAnimationClips()
        {
            var result = new List<ClipCandidate>();
            var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                        result.Add(new ClipCandidate(path, clip));
                }
            }

            return result;
        }

        static AnimationClip FindBestClip(List<ClipCandidate> clips, string context, string role, params string[] keywords)
        {
            var normalizedContext = Normalize(context);
            var contextTokens = normalizedContext.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            var normalizedRole = Normalize(role);
            ClipCandidate best = null;
            var bestScore = int.MinValue;

            foreach (var candidate in clips)
            {
                var haystack = Normalize(candidate.path + "/" + candidate.clip.name);
                var score = 0;

                if (!string.IsNullOrEmpty(normalizedRole) && haystack.Contains(normalizedRole))
                    score += 35;
                foreach (var token in contextTokens)
                {
                    if (token.Length >= 3 && haystack.Contains(token))
                        score += token == "player" || token == "enemy" || token == "arena" || token == "platform" || token == "zombie" || token == "boss" ? 35 : 8;
                }
                foreach (var keyword in keywords.Select(Normalize))
                {
                    if (!string.IsNullOrEmpty(keyword) && haystack.Contains(keyword))
                        score += 120;
                }
                if (haystack.Contains("preview"))
                    score -= 1000;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return bestScore >= 120 ? best?.clip : null;
        }

        static bool EnsureAnimatorParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            var existing = controller.parameters.FirstOrDefault(parameter => parameter.name == name);
            if (existing != null && existing.type != type)
            {
                controller.RemoveParameter(existing);
                existing = null;
            }
            if (existing != null)
                return false;

            controller.AddParameter(name, type);
            return true;
        }

        static AnimatorState AddState(AnimatorStateMachine sm, string name, Motion motion, Vector3 position)
        {
            var state = sm.AddState(name, position);
            state.motion = motion;
            state.writeDefaultValues = true;
            return state;
        }

        static void AddBoolTransition(AnimatorState from, AnimatorState to, string parameter, bool value)
        {
            var transition = from.AddTransition(to);
            transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
            transition.hasExitTime = false;
            transition.duration = 0.08f;
        }

        static void AddTriggeredAction(AnimatorStateMachine sm, AnimatorState idle, string name, Motion motion, string trigger, Vector3 position)
        {
            var state = AddState(sm, name, motion, position);
            state.speed = SpeedForTargetDuration(motion, 0.9f);
            var enter = sm.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.hasExitTime = false;
            enter.duration = 0.05f;
            enter.canTransitionToSelf = false;

            var exit = state.AddTransition(idle);
            exit.hasExitTime = true;
            exit.exitTime = 0.9f;
            exit.duration = 0.08f;
        }

        static void AddBoolAction(AnimatorStateMachine sm, AnimatorState idle, string name, Motion motion, string parameter, Vector3 position)
        {
            var state = AddState(sm, name, motion, position);
            state.speed = SpeedForTargetDuration(motion, 1.1f);
            var enter = sm.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, parameter);
            enter.hasExitTime = false;
            enter.duration = 0.05f;
            enter.canTransitionToSelf = false;

            var exit = state.AddTransition(idle);
            exit.AddCondition(AnimatorConditionMode.IfNot, 0f, parameter);
            exit.hasExitTime = false;
            exit.duration = 0.08f;
        }

        static float SpeedForTargetDuration(Motion motion, float targetDuration)
        {
            if (motion is AnimationClip clip && clip.length > targetDuration)
                return Mathf.Clamp(clip.length / Mathf.Max(0.05f, targetDuration), 1f, 8f);
            return 1f;
        }

        static void ClearStateMachine(AnimatorStateMachine sm)
        {
            foreach (var transition in sm.anyStateTransitions.ToArray())
                sm.RemoveAnyStateTransition(transition);
            foreach (var transition in sm.entryTransitions.ToArray())
                sm.RemoveEntryTransition(transition);
            foreach (var state in sm.states.ToArray())
                sm.RemoveState(state.state);
        }

        static void EnsureFolder(string folder)
        {
            folder = folder.Replace("\\", "/").Trim('/');
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var parts = folder.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static string SafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "VARCO";
            foreach (var c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value.Replace(" ", "_");
        }

        static string Normalize(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.ToLowerInvariant().Replace("-", "_").Replace(" ", "_").Replace("\\", "_").Replace("/", "_");
        }

        static void AddLog(IList<string> log, string message)
        {
            if (log != null && !string.IsNullOrWhiteSpace(message))
                log.Add(message);
        }

        class ClipCandidate
        {
            public readonly string path;
            public readonly AnimationClip clip;

            public ClipCandidate(string path, AnimationClip clip)
            {
                this.path = path;
                this.clip = clip;
            }
        }
    }
}
#endif
