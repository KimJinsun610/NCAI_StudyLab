using UnityEngine;

namespace VARCO_Workshop
{
    public class PrefabPingPongMotion : MonoBehaviour
    {
        public Vector3 localOffset = new Vector3(4f, 0f, 0f);
        public float speed = 1.2f;
        public bool carryCharacterControllers = true;

        Vector3 startPosition;

        void OnEnable()
        {
            startPosition = transform.position;
        }

        void Update()
        {
            var p = Mathf.PingPong(Time.time * Mathf.Max(0.01f, speed), 1f);
            var previous = transform.position;
            transform.position = Vector3.Lerp(startPosition, startPosition + transform.TransformVector(localOffset), p);

            var delta = transform.position - previous;
            if (carryCharacterControllers && delta.sqrMagnitude > 0.000001f)
                CarryCharacters(delta);
        }

        void CarryCharacters(Vector3 delta)
        {
            var half = Vector3.Scale(transform.lossyScale, new Vector3(0.55f, 0.5f, 0.55f));
            half.y = Mathf.Max(half.y, 0.45f);
            var center = transform.position + Vector3.up * (half.y + 0.15f);
            var hits = Physics.OverlapBox(center, half, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (!hit || !hit.CompareTag("Player"))
                    continue;

                var cc = hit.GetComponent<CharacterController>();
                if (cc && cc.enabled)
                    cc.Move(delta);
            }
        }
    }
}
