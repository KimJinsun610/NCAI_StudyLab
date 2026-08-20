#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VARCO_Workshop.Editor
{
    /// <summary>자석(MagnetTarget/MagnetAimController) 기능을 빠르게 테스트하기 위한 1회성 씬 빌더.
    /// 학생용 게임 만들기 파이프라인과는 별개의 개발자 테스트 도구입니다.</summary>
    public static class VARCOMagnetTestSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/VARCO_Magnet/VARCO_Magnet_Test.unity";

        [MenuItem("VARCO/테스트 씬/자석 퍼즐 테스트 씬 생성")]
        public static void BuildMagnetTestScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "VARCO_Magnet_Test";

            CreateLight();
            CreateFloor();

            var player = CreatePlayer(new Vector3(0f, 1f, 0f));
            CreateCamera(player.transform);

            CreateMagnetCube("Magnet_CubeA_Light", new Vector3(2f, 0.5f, 4f), mass: 1f, maxRange: 8f);
            CreateMagnetSphere("Magnet_SphereB_Heavy", new Vector3(-2f, 0.5f, 6f), mass: 4f, maxRange: 8f);
            CreateMagnetCube("Magnet_CubeC_ShortRange", new Vector3(0f, 0.5f, 9f), mass: 1f, maxRange: 7f);

            SaveScene(scene, ScenePath);
            Debug.Log("[VARCO 자석] 테스트 씬을 생성했습니다: " + ScenePath);
        }

        static void CreateLight()
        {
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        }

        static void CreateFloor()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Ground";
            go.transform.position = Vector3.zero;
            go.transform.localScale = new Vector3(4f, 1f, 4f);
            SetObjectColor(go, new Color(0.4f, 0.42f, 0.45f));
        }

        static GameObject CreatePlayer(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Magnet_Player";
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
            go.tag = "Player";
            SetObjectColor(go, new Color(0.25f, 0.55f, 0.9f));

            var rb = GetOrAdd<Rigidbody>(go);
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var ctrl = GetOrAdd<PlayerController_ThirdPerson>(go);
            ctrl.moveSpeed = 5f;
            ctrl.turnSpeed = 12f;
            ctrl.useCameraSpace = true;

            var magnet = GetOrAdd<MagnetAimController>(go);
            magnet.aimRange = 10f;
            magnet.togglePolarityKey = KeyCode.Q;

            return go;
        }

        static void CreateCamera(Transform target)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = target.position + new Vector3(0f, 2.5f, -5f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            camGo.AddComponent<AudioListener>();

            var follow = camGo.AddComponent<ThirdPersonCamera>();
            follow.target = target;
            follow.distance = 5f;
            follow.pivotOffset = new Vector3(0f, 1.25f, 0f);
            follow.shoulderView = true;
            follow.shoulderSideOffset = 0.55f;
            follow.lockCursorOnStart = true;
        }

        static void CreateMagnetCube(string name, Vector3 pos, float mass, float maxRange)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SetupMagnetObject(go, name, pos, mass, maxRange);
        }

        static void CreateMagnetSphere(string name, Vector3 pos, float mass, float maxRange)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            SetupMagnetObject(go, name, pos, mass, maxRange);
        }

        static void SetupMagnetObject(GameObject go, string name, Vector3 pos, float mass, float maxRange)
        {
            go.name = name;
            go.transform.position = pos;
            SetObjectColor(go, new Color(0.75f, 0.75f, 0.78f));

            var rb = GetOrAdd<Rigidbody>(go);
            rb.mass = mass;

            var magnet = GetOrAdd<MagnetTarget>(go);
            magnet.maxRange = maxRange;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component ? component : go.AddComponent<T>();
        }

        static void SetObjectColor(GameObject go, Color color)
        {
            var renderer = go.GetComponentInChildren<Renderer>();
            if (!renderer) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = go.name + "_Mat" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            renderer.sharedMaterial = mat;
        }

        static void SaveScene(Scene scene, string path)
        {
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir.Replace("\\", "/")))
                System.IO.Directory.CreateDirectory(dir);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif
