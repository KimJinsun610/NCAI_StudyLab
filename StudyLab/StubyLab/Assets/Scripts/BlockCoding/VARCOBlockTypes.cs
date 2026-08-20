using System.Collections.Generic;

namespace VARCO_Workshop
{
    // ==================================================================
    // 트리거 · 조건
    // ==================================================================
    public enum BlockTriggerType
    {
        OnPlayerTriggerEnter,
        OnPlayerTriggerExit,
        OnPlayerCollisionEnter,
        OnAnyTriggerEnter,
        OnKeyDown,
        OnKeyHold,
        OnMouseClick,
        OnTimerElapsed,
        OnCollectibleCountReached,
        OnHealthBelow,
        OnHealthAbove,
        OnPlayerDeath,
        OnEnemyDefeated,
        OnVariableReaches,
        OnGameClear,
        OnGameOver,
        OnStart,
        OnManualCall
    }

    public enum BlockConditionType
    {
        RequireTag,
        RequireCollectibleAtLeast,
        RequireGameState,
        RequirePlayerHPAtLeast,
        RequirePlayerHPBelow,
        RequireObjectActive,
        RequireRandomChance,
        RequireVariableAtLeast,
        RequireVariableEquals,
        RequireKillCountAtLeast,
        RequireTimerBelow,
        RequireDistanceBelow
    }

    // ==================================================================
    // 액션 카테고리 (2단 드롭다운의 1단)
    // ==================================================================
    public enum BlockActionCategory
    {
        이동_변형,
        물리,
        플레이어_체력,
        적_전투,
        아이템_점수,
        문_장치,
        사운드,
        시각효과,
        조명,
        카메라,
        UI_HUD,
        게임흐름,
        변수_로직,
        타이머_시간,
        오브젝트,
        애니메이션,
        플레이어_제어
    }

    // ==================================================================
    // 액션 (약 200종)
    // ==================================================================
    public enum BlockActionType
    {
        // --- 이동/변형 ---
        MoveBy, MoveTo, MoveToPoint, MoveForward, MoveUp, SmoothMoveToPoint,
        SetRotation, RotateBy, LookAtPoint, LookAtPlayer,
        SetScale, ScaleBy, PulseScale, SetParent, DetachParent,

        // --- 물리 ---
        AddForce, AddForceUp, AddForceForward, SetVelocity, StopMotion,
        AddTorque, SetGravityEnabled, SetKinematic, SetMass, SetDrag,
        ExplodeFromHere, PushTowardPoint,

        // --- 플레이어 체력 ---
        DamagePlayer, HealPlayer, FullHealPlayer, KillPlayer, SetPlayerMaxHP,
        DamagePlayerPercent, HealPlayerPercent, RespawnPlayerAtCheckpoint,
        SetPlayerRespawnHere, TeleportPlayerToPoint, BouncePlayer, PushPlayer,

        // --- 적/전투 ---
        DamageTarget, KillTarget, DamageAllEnemies, KillAllEnemies,
        SetEnemyDetectionRange, SetEnemyContactDamage, SetEnemyAttackSpeed,
        SetEnemyStopDistance, SpawnEnemyPrefab, RegisterKill,
        DisableEnemyAI, EnableEnemyAI, SetPlayerAttackDamage, SetPlayerAttackCooldown,

        // --- 아이템/점수 ---
        AddCollectible, RemoveCollectible, ResetCollectible, SpawnItemPrefab,
        DropPrefabAtPoint, AddScore, SetScore, ResetScore, AddKillScore, SpawnPrefabRandomNearby,

        // --- 문/장치 ---
        OpenDoor, CloseDoor, ToggleDoor, SetDoorOffset,
        SetPlatformSpeed, StopPlatform, StartPlatform,
        BreakPlatformNow, ResetPlatform, SaveCheckpointHere,
        EnableHazard, DisableHazard, SetHazardDamage,

        // --- 사운드 ---
        PlaySoundClip, PlaySoundEvent, PlayClipAtPoint, PlayBGM, StopBGM,
        PauseAudio, ResumeAudio, SetVolume, SetPitch, FadeInAudio, FadeOutAudio,
        MuteAudio, UnmuteAudio, PlayOneShotOnTarget, SetSpatialBlend,

        // --- 시각효과 ---
        ChangeColor, FlashColor, SetTransparency, FadeOutRenderer, FadeInRenderer,
        SetEmissionColor, PlayParticle, StopParticle, SpawnParticlePrefab, BlinkRenderer,
        SetMaterial, HideRenderer, ShowRenderer, EnableTrail, DisableTrail,
        SetCastShadows, ShakeObject, SetTextureOffset,

        // --- 조명 ---
        SetLightColor, SetLightIntensity, ToggleLight, LightOn, LightOff,
        FlickerLight, SetLightRange, FadeLightIntensity,

        // --- 카메라 ---
        ShakeCamera, SetCameraDistance, SetCameraTarget, SetCameraHeight,
        SetCameraSensitivity, ZoomCameraFOV, ResetCameraFOV, SetCameraBackground,
        SetCameraPitchLimits, SetCameraMinDistance,

        // --- UI/HUD ---
        ShowHudMessage, ShowHudMessageLong, ShowVariableInHud, ShowCollectibleCount,
        ShowPlayerHP, ShowTimerRemaining, ShowKillCount, ShowWarningMessage,
        ShowClearHint, ShowValueWithLabel,

        // --- 게임 흐름 ---
        TriggerGameClear, TriggerGameOver, RestartScene, LoadScene,
        PauseGame, ResumeGame, QuitGame, SetGameStatePlaying, SetGameStateReady,
        ClearAfterDelay,

        // --- 변수/로직 ---
        SetVariable, AddVariable, SubVariable, MultiplyVariable, DivideVariable,
        ResetVariable, RandomVariable, CopyVariable, ClampVariable, ToggleVariable,
        SetVarFromPlayerHP, SetVarFromCollectible, SetVarFromKillCount, LogVariable,

        // --- 타이머/시간 ---
        AddTimerSeconds, SubTimerSeconds, SetTimeScale, SlowMotion, NormalSpeed,
        Wait, WaitRandom, SetTimerTotal, ResetTimerToFull, PauseTimer,

        // --- 오브젝트 ---
        ActivateObject, DeactivateObject, ToggleObject, DestroyTarget,
        DestroyAfterDelay, DestroySelf, SpawnPrefab, SpawnPrefabAtPoint,
        CloneTarget, EnableCollider, DisableCollider, SetColliderTrigger,
        ActivateChildren, DeactivateChildren,

        // --- 애니메이션 ---
        SetAnimatorTrigger, SetAnimatorBool, SetAnimatorFloat, SetAnimatorInt,
        PlayAnimationState, SetAnimatorSpeed, ResetAnimatorTrigger,
        EnableAnimator, DisableAnimator, RebindAnimator,

        // --- 플레이어 제어 ---
        SetPlayerMoveSpeed, SetPlayerJumpForce, SetPlayerGravity, SetPlayerRunMultiplier,
        FreezePlayer, UnfreezePlayer, DisablePlayerAttack, EnablePlayerAttack,
        SetPlayerTurnSpeed, ForcePlayerJump
    }

    /// <summary>
    /// 액션 메타데이터 — 카테고리, 한글 이름, 설명, 인스펙터에 보여줄 필드 목록.
    /// 에디터가 이 표를 읽어 UI를 자동 생성하므로, 액션을 추가할 때
    /// 여기에 한 줄만 등록하면 인스펙터에도 자동 반영됩니다.
    /// 필드 표기법: "직렬화필드명|화면에 보일 이름"
    /// </summary>
    public static class BlockActionCatalog
    {
        public class Info
        {
            public BlockActionCategory Category;
            public string Label;
            public string Hint;
            public string[] Fields;
        }

        public static readonly Dictionary<BlockActionType, Info> Map = new Dictionary<BlockActionType, Info>();

        static void A(BlockActionType t, BlockActionCategory c, string label, string hint, params string[] fields)
        {
            Map[t] = new Info { Category = c, Label = label, Hint = hint, Fields = fields };
        }

        // 자주 쓰는 필드 표기 단축
        const string F_Obj = "targetObject|대상 오브젝트";
        const string F_Tr = "targetTransform|움직일 오브젝트";
        const string F_Pt = "spawnPoint|기준이 될 위치 오브젝트";
        const string F_Rb = "targetRigidbody|밀려날 오브젝트";
        const string F_Rend = "targetRenderer|모양이 바뀔 오브젝트";
        const string F_Light = "targetLight|조명 오브젝트";
        const string F_Audio = "targetAudio|소리가 나올 오브젝트";
        const string F_Anim = "targetAnimator|움직임(애니메이션) 오브젝트";
        const string F_Col = "targetCollider|충돌 범위 오브젝트";
        const string F_Part = "targetParticle|이펙트 오브젝트";
        const string F_Trail = "targetTrail|잔상 오브젝트";
        const string F_Door = "targetDoor|문 오브젝트";
        const string F_Enemy = "targetEnemy|적 오브젝트";
        const string F_Player = "targetPlayer|플레이어 오브젝트";
        const string F_Counter = "targetCounter|아이템 개수를 세는 오브젝트";
        const string F_Timer = "targetTimer|제한시간 오브젝트";
        const string F_Prefab = "prefabToSpawn|만들어 낼 오브젝트";
        const string F_Clip = "clip|소리 파일";
        const string F_Color = "colorValue|색상";
        const string F_Vec = "vectorValue|값 (좌우 / 위아래 / 앞뒤)";
        const string F_Bool = "boolValue|켜기 / 끄기";
        const string F_Mat = "materialValue|겉모습(재질) 파일";
        const string F_Var = "stringValue|변수 이름";
        const string F_Var2 = "stringValue2|대상 변수 이름";

        static string Num(string label) { return "floatValue|" + label; }
        static string Num2(string label) { return "floatValue2|" + label; }
        static string Int(string label) { return "intValue|" + label; }
        static string Str(string label) { return "stringValue|" + label; }

        static BlockActionCatalog()
        {
            const BlockActionCategory MOVE = BlockActionCategory.이동_변형;
            A(BlockActionType.MoveBy, MOVE, "상대 이동", "현재 위치에서 지정한 만큼 옮깁니다.", F_Tr, F_Vec);
            A(BlockActionType.MoveTo, MOVE, "절대 좌표로 이동", "지정한 좌표로 순간 이동합니다.", F_Tr, F_Vec);
            A(BlockActionType.MoveToPoint, MOVE, "기준 위치로 이동", "다른 오브젝트 위치로 옮깁니다.", F_Tr, F_Pt);
            A(BlockActionType.MoveForward, MOVE, "앞으로 이동", "바라보는 방향으로 이동합니다.", F_Tr, Num("거리"));
            A(BlockActionType.MoveUp, MOVE, "위로 이동", "Y축으로 올립니다. 음수면 내려갑니다.", F_Tr, Num("거리"));
            A(BlockActionType.SmoothMoveToPoint, MOVE, "부드럽게 이동", "지정 시간 동안 서서히 이동합니다.", F_Tr, F_Pt, Num("걸리는 시간(초)"));
            A(BlockActionType.SetRotation, MOVE, "회전 값 설정", "각도를 직접 지정합니다.", F_Tr, F_Vec);
            A(BlockActionType.RotateBy, MOVE, "상대 회전", "현재 각도에서 더 돌립니다.", F_Tr, F_Vec);
            A(BlockActionType.LookAtPoint, MOVE, "기준 위치 바라보기", "지정한 대상 쪽을 봅니다.", F_Tr, F_Pt);
            A(BlockActionType.LookAtPlayer, MOVE, "플레이어 바라보기", "플레이어 쪽으로 방향을 돌립니다.", F_Tr);
            A(BlockActionType.SetScale, MOVE, "크기 설정", "크기를 직접 지정합니다.", F_Tr, F_Vec);
            A(BlockActionType.ScaleBy, MOVE, "크기 배율", "현재 크기에 배수를 곱합니다.", F_Tr, Num("배율"));
            A(BlockActionType.PulseScale, MOVE, "크기 튕기기", "커졌다가 원래대로 돌아옵니다.", F_Tr, Num("배율"), Num2("시간(초)"));
            A(BlockActionType.SetParent, MOVE, "부모로 붙이기", "다른 오브젝트에 따라다니게 합니다.", F_Tr, F_Pt);
            A(BlockActionType.DetachParent, MOVE, "부모에서 떼기", "따라다니기를 해제합니다.", F_Tr);

            const BlockActionCategory PHY = BlockActionCategory.물리;
            A(BlockActionType.AddForce, PHY, "힘 가하기", "지정 방향으로 힘을 줍니다.", F_Rb, F_Vec);
            A(BlockActionType.AddForceUp, PHY, "위로 튕기기", "위쪽으로 힘을 줍니다.", F_Rb, Num("힘"));
            A(BlockActionType.AddForceForward, PHY, "앞으로 밀기", "앞 방향으로 힘을 줍니다.", F_Rb, Num("힘"));
            A(BlockActionType.SetVelocity, PHY, "속도 설정", "이동 속도를 직접 지정합니다.", F_Rb, F_Vec);
            A(BlockActionType.StopMotion, PHY, "즉시 멈추기", "속도를 0으로 만듭니다.", F_Rb);
            A(BlockActionType.AddTorque, PHY, "회전력 주기", "빙글빙글 돌립니다.", F_Rb, F_Vec);
            A(BlockActionType.SetGravityEnabled, PHY, "중력 켜기/끄기", "중력의 영향을 조절합니다.", F_Rb, F_Bool);
            A(BlockActionType.SetKinematic, PHY, "물리 고정", "켜면 물리 영향을 받지 않습니다.", F_Rb, F_Bool);
            A(BlockActionType.SetMass, PHY, "무게 설정", "무거울수록 잘 안 밀립니다.", F_Rb, Num("무게"));
            A(BlockActionType.SetDrag, PHY, "공기 저항", "클수록 빨리 멈춥니다.", F_Rb, Num("저항"));
            A(BlockActionType.ExplodeFromHere, PHY, "폭발로 밀어내기", "이 위치에서 바깥으로 튕겨냅니다.", F_Rb, Num("힘"), Num2("범위"));
            A(BlockActionType.PushTowardPoint, PHY, "지정 위치로 밀기", "대상 쪽으로 밀어냅니다.", F_Rb, F_Pt, Num("힘"));

            const BlockActionCategory PHP = BlockActionCategory.플레이어_체력;
            A(BlockActionType.DamagePlayer, PHP, "플레이어 피해", "체력을 깎습니다.", F_Player, Int("피해량"));
            A(BlockActionType.HealPlayer, PHP, "플레이어 회복", "체력을 채웁니다.", F_Player, Int("회복량"));
            A(BlockActionType.FullHealPlayer, PHP, "체력 모두 회복", "최대 체력으로 만듭니다.", F_Player);
            A(BlockActionType.KillPlayer, PHP, "플레이어 즉사", "체력을 0으로 만듭니다.", F_Player);
            A(BlockActionType.SetPlayerMaxHP, PHP, "최대 체력 변경", "최대 HP를 바꿉니다.", F_Player, Int("최대 HP"));
            A(BlockActionType.DamagePlayerPercent, PHP, "비율 피해", "최대 체력의 몇 %를 깎습니다.", F_Player, Num("퍼센트"));
            A(BlockActionType.HealPlayerPercent, PHP, "비율 회복", "최대 체력의 몇 %를 채웁니다.", F_Player, Num("퍼센트"));
            A(BlockActionType.RespawnPlayerAtCheckpoint, PHP, "체크포인트로 부활", "마지막 체크포인트로 되돌립니다.", F_Obj);
            A(BlockActionType.SetPlayerRespawnHere, PHP, "여기를 부활 지점으로", "이 위치를 리스폰 지점으로 저장합니다.", F_Obj);
            A(BlockActionType.TeleportPlayerToPoint, PHP, "플레이어 순간이동", "지정 위치로 옮깁니다.", F_Obj, F_Pt);
            A(BlockActionType.BouncePlayer, PHP, "플레이어 점프시키기", "위로 튕겨 올립니다.", F_Obj, Num("세기"));
            A(BlockActionType.PushPlayer, PHP, "플레이어 밀기", "지정 방향으로 밀어냅니다.", F_Obj, F_Vec);

            const BlockActionCategory ENE = BlockActionCategory.적_전투;
            A(BlockActionType.DamageTarget, ENE, "적 피해", "지정한 적의 체력을 깎습니다.", F_Enemy, Int("피해량"));
            A(BlockActionType.KillTarget, ENE, "적 즉사", "지정한 적을 쓰러뜨립니다.", F_Enemy);
            A(BlockActionType.DamageAllEnemies, ENE, "모든 적 피해", "화면 안의 모든 적에게 피해를 줍니다.", Int("피해량"));
            A(BlockActionType.KillAllEnemies, ENE, "모든 적 처치", "화면 안의 모든 적을 쓰러뜨립니다.");
            A(BlockActionType.SetEnemyDetectionRange, ENE, "적 감지 범위", "플레이어를 알아채는 거리입니다.", F_Obj, Num("범위"));
            A(BlockActionType.SetEnemyContactDamage, ENE, "적 접촉 피해", "닿았을 때 주는 피해입니다.", F_Obj, Int("피해량"));
            A(BlockActionType.SetEnemyAttackSpeed, ENE, "적 공격 속도", "클수록 빨리 공격합니다.", F_Obj, Num("속도"));
            A(BlockActionType.SetEnemyStopDistance, ENE, "적 정지 거리", "이 거리까지만 다가옵니다.", F_Obj, Num("거리"));
            A(BlockActionType.SpawnEnemyPrefab, ENE, "적 만들기", "적을 만들어 냅니다.", F_Prefab, F_Pt);
            A(BlockActionType.RegisterKill, ENE, "처치 수 올리기", "킬 카운트를 1 증가시킵니다.");
            A(BlockActionType.DisableEnemyAI, ENE, "적 멈추기", "추격 AI를 끕니다.", F_Obj);
            A(BlockActionType.EnableEnemyAI, ENE, "적 다시 움직이기", "추격 AI를 켭니다.", F_Obj);
            A(BlockActionType.SetPlayerAttackDamage, ENE, "플레이어 공격력", "공격 피해량을 바꿉니다.", F_Obj, Int("공격력"));
            A(BlockActionType.SetPlayerAttackCooldown, ENE, "플레이어 공격 간격", "작을수록 빨리 때립니다.", F_Obj, Num("간격(초)"));

            const BlockActionCategory ITM = BlockActionCategory.아이템_점수;
            A(BlockActionType.AddCollectible, ITM, "아이템 개수 늘리기", "모은 아이템 수를 늘립니다.", F_Counter, Int("증가량"));
            A(BlockActionType.RemoveCollectible, ITM, "아이템 개수 줄이기", "모은 아이템 수를 내립니다.", F_Counter, Int("감소량"));
            A(BlockActionType.ResetCollectible, ITM, "아이템 개수 0으로", "0으로 되돌립니다.", F_Counter);
            A(BlockActionType.SpawnItemPrefab, ITM, "아이템 만들기", "아이템을 만들어 냅니다.", F_Prefab, F_Pt);
            A(BlockActionType.DropPrefabAtPoint, ITM, "지정 위치에 떨구기", "정해진 위치에 오브젝트를 놓습니다.", F_Prefab, F_Pt);
            A(BlockActionType.AddScore, ITM, "점수 올리기", "score 변수를 증가시킵니다.", Num("점수"));
            A(BlockActionType.SetScore, ITM, "점수 설정", "score 변수를 지정 값으로 만듭니다.", Num("점수"));
            A(BlockActionType.ResetScore, ITM, "점수 초기화", "score 를 0으로 만듭니다.");
            A(BlockActionType.AddKillScore, ITM, "처치 점수", "처치 수를 올리고 점수도 함께 올립니다.", Num("점수"));
            A(BlockActionType.SpawnPrefabRandomNearby, ITM, "주변 랜덤 생성", "근처 임의 위치에 만들어 냅니다.", F_Prefab, Num("반경"));

            const BlockActionCategory DOO = BlockActionCategory.문_장치;
            A(BlockActionType.OpenDoor, DOO, "문 열기", "문을 엽니다.", F_Door);
            A(BlockActionType.CloseDoor, DOO, "문 닫기", "문을 닫습니다.", F_Door);
            A(BlockActionType.ToggleDoor, DOO, "문 열고닫기 전환", "열려 있으면 닫고, 닫혀 있으면 엽니다.", F_Door);
            A(BlockActionType.SetDoorOffset, DOO, "문 열림 방향/거리", "문이 열릴 때 움직일 양입니다.", F_Door, F_Vec);
            A(BlockActionType.SetPlatformSpeed, DOO, "발판 속도", "움직이는 발판의 속도입니다.", F_Obj, Num("속도"));
            A(BlockActionType.StopPlatform, DOO, "발판 멈추기", "발판을 정지시킵니다.", F_Obj);
            A(BlockActionType.StartPlatform, DOO, "발판 움직이기", "발판을 다시 움직입니다.", F_Obj, Num("속도"));
            A(BlockActionType.BreakPlatformNow, DOO, "발판 부수기", "부서지는 발판을 즉시 무너뜨립니다.", F_Obj);
            A(BlockActionType.ResetPlatform, DOO, "발판 복구", "부서진 발판을 되돌립니다.", F_Obj);
            A(BlockActionType.SaveCheckpointHere, DOO, "체크포인트 저장", "이 위치를 체크포인트로 기록합니다.", F_Obj);
            A(BlockActionType.EnableHazard, DOO, "위험 구역 켜기", "함정을 활성화합니다.", F_Obj);
            A(BlockActionType.DisableHazard, DOO, "위험 구역 끄기", "함정을 비활성화합니다.", F_Obj);
            A(BlockActionType.SetHazardDamage, DOO, "위험 구역 피해량", "초당 피해량을 바꿉니다.", F_Obj, Int("초당 피해"));

            const BlockActionCategory SND = BlockActionCategory.사운드;
            A(BlockActionType.PlaySoundClip, SND, "효과음 재생", "지정한 오디오 클립을 재생합니다.", F_Clip);
            A(BlockActionType.PlaySoundEvent, SND, "사운드 이벤트 재생", "미리 등록해 둔 소리 이름을 불러 재생합니다.", Str("소리 이름"));
            A(BlockActionType.PlayClipAtPoint, SND, "지정 위치에서 재생", "특정 위치에서 소리가 나게 합니다.", F_Clip, F_Pt);
            A(BlockActionType.PlayBGM, SND, "배경음 재생", "반복 재생으로 배경음을 켭니다.", F_Audio, F_Clip);
            A(BlockActionType.StopBGM, SND, "배경음 정지", "재생을 멈춥니다.", F_Audio);
            A(BlockActionType.PauseAudio, SND, "소리 일시정지", "일시적으로 멈춥니다.", F_Audio);
            A(BlockActionType.ResumeAudio, SND, "소리 다시 재생", "멈춘 지점부터 이어 재생합니다.", F_Audio);
            A(BlockActionType.SetVolume, SND, "볼륨 조절", "0 부터 1 사이 값입니다.", F_Audio, Num("볼륨"));
            A(BlockActionType.SetPitch, SND, "음 높이 조절", "1이 기본입니다. 높으면 빨라집니다.", F_Audio, Num("피치"));
            A(BlockActionType.FadeInAudio, SND, "서서히 켜기", "볼륨을 서서히 올립니다.", F_Audio, Num("시간(초)"));
            A(BlockActionType.FadeOutAudio, SND, "서서히 끄기", "볼륨을 서서히 내립니다.", F_Audio, Num("시간(초)"));
            A(BlockActionType.MuteAudio, SND, "음소거", "소리를 끕니다.", F_Audio);
            A(BlockActionType.UnmuteAudio, SND, "음소거 해제", "소리를 켭니다.", F_Audio);
            A(BlockActionType.PlayOneShotOnTarget, SND, "대상에서 한 번 재생", "지정 스피커로 한 번 재생합니다.", F_Audio, F_Clip);
            A(BlockActionType.SetSpatialBlend, SND, "거리감 있는 소리로", "0이면 어디서나 같게, 1이면 멀수록 작게 들립니다.", F_Audio, Num("0~1"));

            const BlockActionCategory VFX = BlockActionCategory.시각효과;
            A(BlockActionType.ChangeColor, VFX, "색 바꾸기", "오브젝트 색을 바꿉니다.", F_Rend, F_Color);
            A(BlockActionType.FlashColor, VFX, "색 번쩍이기", "잠깐 다른 색이 됐다가 돌아옵니다.", F_Rend, F_Color, Num("시간(초)"));
            A(BlockActionType.SetTransparency, VFX, "투명도 설정", "0이면 완전 투명입니다.", F_Rend, Num("0~1"));
            A(BlockActionType.FadeOutRenderer, VFX, "서서히 사라지기", "점점 투명해집니다.", F_Rend, Num("시간(초)"));
            A(BlockActionType.FadeInRenderer, VFX, "서서히 나타나기", "점점 진해집니다.", F_Rend, Num("시간(초)"));
            A(BlockActionType.SetEmissionColor, VFX, "발광색 설정", "빛나는 느낌을 줍니다.", F_Rend, F_Color);
            A(BlockActionType.PlayParticle, VFX, "파티클 재생", "이펙트를 터뜨립니다.", F_Part);
            A(BlockActionType.StopParticle, VFX, "파티클 정지", "이펙트를 멈춥니다.", F_Part);
            A(BlockActionType.SpawnParticlePrefab, VFX, "이펙트 만들기", "이펙트를 만들어 냅니다.", F_Prefab, F_Pt);
            A(BlockActionType.BlinkRenderer, VFX, "깜빡이기", "여러 번 껐다 켭니다.", F_Rend, Int("횟수"), Num("간격(초)"));
            A(BlockActionType.SetMaterial, VFX, "겉모습 바꾸기", "오브젝트의 겉모습을 통째로 바꿉니다.", F_Rend, F_Mat);
            A(BlockActionType.HideRenderer, VFX, "모습 숨기기", "보이지 않게 합니다. 충돌은 남습니다.", F_Rend);
            A(BlockActionType.ShowRenderer, VFX, "모습 보이기", "다시 보이게 합니다.", F_Rend);
            A(BlockActionType.EnableTrail, VFX, "잔상 켜기", "움직일 때 꼬리를 남깁니다.", F_Trail);
            A(BlockActionType.DisableTrail, VFX, "잔상 끄기", "꼬리를 없앱니다.", F_Trail);
            A(BlockActionType.SetCastShadows, VFX, "그림자 켜기/끄기", "그림자 생성 여부입니다.", F_Rend, F_Bool);
            A(BlockActionType.ShakeObject, VFX, "오브젝트 흔들기", "제자리에서 진동합니다.", F_Tr, Num("세기"), Num2("시간(초)"));
            A(BlockActionType.SetTextureOffset, VFX, "텍스처 이동", "무늬를 밀어 흐르는 효과를 냅니다.", F_Rend, F_Vec);

            const BlockActionCategory LIT = BlockActionCategory.조명;
            A(BlockActionType.SetLightColor, LIT, "빛 색 바꾸기", "조명 색을 바꿉니다.", F_Light, F_Color);
            A(BlockActionType.SetLightIntensity, LIT, "빛 세기", "밝기를 조절합니다.", F_Light, Num("세기"));
            A(BlockActionType.ToggleLight, LIT, "불 켜고끄기 전환", "켜져 있으면 끕니다.", F_Light);
            A(BlockActionType.LightOn, LIT, "불 켜기", "조명을 켭니다.", F_Light);
            A(BlockActionType.LightOff, LIT, "불 끄기", "조명을 끕니다.", F_Light);
            A(BlockActionType.FlickerLight, LIT, "불 깜빡이기", "공포 연출에 좋습니다.", F_Light, Int("횟수"), Num("간격(초)"));
            A(BlockActionType.SetLightRange, LIT, "빛 범위", "빛이 닿는 거리입니다.", F_Light, Num("범위"));
            A(BlockActionType.FadeLightIntensity, LIT, "밝기 서서히 변경", "지정 시간 동안 밝기를 바꿉니다.", F_Light, Num("목표 세기"), Num2("시간(초)"));

            const BlockActionCategory CAM = BlockActionCategory.카메라;
            A(BlockActionType.ShakeCamera, CAM, "카메라 흔들기", "타격감을 줄 때 씁니다.", Num("세기"), Num2("시간(초)"));
            A(BlockActionType.SetCameraDistance, CAM, "카메라 거리", "플레이어와의 거리입니다.", Num("거리"));
            A(BlockActionType.SetCameraTarget, CAM, "카메라가 볼 대상", "따라다닐 대상을 바꿉니다.", F_Pt);
            A(BlockActionType.SetCameraHeight, CAM, "카메라 높이", "시점 높이를 조절합니다.", Num("높이"));
            A(BlockActionType.SetCameraSensitivity, CAM, "카메라 감도", "마우스 회전 감도입니다.", Num("감도"));
            A(BlockActionType.ZoomCameraFOV, CAM, "확대/축소", "작을수록 확대됩니다. 기본 60.", Num("시야각"));
            A(BlockActionType.ResetCameraFOV, CAM, "시야각 원상복구", "기본 60으로 되돌립니다.");
            A(BlockActionType.SetCameraBackground, CAM, "배경색 바꾸기", "하늘/배경 색입니다.", F_Color);
            A(BlockActionType.SetCameraPitchLimits, CAM, "상하 회전 제한", "위아래로 볼 수 있는 각도입니다.", Num("최소"), Num2("최대"));
            A(BlockActionType.SetCameraMinDistance, CAM, "최소 거리", "벽에 붙었을 때 최소 거리입니다.", Num("거리"));

            const BlockActionCategory UI = BlockActionCategory.UI_HUD;
            A(BlockActionType.ShowHudMessage, UI, "메시지 표시", "화면에 안내 문구를 띄웁니다.", Str("메시지"));
            A(BlockActionType.ShowHudMessageLong, UI, "메시지 길게 표시", "표시 시간을 지정합니다.", Str("메시지"), Num("표시 시간(초)"));
            A(BlockActionType.ShowVariableInHud, UI, "변수 값 보여주기", "변수 현재 값을 화면에 띄웁니다.", F_Var);
            A(BlockActionType.ShowCollectibleCount, UI, "모은 개수 보여주기", "지금까지 모은 개수를 화면에 띄웁니다.", F_Counter);
            A(BlockActionType.ShowPlayerHP, UI, "체력 보여주기", "현재 체력을 띄웁니다.", F_Player);
            A(BlockActionType.ShowTimerRemaining, UI, "남은 시간 보여주기", "타이머 잔여 시간을 띄웁니다.", F_Timer);
            A(BlockActionType.ShowKillCount, UI, "처치 수 보여주기", "지금까지 처치한 수를 띄웁니다.");
            A(BlockActionType.ShowWarningMessage, UI, "경고 표시", "경고 말머리를 붙여 띄웁니다.", Str("메시지"));
            A(BlockActionType.ShowClearHint, UI, "목표 안내", "무엇을 해야 하는지 안내합니다.", Str("목표"));
            A(BlockActionType.ShowValueWithLabel, UI, "이름과 값 함께 표시", "예) 점수: 12", Str("앞에 붙일 말"), F_Var2);

            const BlockActionCategory FLW = BlockActionCategory.게임흐름;
            A(BlockActionType.TriggerGameClear, FLW, "게임 클리어", "클리어 처리합니다.");
            A(BlockActionType.TriggerGameOver, FLW, "게임 오버", "게임 오버 처리합니다.");
            A(BlockActionType.RestartScene, FLW, "이 스테이지 처음부터 다시", "지금 스테이지를 처음부터 다시 시작합니다.");
            A(BlockActionType.LoadScene, FLW, "다른 스테이지로 이동", "미리 등록해 둔 다른 스테이지로 넘어갑니다.", Str("스테이지 이름"));
            A(BlockActionType.PauseGame, FLW, "일시정지", "시간을 멈춥니다.");
            A(BlockActionType.ResumeGame, FLW, "일시정지 해제", "시간을 다시 흐르게 합니다.");
            A(BlockActionType.QuitGame, FLW, "게임 종료", "빌드된 게임을 종료합니다. 에디터에서는 동작하지 않습니다.");
            A(BlockActionType.SetGameStatePlaying, FLW, "플레이 상태로", "게임 상태를 Playing 으로 바꿉니다.");
            A(BlockActionType.SetGameStateReady, FLW, "대기 상태로", "게임 상태를 Ready 로 바꿉니다.");
            A(BlockActionType.ClearAfterDelay, FLW, "잠시 후 클리어", "지정 시간 뒤 클리어 처리합니다.", Num("지연(초)"));

            const BlockActionCategory VAR = BlockActionCategory.변수_로직;
            A(BlockActionType.SetVariable, VAR, "변수 설정", "변수를 지정 값으로 만듭니다.", F_Var, Num("값"));
            A(BlockActionType.AddVariable, VAR, "변수 더하기", "변수 값을 증가시킵니다.", F_Var, Num("더할 값"));
            A(BlockActionType.SubVariable, VAR, "변수 빼기", "변수 값을 감소시킵니다.", F_Var, Num("뺄 값"));
            A(BlockActionType.MultiplyVariable, VAR, "변수 곱하기", "변수 값에 곱합니다.", F_Var, Num("곱할 값"));
            A(BlockActionType.DivideVariable, VAR, "변수 나누기", "변수 값을 나눕니다. 0으로는 나누지 않습니다.", F_Var, Num("나눌 값"));
            A(BlockActionType.ResetVariable, VAR, "변수 초기화", "0으로 되돌립니다.", F_Var);
            A(BlockActionType.RandomVariable, VAR, "변수에 랜덤 값", "최소~최대 사이 값을 넣습니다.", F_Var, Num("최소"), Num2("최대"));
            A(BlockActionType.CopyVariable, VAR, "변수 복사", "한 변수 값을 다른 변수에 넣습니다.", F_Var, F_Var2);
            A(BlockActionType.ClampVariable, VAR, "변수 범위 제한", "최소~최대 범위를 벗어나지 않게 합니다.", F_Var, Num("최소"), Num2("최대"));
            A(BlockActionType.ToggleVariable, VAR, "변수 0↔1 전환", "스위치처럼 씁니다.", F_Var);
            A(BlockActionType.SetVarFromPlayerHP, VAR, "체력을 변수에 저장", "현재 HP를 변수에 넣습니다.", F_Var, F_Player);
            A(BlockActionType.SetVarFromCollectible, VAR, "수집 수를 변수에 저장", "현재 개수를 변수에 넣습니다.", F_Var, F_Counter);
            A(BlockActionType.SetVarFromKillCount, VAR, "처치 수를 변수에 저장", "킬 카운트를 변수에 넣습니다.", F_Var);
            A(BlockActionType.LogVariable, VAR, "변수 값 콘솔 출력", "디버그용으로 값을 찍어봅니다.", F_Var);

            const BlockActionCategory TIM = BlockActionCategory.타이머_시간;
            A(BlockActionType.AddTimerSeconds, TIM, "제한시간 늘리기", "남은 시간을 더합니다.", F_Timer, Num("초"));
            A(BlockActionType.SubTimerSeconds, TIM, "제한시간 줄이기", "남은 시간을 뺍니다.", F_Timer, Num("초"));
            A(BlockActionType.SetTimeScale, TIM, "게임 속도 조절", "1이 기본입니다.", Num("배속"));
            A(BlockActionType.SlowMotion, TIM, "슬로우 모션", "잠깐 느려졌다 돌아옵니다.", Num("배속"), Num2("시간(초)"));
            A(BlockActionType.NormalSpeed, TIM, "속도 원상복구", "배속을 1로 되돌립니다.");
            A(BlockActionType.Wait, TIM, "잠시 기다리기", "다음 액션까지 대기합니다.", Num("초"));
            A(BlockActionType.WaitRandom, TIM, "랜덤 시간 기다리기", "최소~최대 사이만큼 대기합니다.", Num("최소 초"), Num2("최대 초"));
            A(BlockActionType.SetTimerTotal, TIM, "제한시간 전체 설정", "타이머 총 시간을 바꿉니다.", F_Timer, Num("초"));
            A(BlockActionType.ResetTimerToFull, TIM, "제한시간 가득 채우기", "남은 시간을 최대로 만듭니다.", F_Timer);
            A(BlockActionType.PauseTimer, TIM, "타이머 멈추기/재개", "카운트다운을 멈추거나 다시 켭니다.", F_Timer, F_Bool);

            const BlockActionCategory OBJ = BlockActionCategory.오브젝트;
            A(BlockActionType.ActivateObject, OBJ, "오브젝트 켜기", "비활성 오브젝트를 켭니다.", F_Obj);
            A(BlockActionType.DeactivateObject, OBJ, "오브젝트 끄기", "오브젝트를 숨깁니다.", F_Obj);
            A(BlockActionType.ToggleObject, OBJ, "오브젝트 켜고끄기 전환", "상태를 뒤집습니다.", F_Obj);
            A(BlockActionType.DestroyTarget, OBJ, "오브젝트 삭제", "완전히 없앱니다.", F_Obj);
            A(BlockActionType.DestroyAfterDelay, OBJ, "잠시 후 삭제", "지정 시간 뒤에 없앱니다.", F_Obj, Num("지연(초)"));
            A(BlockActionType.DestroySelf, OBJ, "자기 자신 삭제", "이 규칙이 붙은 오브젝트를 없앱니다.");
            A(BlockActionType.SpawnPrefab, OBJ, "오브젝트 만들기", "이 위치에 만들어 냅니다.", F_Prefab);
            A(BlockActionType.SpawnPrefabAtPoint, OBJ, "지정 위치에 생성", "기준 위치에 만들어 냅니다.", F_Prefab, F_Pt);
            A(BlockActionType.CloneTarget, OBJ, "오브젝트 복제", "똑같은 것을 하나 더 만듭니다.", F_Obj);
            A(BlockActionType.EnableCollider, OBJ, "충돌 켜기", "부딪힐 수 있게 합니다.", F_Col);
            A(BlockActionType.DisableCollider, OBJ, "충돌 끄기", "통과할 수 있게 합니다.", F_Col);
            A(BlockActionType.SetColliderTrigger, OBJ, "통과할 수 있게 전환", "켜면 밀리지 않고 통과합니다.", F_Col, F_Bool);
            A(BlockActionType.ActivateChildren, OBJ, "자식 모두 켜기", "하위 오브젝트를 전부 켭니다.", F_Obj);
            A(BlockActionType.DeactivateChildren, OBJ, "자식 모두 끄기", "하위 오브젝트를 전부 끕니다.", F_Obj);

            const BlockActionCategory ANI = BlockActionCategory.애니메이션;
            A(BlockActionType.SetAnimatorTrigger, ANI, "애니메이션 동작 실행", "정해둔 동작(예: 공격, 점프)을 실행합니다.", F_Anim, Str("동작 이름"));
            A(BlockActionType.SetAnimatorBool, ANI, "애니메이션 켜기/끄기 값", "켜짐/꺼짐으로 동작을 전환합니다.", F_Anim, Str("값 이름"), F_Bool);
            A(BlockActionType.SetAnimatorFloat, ANI, "애니메이션 숫자 값", "속도처럼 숫자로 조절되는 값을 바꿉니다.", F_Anim, Str("값 이름"), Num("값"));
            A(BlockActionType.SetAnimatorInt, ANI, "애니메이션 단계 값", "1단계, 2단계처럼 정수로 구분되는 값을 바꿉니다.", F_Anim, Str("값 이름"), Int("값"));
            A(BlockActionType.PlayAnimationState, ANI, "특정 동작 바로 재생", "상태 이름으로 즉시 재생합니다.", F_Anim, Str("동작 이름"));
            A(BlockActionType.SetAnimatorSpeed, ANI, "애니메이션 속도", "1이 기본 속도입니다.", F_Anim, Num("속도"));
            A(BlockActionType.ResetAnimatorTrigger, ANI, "예약된 동작 취소", "실행을 기다리던 동작을 취소합니다.", F_Anim, Str("동작 이름"));
            A(BlockActionType.EnableAnimator, ANI, "애니메이션 켜기", "애니메이션을 재개합니다.", F_Anim);
            A(BlockActionType.DisableAnimator, ANI, "애니메이션 끄기", "그 자세로 멈춥니다.", F_Anim);
            A(BlockActionType.RebindAnimator, ANI, "애니메이션 처음으로", "처음 상태로 되돌립니다.", F_Anim);

            const BlockActionCategory PC = BlockActionCategory.플레이어_제어;
            A(BlockActionType.SetPlayerMoveSpeed, PC, "이동 속도 변경", "플레이어가 걷는 속도입니다.", F_Obj, Num("속도"));
            A(BlockActionType.SetPlayerJumpForce, PC, "점프력 변경", "플랫폼 플레이어의 점프 세기입니다.", F_Obj, Num("점프력"));
            A(BlockActionType.SetPlayerGravity, PC, "중력 변경", "음수일수록 빨리 떨어집니다.", F_Obj, Num("중력"));
            A(BlockActionType.SetPlayerRunMultiplier, PC, "달리기 배율", "달릴 때 몇 배 빨라지는지입니다.", F_Obj, Num("배율"));
            A(BlockActionType.FreezePlayer, PC, "조작 잠그기", "움직일 수 없게 합니다.", F_Obj);
            A(BlockActionType.UnfreezePlayer, PC, "조작 잠금 해제", "다시 움직일 수 있게 합니다.", F_Obj);
            A(BlockActionType.DisablePlayerAttack, PC, "공격 막기", "공격을 못 하게 합니다.", F_Obj);
            A(BlockActionType.EnablePlayerAttack, PC, "공격 허용", "다시 공격할 수 있게 합니다.", F_Obj);
            A(BlockActionType.SetPlayerTurnSpeed, PC, "회전 속도", "방향 전환이 얼마나 부드러운지입니다.", F_Obj, Num("속도"));
            A(BlockActionType.ForcePlayerJump, PC, "강제 점프", "지금 즉시 뛰어오르게 합니다.", F_Obj, Num("세기"));
        }

        public static Info Get(BlockActionType t)
        {
            return Map.TryGetValue(t, out var i)
                ? i
                : new Info { Category = BlockActionCategory.오브젝트, Label = t.ToString(), Hint = "", Fields = new string[0] };
        }

        static Dictionary<BlockActionCategory, List<BlockActionType>> byCategory;

        public static List<BlockActionType> InCategory(BlockActionCategory c)
        {
            if (byCategory == null)
            {
                byCategory = new Dictionary<BlockActionCategory, List<BlockActionType>>();
                foreach (var kv in Map)
                {
                    if (!byCategory.TryGetValue(kv.Value.Category, out var list))
                    {
                        list = new List<BlockActionType>();
                        byCategory[kv.Value.Category] = list;
                    }
                    list.Add(kv.Key);
                }
            }
            return byCategory.TryGetValue(c, out var r) ? r : new List<BlockActionType>();
        }
    }

    /// <summary>
    /// 화면에 보여줄 우리말 이름표. Unity를 처음 배우는 학생이 영어 용어 없이
    /// 고를 수 있도록, 트리거·조건·카테고리·게임상태를 전부 우리말 문장으로 보여줍니다.
    /// </summary>
    public static class BlockLabels
    {
        public static readonly Dictionary<BlockTriggerType, string> Trigger = new Dictionary<BlockTriggerType, string>
        {
            { BlockTriggerType.OnPlayerTriggerEnter,      "플레이어가 닿았을 때" },
            { BlockTriggerType.OnPlayerTriggerExit,       "플레이어가 벗어났을 때" },
            { BlockTriggerType.OnPlayerCollisionEnter,    "플레이어와 부딪혔을 때" },
            { BlockTriggerType.OnAnyTriggerEnter,         "무엇이든 닿았을 때" },
            { BlockTriggerType.OnKeyDown,                 "키를 눌렀을 때" },
            { BlockTriggerType.OnKeyHold,                 "키를 누르고 있는 동안" },
            { BlockTriggerType.OnMouseClick,              "마우스로 클릭했을 때" },
            { BlockTriggerType.OnTimerElapsed,            "몇 초마다 반복해서" },
            { BlockTriggerType.OnCollectibleCountReached, "아이템을 정해진 개수만큼 모았을 때" },
            { BlockTriggerType.OnHealthBelow,             "체력이 정해진 값 아래로 떨어졌을 때" },
            { BlockTriggerType.OnHealthAbove,             "체력이 정해진 값 위로 올라갔을 때" },
            { BlockTriggerType.OnPlayerDeath,             "플레이어가 쓰러졌을 때" },
            { BlockTriggerType.OnEnemyDefeated,           "적을 쓰러뜨렸을 때" },
            { BlockTriggerType.OnVariableReaches,         "점수가 목표에 닿았을 때" },
            { BlockTriggerType.OnGameClear,               "게임을 클리어했을 때" },
            { BlockTriggerType.OnGameOver,                "게임 오버가 됐을 때" },
            { BlockTriggerType.OnStart,                   "게임이 시작될 때" },
            { BlockTriggerType.OnManualCall,              "다른 규칙이 불러줄 때" },
        };

        public static readonly Dictionary<BlockTriggerType, string> TriggerHint = new Dictionary<BlockTriggerType, string>
        {
            { BlockTriggerType.OnPlayerTriggerEnter,      "이 오브젝트 안으로 플레이어가 들어오면 실행됩니다. 통과할 수 있는 범위여야 합니다." },
            { BlockTriggerType.OnPlayerTriggerExit,       "이 오브젝트 범위에서 플레이어가 빠져나가면 실행됩니다." },
            { BlockTriggerType.OnPlayerCollisionEnter,    "플레이어와 실제로 부딪혀 막힐 때 실행됩니다." },
            { BlockTriggerType.OnAnyTriggerEnter,         "플레이어가 아니어도, 상자 같은 것이 들어오면 실행됩니다." },
            { BlockTriggerType.OnKeyHold,                 "누르고 있는 동안 계속 실행되므로 '재실행 간격'을 함께 정하세요." },
            { BlockTriggerType.OnMouseClick,              "클릭을 받으려면 이 오브젝트에 충돌 범위가 있어야 합니다." },
            { BlockTriggerType.OnManualCall,              "스스로는 실행되지 않습니다. 다른 곳에서 규칙 이름을 불러야 합니다." },
        };

        public static readonly Dictionary<BlockConditionType, string> Condition = new Dictionary<BlockConditionType, string>
        {
            { BlockConditionType.RequireTag,               "닿은 대상이 특정 이름표일 때만" },
            { BlockConditionType.RequireCollectibleAtLeast,"아이템을 몇 개 이상 모았을 때만" },
            { BlockConditionType.RequireGameState,         "게임이 특정 상태일 때만" },
            { BlockConditionType.RequirePlayerHPAtLeast,   "체력이 몇 이상일 때만" },
            { BlockConditionType.RequirePlayerHPBelow,     "체력이 몇 미만일 때만" },
            { BlockConditionType.RequireObjectActive,      "어떤 오브젝트가 켜져 있을 때만" },
            { BlockConditionType.RequireRandomChance,      "정해진 확률로만" },
            { BlockConditionType.RequireVariableAtLeast,   "점수가 몇 이상일 때만" },
            { BlockConditionType.RequireVariableEquals,    "점수가 정확히 몇일 때만" },
            { BlockConditionType.RequireKillCountAtLeast,  "적을 몇 마리 이상 잡았을 때만" },
            { BlockConditionType.RequireTimerBelow,        "남은 시간이 얼마 이하일 때만" },
            { BlockConditionType.RequireDistanceBelow,     "정해진 위치에 가까이 있을 때만" },
        };

        public static readonly Dictionary<BlockActionCategory, string> Category = new Dictionary<BlockActionCategory, string>
        {
            { BlockActionCategory.이동_변형,    "움직이기 · 회전 · 크기" },
            { BlockActionCategory.물리,         "밀기 · 튕기기" },
            { BlockActionCategory.플레이어_체력,"플레이어 체력" },
            { BlockActionCategory.적_전투,      "적 · 전투" },
            { BlockActionCategory.아이템_점수,  "아이템 · 점수" },
            { BlockActionCategory.문_장치,      "문 · 발판 · 장치" },
            { BlockActionCategory.사운드,       "소리" },
            { BlockActionCategory.시각효과,     "보이는 효과" },
            { BlockActionCategory.조명,         "조명(빛)" },
            { BlockActionCategory.카메라,       "카메라" },
            { BlockActionCategory.UI_HUD,       "화면 글자 안내" },
            { BlockActionCategory.게임흐름,     "게임 시작 · 끝" },
            { BlockActionCategory.변수_로직,    "점수 저장하기" },
            { BlockActionCategory.타이머_시간,  "시간 · 제한시간" },
            { BlockActionCategory.오브젝트,     "오브젝트 켜기 · 끄기 · 만들기" },
            { BlockActionCategory.애니메이션,   "애니메이션" },
            { BlockActionCategory.플레이어_제어,"플레이어 조작" },
        };

        public static readonly Dictionary<GameState, string> State = new Dictionary<GameState, string>
        {
            { GameState.Ready,    "시작 전" },
            { GameState.Playing,  "플레이 중" },
            { GameState.Clear,    "클리어함" },
            { GameState.GameOver, "게임 오버" },
        };

        public static string Of(BlockTriggerType t) { return Trigger.TryGetValue(t, out var v) ? v : t.ToString(); }
        public static string HintOf(BlockTriggerType t) { return TriggerHint.TryGetValue(t, out var v) ? v : ""; }
        public static string Of(BlockConditionType t) { return Condition.TryGetValue(t, out var v) ? v : t.ToString(); }
        public static string Of(BlockActionCategory t) { return Category.TryGetValue(t, out var v) ? v : t.ToString(); }
        public static string Of(GameState t) { return State.TryGetValue(t, out var v) ? v : t.ToString(); }
    }
}
