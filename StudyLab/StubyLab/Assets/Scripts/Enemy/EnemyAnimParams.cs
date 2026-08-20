using UnityEngine;

namespace VARCO_Workshop
{
    /// <summary>몬스터/보스 FSM: <c>Boss.controller</c>·기타 적 Animator와 동일한 파라미터명(IsWalk, IsAttack Trigger, IsDead Trigger).</summary>
    public static class EnemyAnimParams
    {
        public static readonly int IsWalk  = Animator.StringToHash("IsWalk");
        public static readonly int IsAttack = Animator.StringToHash("IsAttack");
        public static readonly int IsDead  = Animator.StringToHash("IsDead");
    }
}
