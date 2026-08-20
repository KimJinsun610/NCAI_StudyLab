using UnityEngine;

namespace VARCO_Workshop
{
    /// <summary>장르, 체력, 목표, 웨이브, HUD 안내 문구를 담는 게임 프로필입니다.</summary>
    [CreateAssetMenu(fileName = "GameProfile", menuName = "VARCO/데이터/게임 프로필", order = 0)]
    public class GameProfile : ScriptableObject
    {
        [Header("게임 규칙")]
        public GenreType genre = GenreType.Arena;
        [Min(1)] public int playerMaxHP = 100;
        [Min(0)] public int waveCount = 3;
        [Min(0)] public int itemGoal = 0; // 0이면 사용하지 않음
        [Tooltip("툴과 HUD가 함께 사용하는 주요 클리어 조건입니다.")]
        public CompletionCondition clearCondition = CompletionCondition.DefeatWaves;

        [Header("HUD 한글 문구")]
        [TextArea(1, 3)]
        public string objectiveText = "";
        [TextArea(1, 2)]
        public string controlGuideText = "";
        [TextArea(1, 2)]
        public string clearMessage = "";
        [TextArea(1, 2)]
        public string gameOverMessage = "";

        [TextArea(1, 4)]
        public string designNotes = "";
    }
}
