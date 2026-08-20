using System.Text;
using UnityEngine;
using UnityEngine.AI;

namespace VARCO_Workshop
{
    /// <summary>
    /// Play 버튼 전 씬 상태를 점검하고 콘솔에 경고를 출력합니다.
    /// Inspector → Context Menu에서 "빠른 점검 실행" 또는
    /// VARCO 블록코딩 수업 중 Console 탭에서 결과를 확인하세요.
    /// </summary>
    public class WorkshopValidator : MonoBehaviour
    {
        [Header("점검 설정")]
        [Tooltip("Play 시작 시 자동으로 점검 실행")]
        public bool checkOnPlay = true;

        void Start()
        {
            if (checkOnPlay)
                RunQuick();
        }

        [ContextMenu("빠른 점검 실행 (콘솔)")]
        public void RunQuick()
        {
            var sb = new StringBuilder();
            var ok = new StringBuilder();

            // ── GameManager ───────────────────────────────────
            if (GameManager.Instance == null)
            {
                sb.AppendLine("❌ GameManager 없음 → VARCO / 자동 제작 / 가장 맞는 게임 만들기 실행.");
            }
            else
            {
                ok.AppendLine("✅ GameManager 확인");
                CheckGameProfile(GameManager.Instance.profile, sb, ok);
            }

            // ── Player 태그 ────────────────────────────────────
            GameObject player = null;
            var playerTagLookupFailed = false;
            try
            {
                player = GameObject.FindWithTag("Player");
            }
            catch (UnityException)
            {
                playerTagLookupFailed = true;
                sb.AppendLine("❌ Player 태그가 프로젝트에 없음 → VARCO / 자동 제작 / 가장 맞는 게임 만들기 실행.");
            }

            if (player != null)
            {
                ok.AppendLine($"✅ Player 확인: {player.name}");
                CheckPlayerSetup(player, sb, ok);
            }
            else if (!playerTagLookupFailed)
            {
                sb.AppendLine("❌ Player 태그 없음 → VARCO / 자동 제작 / 가장 맞는 게임 만들기 실행.");
            }

            // ── Animator Controller ────────────────────────────
            if (player != null)
            {
                var anim = RuntimeAnimatorResolver.FindBestAnimator(player);
                if (anim == null || anim.runtimeAnimatorController == null)
                    sb.AppendLine("⚠️ Player Animator Controller 없음 → VARCO / 게임 메이커에서 자동 제작 다시 실행.");
                else
                    ok.AppendLine($"✅ Player Animator: {anim.runtimeAnimatorController.name}");
            }

            // ── WaveManager (아레나/탐험용) ──────────────────────
            var wave = FindFirstObjectByType<WaveManager>();
            if (wave != null)
            {
                if (wave.waves == null || wave.waves.Length == 0)
                    sb.AppendLine("⚠️ WaveManager.waves 없음 → VARCO / 자동 제작 / 현재 씬 자동 보정 실행.");
                else if (System.Array.Exists(wave.waves, w => w == null || w.enemyPrefab == null))
                    sb.AppendLine("⚠️ 일부 WaveManager waves enemyPrefab 없음 → VARCO / 자동 제작 / 가장 맞는 게임 만들기 실행.");
                else
                    ok.AppendLine($"✅ WaveManager 확인 ({wave.waves.Length} waves)");
            }

            // ── NavMesh ────────────────────────────────────────
            // 씬에 NavMeshAgent가 있으면 NavMesh 베이크 여부 간접 확인
            var agents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
            if (agents.Length > 0)
            {
                NavMeshHit hit;
                bool baked = NavMesh.SamplePosition(Vector3.zero, out hit, 1000f, NavMesh.AllAreas);
                if (!baked)
                    sb.AppendLine("⚠️ NavMesh 미베이크 → Window > AI > Navigation > Bake 탭에서 Bake 버튼을 누르세요.");
                else
                    ok.AppendLine($"✅ NavMesh 확인 (Agent {agents.Length}개)");
            }

            // ── Camera ─────────────────────────────────────────
            var cam = Camera.main;
            if (cam == null)
                sb.AppendLine("⚠️ MainCamera 없음 → VARCO / 자동 제작 / 현재 씬 자동 보정 실행 또는 카메라 태그 확인.");
            else
            {
                ok.AppendLine($"✅ 메인 카메라: {cam.name}");
                CheckCameraSetup(cam, player, sb, ok);
            }

            // ── HUD ────────────────────────────────────────────
            var hud = FindFirstObjectByType<VARCOGameHUD>();
            if (hud == null)
                sb.AppendLine("⚠️ VARCO 게임 HUD 없음 → VARCO / 자동 제작 / 현재 씬 자동 보정 실행.");
            else
                ok.AppendLine("✅ VARCO 게임 HUD 확인");

            // ── 결과 출력 ──────────────────────────────────────
            string result = sb.Length == 0
                ? "✅ 모든 항목 통과! Play 실행 가능합니다.\n\n" + ok
                : "── 점검 결과 ──\n" + sb + "\n── 정상 항목 ──\n" + ok;

            if (sb.Length > 0)
                Debug.LogWarning("[워크숍 검사]\n" + result, this);
            else
                Debug.Log("[워크숍 검사]\n" + result, this);
        }

        static void CheckGameProfile(GameProfile profile, StringBuilder sb, StringBuilder ok)
        {
            if (profile == null)
            {
                sb.AppendLine("⚠️ GameProfile 없음 → VARCO / 자동 제작 / 현재 씬 자동 보정 실행.");
                return;
            }

            ok.AppendLine($"✅ GameProfile 확인: {profile.genre} / {profile.clearCondition}");

            var hudTextCount = 0;
            if (HasDisplayText(profile.objectiveText)) hudTextCount++;
            if (HasDisplayText(profile.controlGuideText)) hudTextCount++;
            if (HasDisplayText(profile.clearMessage)) hudTextCount++;
            if (HasDisplayText(profile.gameOverMessage)) hudTextCount++;

            if (hudTextCount < 4)
                sb.AppendLine($"⚠️ HUD 한글 문구 부족 ({hudTextCount}/4) → VARCO / 게임 메이커에서 자동 제작 다시 실행.");
            else
                ok.AppendLine("✅ HUD 한글 문구 확인");
        }

        static bool HasDisplayText(string value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        static void CheckPlayerSetup(GameObject player, StringBuilder sb, StringBuilder ok)
        {
            var thirdPerson = player.GetComponent<PlayerController_ThirdPerson>();
            var platform = player.GetComponent<PlayerController_Platform>();
            var health = player.GetComponent<PlayerHealth>();
            var attack = player.GetComponent<PlayerAttack>();

            if (!thirdPerson && !platform)
                sb.AppendLine("❌ Player 컨트롤러 없음 → VARCO / 자동 제작 / 현재 씬 자동 보정 실행.");
            else
                ok.AppendLine(thirdPerson ? "✅ 3인칭 이동 컨트롤러 확인" : "✅ 플랫폼 이동 컨트롤러 확인");

            if (!health)
                sb.AppendLine("⚠️ PlayerHealth 없음 → VARCO / 자동 제작 / 현재 씬 자동 보정 실행.");
            else
                ok.AppendLine("✅ PlayerHealth 확인");

            if (attack && attack.keyboardAttackKey == KeyCode.Space)
                sb.AppendLine("⚠️ 공격 키가 Space라 점프와 충돌할 수 있음 → VARCO / 자동 제작 / 현재 씬 자동 보정 실행.");
            else if (attack)
                ok.AppendLine("✅ 공격 입력 충돌 없음");

            var itemGoalExists = false;
            foreach (var goal in FindObjectsByType<GoalTrigger>(FindObjectsSortMode.None))
            {
                if (goal && goal.requiredItems > 0)
                {
                    itemGoalExists = true;
                    break;
                }
            }

            if (itemGoalExists && !player.GetComponent<CollectibleCounter>())
                sb.AppendLine("⚠️ 수집 목표가 있지만 CollectibleCounter 없음 → VARCO / 자동 제작 / 현재 씬 자동 보정 실행.");
        }

        static void CheckCameraSetup(Camera cam, GameObject player, StringBuilder sb, StringBuilder ok)
        {
            var follow = cam.GetComponent<ThirdPersonCamera>();
            if (!follow)
            {
                sb.AppendLine("⚠️ ThirdPersonCamera 없음 → VARCO / 자동 제작 / 현재 씬 자동 보정 실행.");
                return;
            }

            if (player && follow.target && follow.target != player.transform)
                sb.AppendLine("⚠️ 카메라 Target이 Player와 다름 → VARCO / 자동 제작 / 현재 씬 자동 보정 실행.");
            else
                ok.AppendLine("✅ 카메라 추적 설정 확인");
        }

        static bool HasKoreanText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            foreach (var ch in value)
                if (ch >= '가' && ch <= '힣')
                    return true;
            return false;
        }
    }
}
