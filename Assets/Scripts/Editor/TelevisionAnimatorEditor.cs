using UnityEditor;
using UnityEngine;

/// <summary>
/// TelevisionAnimator 用のカスタム Inspector エディタ。
/// 3つの座標 (1.出現, 2.スタート, 3.ゴール) のワンクリック保存・プレビュー移動・テスト再生ボタンを配置します。
/// </summary>
[CustomEditor(typeof(TelevisionAnimator))]
[CanEditMultipleObjects]
public class TelevisionAnimatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (target == null || target.Equals(null))
        {
            return;
        }

        serializedObject.Update();

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("ワンクリック座標保存", EditorStyles.boldLabel);

        TelevisionAnimator animator = target as TelevisionAnimator;
        if (animator == null) return;

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("1. 『出現(初期)座標』に保存", GUILayout.Height(28)))
        {
            Undo.RecordObject(animator, "Save Television Spawn Transform");
            animator.SaveCurrentTransformAsSpawn();
            EditorUtility.SetDirty(animator);
        }

        if (GUILayout.Button("2. 『スタート座標』に保存", GUILayout.Height(28)))
        {
            Undo.RecordObject(animator, "Save Television Start Transform");
            animator.SaveCurrentTransformAsStart();
            EditorUtility.SetDirty(animator);
        }

        if (GUILayout.Button("3. 『ゴール座標』に保存", GUILayout.Height(28)))
        {
            Undo.RecordObject(animator, "Save Television End Transform");
            animator.SaveCurrentTransformAsEnd();
            EditorUtility.SetDirty(animator);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("プレビュー配置", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("出現位置に移動", GUILayout.Height(24)))
        {
            Undo.RecordObject(animator.transform, "Set Television to Spawn Transform");
            animator.SetToSpawnTransform();
        }

        if (GUILayout.Button("スタート位置に移動", GUILayout.Height(24)))
        {
            Undo.RecordObject(animator.transform, "Set Television to Start Transform");
            animator.SetToStartTransform();
        }

        if (GUILayout.Button("ゴール位置に移動", GUILayout.Height(24)))
        {
            Undo.RecordObject(animator.transform, "Set Television to End Transform");
            animator.SetToEndTransform();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("テスト再生", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("テスト: 進入時 (出現 → スタート)", GUILayout.Height(30)))
        {
            animator.PlayEnterAnimation();
        }

        if (GUILayout.Button("テスト: コイン時 (スタート → ゴール)", GUILayout.Height(30)))
        {
            animator.PlayCoinAnimation();
        }

        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }
}
