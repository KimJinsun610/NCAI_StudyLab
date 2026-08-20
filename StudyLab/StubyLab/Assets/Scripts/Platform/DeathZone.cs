using UnityEngine;

namespace VARCO_Workshop
{
    [RequireComponent(typeof(Collider))]
    public class DeathZone : MonoBehaviour
    {
        void Reset() => GetComponent<Collider>().isTrigger = true;
        void OnTriggerEnter(Collider o)
        {
            var player = o.GetComponentInParent<PlayerController_Platform>();
            if (player == null && !o.CompareTag("Player")) return;

            if (player)
            {
                player.RespawnAtCheckpoint();
                VARCOGameHUD.ShowNotice("낙사했습니다. 마지막 체크포인트로 돌아갑니다.");
            }
            else if (Checkpoint.TryGetLastPosition(out var lastPos))
            {
                o.transform.position = lastPos;
                VARCOGameHUD.ShowNotice("낙사했습니다. 마지막 체크포인트로 돌아갑니다.");
            }
            else
            {
                o.transform.position = Vector3.zero;
                VARCOGameHUD.ShowNotice("낙사했습니다. 시작 위치로 돌아갑니다.");
            }

            var ph = o.GetComponentInParent<PlayerHealth>();
            if (ph) ph.TakeDamage(15);
        }
    }
}
