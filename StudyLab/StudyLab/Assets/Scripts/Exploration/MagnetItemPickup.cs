using UnityEngine;

namespace VARCO_Workshop
{
    /// <summary>월드에 놓인 자석 아이템. 플레이어가 닿으면 <see cref="MagnetAimController"/>의
    /// 자석 능력을 켜고(모델이 설정돼 있으면 장착까지) 자기 자신은 사라집니다.</summary>
    [RequireComponent(typeof(Collider))]
    public class MagnetItemPickup : MonoBehaviour
    {
        public AudioClip pickupClip;
        public bool destroyOnPickup = true;

        void Reset() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            var magnet = other.GetComponent<MagnetAimController>() ?? other.GetComponentInParent<MagnetAimController>();
            if (!magnet) return;
            if (magnet.hasMagnetAbility) return;

            magnet.GrantMagnetAbility();
            VARCOGameHUD.ShowNotice("자석 능력을 습득했습니다!");
            if (pickupClip) AudioSource.PlayClipAtPoint(pickupClip, transform.position);
            if (destroyOnPickup) Destroy(gameObject);
        }
    }
}
