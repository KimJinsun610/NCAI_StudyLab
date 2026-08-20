using UnityEngine;

namespace VARCO_Workshop
{
    [RequireComponent(typeof(Collider))]
    public class PlatformGoal : MonoBehaviour
    {
        void Reset() => GetComponent<Collider>().isTrigger = true;
        void OnTriggerEnter(Collider o) { if (o.CompareTag("Player") && GameManager.Instance) GameManager.Instance.TriggerClear(); }
    }
}
