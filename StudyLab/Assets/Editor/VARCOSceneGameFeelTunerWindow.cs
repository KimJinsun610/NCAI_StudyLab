#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VWS = VARCO_Workshop;
using Object = UnityEngine.Object;

namespace VARCO_Workshop.Editor
{
    public class VARCOSceneGameFeelTunerWindow : EditorWindow
    {
        const string ProfileRootName = "VARCO_GameFeelTuning";
        const string PolishRootName = "VARCO_GameFeelPolish";
        const string MaterialFolder = "Assets/Materials/VARCO_GameFeel";

        VWS.VARCOSceneTuningProfile profile;
        SerializedObject serializedProfile;
        Vector2 scroll;
        string sceneSummary = "";
        string lastAction = "";

        public static void Open()
        {
            var window = GetWindow<VARCOSceneGameFeelTunerWindow>("Game Feel Tuner");
            window.minSize = new Vector2(460f, 560f);
            window.Scan();
        }

        public static void ApplyCurrentScenePolishFromMenu()
        {
            var profile = EnsureSceneProfile();
            profile.ResetArenaDefaults();
            EditorUtility.SetDirty(profile);
            ApplyProfileToCurrentScene(profile, true);
        }

        void OnEnable()
        {
            Scan();
        }

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Scan", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                    Scan();
                if (GUILayout.Button("Ensure Profile", EditorStyles.toolbarButton, GUILayout.Width(104f)))
                    SetProfile(EnsureSceneProfile());
                if (GUILayout.Button("Reset Defaults", EditorStyles.toolbarButton, GUILayout.Width(104f)) && EnsureProfileSelected())
                {
                    Undo.RecordObject(profile, "Reset VARCO scene tuning defaults");
                    profile.ResetArenaDefaults();
                    EditorUtility.SetDirty(profile);
                    UpdateSerializedProfile();
                }
                GUILayout.FlexibleSpace();
            }

            if (!string.IsNullOrWhiteSpace(sceneSummary))
                EditorGUILayout.HelpBox(sceneSummary, MessageType.Info);
            if (!string.IsNullOrWhiteSpace(lastAction))
                EditorGUILayout.HelpBox(lastAction, MessageType.None);

            profile = (VWS.VARCOSceneTuningProfile)EditorGUILayout.ObjectField(
                "Scene Profile", profile, typeof(VWS.VARCOSceneTuningProfile), true);
            if (profile && (serializedProfile == null || serializedProfile.targetObject != profile))
                UpdateSerializedProfile();

            using (new EditorGUI.DisabledScope(profile == null))
            {
                scroll = EditorGUILayout.BeginScrollView(scroll);
                if (serializedProfile != null)
                {
                    serializedProfile.Update();
                    DrawProfileFields();
                    serializedProfile.ApplyModifiedProperties();
                }
                EditorGUILayout.EndScrollView();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Apply To Current Scene", GUILayout.Height(34f)))
                    {
                        ApplyProfileToCurrentScene(profile, true);
                        lastAction = "Applied profile to current scene.";
                        Scan();
                    }
                    if (GUILayout.Button("Select Profile", GUILayout.Height(34f), GUILayout.Width(120f)))
                    {
                        Selection.activeObject = profile;
                        EditorGUIUtility.PingObject(profile);
                    }
                }
            }

            if (profile == null)
            {
                EditorGUILayout.HelpBox(
                    "Create a scene profile first. The profile stores camera, lighting, color, and helper-object values in the scene so they can be adjusted before exporting a package.",
                    MessageType.Warning);
            }
        }

        void DrawProfileFields()
        {
            var iterator = serializedProfile.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script")
                    continue;
                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        bool EnsureProfileSelected()
        {
            if (profile)
                return true;
            SetProfile(EnsureSceneProfile());
            return profile;
        }

        void SetProfile(VWS.VARCOSceneTuningProfile next)
        {
            profile = next;
            UpdateSerializedProfile();
            if (profile)
                Selection.activeObject = profile;
        }

        void UpdateSerializedProfile()
        {
            serializedProfile = profile ? new SerializedObject(profile) : null;
        }

        void Scan()
        {
            profile = Object.FindFirstObjectByType<VWS.VARCOSceneTuningProfile>();
            UpdateSerializedProfile();
            sceneSummary = BuildSceneSummary();
            Repaint();
        }

        static string BuildSceneSummary()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            var camera = Camera.main;
            var layout = GameObject.Find("VARCO_AutoBuildLayout");
            var enemies = Object.FindObjectsByType<VWS.EnemyAI_NavMesh>(FindObjectsSortMode.None).Length;
            var pickups = Object.FindObjectsByType<VWS.HealthPickup>(FindObjectsSortMode.None).Length;
            var profile = Object.FindFirstObjectByType<VWS.VARCOSceneTuningProfile>();
            return "Current scene: " + SceneManager.GetActiveScene().name
                + "\nPlayer: " + (player ? player.name : "missing")
                + " | Main Camera: " + (camera ? camera.name : "missing")
                + " | Auto Layout: " + (layout ? layout.transform.childCount + " children" : "missing")
                + "\nEnemies: " + enemies
                + " | Health pickups: " + pickups
                + " | Tuning profile: " + (profile ? profile.name : "missing");
        }

        public static VWS.VARCOSceneTuningProfile EnsureSceneProfile()
        {
            var profile = Object.FindFirstObjectByType<VWS.VARCOSceneTuningProfile>();
            if (profile)
                return profile;

            var root = GameObject.Find(ProfileRootName);
            if (!root)
            {
                root = new GameObject(ProfileRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create VARCO game feel profile");
            }

            profile = root.GetComponent<VWS.VARCOSceneTuningProfile>();
            if (!profile)
                profile = Undo.AddComponent<VWS.VARCOSceneTuningProfile>(root);
            profile.ResetArenaDefaults();
            EditorUtility.SetDirty(profile);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return profile;
        }

        public static void ApplyProfileToCurrentScene(VWS.VARCOSceneTuningProfile profile, bool saveScene)
        {
            if (!profile)
                profile = EnsureSceneProfile();
            if (!profile)
                return;

            Undo.SetCurrentGroupName("Apply VARCO game feel tuning");
            var undoGroup = Undo.GetCurrentGroup();

            if (profile.snapPlayerToStartPad)
                SnapPlayerToStart(profile);
            if (profile.applyPlayerVisualFacing)
                ApplyPlayerVisualFacing(profile);
            if (profile.applyCamera)
                ApplyCamera(profile);
            if (profile.applyLighting)
                ApplyLighting(profile);
            if (profile.applyArenaMaterials)
                ApplyArenaMaterials(profile);
            if (profile.createPolishObjects)
                ApplyPolishObjects(profile);
            ApplyHudHints();

            EditorUtility.SetDirty(profile);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            if (saveScene)
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        static void SnapPlayerToStart(VWS.VARCOSceneTuningProfile profile)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (!player)
                return;

            Undo.RecordObject(player.transform, "Move player to tuned start");
            player.transform.position = profile.playerStartPosition;
            player.transform.rotation = Quaternion.identity;
            var body = player.GetComponent<Rigidbody>();
            if (body)
            {
                Undo.RecordObject(body, "Reset player velocity");
#if UNITY_6000_0_OR_NEWER
                body.linearVelocity = Vector3.zero;
#else
                body.velocity = Vector3.zero;
#endif
                body.angularVelocity = Vector3.zero;
            }
            EditorUtility.SetDirty(player);
        }

        static void ApplyPlayerVisualFacing(VWS.VARCOSceneTuningProfile profile)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (!player)
                return;

            var controller = player.GetComponent<VWS.PlayerController_ThirdPerson>();
            if (!controller)
                return;

            Undo.RecordObject(controller, "Tune player visual facing");
            controller.preferTopLevelVisualRoot = true;
            controller.visualYawOffset = profile.playerVisualYawOffset;

            RemoveEmptyVisualFacingWrapper(player.transform);

            var visualRoot = FindRendererTopRoot(player.transform);
            if (visualRoot && visualRoot != player.transform)
                controller.modelRoot = visualRoot;

            if (profile.normalizeGeneratedMeshYaw)
                ApplyGeneratedMeshYawCorrection(player.transform, profile.generatedMeshYawCorrection);

            if (controller.modelRoot && controller.modelRoot != player.transform)
            {
                Undo.RecordObject(controller.modelRoot, "Align player visual root");
                controller.modelRoot.rotation = Quaternion.Euler(0f, player.transform.eulerAngles.y + controller.visualYawOffset, 0f);
                EditorUtility.SetDirty(controller.modelRoot);
            }

            EditorUtility.SetDirty(controller);
        }

        static void RemoveEmptyVisualFacingWrapper(Transform player)
        {
            var wrapper = player.Find("VARCO_VisualFacingRoot");
            if (wrapper && wrapper.childCount == 0)
                Undo.DestroyObjectImmediate(wrapper.gameObject);
        }

        static Transform FindRendererTopRoot(Transform owner)
        {
            Transform candidate = null;
            foreach (var renderer in owner.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer || renderer.transform == owner)
                    continue;

                var top = renderer.transform;
                while (top.parent && top.parent != owner)
                    top = top.parent;

                if (top == owner)
                    continue;
                if (!candidate)
                {
                    candidate = top;
                    continue;
                }

                if (candidate != top)
                    return null;
            }

            return candidate;
        }

        static void ApplyGeneratedMeshYawCorrection(Transform owner, float yawCorrection)
        {
            foreach (var renderer in owner.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer || !renderer.transform.parent)
                    continue;

                var meshAxis = renderer.transform.parent;
                if (meshAxis == owner)
                    continue;

                Undo.RecordObject(meshAxis, "Apply generated mesh yaw correction");
                meshAxis.localRotation = Quaternion.Euler(0f, yawCorrection, 0f);
                EditorUtility.SetDirty(meshAxis);
            }
        }

        static void ApplyCamera(VWS.VARCOSceneTuningProfile profile)
        {
            var camera = Camera.main;
            if (!camera)
                return;

            var player = GameObject.FindGameObjectWithTag("Player");
            var follow = camera.GetComponent<VWS.ThirdPersonCamera>();
            if (!follow)
                follow = Undo.AddComponent<VWS.ThirdPersonCamera>(camera.gameObject);

            Undo.RecordObject(camera, "Tune game camera");
            Undo.RecordObject(camera.transform, "Tune game camera transform");
            Undo.RecordObject(follow, "Tune third person camera");

            if (player)
                follow.target = player.transform;
            camera.fieldOfView = profile.cameraFov;
            follow.ApplyViewPreset(
                profile.cameraYaw,
                profile.cameraPitch,
                profile.cameraDistance,
                profile.cameraPivotOffset,
                profile.cameraMinPitch,
                profile.cameraMaxPitch,
                profile.orbitOnlyWhileRightMouse,
                true);
            follow.lockCursorOnStart = true;
            follow.positionSharpness = Mathf.Max(10f, follow.positionSharpness);
            follow.pivotSharpness = Mathf.Max(12f, follow.pivotSharpness);
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(follow);
        }

        static void ApplyLighting(VWS.VARCOSceneTuningProfile profile)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = profile.ambientColor;
            RenderSettings.fog = profile.useFog;
            RenderSettings.fogColor = profile.fogColor;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = profile.fogDensity;

            var sun = GameObject.Find("Directional Light");
            if (sun)
            {
                var light = sun.GetComponent<Light>();
                if (light)
                {
                    Undo.RecordObject(light, "Tune directional light");
                    light.color = profile.sunColor;
                    light.intensity = profile.sunIntensity;
                    light.shadows = LightShadows.Soft;
                    EditorUtility.SetDirty(light);
                }
                Undo.RecordObject(sun.transform, "Tune directional light rotation");
                sun.transform.rotation = Quaternion.Euler(profile.sunEulerAngles);
            }

            var key = GameObject.Find("VARCO_Arena_KeyLight");
            if (!key)
                key = GameObject.Find("VARCO_GameFeel_KeyLight");
            if (!key)
            {
                key = new GameObject("VARCO_GameFeel_KeyLight");
                Undo.RegisterCreatedObjectUndo(key, "Create game feel key light");
            }

            var keyLight = key.GetComponent<Light>();
            if (!keyLight)
                keyLight = Undo.AddComponent<Light>(key);
            Undo.RecordObject(key.transform, "Tune key light transform");
            Undo.RecordObject(keyLight, "Tune key light");
            key.transform.position = new Vector3(0f, 5.7f, -4.2f);
            key.transform.rotation = Quaternion.Euler(62f, 0f, 0f);
            keyLight.type = LightType.Spot;
            keyLight.color = profile.keyLightColor;
            keyLight.intensity = profile.keyLightIntensity;
            keyLight.range = 18f;
            keyLight.spotAngle = 72f;
            keyLight.shadows = LightShadows.Soft;
            EditorUtility.SetDirty(keyLight);
        }

        static void ApplyArenaMaterials(VWS.VARCOSceneTuningProfile profile)
        {
            var outer = GetOrCreateMaterial("OuterGround", profile.outerGroundColor, 0f);
            var floor = GetOrCreateMaterial("ArenaFloor", profile.arenaFloorColor, 0f);
            var lane = GetOrCreateMaterial("Lane", profile.laneColor, 0.35f);
            var start = GetOrCreateMaterial("Start", profile.startColor, 0.35f);
            var guide = GetOrCreateMaterial("Guide", profile.guideColor, 0.7f);
            var goal = GetOrCreateMaterial("Goal", profile.goalColor, 0.9f);
            var spawn = GetOrCreateMaterial("Spawn", profile.spawnColor, 0.45f);
            var prop = GetOrCreateMaterial("Prop", profile.propColor, 0f);

            AssignMaterial(GameObject.Find("VARCO_Ground"), outer);
            AssignMaterial(GameObject.Find("VARCO_Arena_CombatWave_Floor"), floor);
            AssignMaterial(GameObject.Find("VARCO_Arena_StartPad"), start);

            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!renderer)
                    continue;
                var name = renderer.gameObject.name;
                if (name.Contains("MainLane") || name.Contains("CoverLane"))
                    AssignMaterial(renderer, lane);
                else if (name.Contains("Guide_"))
                    AssignMaterial(renderer, guide);
                else if (name.Contains("GoalFrame"))
                    AssignMaterial(renderer, goal);
                else if (name.Contains("SpawnMarker"))
                    AssignMaterial(renderer, spawn);
                else if (name.Contains("EnvironmentProp"))
                    AssignMaterial(renderer, prop);
            }
        }

        static void ApplyPolishObjects(VWS.VARCOSceneTuningProfile profile)
        {
            var root = EnsurePolishRoot(profile);
            if (profile.clearOldPolishObjects)
                ClearChildren(root.transform);

            var lane = GetOrCreateMaterial("LaneAccent", profile.laneColor, 1f);
            var guide = GetOrCreateMaterial("GuideAccent", profile.guideColor, 1.2f);
            var cover = GetOrCreateMaterial("CoverVisual", profile.coverColor, 0f);
            var goal = GetOrCreateMaterial("GoalBeacon", profile.goalColor, 1.6f);
            var spawn = GetOrCreateMaterial("EnemyDanger", profile.spawnColor, 0.9f);
            var start = GetOrCreateMaterial("StartAccent", profile.startColor, 0.9f);

            CreateDisc(root.transform, "PlayerStartHalo", new Vector3(0f, 0.092f, -6.55f), new Vector3(2.05f, 0.026f, 2.05f), start);
            CreateVisualBox(root.transform, "LaneSpine", new Vector3(0f, 0.12f, 0.45f), new Vector3(0.24f, Mathf.Max(0.025f, profile.laneAccentHeight), 10.7f), lane);
            CreateChevron(root.transform, "PathChevron_01", -4.75f, profile, guide);
            CreateChevron(root.transform, "PathChevron_02", -2.15f, profile, lane);
            CreateChevron(root.transform, "PathChevron_03", 0.45f, profile, guide);
            CreateChevron(root.transform, "PathChevron_04", 3.05f, profile, lane);
            CreateChevron(root.transform, "PathChevron_05", 5.65f, profile, guide);

            CreateVisualBox(root.transform, "CoverVisual_L_01", new Vector3(-3.4f, profile.coverVisualHeight * 0.5f, -1.1f), new Vector3(1.55f, profile.coverVisualHeight, 0.58f), cover);
            CreateVisualBox(root.transform, "CoverVisual_R_01", new Vector3(3.4f, profile.coverVisualHeight * 0.5f, -1.1f), new Vector3(1.55f, profile.coverVisualHeight, 0.58f), cover);
            CreateVisualBox(root.transform, "CoverVisual_L_02", new Vector3(-3.65f, profile.coverVisualHeight * 0.5f, 3.15f), new Vector3(1.35f, profile.coverVisualHeight, 0.58f), cover);
            CreateVisualBox(root.transform, "CoverVisual_R_02", new Vector3(3.65f, profile.coverVisualHeight * 0.5f, 3.15f), new Vector3(1.35f, profile.coverVisualHeight, 0.58f), cover);
            CreateCoverTopAccent(root.transform, "CoverVisual_L_01", new Vector3(-3.4f, profile.coverVisualHeight + 0.04f, -1.1f), new Vector3(1.64f, 0.055f, 0.66f), lane);
            CreateCoverTopAccent(root.transform, "CoverVisual_R_01", new Vector3(3.4f, profile.coverVisualHeight + 0.04f, -1.1f), new Vector3(1.64f, 0.055f, 0.66f), lane);
            CreateCoverTopAccent(root.transform, "CoverVisual_L_02", new Vector3(-3.65f, profile.coverVisualHeight + 0.04f, 3.15f), new Vector3(1.44f, 0.055f, 0.66f), guide);
            CreateCoverTopAccent(root.transform, "CoverVisual_R_02", new Vector3(3.65f, profile.coverVisualHeight + 0.04f, 3.15f), new Vector3(1.44f, 0.055f, 0.66f), guide);

            CreateDisc(root.transform, "EnemyFocusPad", new Vector3(0f, 0.075f, 6.8f), new Vector3(2.25f, 0.028f, 2.25f), spawn);
            CreateDisc(root.transform, "GoalGlowPad", new Vector3(0f, 0.08f, 8.9f), new Vector3(2.6f, 0.03f, 2.6f), goal);
            CreateBeacon(root.transform, "GoalBeacon_L", new Vector3(-2.3f, 1.12f, 8.75f), profile, goal);
            CreateBeacon(root.transform, "GoalBeacon_R", new Vector3(2.3f, 1.12f, 8.75f), profile, goal);
        }

        static GameObject EnsurePolishRoot(VWS.VARCOSceneTuningProfile profile)
        {
            var root = GameObject.Find(PolishRootName);
            if (!root)
            {
                root = new GameObject(PolishRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create VARCO game feel polish root");
            }

            Undo.RecordObject(root.transform, "Tune polish root");
            root.transform.position = profile.polishRootPosition;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            return root;
        }

        static void ClearChildren(Transform root)
        {
            var children = new List<GameObject>();
            foreach (Transform child in root)
                children.Add(child.gameObject);
            foreach (var child in children)
                Undo.DestroyObjectImmediate(child);
        }

        static void CreateChevron(Transform parent, string name, float z, VWS.VARCOSceneTuningProfile profile, Material material)
        {
            var left = CreateVisualBox(parent, name + "_L", new Vector3(-0.42f, 0.14f, z), new Vector3(0.18f, Mathf.Max(0.025f, profile.laneAccentHeight), 1.06f * profile.guideMarkerScale), material);
            var right = CreateVisualBox(parent, name + "_R", new Vector3(0.42f, 0.14f, z), new Vector3(0.18f, Mathf.Max(0.025f, profile.laneAccentHeight), 1.06f * profile.guideMarkerScale), material);
            left.transform.rotation = Quaternion.Euler(0f, 34f, 0f);
            right.transform.rotation = Quaternion.Euler(0f, -34f, 0f);
        }

        static void CreateCoverTopAccent(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            CreateVisualBox(parent, name + "_TopAccent", position, scale, material);
        }

        static GameObject CreateVisualBox(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(go, "Create visual polish object");
            go.name = "VARCO_Polish_" + name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = scale;
            RemoveCollider(go);
            AssignMaterial(go, material);
            return go;
        }

        static GameObject CreateDisc(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Undo.RegisterCreatedObjectUndo(go, "Create visual polish disc");
            go.name = "VARCO_Polish_" + name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = scale;
            RemoveCollider(go);
            AssignMaterial(go, material);
            return go;
        }

        static void CreateBeacon(Transform parent, string name, Vector3 position, VWS.VARCOSceneTuningProfile profile, Material material)
        {
            var post = CreateVisualBox(parent, name + "_Post", position, new Vector3(0.18f, 1.55f, 0.18f), material);
            var lightGo = new GameObject("VARCO_Polish_" + name + "_Light");
            Undo.RegisterCreatedObjectUndo(lightGo, "Create goal beacon light");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.position = position + Vector3.up * 1.1f;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = profile.goalColor;
            light.intensity = profile.beaconIntensity;
            light.range = 4.2f;
            light.shadows = LightShadows.None;
            EditorUtility.SetDirty(post);
            EditorUtility.SetDirty(light);
        }

        static void RemoveCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider)
                Undo.DestroyObjectImmediate(collider);
        }

        static void ApplyHudHints()
        {
            var hud = Object.FindFirstObjectByType<VWS.VARCOGameHUD>();
            if (!hud)
                return;
            Undo.RecordObject(hud, "Tune HUD hints");
            hud.fallbackGenre = VWS.GenreType.Arena;
            hud.modeLabelOverride = "Combat Arena";
            hud.objectiveOverride = "Defeat the enemy wave, recover when low, then push to the golden gate.";
            EditorUtility.SetDirty(hud);
        }

        static void AssignMaterial(GameObject go, Material material)
        {
            if (!go || !material)
                return;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                AssignMaterial(renderer, material);
        }

        static void AssignMaterial(Renderer renderer, Material material)
        {
            if (!renderer || !material)
                return;
            Undo.RecordObject(renderer, "Assign tuned material");
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
        }

        static Material GetOrCreateMaterial(string name, Color color, float emission)
        {
            EnsureFolder(MaterialFolder);
            var path = MaterialFolder + "/VARCO_GameFeel_" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader)
                    shader = Shader.Find("Standard");
                if (!shader)
                    shader = Shader.Find("Sprites/Default");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            SetMaterialColor(material, color, emission);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void SetMaterialColor(Material material, Color color, float emission)
        {
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            if (emission > 0f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
            }
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
