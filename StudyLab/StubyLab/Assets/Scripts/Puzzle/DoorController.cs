using UnityEngine;

namespace VARCO_Workshop
{
    public class DoorController : MonoBehaviour
    {
        public Vector3 openOffset = new Vector3(0, 3f, 0);
        Vector3 start;
        bool open;

        void Start() => start = transform.position;

        public void SetOpen(bool v) => open = v;

        void Update() => transform.position = Vector3.Lerp(transform.position, start + (open ? openOffset : Vector3.zero), 5f * Time.deltaTime);
    }
}
