using UnityEngine;

namespace VARCO_Workshop
{
    public class VARCOSceneTuningProfile : MonoBehaviour
    {
        [Header("Scope")]
        public bool snapPlayerToStartPad = true;
        public bool applyCamera = true;
        public bool applyLighting = true;
        public bool applyArenaMaterials = true;
        public bool applyPlayerVisualFacing = true;
        public bool createPolishObjects = true;
        public bool clearOldPolishObjects = true;

        [Header("Camera")]
        public float cameraYaw = 0f;
        [Range(5f, 70f)]
        public float cameraPitch = 24f;
        [Range(2f, 14f)]
        public float cameraDistance = 5.8f;
        [Range(35f, 80f)]
        public float cameraFov = 52f;
        public Vector3 cameraPivotOffset = new Vector3(0f, 1.32f, 0.28f);
        [Range(-10f, 40f)]
        public float cameraMinPitch = 12f;
        [Range(25f, 80f)]
        public float cameraMaxPitch = 58f;
        public bool orbitOnlyWhileRightMouse = false;

        [Header("Player Facing")]
        public bool normalizeGeneratedMeshYaw = true;
        [Range(-180f, 180f)]
        public float generatedMeshYawCorrection = 180f;
        [Range(-180f, 180f)]
        public float playerVisualYawOffset = 0f;

        [Header("Arena Layout")]
        public Vector3 playerStartPosition = new Vector3(0f, 0.5f, -6.55f);
        public Vector3 polishRootPosition = Vector3.zero;
        [Range(0.1f, 2f)]
        public float guideMarkerScale = 1.08f;
        [Range(0f, 2f)]
        public float laneAccentHeight = 0.06f;
        [Range(0f, 5f)]
        public float coverVisualHeight = 1.35f;
        [Range(0f, 4f)]
        public float beaconIntensity = 2.4f;

        [Header("Colors")]
        public Color outerGroundColor = new Color(0.13f, 0.15f, 0.13f, 1f);
        public Color arenaFloorColor = new Color(0.25f, 0.27f, 0.25f, 1f);
        public Color laneColor = new Color(0.05f, 0.72f, 0.66f, 1f);
        public Color startColor = new Color(0.1f, 0.64f, 0.8f, 1f);
        public Color guideColor = new Color(1f, 0.88f, 0.2f, 1f);
        public Color goalColor = new Color(1f, 0.67f, 0.08f, 1f);
        public Color spawnColor = new Color(0.82f, 0.18f, 0.18f, 1f);
        public Color propColor = new Color(0.34f, 0.37f, 0.39f, 1f);
        public Color coverColor = new Color(0.24f, 0.27f, 0.3f, 1f);

        [Header("Lighting")]
        public Color ambientColor = new Color(0.48f, 0.5f, 0.49f, 1f);
        public Color sunColor = new Color(1f, 0.91f, 0.76f, 1f);
        [Range(0f, 5f)]
        public float sunIntensity = 1.65f;
        public Vector3 sunEulerAngles = new Vector3(50f, -35f, 0f);
        public Color keyLightColor = new Color(1f, 0.82f, 0.48f, 1f);
        [Range(0f, 15f)]
        public float keyLightIntensity = 7.6f;
        public Color fogColor = new Color(0.39f, 0.4f, 0.39f, 1f);
        [Range(0f, 0.08f)]
        public float fogDensity = 0.01f;
        public bool useFog = true;

        public void ResetArenaDefaults()
        {
            snapPlayerToStartPad = true;
            applyCamera = true;
            applyLighting = true;
            applyArenaMaterials = true;
            applyPlayerVisualFacing = true;
            createPolishObjects = true;
            clearOldPolishObjects = true;
            cameraYaw = 0f;
            cameraPitch = 24f;
            cameraDistance = 5.8f;
            cameraFov = 52f;
            cameraPivotOffset = new Vector3(0f, 1.32f, 0.28f);
            cameraMinPitch = 12f;
            cameraMaxPitch = 58f;
            orbitOnlyWhileRightMouse = false;
            normalizeGeneratedMeshYaw = true;
            generatedMeshYawCorrection = 180f;
            playerVisualYawOffset = 0f;
            playerStartPosition = new Vector3(0f, 0.5f, -6.55f);
            guideMarkerScale = 1.08f;
            laneAccentHeight = 0.06f;
            coverVisualHeight = 1.35f;
            beaconIntensity = 2.4f;
            outerGroundColor = new Color(0.13f, 0.15f, 0.13f, 1f);
            arenaFloorColor = new Color(0.25f, 0.27f, 0.25f, 1f);
            laneColor = new Color(0.05f, 0.72f, 0.66f, 1f);
            startColor = new Color(0.1f, 0.64f, 0.8f, 1f);
            guideColor = new Color(1f, 0.88f, 0.2f, 1f);
            goalColor = new Color(1f, 0.67f, 0.08f, 1f);
            spawnColor = new Color(0.82f, 0.18f, 0.18f, 1f);
            propColor = new Color(0.34f, 0.37f, 0.39f, 1f);
            coverColor = new Color(0.24f, 0.27f, 0.3f, 1f);
            ambientColor = new Color(0.48f, 0.5f, 0.49f, 1f);
            sunColor = new Color(1f, 0.91f, 0.76f, 1f);
            sunIntensity = 1.65f;
            sunEulerAngles = new Vector3(50f, -35f, 0f);
            keyLightColor = new Color(1f, 0.82f, 0.48f, 1f);
            keyLightIntensity = 7.6f;
            fogColor = new Color(0.39f, 0.4f, 0.39f, 1f);
            fogDensity = 0.01f;
            useFog = true;
        }
    }
}
