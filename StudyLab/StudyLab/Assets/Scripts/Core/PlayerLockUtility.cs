using UnityEngine;

namespace VARCO_Workshop
{
    /// <summary>게임 클리어/오버처럼 결과 화면이 떠있는 동안 플레이어 조작을 막아둘 때 쓰는 공용 헬퍼.
    /// PlayerController_ThirdPerson/_Platform이 이미 구현한 IExternalMoveOverride(자석 텔레키네시스 잠금과 동일한 훅)를 재사용합니다.</summary>
    public static class PlayerLockUtility
    {
        public static void FreezePlayer()
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (!playerGo) return;

            var mover = playerGo.GetComponent<IExternalMoveOverride>();
            if (mover != null)
                mover.SetQaMoveInput(Vector2.zero, false);
        }
    }
}
