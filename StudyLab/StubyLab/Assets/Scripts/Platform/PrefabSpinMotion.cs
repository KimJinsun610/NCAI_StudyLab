using UnityEngine;

namespace VARCO_Workshop
{
    [DisallowMultipleComponent]
    public class PrefabSpinMotion : MonoBehaviour
    {
        public Vector3 localAxis = Vector3.up;
        public float degreesPerSecond = 120f;
        public bool useLocalSpace = true;
        public bool pauseWhenGamePaused = true;

        void Update()
        {
            if (pauseWhenGamePaused && Time.timeScale <= 0f)
                return;

            var axis = localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.up;
            transform.Rotate(axis, degreesPerSecond * Time.deltaTime, useLocalSpace ? Space.Self : Space.World);
        }
    }
}
