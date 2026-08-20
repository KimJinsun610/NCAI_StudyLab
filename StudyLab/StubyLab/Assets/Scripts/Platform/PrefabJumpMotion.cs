using UnityEngine;

namespace VARCO_Workshop
{
    public class PrefabJumpMotion : MonoBehaviour
    {
        public float height = 1.2f;
        public float speed = 1.5f;
        public bool playOnStart = true;
        public bool useLocalSpace = true;

        Vector3 startLocalPosition;
        Vector3 startWorldPosition;

        void OnEnable()
        {
            startLocalPosition = transform.localPosition;
            startWorldPosition = transform.position;
        }

        void Update()
        {
            if (!playOnStart)
                return;

            var offset = Mathf.Abs(Mathf.Sin(Time.time * Mathf.Max(0.01f, speed))) * Mathf.Max(0f, height);
            if (useLocalSpace)
                transform.localPosition = startLocalPosition + Vector3.up * offset;
            else
                transform.position = startWorldPosition + Vector3.up * offset;
        }
    }
}
