using UnityEngine;

namespace VARCO_Workshop
{
    /// <summary>Player.controller FSM: Animator parameters와 이름 일치.</summary>
    public static class PlayerAnimParams
    {
        public static readonly int IsWalk   = Animator.StringToHash("IsWalk");
        public static readonly int IsRun    = Animator.StringToHash("IsRun");
        public static readonly int IsJump   = Animator.StringToHash("IsJump");
        public static readonly int IsAttack = Animator.StringToHash("IsAttack");
        public static readonly int IsDead   = Animator.StringToHash("IsDead");
        public static readonly int InWater  = Animator.StringToHash("InWater");
    }
}
