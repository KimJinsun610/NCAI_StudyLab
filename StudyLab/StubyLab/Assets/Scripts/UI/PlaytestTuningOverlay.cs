using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VARCO_Workshop
{
    public class PlaytestTuningOverlay : MonoBehaviour
    {
        public bool visible = true;
        public KeyCode toggleKey = KeyCode.F8;
        public CameraView cameraView = CameraView.ThirdPerson;

        public enum CameraView
        {
            ThirdPerson,
            QuarterView,
            TopDown,
            SideView
        }

        PlayerAttack attack;
        PlayerController_Platform platformController;
        ThirdPersonCamera followCamera;

#if UNITY_EDITOR
        static void AddOverlay()
        {
            var existing = FindFirstObjectByType<PlaytestTuningOverlay>();
            if (existing)
            {
                Selection.activeObject = existing.gameObject;
                return;
            }

            var go = new GameObject("VW_PlaytestTuningOverlay");
            Undo.RegisterCreatedObjectUndo(go, "VARCO 튜닝 오버레이 추가");
            go.AddComponent<PlaytestTuningOverlay>();
            Selection.activeObject = go;
        }
#endif

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                visible = !visible;

            if (!attack)
                attack = FindFirstObjectByType<PlayerAttack>();
            if (!platformController)
                platformController = FindFirstObjectByType<PlayerController_Platform>();
            if (!followCamera)
                followCamera = FindFirstObjectByType<ThirdPersonCamera>();
        }

        void OnGUI()
        {
            if (!Application.isPlaying || !visible)
                return;

            GUILayout.BeginArea(new Rect(16f, 96f, 320f, 210f), GUI.skin.box);
            GUILayout.Label("VARCO 플레이 테스트 튜닝");
            GUILayout.Label("표시 전환: F8");

            if (attack)
            {
                GUILayout.Space(6f);
                GUILayout.Label($"공격 쿨타임: {attack.cooldown:0.00}초");
                GUILayout.Label($"실제 공격 애니메이션 속도: {attack.EffectiveAttackAnimationSpeed:0.00}배");
                GUILayout.Label("공격 값은 PlayerAttack 인스펙터에서 조정합니다.");
            }
            else
            {
                GUILayout.Label("PlayerAttack을 찾지 못했습니다.");
            }

            if (followCamera)
            {
                GUILayout.Space(6f);
                GUILayout.Label($"카메라: {GetCameraLabel(cameraView)}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("3인칭")) SetCamera(CameraView.ThirdPerson);
                if (GUILayout.Button("쿼터")) SetCamera(CameraView.QuarterView);
                if (GUILayout.Button("탑다운")) SetCamera(CameraView.TopDown);
                if (GUILayout.Button("사이드")) SetCamera(CameraView.SideView);
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("ThirdPersonCamera를 찾지 못했습니다.");
            }

            GUILayout.EndArea();
        }

        void SetCamera(CameraView view)
        {
            cameraView = view;
            if (!followCamera)
                return;

            switch (view)
            {
                case CameraView.QuarterView:
                    followCamera.ApplyViewPreset(135f, 42f, 7.2f, new Vector3(0f, 1.4f, 0f), 30f, 55f, false);
                    SetPlatform3DMode();
                    break;
                case CameraView.TopDown:
                    followCamera.ApplyViewPreset(180f, 75f, 9f, new Vector3(0f, 1f, 0f), 65f, 80f, true);
                    SetPlatform3DMode();
                    break;
                case CameraView.SideView:
                    followCamera.ApplyViewPreset(90f, 12f, 8f, new Vector3(0f, 1.4f, 0f), 0f, 20f, true);
                    if (platformController)
                    {
                        platformController.lockZAxis = true;
                        platformController.useCameraSpace = false;
                    }
                    break;
                default:
                    followCamera.ApplyViewPreset(180f, 16f, 5.8f, new Vector3(0f, 1.25f, 0f), -5f, 45f, false);
                    SetPlatform3DMode();
                    break;
            }
        }

        void SetPlatform3DMode()
        {
            if (!platformController) return;
            platformController.lockZAxis = false;
            platformController.useCameraSpace = true;
        }

        static string GetCameraLabel(CameraView view)
        {
            switch (view)
            {
                case CameraView.QuarterView: return "쿼터뷰";
                case CameraView.TopDown: return "탑다운";
                case CameraView.SideView: return "사이드뷰";
                default: return "3인칭 추적뷰";
            }
        }
    }
}
