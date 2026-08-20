using UnityEngine;

namespace VARCO_Workshop
{
    public class PickupBob : MonoBehaviour
    {
        public float rotateSpeed = 90f;
        public float bobHeight = 0.12f;
        public float bobSpeed = 2.2f;
        public float phase;

        Vector3 baseLocalPosition;

        void Start()
        {
            baseLocalPosition = transform.localPosition;
        }

        void Update()
        {
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
            var next = baseLocalPosition;
            next.y += Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight;
            transform.localPosition = next;
        }
    }
}
