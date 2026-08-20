#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace VARCO_Workshop.Editor
{
    public enum BlockGenre { 공통, 플랫폼, 아레나, 탐험, 퍼즐 }

    /// <summary>
    /// 장르별 시작 템플릿 모음. 규칙의 뼈대(트리거·조건·액션 구성)만 만들어 주고,
    /// 씬에 있는 실제 대상(문, 카운터, 클립 등)은 추가 후 인스펙터에서 연결합니다.
    /// </summary>
    public static class VARCOBlockTemplates
    {
        public class Def
        {
            public string Name;
            public BlockGenre Genre;
            public string Desc;
            public Action<BlockRule> Build;
        }

        static BlockActionEntry A(BlockActionType t) { return new BlockActionEntry { type = t }; }
        static BlockActionEntry A(BlockActionType t, string s) { return new BlockActionEntry { type = t, stringValue = s }; }
        static BlockActionEntry A(BlockActionType t, float f) { return new BlockActionEntry { type = t, floatValue = f }; }
        static BlockActionEntry A(BlockActionType t, int i) { return new BlockActionEntry { type = t, intValue = i }; }
        static BlockActionEntry A(BlockActionType t, string s, float f) { return new BlockActionEntry { type = t, stringValue = s, floatValue = f }; }
        static BlockActionEntry A2(BlockActionType t, float f, float f2) { return new BlockActionEntry { type = t, floatValue = f, floatValue2 = f2 }; }

        public static readonly List<Def> All = new List<Def>
        {
            // ================= 공통 =================
            new Def { Name="빈 규칙", Genre=BlockGenre.공통, Desc="아무것도 없는 상태에서 직접 조립합니다.",
                Build = r => { r.ruleName="새 규칙"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter; } },

            new Def { Name="닿으면 소리와 메시지", Genre=BlockGenre.공통, Desc="가장 기본. 효과음 클립을 연결하세요.",
                Build = r => { r.ruleName="접촉 반응"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.actions.Add(A(BlockActionType.PlaySoundClip));
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "무언가를 발견했다!")); } },

            new Def { Name="닿으면 점수 올리고 사라지기", Genre=BlockGenre.공통, Desc="코인/아이템의 기본 형태입니다.",
                Build = r => { r.ruleName="획득"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.actions.Add(A(BlockActionType.PlaySoundClip));
                    r.actions.Add(A(BlockActionType.AddScore, 1f));
                    r.actions.Add(A(BlockActionType.DestroySelf)); } },

            new Def { Name="키 누르면 클리어", Genre=BlockGenre.공통, Desc="테스트용. 발표 시연에 편리합니다.",
                Build = r => { r.ruleName="키로 클리어"; r.trigger=BlockTriggerType.OnKeyDown; r.key=KeyCode.E;
                    r.actions.Add(A(BlockActionType.TriggerGameClear)); } },

            new Def { Name="키 누르면 처음부터 다시", Genre=BlockGenre.공통, Desc="R 키를 누르면 스테이지를 처음부터 다시 시작합니다.",
                Build = r => { r.ruleName="처음부터 다시"; r.trigger=BlockTriggerType.OnKeyDown; r.key=KeyCode.R;
                    r.actions.Add(A(BlockActionType.RestartScene)); } },

            new Def { Name="시작하자마자 목표 안내", Genre=BlockGenre.공통, Desc="게임 시작 시 무엇을 해야 하는지 알려줍니다.",
                Build = r => { r.ruleName="시작 안내"; r.trigger=BlockTriggerType.OnStart;
                    r.actions.Add(A(BlockActionType.ShowClearHint, "목표 지점까지 이동하세요")); } },

            new Def { Name="몇 초마다 반복 효과", Genre=BlockGenre.공통, Desc="주기적으로 무언가 일어나게 합니다.",
                Build = r => { r.ruleName="주기 이벤트"; r.trigger=BlockTriggerType.OnTimerElapsed; r.seconds=5f; r.once=false;
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "주기 이벤트 발생")); } },

            new Def { Name="체력 낮으면 경고", Genre=BlockGenre.공통, Desc="위험 상황을 화면과 진동으로 알립니다.",
                Build = r => { r.ruleName="체력 경고"; r.trigger=BlockTriggerType.OnHealthBelow; r.hpThreshold=30; r.once=false; r.cooldown=3f;
                    r.actions.Add(A(BlockActionType.ShowWarningMessage, "체력이 위험합니다!"));
                    r.actions.Add(A2(BlockActionType.ShakeCamera, 0.15f, 0.4f)); } },

            new Def { Name="사망 시 슬로우모션 후 게임오버", Genre=BlockGenre.공통, Desc="연출을 넣은 사망 처리입니다.",
                Build = r => { r.ruleName="사망 연출"; r.trigger=BlockTriggerType.OnPlayerDeath;
                    r.actions.Add(A2(BlockActionType.SlowMotion, 0.3f, 1.5f));
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "쓰러졌습니다..."));
                    r.actions.Add(A(BlockActionType.Wait, 1.5f));
                    r.actions.Add(A(BlockActionType.TriggerGameOver)); } },

            new Def { Name="확률적 보상", Genre=BlockGenre.공통, Desc="30% 확률로만 회복 보상을 줍니다.",
                Build = r => { r.ruleName="확률 보상"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.conditions.Add(new BlockConditionEntry { type=BlockConditionType.RequireRandomChance, floatValue=30f });
                    r.actions.Add(new BlockActionEntry { type=BlockActionType.HealPlayer, intValue=10 });
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "행운의 보상을 얻었습니다!")); } },

            // ================= 플랫폼 =================
            new Def { Name="점프대 (밟으면 튕김)", Genre=BlockGenre.플랫폼, Desc="플레이어를 위로 튕겨 올립니다.",
                Build = r => { r.ruleName="점프대"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter; r.once=false; r.cooldown=0.3f;
                    r.actions.Add(A(BlockActionType.BouncePlayer, 12f));
                    r.actions.Add(A(BlockActionType.PlaySoundClip)); } },

            new Def { Name="부서지는 발판", Genre=BlockGenre.플랫폼, Desc="밟으면 흔들리다가 잠시 후 무너집니다.",
                Build = r => { r.ruleName="발판 붕괴"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.actions.Add(A2(BlockActionType.ShakeObject, 0.06f, 0.4f));
                    r.actions.Add(A(BlockActionType.Wait, 0.4f));
                    r.actions.Add(A(BlockActionType.BreakPlatformNow)); } },

            new Def { Name="체크포인트", Genre=BlockGenre.플랫폼, Desc="닿으면 부활 지점으로 저장합니다.",
                Build = r => { r.ruleName="체크포인트"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.actions.Add(A(BlockActionType.SaveCheckpointHere));
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "체크포인트 저장!"));
                    r.actions.Add(new BlockActionEntry { type=BlockActionType.ChangeColor, colorValue=Color.green }); } },

            new Def { Name="낙사 구역", Genre=BlockGenre.플랫폼, Desc="떨어지면 체크포인트로 되돌리고 피해를 줍니다.",
                Build = r => { r.ruleName="낙사"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter; r.once=false; r.cooldown=0.5f;
                    r.actions.Add(new BlockActionEntry { type=BlockActionType.DamagePlayer, intValue=15 });
                    r.actions.Add(A(BlockActionType.RespawnPlayerAtCheckpoint));
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "낙사! 체크포인트로 돌아갑니다.")); } },

            new Def { Name="움직이는 발판 작동", Genre=BlockGenre.플랫폼, Desc="밟으면 발판이 움직이기 시작합니다.",
                Build = r => { r.ruleName="발판 작동"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.actions.Add(A(BlockActionType.StartPlatform, 1.5f));
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "발판이 움직입니다!")); } },

            new Def { Name="골인 지점", Genre=BlockGenre.플랫폼, Desc="도착하면 클리어 처리합니다.",
                Build = r => { r.ruleName="골인"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.actions.Add(A(BlockActionType.PlaySoundClip));
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "골인!"));
                    r.actions.Add(A(BlockActionType.ClearAfterDelay, 1f)); } },

            new Def { Name="코인 획득", Genre=BlockGenre.플랫폼, Desc="점수를 올리고 이펙트와 함께 사라집니다.",
                Build = r => { r.ruleName="코인"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.actions.Add(A(BlockActionType.PlaySoundClip));
                    r.actions.Add(A(BlockActionType.AddScore, 1f));
                    r.actions.Add(A(BlockActionType.SpawnParticlePrefab));
                    r.actions.Add(A(BlockActionType.DestroySelf)); } },

            new Def { Name="가시 함정", Genre=BlockGenre.플랫폼, Desc="피해를 주고 뒤로 밀어냅니다.",
                Build = r => { r.ruleName="가시"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter; r.once=false; r.cooldown=1f;
                    r.actions.Add(new BlockActionEntry { type=BlockActionType.DamagePlayer, intValue=10 });
                    r.actions.Add(new BlockActionEntry { type=BlockActionType.PushPlayer, vectorValue=new Vector3(0,4,-5) });
                    r.actions.Add(A2(BlockActionType.ShakeCamera, 0.2f, 0.25f)); } },

            new Def { Name="열쇠 먹으면 문 열림", Genre=BlockGenre.플랫폼, Desc="열쇠를 획득하면 지정한 문이 열립니다.",
                Build = r => { r.ruleName="열쇠"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.actions.Add(A(BlockActionType.PlaySoundClip));
                    r.actions.Add(A(BlockActionType.OpenDoor));
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "문이 열렸습니다!"));
                    r.actions.Add(A(BlockActionType.DestroySelf)); } },

            new Def { Name="스피드 부스터", Genre=BlockGenre.플랫폼, Desc="잠깐 빨라졌다가 원래대로 돌아옵니다.",
                Build = r => { r.ruleName="가속"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.actions.Add(A(BlockActionType.SetPlayerMoveSpeed, 12f));
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "속도 증가!"));
                    r.actions.Add(A(BlockActionType.Wait, 5f));
                    r.actions.Add(A(BlockActionType.SetPlayerMoveSpeed, 6f));
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "속도가 원래대로 돌아왔습니다.")); } },

            // ================= 아레나 =================
            new Def { Name="적 처치 시 점수", Genre=BlockGenre.아레나, Desc="적이 쓰러지면 점수와 킬 수가 올라갑니다.",
                Build = r => { r.ruleName="처치 점수"; r.trigger=BlockTriggerType.OnEnemyDefeated; r.once=false;
                    r.actions.Add(A(BlockActionType.AddKillScore, 10f));
                    r.actions.Add(A(BlockActionType.ShowKillCount)); } },

            new Def { Name="목표 처치 수 달성하면 클리어", Genre=BlockGenre.아레나, Desc="score 가 목표에 닿으면 승리합니다.",
                Build = r => { r.ruleName="처치 목표"; r.trigger=BlockTriggerType.OnVariableReaches; r.variableName="score"; r.variableThreshold=50f;
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "모든 적을 처치했습니다!"));
                    r.actions.Add(A(BlockActionType.ClearAfterDelay, 1.5f)); } },

            new Def { Name="웨이브 시작 알림", Genre=BlockGenre.아레나, Desc="일정 시간마다 다음 웨이브를 알립니다.",
                Build = r => { r.ruleName="웨이브 알림"; r.trigger=BlockTriggerType.OnTimerElapsed; r.seconds=20f; r.once=false;
                    r.actions.Add(A(BlockActionType.AddVariable, "wave", 1f));
                    r.actions.Add(A(BlockActionType.ShowVariableInHud, "wave"));
                    r.actions.Add(A(BlockActionType.PlaySoundClip)); } },

            new Def { Name="보스 등장 연출", Genre=BlockGenre.아레나, Desc="보스를 켜고 카메라 흔들림과 BGM을 넣습니다.",
                Build = r => { r.ruleName="보스 등장"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.actions.Add(A(BlockActionType.ActivateObject));
                    r.actions.Add(A2(BlockActionType.ShakeCamera, 0.4f, 1.2f));
                    r.actions.Add(A(BlockActionType.ShowWarningMessage, "보스가 나타났다!"));
                    r.actions.Add(A(BlockActionType.PlayBGM)); } },

            new Def { Name="체력 회복 아이템", Genre=BlockGenre.아레나, Desc="먹으면 체력을 채우고 사라집니다.",
                Build = r => { r.ruleName="회복"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.actions.Add(new BlockActionEntry { type=BlockActionType.HealPlayer, intValue=30 });
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "체력을 회복했습니다."));
                    r.actions.Add(A(BlockActionType.DestroySelf)); } },

            new Def { Name="지속 피해 구역", Genre=BlockGenre.아레나, Desc="머무는 동안 계속 피해를 입습니다.",
                Build = r => { r.ruleName="독 지대"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter; r.once=false; r.cooldown=1f;
                    r.actions.Add(new BlockActionEntry { type=BlockActionType.DamagePlayer, intValue=5 });
                    r.actions.Add(new BlockActionEntry { type=BlockActionType.FlashColor, colorValue=Color.red, floatValue=0.2f }); } },

            new Def { Name="공격력 강화 아이템", Genre=BlockGenre.아레나, Desc="일정 시간 동안 공격력이 올라갑니다.",
                Build = r => { r.ruleName="공격 강화"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.actions.Add(new BlockActionEntry { type=BlockActionType.SetPlayerAttackDamage, intValue=40 });
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "공격력 강화!"));
                    r.actions.Add(A(BlockActionType.DeactivateObject));
                    r.actions.Add(A(BlockActionType.Wait, 8f));
                    r.actions.Add(new BlockActionEntry { type=BlockActionType.SetPlayerAttackDamage, intValue=15 });
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "강화가 끝났습니다.")); } },

            new Def { Name="적 전멸 (시연용)", Genre=BlockGenre.아레나, Desc="키 하나로 모든 적을 처치합니다. 발표 시연에 유용합니다.",
                Build = r => { r.ruleName="전멸"; r.trigger=BlockTriggerType.OnKeyDown; r.key=KeyCode.K;
                    r.actions.Add(A(BlockActionType.KillAllEnemies));
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "모든 적을 쓰러뜨렸습니다!")); } },

            new Def { Name="피격 시 화면 번쩍", Genre=BlockGenre.아레나, Desc="맞았을 때 타격감을 줍니다.",
                Build = r => { r.ruleName="피격 연출"; r.trigger=BlockTriggerType.OnHealthBelow; r.hpThreshold=99; r.once=false; r.cooldown=0.5f;
                    r.actions.Add(A2(BlockActionType.ShakeCamera, 0.25f, 0.2f));
                    r.actions.Add(new BlockActionEntry { type=BlockActionType.FlashColor, colorValue=Color.red, floatValue=0.15f }); } },

            new Def { Name="처치 시 이펙트와 드랍", Genre=BlockGenre.아레나, Desc="적이 죽으면 이펙트와 아이템을 남깁니다.",
                Build = r => { r.ruleName="드랍"; r.trigger=BlockTriggerType.OnEnemyDefeated; r.once=false;
                    r.actions.Add(A(BlockActionType.SpawnParticlePrefab));
                    r.actions.Add(A(BlockActionType.SpawnPrefabRandomNearby, 1.5f));
                    r.actions.Add(A(BlockActionType.PlaySoundClip)); } },

            // ================= 탐험 =================
            new Def { Name="아이템 수집", Genre=BlockGenre.탐험, Desc="모은 개수를 1 올리고 사라집니다.",
                Build = r => { r.ruleName="수집"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.actions.Add(new BlockActionEntry { type=BlockActionType.AddCollectible, intValue=1 });
                    r.actions.Add(A(BlockActionType.PlaySoundClip));
                    r.actions.Add(A(BlockActionType.DestroySelf)); } },

            new Def { Name="N개 모으면 문 열림", Genre=BlockGenre.탐험, Desc="수집 목표를 채우면 문이 열립니다.",
                Build = r => { r.ruleName="수집으로 문 열기"; r.trigger=BlockTriggerType.OnCollectibleCountReached; r.requiredCount=3;
                    r.actions.Add(A(BlockActionType.OpenDoor));
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "문이 열렸습니다!"));
                    r.actions.Add(A(BlockActionType.PlaySoundClip)); } },

            new Def { Name="N개 모으면 클리어", Genre=BlockGenre.탐험, Desc="목표 개수를 다 모으면 승리합니다.",
                Build = r => { r.ruleName="수집 완료"; r.trigger=BlockTriggerType.OnCollectibleCountReached; r.requiredCount=5;
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "보물을 모두 찾았습니다!"));
                    r.actions.Add(A(BlockActionType.ClearAfterDelay, 1.5f)); } },

            new Def { Name="보물 상자 열기", Genre=BlockGenre.탐험, Desc="상자를 열고 안에서 아이템이 나옵니다.",
                Build = r => { r.ruleName="보물 상자"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.actions.Add(A(BlockActionType.SetAnimatorTrigger, "Open"));
                    r.actions.Add(A(BlockActionType.PlaySoundClip));
                    r.actions.Add(A(BlockActionType.Wait, 0.5f));
                    r.actions.Add(A(BlockActionType.SpawnItemPrefab)); } },

            new Def { Name="어두운 구역 진입", Genre=BlockGenre.탐험, Desc="조명을 끄고 긴장감을 만듭니다.",
                Build = r => { r.ruleName="암전"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.actions.Add(A2(BlockActionType.FadeLightIntensity, 0.1f, 1.5f));
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "어두워졌다...")); } },

            new Def { Name="밝은 구역 복귀", Genre=BlockGenre.탐험, Desc="조명을 다시 켭니다.",
                Build = r => { r.ruleName="점등"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.actions.Add(A2(BlockActionType.FadeLightIntensity, 1.5f, 1f)); } },

            new Def { Name="숨겨진 통로 발견", Genre=BlockGenre.탐험, Desc="벽이 사라지며 길이 열립니다.",
                Build = r => { r.ruleName="비밀 통로"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.actions.Add(A(BlockActionType.FadeOutRenderer, 1f));
                    r.actions.Add(A(BlockActionType.DisableCollider));
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "숨겨진 길을 발견했다!")); } },

            new Def { Name="좀비 경보 (감지 범위 증가)", Genre=BlockGenre.탐험, Desc="소음을 내면 적이 더 멀리서도 알아챕니다.",
                Build = r => { r.ruleName="경보"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.actions.Add(A(BlockActionType.SetEnemyDetectionRange, 30f));
                    r.actions.Add(A(BlockActionType.ShowWarningMessage, "소리를 들었다! 적이 몰려온다!"));
                    r.actions.Add(A(BlockActionType.PlaySoundClip)); } },

            new Def { Name="안전지대 (서서히 회복)", Genre=BlockGenre.탐험, Desc="머무는 동안 조금씩 회복합니다.",
                Build = r => { r.ruleName="안전지대"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter; r.once=false; r.cooldown=2f;
                    r.actions.Add(new BlockActionEntry { type=BlockActionType.HealPlayer, intValue=5 });
                    r.actions.Add(new BlockActionEntry { type=BlockActionType.FlashColor, colorValue=Color.green, floatValue=0.3f }); } },

            new Def { Name="지도 조각 획득", Genre=BlockGenre.탐험, Desc="변수로 진행도를 기록합니다.",
                Build = r => { r.ruleName="지도 조각"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.actions.Add(A(BlockActionType.AddVariable, "map", 1f));
                    r.actions.Add(A(BlockActionType.ShowValueWithLabel, "지도 조각"));
                    r.actions.Add(A(BlockActionType.DestroySelf)); } },

            // ================= 퍼즐 =================
            new Def { Name="압력판 → 문 열기", Genre=BlockGenre.퍼즐, Desc="올라서면 문이 열립니다.",
                Build = r => { r.ruleName="압력판 ON"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter; r.once=false;
                    r.actions.Add(A(BlockActionType.OpenDoor));
                    r.actions.Add(new BlockActionEntry { type=BlockActionType.ChangeColor, colorValue=Color.green });
                    r.actions.Add(A(BlockActionType.PlaySoundClip)); } },

            new Def { Name="압력판에서 내려오면 문 닫기", Genre=BlockGenre.퍼즐, Desc="위 규칙과 짝으로 사용합니다.",
                Build = r => { r.ruleName="압력판 OFF"; r.trigger=BlockTriggerType.OnPlayerTriggerExit; r.once=false;
                    r.actions.Add(A(BlockActionType.CloseDoor));
                    r.actions.Add(new BlockActionEntry { type=BlockActionType.ChangeColor, colorValue=Color.red }); } },

            new Def { Name="상자 밀어 넣기 성공", Genre=BlockGenre.퍼즐, Desc="아무 오브젝트나 들어오면 반응합니다.",
                Build = r => { r.ruleName="상자 안착"; r.trigger=BlockTriggerType.OnAnyTriggerEnter;
                    r.conditions.Add(new BlockConditionEntry { type=BlockConditionType.RequireTag, stringValue="Untagged", invert=true });
                    r.actions.Add(A(BlockActionType.AddVariable, "puzzle", 1f));
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "딸깍! 무언가 맞물렸다.")); } },

            new Def { Name="순서 맞추기 (버튼 누르기)", Genre=BlockGenre.퍼즐, Desc="클릭할 때마다 진행도가 올라갑니다.",
                Build = r => { r.ruleName="퍼즐 버튼"; r.trigger=BlockTriggerType.OnMouseClick; r.once=false;
                    r.actions.Add(A(BlockActionType.AddVariable, "puzzle", 1f));
                    r.actions.Add(A(BlockActionType.PulseScale, 1.2f));
                    r.actions.Add(A(BlockActionType.ShowVariableInHud, "puzzle")); } },

            new Def { Name="퍼즐 완성하면 클리어", Genre=BlockGenre.퍼즐, Desc="진행도가 목표에 닿으면 승리합니다.",
                Build = r => { r.ruleName="퍼즐 완료"; r.trigger=BlockTriggerType.OnVariableReaches; r.variableName="puzzle"; r.variableThreshold=3f;
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "퍼즐을 풀었습니다!"));
                    r.actions.Add(A(BlockActionType.OpenDoor));
                    r.actions.Add(A(BlockActionType.ClearAfterDelay, 2f)); } },

            new Def { Name="레버 당기기 (토글)", Genre=BlockGenre.퍼즐, Desc="누를 때마다 문이 열리고 닫힙니다.",
                Build = r => { r.ruleName="레버"; r.trigger=BlockTriggerType.OnMouseClick; r.once=false; r.cooldown=0.5f;
                    r.actions.Add(A(BlockActionType.ToggleDoor));
                    r.actions.Add(new BlockActionEntry { type=BlockActionType.RotateBy, vectorValue=new Vector3(0,0,45) });
                    r.actions.Add(A(BlockActionType.PlaySoundClip)); } },

            new Def { Name="잘못된 선택 (진행도 초기화)", Genre=BlockGenre.퍼즐, Desc="틀리면 피해를 입고 처음부터 다시입니다.",
                Build = r => { r.ruleName="오답"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter; r.once=false; r.cooldown=1f;
                    r.actions.Add(new BlockActionEntry { type=BlockActionType.DamagePlayer, intValue=10 });
                    r.actions.Add(A(BlockActionType.ResetVariable, "puzzle"));
                    r.actions.Add(A(BlockActionType.ShowWarningMessage, "틀렸습니다! 처음부터 다시."));
                    r.actions.Add(A2(BlockActionType.ShakeCamera, 0.3f, 0.3f)); } },

            new Def { Name="제한시간 퍼즐 (시간 추가)", Genre=BlockGenre.퍼즐, Desc="정답을 맞히면 시간을 벌 수 있습니다.",
                Build = r => { r.ruleName="시간 보너스"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.actions.Add(A(BlockActionType.AddTimerSeconds, 15f));
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "시간 +15초!"));
                    r.actions.Add(A(BlockActionType.DestroySelf)); } },

            new Def { Name="힌트 표시", Genre=BlockGenre.퍼즐, Desc="다가가면 힌트를 알려줍니다.",
                Build = r => { r.ruleName="힌트"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.actions.Add(new BlockActionEntry { type=BlockActionType.ShowHudMessageLong, stringValue="세 개의 발판을 모두 눌러야 합니다.", floatValue=4f }); } },

            new Def { Name="모든 퍼즐 완료 → 탈출문", Genre=BlockGenre.퍼즐, Desc="조건을 만족해야만 탈출문이 열립니다.",
                Build = r => { r.ruleName="탈출문"; r.trigger=BlockTriggerType.OnPlayerTriggerEnter;
                    r.conditions.Add(new BlockConditionEntry { type=BlockConditionType.RequireVariableAtLeast, stringValue="puzzle", floatValue=3f });
                    r.actions.Add(A(BlockActionType.ShowHudMessage, "탈출 성공!"));
                    r.actions.Add(A(BlockActionType.ClearAfterDelay, 1f)); } },
        };

        public static List<Def> ByGenre(BlockGenre g)
        {
            var list = new List<Def>();
            foreach (var d in All) if (d.Genre == g) list.Add(d);
            return list;
        }
    }
}
#endif
