#if UNITY_EDITOR
// ============================================================================
// VARCOAnimationNormalizerWindow
//
// USDZ 파일마다 캐릭터 forward 축이 다를 때, 모든 루트 회전/이동 커브에
// 고정 Yaw 오프셋을 구워(bake) 넣어 동일한 방향으로 맞추는 도구.
//
// 메뉴: VARCO / 애니메이션 방향 통일 도구
//
// 작동 원리
// ─────────
// 1. 클립의 루트 Transform(경로 "")에서 회전 커브를 읽는다.
//    · Quaternion 방식 (localRotation.x/y/z/w)
//    · Euler 방식    (localEulerAnglesRaw.x/y/z)  — 둘 다 지원
//
// 2. 각 키프레임 값에 correction = Quaternion.Euler(0, yawDeg, 0) 를 곱한다.
//    회전:   Q_fixed = correction * Q_original
//    위치:   P_fixed = correction * P_original   (루트 이동도 같이 회전)
//
// 3. 접선(tangent)은 스무딩 재계산.  쿼터니언 특성상 고주파 키가 아닌
//    일반 캐릭터 애니메이션(걷기/뛰기/공격)에서는 오차가 무시할 수준이다.
//
// 4. 보정된 클립을 독립 .anim 파일로 저장하고 AnimatorController 를 빌드.
// ============================================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace VARCO_Workshop.Editor
{
    public class VARCOAnimationNormalizerWindow : EditorWindow
    {
        // ────────────────────────────────────────────────────────────────
        // 내부 데이터
        // ────────────────────────────────────────────────────────────────

        private class ClipEntry
        {
            public AnimationClip clip;
            public string        assetPath;        // 원본 에셋 경로
            public string        stateName;        // AnimatorController 상태 이름
            public float         yawOffset = 0f;   // 보정 각도 (도)
            public bool          include   = true;
        }

        private readonly List<ClipEntry> _entries = new List<ClipEntry>();
        private Vector2                  _scroll;

        // 설정
        private string _outputDir       = "Assets/VARCO3DImports/_Normalized";
        private string _controllerName  = "VARCO_Combined";
        private float  _globalYaw       = 0f;
        private bool   _applyGlobal     = false;

        // ────────────────────────────────────────────────────────────────
        // 메뉴 / 열기
        // ────────────────────────────────────────────────────────────────

        public static void Open()
        {
            var w = GetWindow<VARCOAnimationNormalizerWindow>("애니메이션 방향 통일");
            w.minSize = new Vector2(560, 520);
            w.Focus();
        }

        // ────────────────────────────────────────────────────────────────
        // GUI
        // ────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            GUILayout.Space(6);
            EditorGUILayout.LabelField("VARCO 애니메이션 방향 통일 도구", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "USDZ 파일마다 캐릭터 방향이 다를 때 사용합니다.\n" +
                "① 스캔 또는 드래그로 클립 추가  →  ② 각 클립의 Yaw 오프셋 설정  →  ③ 빌드",
                MessageType.Info);

            GUILayout.Space(4);

            // ── 스캔 / 추가 버튼 ─────────────────────────────────────────
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("VARCO 임포트 폴더 스캔", GUILayout.Height(28)))
                    ScanVARCOImports();

                if (GUILayout.Button("선택 오브젝트에서 추가", GUILayout.Width(160), GUILayout.Height(28)))
                    AddFromSelection();

                if (GUILayout.Button("목록 비우기", GUILayout.Width(90), GUILayout.Height(28)))
                    _entries.Clear();
            }

            GUILayout.Space(4);

            // ── 전체 동일 오프셋 ─────────────────────────────────────────
            using (new EditorGUILayout.HorizontalScope())
            {
                _applyGlobal = EditorGUILayout.ToggleLeft("전체 동일 Yaw 오프셋", _applyGlobal, GUILayout.Width(160));
                EditorGUI.BeginDisabledGroup(!_applyGlobal);
                _globalYaw = EditorGUILayout.Slider(_globalYaw, -180f, 180f);
                if (GUILayout.Button("적용", GUILayout.Width(44)))
                    foreach (var e in _entries)
                        e.yawOffset = _globalYaw;
                EditorGUI.EndDisabledGroup();
            }

            GUILayout.Space(4);
            DrawDivider();

            // ── 클립 목록 헤더 ────────────────────────────────────────────
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(20);
                EditorGUILayout.LabelField("클립",          EditorStyles.miniLabel, GUILayout.Width(200));
                EditorGUILayout.LabelField("상태 이름",      EditorStyles.miniLabel, GUILayout.Width(140));
                EditorGUILayout.LabelField("Yaw 오프셋 (°)", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
            }

            // ── 클립 목록 ─────────────────────────────────────────────────
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            if (_entries.Count == 0)
            {
                EditorGUILayout.HelpBox("스캔하거나 Project 창에서 AnimationClip을 선택 후 '선택 오브젝트에서 추가'를 누르세요.", MessageType.None);
            }
            else
            {
                for (int i = _entries.Count - 1; i >= 0; i--)
                {
                    var entry = _entries[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        // 포함 여부
                        entry.include = EditorGUILayout.Toggle(entry.include, GUILayout.Width(18));

                        // 클립 오브젝트 필드 (읽기 전용)
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.ObjectField(entry.clip, typeof(AnimationClip), false, GUILayout.Width(200));
                        EditorGUI.EndDisabledGroup();

                        // 상태 이름 편집 가능
                        entry.stateName = EditorGUILayout.TextField(entry.stateName, GUILayout.Width(140));

                        // Yaw 슬라이더
                        entry.yawOffset = EditorGUILayout.Slider(entry.yawOffset, -180f, 180f);

                        // 삭제 버튼
                        if (GUILayout.Button("✕", GUILayout.Width(22)))
                        {
                            _entries.RemoveAt(i);
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            DrawDivider();
            GUILayout.Space(4);

            // ── 출력 설정 ─────────────────────────────────────────────────
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("저장 폴더:", GUILayout.Width(72));
                _outputDir = EditorGUILayout.TextField(_outputDir);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("컨트롤러 이름:", GUILayout.Width(84));
                _controllerName = EditorGUILayout.TextField(_controllerName);
            }

            GUILayout.Space(6);

            // ── 빌드 버튼 ─────────────────────────────────────────────────
            bool hasIncluded = _entries.Any(e => e.include && e.clip != null);
            EditorGUI.BeginDisabledGroup(!hasIncluded);
            if (GUILayout.Button("보정 클립 생성 + AnimatorController 빌드", GUILayout.Height(36)))
                BuildNormalized();
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(4);

            EditorGUILayout.HelpBox(
                "Yaw = 0°  → 보정 없이 원본 그대로 사용\n" +
                "Yaw = 180° → 캐릭터가 반대 방향 (Z-)을 바라볼 때\n" +
                "Yaw = -90° → 캐릭터가 X- 방향을 바라볼 때\n" +
                "Yaw = 90°  → 캐릭터가 X+ 방향을 바라볼 때",
                MessageType.None);
        }

        // ────────────────────────────────────────────────────────────────
        // 스캔 / 추가
        // ────────────────────────────────────────────────────────────────

        private void ScanVARCOImports()
        {
            int added = 0;
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip",
                                new[] { "Assets/VARCO3DImports" });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // 직접 에셋 + 서브 에셋(USDZ 내장 클립)
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        if (TryAddEntry(clip, path))
                            added++;
                    }
                }
            }

            if (added == 0)
            {
                EditorUtility.DisplayDialog("스캔 결과",
                    "Assets/VARCO3DImports 폴더에서 AnimationClip을 찾지 못했습니다.\n" +
                    "VARCO 에셋을 먼저 임포트하세요.", "확인");
            }
            else
            {
                Debug.Log($"[VARCO 방향 통일] {added}개 클립 추가됨.");
            }
        }

        private void AddFromSelection()
        {
            int added = 0;
            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);

                if (obj is AnimationClip clip)
                {
                    if (TryAddEntry(clip, path)) added++;
                    continue;
                }

                // 게임오브젝트(프리팹)이나 다른 에셋 선택 시 서브 에셋 탐색
                if (!string.IsNullOrEmpty(path))
                {
                    foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        if (sub is AnimationClip c && !c.name.StartsWith("__preview__"))
                            if (TryAddEntry(c, path)) added++;
                    }
                }
            }

            if (added == 0)
                Debug.LogWarning("[VARCO 방향 통일] 선택된 오브젝트에서 AnimationClip을 찾지 못했습니다.");
        }

        private bool TryAddEntry(AnimationClip clip, string assetPath)
        {
            if (_entries.Any(e => e.clip == clip)) return false;
            _entries.Add(new ClipEntry
            {
                clip      = clip,
                assetPath = assetPath,
                stateName = clip.name,
                yawOffset = 0f,
                include   = true
            });
            return true;
        }

        // ────────────────────────────────────────────────────────────────
        // 빌드
        // ────────────────────────────────────────────────────────────────

        private void BuildNormalized()
        {
            EnsureFolderExists(_outputDir);

            string controllerPath = $"{_outputDir}/{SanitizeName(_controllerName)}.controller";
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var sm = controller.layers[0].stateMachine;

            bool first = true;

            foreach (var entry in _entries)
            {
                if (!entry.include || entry.clip == null) continue;

                AnimationClip usedClip;

                if (Mathf.Abs(entry.yawOffset) > 0.01f)
                {
                    string outPath = $"{_outputDir}/{SanitizeName(entry.stateName)}_YawFixed.anim";
                    usedClip = CreateCorrectedClip(entry.clip, entry.yawOffset, outPath);
                }
                else
                {
                    usedClip = entry.clip;
                }

                string stateName = string.IsNullOrWhiteSpace(entry.stateName)
                    ? entry.clip.name
                    : entry.stateName;

                var state = sm.AddState(stateName);
                state.motion = usedClip;

                if (first) { sm.defaultState = state; first = false; }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(controller);
            Debug.Log($"[VARCO 방향 통일] AnimatorController 생성: {controllerPath}");
            EditorUtility.DisplayDialog("빌드 완료",
                $"AnimatorController 생성 완료\n{controllerPath}", "확인");
        }

        // ────────────────────────────────────────────────────────────────
        // 핵심: Yaw 보정 클립 생성
        // ────────────────────────────────────────────────────────────────

        private static AnimationClip CreateCorrectedClip(AnimationClip src,
            float yawDeg, string outputPath)
        {
            Quaternion correction = Quaternion.Euler(0f, yawDeg, 0f);

            // 새 클립 초기화
            var dst = new AnimationClip
            {
                name      = src.name,
                frameRate = src.frameRate,
                legacy    = src.legacy,
                wrapMode  = src.wrapMode
            };

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(src);

            // 루트 경로 결정 (대부분의 USDZ는 "" / FBX는 "Armature" 등)
            string rootPath = FindRootPath(bindings);

            // 이미 처리한 루트 바인딩 집합
            var processedProps = new HashSet<string>();

            // ① 루트 회전 커브 처리
            string rotPrefix = FindPrefix(bindings, rootPath,
                new[] { "localRotation", "m_LocalRotation" }, ".x");
            if (rotPrefix != null)
            {
                ApplyQuatCorrection(src, dst, rootPath, rotPrefix, correction);
                processedProps.UnionWith(new[]
                {
                    rotPrefix + ".x", rotPrefix + ".y",
                    rotPrefix + ".z", rotPrefix + ".w"
                });
            }
            else
            {
                // Euler 방식 시도 (localEulerAnglesRaw 또는 eulerAngles)
                string eulerPrefix = FindPrefix(bindings, rootPath,
                    new[] { "localEulerAnglesRaw", "localEulerAngles", "eulerAngles" }, ".y");
                if (eulerPrefix != null)
                {
                    ApplyEulerYCorrection(src, dst, rootPath, eulerPrefix, yawDeg);
                    processedProps.UnionWith(new[]
                    {
                        eulerPrefix + ".x", eulerPrefix + ".y", eulerPrefix + ".z"
                    });
                }
            }

            // ② 루트 위치 커브 처리 (루트 모션 포함 클립에서만 의미 있음)
            string posPrefix = FindPrefix(bindings, rootPath,
                new[] { "localPosition", "m_LocalPosition" }, ".x");
            if (posPrefix != null)
            {
                ApplyPosCorrection(src, dst, rootPath, posPrefix, correction);
                processedProps.UnionWith(new[]
                {
                    posPrefix + ".x", posPrefix + ".y", posPrefix + ".z"
                });
            }

            // ③ 나머지 커브는 원본 그대로 복사
            foreach (var b in bindings)
            {
                if (b.path == rootPath && processedProps.Contains(b.propertyName))
                    continue;

                var curve = AnimationUtility.GetEditorCurve(src, b);
                if (curve != null)
                    AnimationUtility.SetEditorCurve(dst, b, curve);
            }

            // 저장
            if (File.Exists(Path.Combine(
                Application.dataPath, "..", outputPath).Replace('\\', '/')))
                AssetDatabase.DeleteAsset(outputPath);

            AssetDatabase.CreateAsset(dst, outputPath);
            Debug.Log($"[VARCO 방향 통일] 보정 클립 저장: {outputPath}");
            return dst;
        }

        // ────────────────────────────────────────────────────────────────
        // 회전 커브 보정 (쿼터니언)
        // ────────────────────────────────────────────────────────────────

        private static void ApplyQuatCorrection(AnimationClip src, AnimationClip dst,
            string rootPath, string prefix, Quaternion correction)
        {
            var bX = GetBinding(src, rootPath, prefix + ".x");
            var bY = GetBinding(src, rootPath, prefix + ".y");
            var bZ = GetBinding(src, rootPath, prefix + ".z");
            var bW = GetBinding(src, rootPath, prefix + ".w");

            var cx = AnimationUtility.GetEditorCurve(src, bX);
            var cy = AnimationUtility.GetEditorCurve(src, bY);
            var cz = AnimationUtility.GetEditorCurve(src, bZ);
            var cw = AnimationUtility.GetEditorCurve(src, bW);

            if (cx == null || cy == null || cz == null || cw == null) return;

            // 모든 키프레임 시간 수집 (4개 커브의 합집합)
            float[] times = cx.keys.Select(k => k.time)
                              .Union(cy.keys.Select(k => k.time))
                              .Union(cz.keys.Select(k => k.time))
                              .Union(cw.keys.Select(k => k.time))
                              .Distinct().OrderBy(t => t).ToArray();

            var nx = new AnimationCurve();
            var ny = new AnimationCurve();
            var nz = new AnimationCurve();
            var nw = new AnimationCurve();

            foreach (float t in times)
            {
                Quaternion orig = new Quaternion(
                    cx.Evaluate(t), cy.Evaluate(t),
                    cz.Evaluate(t), cw.Evaluate(t));
                orig.Normalize();

                Quaternion fixed_ = (correction * orig).normalized;

                // 인접 키프레임과 쿼터니언 부호 연속성 유지
                if (nx.keys.Length > 0)
                {
                    var last = new Quaternion(
                        nx.keys[nx.keys.Length - 1].value,
                        ny.keys[ny.keys.Length - 1].value,
                        nz.keys[nz.keys.Length - 1].value,
                        nw.keys[nw.keys.Length - 1].value);

                    if (Quaternion.Dot(last, fixed_) < 0f)
                        fixed_ = new Quaternion(-fixed_.x, -fixed_.y, -fixed_.z, -fixed_.w);
                }

                nx.AddKey(t, fixed_.x);
                ny.AddKey(t, fixed_.y);
                nz.AddKey(t, fixed_.z);
                nw.AddKey(t, fixed_.w);
            }

            SmoothAllKeys(nx);
            SmoothAllKeys(ny);
            SmoothAllKeys(nz);
            SmoothAllKeys(nw);

            AnimationUtility.SetEditorCurve(dst, bX, nx);
            AnimationUtility.SetEditorCurve(dst, bY, ny);
            AnimationUtility.SetEditorCurve(dst, bZ, nz);
            AnimationUtility.SetEditorCurve(dst, bW, nw);
        }

        // ────────────────────────────────────────────────────────────────
        // 회전 커브 보정 (오일러)
        // ────────────────────────────────────────────────────────────────

        private static void ApplyEulerYCorrection(AnimationClip src, AnimationClip dst,
            string rootPath, string prefix, float yawDeg)
        {
            // X, Z 커브는 그대로, Y 커브만 yawDeg 더하기
            foreach (string component in new[] { ".x", ".y", ".z" })
            {
                var b     = GetBinding(src, rootPath, prefix + component);
                var curve = AnimationUtility.GetEditorCurve(src, b);
                if (curve == null) continue;

                if (component == ".y")
                {
                    // Y 커브 각도 오프셋
                    var keys = curve.keys;
                    for (int i = 0; i < keys.Length; i++)
                        keys[i].value += yawDeg;
                    curve.keys = keys;
                }

                AnimationUtility.SetEditorCurve(dst, b, curve);
            }
        }

        // ────────────────────────────────────────────────────────────────
        // 위치 커브 보정 (루트 모션)
        // ────────────────────────────────────────────────────────────────

        private static void ApplyPosCorrection(AnimationClip src, AnimationClip dst,
            string rootPath, string prefix, Quaternion correction)
        {
            var bX = GetBinding(src, rootPath, prefix + ".x");
            var bY = GetBinding(src, rootPath, prefix + ".y");
            var bZ = GetBinding(src, rootPath, prefix + ".z");

            var cx = AnimationUtility.GetEditorCurve(src, bX);
            var cy = AnimationUtility.GetEditorCurve(src, bY);
            var cz = AnimationUtility.GetEditorCurve(src, bZ);

            if (cx == null || cy == null || cz == null) return;

            float[] times = cx.keys.Select(k => k.time)
                              .Union(cy.keys.Select(k => k.time))
                              .Union(cz.keys.Select(k => k.time))
                              .Distinct().OrderBy(t => t).ToArray();

            var nx = new AnimationCurve();
            var ny = new AnimationCurve();
            var nz = new AnimationCurve();

            foreach (float t in times)
            {
                Vector3 orig  = new Vector3(cx.Evaluate(t), cy.Evaluate(t), cz.Evaluate(t));
                Vector3 fixed_ = correction * orig;

                nx.AddKey(t, fixed_.x);
                ny.AddKey(t, fixed_.y);   // Y(높이)는 correction * 결과로 자동 처리
                nz.AddKey(t, fixed_.z);
            }

            SmoothAllKeys(nx);
            SmoothAllKeys(ny);
            SmoothAllKeys(nz);

            AnimationUtility.SetEditorCurve(dst, bX, nx);
            AnimationUtility.SetEditorCurve(dst, bY, ny);
            AnimationUtility.SetEditorCurve(dst, bZ, nz);
        }

        // ────────────────────────────────────────────────────────────────
        // 헬퍼
        // ────────────────────────────────────────────────────────────────

        /// <summary>회전 커브가 붙어있는 루트 경로 탐색 ("" 우선).</summary>
        private static string FindRootPath(EditorCurveBinding[] bindings)
        {
            // 빈 경로("")에 회전 커브가 있으면 그것이 루트
            if (bindings.Any(b => b.path == "" && IsRotProp(b.propertyName)))
                return "";

            // 없으면 가장 짧은 경로 (상위 뼈대)
            string shortest = bindings
                .Where(b => IsRotProp(b.propertyName))
                .Select(b => b.path)
                .OrderBy(p => p.Length)
                .FirstOrDefault();

            return shortest ?? "";
        }

        /// <summary>주어진 경로·접두사에 해당하는 바인딩 접두사 반환. 없으면 null.</summary>
        private static string FindPrefix(EditorCurveBinding[] bindings,
            string path, string[] candidates, string suffix)
        {
            foreach (var prefix in candidates)
                if (bindings.Any(b => b.path == path && b.propertyName == prefix + suffix))
                    return prefix;
            return null;
        }

        /// <summary>소스 클립에서 실제 바인딩 타입을 찾아 반환.</summary>
        private static EditorCurveBinding GetBinding(AnimationClip clip,
            string path, string propertyName)
        {
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
                if (b.path == path && b.propertyName == propertyName)
                    return b;

            // 못 찾으면 기본값 (Transform)
            return EditorCurveBinding.FloatCurve(path, typeof(Transform), propertyName);
        }

        private static bool IsRotProp(string prop)
            => prop.Contains("Rotation") || prop.Contains("rotation") || prop.Contains("Euler") || prop.Contains("euler");

        private static void SmoothAllKeys(AnimationCurve curve)
        {
            for (int i = 0; i < curve.keys.Length; i++)
                curve.SmoothTangents(i, 0.5f);
        }

        private static string SanitizeName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private static void EnsureFolderExists(string assetPath)
        {
            string[] parts   = assetPath.Split('/');
            string   current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void DrawDivider()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
        }
    }
}
#endif
