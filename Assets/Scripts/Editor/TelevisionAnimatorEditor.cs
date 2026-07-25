using UnityEditor;
using UnityEngine;

/// <summary>
/// TelevisionAnimator 用のカスタム Inspector エディタ。
/// Inspector 上で「スタート座標保存」「ゴール座標保存」「プレビュー」「テスト再生」ボタンを分かりやすく表示します。
/// </summary>
[CustomEditor(typeof(TelevisionAnimator))]
[CanEditMultipleObjects]
public class TelevisionAnimatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 破棄済みオブジェクトへの参照による MissingReferenceException を完全回避
        if (target == null || target.Equals(null))
        {
            return;
        }

        serializedObject.Update();

        // 通常のプロパティ（フィールド）を描画
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("ワンクリック座標設定 & テスト機能", EditorStyles.boldLabel);

        TelevisionAnimator animator = target as TelevisionAnimator;
        if (animator == null) return;

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("1. 現在地を『スタート座標』に保存", GUILayout.Height(30)))
        {
            Undo.RecordObject(animator, "Save Television Start Transform");
            animator.SaveCurrentTransformAsStart();
            EditorUtility.SetDirty(animator);
        }

        if (GUILayout.Button("2. 現在地を『ゴール座標』に保存", GUILayout.Height(30)))
        {
            Undo.RecordObject(animator, "Save Television End Transform");
            animator.SaveCurrentTransformAsEnd();
            EditorUtility.SetDirty(animator);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("3. スタート位置に配置", GUILayout.Height(25)))
        {
            Undo.RecordObject(animator.transform, "Set Television to Start Transform");
            animator.SetToStartTransform();
        }

        if (GUILayout.Button("4. ゴール位置に配置", GUILayout.Height(25)))
        {
            Undo.RecordObject(animator.transform, "Set Television to End Transform");
            animator.SetToEndTransform();
        }

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("5. アニメーションをテスト再生", GUILayout.Height(35)))
        {
            animator.PlayAnimation();
        }

        serializedObject.ApplyModifiedProperties();
    }
}
