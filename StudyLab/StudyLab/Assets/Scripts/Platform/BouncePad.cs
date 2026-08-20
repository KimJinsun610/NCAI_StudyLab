using UnityEngine;

namespace VARCO_Workshop
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class BouncePad : MonoBehaviour
    {
        public float bounceVelocity = 10f;
        public float horizontalBoost = 2f;
        public bool usePadForwardForBoost = true;

        void Reset()
        {
            ConfigureTrigger();
        }

        void OnValidate()
        {
            ConfigureTrigger();
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other)
                return;

            var horizontalVelocity = ResolveHorizontalBoost(other);
            var platformPlayer = other.GetComponentInParent<PlayerController_Platform>();
            if (platformPlayer)
            {
                platformPlayer.Bounce(bounceVelocity, horizontalVelocity);
                return;
            }

            var body = other.attachedRigidbody;
            if (!body)
                body = other.GetComponentInParent<Rigidbody>();

            if (!body)
                return;

#if UNITY_6000_0_OR_NEWER
            var velocity = body.linearVelocity;
            velocity.y = Mathf.Max(velocity.y, Mathf.Abs(bounceVelocity));
            velocity += horizontalVelocity;
            body.linearVelocity = velocity;
#else
            var velocity = body.velocity;
            velocity.y = Mathf.Max(velocity.y, Mathf.Abs(bounceVelocity));
            velocity += horizontalVelocity;
            body.velocity = velocity;
#endif
        }

        Vector3 ResolveHorizontalBoost(Collider other)
        {
            if (horizontalBoost <= 0f)
                return Vector3.zero;

            var direction = usePadForwardForBoost ? transform.forward : other.transform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            return direction.normalized * horizontalBoost;
        }

        void ConfigureTrigger()
        {
            var collider = GetComponent<Collider>();
            if (collider)
                collider.isTrigger = true;
        }
    }
}
