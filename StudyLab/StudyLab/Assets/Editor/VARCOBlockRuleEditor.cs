#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VARCO_Workshop.Editor
{
    [CustomEditor(typeof(VARCOBlockRule))]
    public class VARCOBlockRuleEditor : UnityEditor.Editor
    {
        SerializedProperty rulesProp;
        SerializedProperty verboseLogProp;

        static string[] categoryNames;
        static BlockActionCategory[] categoryValues;
        static string[] triggerNames;
        static BlockTriggerType[] triggerValues;
        static string[] conditionNames;
        static BlockConditionType[] conditionValues;
        static string[] stateNames;
        static GameState[] stateValues;

        void OnEnable()
        {
            rulesProp = serializedObject.FindProperty("rules");
            verboseLogProp = serializedObject.FindProperty("verboseLog");

            if (categoryNames == null)
            {
                categoryValues = (BlockActionCategory[])System.Enum.GetValues(typeof(BlockActionCategory));
                categoryNames = new string[categoryValues.Length];
                for (int i = 0; i < categoryValues.Length; i++)
                    categoryNames[i] = BlockLabels.Of(categoryValues[i]);

                triggerValues = (BlockTriggerType[])System.Enum.GetValues(typeof(BlockTriggerType));
                triggerNames = new string[triggerValues.Length];
                for (int i = 0; i < triggerValues.Length; i++)
                    triggerNames[i] = BlockLabels.Of(triggerValues[i]);

                conditionValues = (BlockConditionType[])System.Enum.GetValues(typeof(BlockConditionType));
                conditionNames = new string[conditionValues.Length];
                for (int i = 0; i < conditionValues.Length; i++)
                    conditionNames[i] = BlockLabels.Of(conditionValues[i]);

                stateValues = (GameState[])System.Enum.GetValues(typeof(GameState));
                stateNames = new string[stateValues.Length];
                for (int i = 0; i < stateValues.Length; i++)
                    stateNames[i] = BlockLabels.Of(stateValues[i]);
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "\"① 언제 → ② 이럴 때만 → ③ 무엇을 한다\" 순서로 채우면 규칙이 완성됩니다.\n" +
                "③은 종류를 먼저 고른 뒤 그 안에서 선택하며, 여러 개를 쌓으면 위에서부터 차례로 실행됩니다.",
                MessageType.Info);

            EditorGUILayout.PropertyField(verboseLogProp, new GUIContent("실행 로그 출력"));
            EditorGUILayout.Space(8);

            for (int i = 0; i < rulesProp.arraySize; i++)
            {
                if (DrawRule(rulesProp.GetArrayElementAtIndex(i), i)) break;
                EditorGUILayout.Space(6);
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("+ 새 규칙 추가", GUILayout.Height(30)))
            {
                rulesProp.InsertArrayElementAtIndex(rulesProp.arraySize);
                var r = rulesProp.GetArrayElementAtIndex(rulesProp.arraySize - 1);
                r.FindPropertyRelative("ruleName").stringValue = "새 규칙";
                r.FindPropertyRelative("enabledRule").boolValue = true;
                r.FindPropertyRelative("once").boolValue = true;
                r.FindPropertyRelative("conditions").ClearArray();
                r.FindPropertyRelative("actions").ClearArray();
            }

            serializedObject.ApplyModifiedProperties();
        }

        bool DrawRule(SerializedProperty rule, int index)
        {
            var nameProp = rule.FindPropertyRelative("ruleName");
            var enabledProp = rule.FindPropertyRelative("enabledRule");

            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.BeginHorizontal();
            enabledProp.boolValue = EditorGUILayout.Toggle(enabledProp.boolValue, GUILayout.Width(18));
            nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue, EditorStyles.boldLabel);
            var deleted = GUILayout.Button("규칙 삭제", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            if (deleted)
            {
                rulesProp.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndVertical();
                return true;
            }

            var prev = GUI.backgroundColor;

            GUI.backgroundColor = new Color(0.6f, 0.85f, 1f);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUI.backgroundColor = prev;
            EditorGUILayout.LabelField("① 언제 — 무슨 일이 일어나면", EditorStyles.boldLabel);
            DrawTrigger(rule);
            EditorGUILayout.EndVertical();

            GUI.backgroundColor = new Color(1f, 0.85f, 0.5f);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUI.backgroundColor = prev;
            EditorGUILayout.LabelField("② 조건 — 이럴 때만 (없어도 됨)", EditorStyles.boldLabel);
            DrawConditions(rule.FindPropertyRelative("conditions"));
            EditorGUILayout.EndVertical();

            GUI.backgroundColor = new Color(0.6f, 0.9f, 0.65f);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUI.backgroundColor = prev;
            EditorGUILayout.LabelField("③ 무엇을 — 위에서부터 차례로 실행", EditorStyles.boldLabel);
            DrawActions(rule.FindPropertyRelative("actions"));
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();
            return false;
        }

        void DrawTrigger(SerializedProperty rule)
        {
            var triggerProp = rule.FindPropertyRelative("trigger");
            triggerProp.enumValueIndex = EditorGUILayout.Popup("언제?", triggerProp.enumValueIndex, triggerNames);

            var tHint = BlockLabels.HintOf((BlockTriggerType)triggerProp.enumValueIndex);
            if (!string.IsNullOrEmpty(tHint))
                EditorGUILayout.LabelField(tHint, EditorStyles.wordWrappedMiniLabel);

            switch ((BlockTriggerType)triggerProp.enumValueIndex)
            {
                case BlockTriggerType.OnKeyDown:
                case BlockTriggerType.OnKeyHold:
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("key"), new GUIContent("키"));
                    break;
                case BlockTriggerType.OnTimerElapsed:
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("seconds"), new GUIContent("초 간격"));
                    break;
                case BlockTriggerType.OnCollectibleCountReached:
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("watchedCounter"), new GUIContent("아이템 개수를 세는 오브젝트"));
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("requiredCount"), new GUIContent("필요 개수"));
                    break;
                case BlockTriggerType.OnHealthBelow:
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("watchedPlayerHealth"), new GUIContent("플레이어 오브젝트"));
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("hpThreshold"), new GUIContent("이 HP 이하일 때"));
                    break;
                case BlockTriggerType.OnHealthAbove:
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("watchedPlayerHealth"), new GUIContent("플레이어 오브젝트"));
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("hpThreshold"), new GUIContent("이 HP 이상일 때"));
                    break;
                case BlockTriggerType.OnPlayerDeath:
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("watchedPlayerHealth"), new GUIContent("플레이어 오브젝트"));
                    break;
                case BlockTriggerType.OnEnemyDefeated:
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("watchedEnemy"), new GUIContent("적 오브젝트"));
                    break;
                case BlockTriggerType.OnVariableReaches:
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("variableName"), new GUIContent("점수 이름"));
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("variableThreshold"), new GUIContent("이 점수 이상일 때"));
                    break;
                case BlockTriggerType.OnMouseClick:
                    EditorGUILayout.HelpBox("클릭을 받으려면 이 오브젝트에 충돌 범위가 있어야 합니다.", MessageType.None);
                    break;
                case BlockTriggerType.OnManualCall:
                    EditorGUILayout.HelpBox("스스로 실행되지 않고, 다른 곳에서 이 규칙 이름을 불러야 실행됩니다.", MessageType.None);
                    break;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("once"), new GUIContent("한 번만 실행"));
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("cooldown"), new GUIContent("재실행 간격(초)"));
            EditorGUILayout.EndHorizontal();
        }

        void DrawConditions(SerializedProperty conditions)
        {
            int del = -1;
            for (int i = 0; i < conditions.arraySize; i++)
            {
                var c = conditions.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                var typeProp = c.FindPropertyRelative("type");
                typeProp.enumValueIndex = EditorGUILayout.Popup(typeProp.enumValueIndex, conditionNames);
                EditorGUILayout.PropertyField(c.FindPropertyRelative("invert"), new GUIContent("반대로"), GUILayout.Width(90));
                if (GUILayout.Button("－", GUILayout.Width(24))) del = i;
                EditorGUILayout.EndHorizontal();

                switch ((BlockConditionType)typeProp.enumValueIndex)
                {
                    case BlockConditionType.RequireTag:
                        EditorGUILayout.PropertyField(c.FindPropertyRelative("stringValue"), new GUIContent("필요한 이름표(태그)"));
                        EditorGUILayout.HelpBox("플레이어가 닿거나 부딪히는 경우에만 검사됩니다.", MessageType.None);
                        break;
                    case BlockConditionType.RequireCollectibleAtLeast:
                        EditorGUILayout.PropertyField(c.FindPropertyRelative("targetCounter"), new GUIContent("아이템 개수를 세는 오브젝트"));
                        EditorGUILayout.PropertyField(c.FindPropertyRelative("intValue"), new GUIContent("최소 개수"));
                        break;
                    case BlockConditionType.RequireGameState:
                        {
                            var gs = c.FindPropertyRelative("gameStateValue");
                            gs.enumValueIndex = EditorGUILayout.Popup("게임 상태", gs.enumValueIndex, stateNames);
                        }
                        break;
                    case BlockConditionType.RequirePlayerHPAtLeast:
                        EditorGUILayout.PropertyField(c.FindPropertyRelative("targetPlayerHealth"), new GUIContent("플레이어 오브젝트"));
                        EditorGUILayout.PropertyField(c.FindPropertyRelative("intValue"), new GUIContent("최소 HP"));
                        break;
                    case BlockConditionType.RequirePlayerHPBelow:
                        EditorGUILayout.PropertyField(c.FindPropertyRelative("targetPlayerHealth"), new GUIContent("플레이어 오브젝트"));
                        EditorGUILayout.PropertyField(c.FindPropertyRelative("intValue"), new GUIContent("이 HP 미만"));
                        break;
                    case BlockConditionType.RequireObjectActive:
                        EditorGUILayout.PropertyField(c.FindPropertyRelative("targetObject"), new GUIContent("대상 오브젝트"));
                        EditorGUILayout.PropertyField(c.FindPropertyRelative("boolValue"), new GUIContent("활성 상태여야 함"));
                        break;
                    case BlockConditionType.RequireRandomChance:
                        EditorGUILayout.PropertyField(c.FindPropertyRelative("floatValue"), new GUIContent("통과 확률(%)"));
                        break;
                    case BlockConditionType.RequireVariableAtLeast:
                        EditorGUILayout.PropertyField(c.FindPropertyRelative("stringValue"), new GUIContent("점수 이름"));
                        EditorGUILayout.PropertyField(c.FindPropertyRelative("floatValue"), new GUIContent("최소 값"));
                        break;
                    case BlockConditionType.RequireVariableEquals:
                        EditorGUILayout.PropertyField(c.FindPropertyRelative("stringValue"), new GUIContent("점수 이름"));
                        EditorGUILayout.PropertyField(c.FindPropertyRelative("floatValue"), new GUIContent("같아야 할 값"));
                        break;
                    case BlockConditionType.RequireKillCountAtLeast:
                        EditorGUILayout.PropertyField(c.FindPropertyRelative("intValue"), new GUIContent("최소 처치 수"));
                        break;
                    case BlockConditionType.RequireTimerBelow:
                        EditorGUILayout.PropertyField(c.FindPropertyRelative("targetTimer"), new GUIContent("제한시간 오브젝트"));
                        EditorGUILayout.PropertyField(c.FindPropertyRelative("floatValue"), new GUIContent("남은 시간 이하(초)"));
                        break;
                    case BlockConditionType.RequireDistanceBelow:
                        EditorGUILayout.PropertyField(c.FindPropertyRelative("targetPoint"), new GUIContent("기준이 될 위치 오브젝트"));
                        EditorGUILayout.PropertyField(c.FindPropertyRelative("floatValue"), new GUIContent("거리 이하"));
                        break;
                }
                EditorGUILayout.EndVertical();
            }

            if (del >= 0) conditions.DeleteArrayElementAtIndex(del);
            if (GUILayout.Button("+ 조건 추가")) conditions.InsertArrayElementAtIndex(conditions.arraySize);
        }

        void DrawActions(SerializedProperty actions)
        {
            int del = -1;
            int moveUp = -1, moveDown = -1;

            for (int i = 0; i < actions.arraySize; i++)
            {
                var a = actions.GetArrayElementAtIndex(i);
                var typeProp = a.FindPropertyRelative("type");
                var current = (BlockActionType)typeProp.enumValueIndex;
                var info = BlockActionCatalog.Get(current);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // 순서 + 삭제
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField((i + 1) + ".", GUILayout.Width(20));
                EditorGUILayout.LabelField(info.Label, EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(i == 0))
                    if (GUILayout.Button("▲", GUILayout.Width(24))) moveUp = i;
                using (new EditorGUI.DisabledScope(i == actions.arraySize - 1))
                    if (GUILayout.Button("▼", GUILayout.Width(24))) moveDown = i;
                if (GUILayout.Button("－", GUILayout.Width(24))) del = i;
                EditorGUILayout.EndHorizontal();

                // 1단: 카테고리
                int catIndex = System.Array.IndexOf(categoryValues, info.Category);
                int newCatIndex = EditorGUILayout.Popup("종류", catIndex, categoryNames);
                if (newCatIndex != catIndex)
                {
                    var list = BlockActionCatalog.InCategory(categoryValues[newCatIndex]);
                    if (list.Count > 0)
                    {
                        typeProp.enumValueIndex = (int)list[0];
                        current = list[0];
                        info = BlockActionCatalog.Get(current);
                    }
                }

                // 2단: 카테고리 안의 액션
                var inCat = BlockActionCatalog.InCategory(info.Category);
                var labels = new string[inCat.Count];
                int sel = 0;
                for (int k = 0; k < inCat.Count; k++)
                {
                    labels[k] = BlockActionCatalog.Get(inCat[k]).Label;
                    if (inCat[k] == current) sel = k;
                }
                int newSel = EditorGUILayout.Popup("무엇을", sel, labels);
                if (newSel != sel)
                {
                    typeProp.enumValueIndex = (int)inCat[newSel];
                    info = BlockActionCatalog.Get(inCat[newSel]);
                }

                if (!string.IsNullOrEmpty(info.Hint))
                    EditorGUILayout.LabelField(info.Hint, EditorStyles.wordWrappedMiniLabel);

                // 메타데이터로 필드 자동 생성
                foreach (var spec in info.Fields)
                {
                    var parts = spec.Split('|');
                    if (parts.Length < 2) continue;
                    var prop = a.FindPropertyRelative(parts[0]);
                    if (prop != null)
                        EditorGUILayout.PropertyField(prop, new GUIContent(parts[1]));
                }

                EditorGUILayout.EndVertical();
            }

            if (moveUp > 0) actions.MoveArrayElement(moveUp, moveUp - 1);
            if (moveDown >= 0 && moveDown < actions.arraySize - 1) actions.MoveArrayElement(moveDown, moveDown + 1);
            if (del >= 0) actions.DeleteArrayElementAtIndex(del);

            if (GUILayout.Button("+ 액션 추가")) actions.InsertArrayElementAtIndex(actions.arraySize);
        }
    }
}
#endif
