using UnityEngine;

namespace VARCO_Workshop
{
    /// <summary>물(위험 구역) 오브젝트에 HazardZone과 함께 붙여서 씁니다. 데미지 로직은 건드리지 않고,
    /// 플레이어가 들어오면 Animator의 InWater 파라미터만 켜고 끕니다(Drowning 애니메이션 재생용).</summary>
    [RequireComponent(typeof(Collider))]
    public class WaterAnimationTrigger : MonoBehaviour
    {
        void Reset() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other) => SetInWater(other, true);
        void OnTriggerExit(Collider other) => SetInWater(other, false);

        void SetInWater(Collider other, bool value)
        {
            var health = other.GetComponent<PlayerHealth>() ?? other.GetComponentInParent<PlayerHealth>();
            if (!health || !health.CompareTag("Player")) return;

            var anim = RuntimeAnimatorResolver.FindBestAnimator(health.gameObject);
            if (anim) anim.SetBool(PlayerAnimParams.InWater, value);
        }
    }
}
