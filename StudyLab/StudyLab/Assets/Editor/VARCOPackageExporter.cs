#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace VARCO_Workshop.Editor
{
    public static class VARCOPackageExporter
    {
        const string MenuRoot = "VARCO/\uD328\uD0A4\uC9C0 \uB0B4\uBCF4\uB0B4\uAE30";
        const string DefaultOutputFolder = "E:/Yong-s-Workspace/obsidian-vault/\uC218\uC5C5_\uC790\uB8CC/Varco/Varco_\uC720\uB2C8\uD2F0\uD234";

        public static void ExportVarcoUnityPackageFromMenu()
        {
            ExportPackage(BuildDefaultPackagePath(), interactive: true);
        }

        public static void RepairPackagePrefabsBeforeExportFromMenu()
        {
            RepairPackagePrefabsBeforeExport();
        }

        public static void ExportVarcoUnityPackage()
        {
            ExportPackage(BuildDefaultPackagePath(), interactive: false);
        }

        static string BuildDefaultPackagePath()
        {
            var folder = Directory.Exists(DefaultOutputFolder)
                ? DefaultOutputFolder
                : Path.Combine(Application.dataPath, "../Builds");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "VarcoUnity_Codex_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unitypackage")
                .Replace('\\', '/');
        }

        static void ExportPackage(string outputPath, bool interactive)
        {
            RepairPackagePrefabsBeforeExport();

            var assets = new[]
            {
                "Assets/Editor",
                "Assets/Scripts",
                "Assets/Animations/Generated",
                "Assets/Prefabs",
                "Assets/ScriptableObjects",
                "Assets/Documentation",
                "Assets/VARCOPresetKits"
            }.Where(AssetExists).ToArray();

            AssetDatabase.ExportPackage(
                assets,
                outputPath,
                ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);

            Debug.Log("VARCO Unity package exported: " + outputPath);
            if (interactive)
            {
                EditorUtility.DisplayDialog("VARCO \uD328\uD0A4\uC9C0 \uC800\uC7A5", "\uD328\uD0A4\uC9C0\uB97C \uC800\uC7A5\uD588\uC2B5\uB2C8\uB2E4.\n" + outputPath, "\uD655\uC778");
                EditorUtility.RevealInFinder(outputPath);
            }
        }

        public static void RepairPackagePrefabsBeforeExport()
        {
            var roots = new[]
            {
                "Assets/VARCOPresetKits",
                "Assets/Prefabs/Characters",
                "Assets/Prefabs/VARCO_FunctionApplied"
            }.Where(AssetDatabase.IsValidFolder).ToArray();

            var prefabPaths = roots
                .SelectMany(root => AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                .Distinct()
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct()
                .ToArray();

            var repaired = 0;
            var skipped = 0;
            var failed = 0;

            foreach (var path in prefabPaths)
            {
                try
                {
                    if (RepairPrefabAsset(path))
                        repaired++;
                    else
                        skipped++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Debug.LogWarning("VARCO package prefab repair failed: " + path + "\n" + ex.Message);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"VARCO package prefab repair completed. total={prefabPaths.Length}, repaired={repaired}, skipped={skipped}, failed={failed}");
        }

        static bool RepairPrefabAsset(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var role = GuessRole(path, root);
                if (string.IsNullOrEmpty(role))
                    return false;

                var changed = VARCOPrefabRepairUtility.RepairGameplayPrefab(root, role, path, null);
                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                return changed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static string GuessRole(string path, GameObject root)
        {
            var text = (path + " " + (root ? root.name : string.Empty)).ToLowerInvariant();
            if (text.Contains("player") || text.Contains("01_player") || root.GetComponent<VARCO_Workshop.PlayerController_ThirdPerson>() || root.GetComponent<VARCO_Workshop.PlayerController_Platform>())
                return "Player";
            if (text.Contains("enemy") || text.Contains("zombie") || text.Contains("boss") || text.Contains("02_enemy") || root.GetComponent<VARCO_Workshop.EnemyAI_NavMesh>() || root.GetComponent<VARCO_Workshop.EnemyHealth>() || root.GetComponent<NavMeshAgent>())
                return "Enemy";
            return string.Empty;
        }

        static bool AssetExists(string path)
        {
            return AssetDatabase.IsValidFolder(path) || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
        }
    }
}
#endif
