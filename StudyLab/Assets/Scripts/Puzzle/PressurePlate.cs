using UnityEngine;

namespace VARCO_Workshop
{
    [RequireComponent(typeof(Collider))]
    public class PressurePlate : MonoBehaviour
    {
        public DoorController[] targets;
        int weight;

        void Reset() { var c = GetComponent<Collider>(); c.isTrigger = true; }

        void OnTriggerEnter(Collider o) { if (o.GetComponentInParent<Rigidbody>() || o.CompareTag("Player")) { weight++; Open(); } }
        void OnTriggerExit(Collider o)  { if (o.GetComponentInParent<Rigidbody>() || o.CompareTag("Player")) { weight = Mathf.Max(0, weight - 1); if (weight==0) Close(); } }

        void Open()  { foreach (var t in targets) if (t) t.SetOpen(true);  }
        void Close() { foreach (var t in targets) if (t) t.SetOpen(false); }
    }
}
