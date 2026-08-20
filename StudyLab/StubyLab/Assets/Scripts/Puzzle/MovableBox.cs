using UnityEngine;

namespace VARCO_Workshop
{
    [RequireComponent(typeof(Rigidbody))]
    public class MovableBox : MonoBehaviour
    {
        public float pushForce = 4f;
        void OnCollisionStay(Collision c) { if (!c.collider.CompareTag("Player")) return; var p = c.contacts[0].point; var dir = c.contacts[0].normal; GetComponent<Rigidbody>().AddForce(-dir * pushForce, ForceMode.Force); }
    }
}
