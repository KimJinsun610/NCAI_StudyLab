using UnityEngine;

namespace VARCO_Workshop
{
    public class Checkpoint : MonoBehaviour
    {
        public static Vector3 LastPos { get; private set; }
        public static bool HasLastPos { get; private set; }

        [SerializeField] bool setOnStart = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            LastPos = Vector3.zero;
            HasLastPos = false;
        }

        void Start()
        {
            if (setOnStart && !HasLastPos)
                SetLastPosition(transform.position);
        }

        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<PlayerController_Platform>();
            if (player == null && !other.CompareTag("Player"))
                return;

            var respawnPosition = transform.position;
            SetLastPosition(respawnPosition);
            VARCOGameHUD.ShowNotice("체크포인트가 저장되었습니다.");

            if (player)
                player.SetRespawnPoint(respawnPosition, player.transform.rotation);
        }

        public static void SetLastPosition(Vector3 position)
        {
            LastPos = position;
            HasLastPos = true;
        }

        public static bool TryGetLastPosition(out Vector3 position)
        {
            position = LastPos;
            return HasLastPos;
        }
    }
}
