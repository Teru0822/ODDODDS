using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TypewriterInteractable))]
public class TypewriterInteractableEditor : Editor
{
    private bool _skillsFoldout = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        serializedObject.Update();
        var debugModeProp = serializedObject.FindProperty("_debugMode");
        if (debugModeProp == null || !debugModeProp.boolValue) return;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("── ローグライク デバッグ ──", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Playモード中のみ使用できます。", MessageType.Info);
            return;
        }

        var manager = Object.FindFirstObjectByType<RoguelikeManager>();
        if (manager == null)
        {
            EditorGUILayout.HelpBox("RoguelikeManager がシーンに見つかりません。", MessageType.Warning);
            return;
        }

        // 全アンロック / 全ロック ボタン
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全スキルをアンロック"))
        {
            foreach (var skill in manager.AllSkillsDebug.Values.Where(s => !s.isGet).ToList())
                manager.UnlockSkill(skill);
        }
        if (GUILayout.Button("全スキルをロック"))
        {
            foreach (var skill in manager.AllSkillsDebug.Values.Where(s => s.isGet).ToList())
                manager.LockSkill(skill);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // スキルタイプごとにグループ表示
        _skillsFoldout = EditorGUILayout.Foldout(_skillsFoldout, "スキル一覧", true, EditorStyles.foldoutHeader);
        if (!_skillsFoldout) return;

        EditorGUI.indentLevel++;
        foreach (SkillType type in System.Enum.GetValues(typeof(SkillType)))
        {
            if (type == SkillType.None) continue;

            var skills = manager.AllSkillsDebug.Values
                .Where(s => s.skillType == type)
                .OrderBy(s => s.id)
                .ToList();

            if (skills.Count == 0) continue;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(type.ToString(), EditorStyles.miniBoldLabel);

            foreach (var skill in skills)
            {
                EditorGUILayout.BeginHorizontal();
                bool newVal = EditorGUILayout.Toggle(skill.isGet, GUILayout.Width(20));
                EditorGUILayout.LabelField($"[{skill.id}] {skill.skillName}", GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();

                if (newVal == skill.isGet) continue;
                if (newVal)
                    manager.UnlockSkill(skill);
                else
                    manager.LockSkill(skill);
            }
        }
        EditorGUI.indentLevel--;
    }
}
