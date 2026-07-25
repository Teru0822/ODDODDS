using UnityEditor;
using UnityEngine;

/// <summary>
/// TelevisionAnimator 用のカスタム Inspector エディタ。
/// 4つの座標 (1.出現, 2.スタート, 3.ゴール, 4.収納) のワンクリック保存・プレビュー移動・テスト再生ボタンを配置します。
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

        if (GUILayout.Button("1. 『出現座標』に保存", GUILayout.Height(28)))
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

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("3. 『ゴール座標』に保存", GUILayout.Height(28)))
        {
            Undo.RecordObject(animator, "Save Television End Transform");
            animator.SaveCurrentTransformAsEnd();
            EditorUtility.SetDirty(animator);
        }

        if (GUILayout.Button("4. 『収納(しまい)座標』に保存", GUILayout.Height(28)))
        {
            Undo.RecordObject(animator, "Save Television Stow Transform");
            animator.SaveCurrentTransformAsStow();
            EditorUtility.SetDirty(animator);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("プレビュー配置", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("出現位置", GUILayout.Height(24)))
        {
            Undo.RecordObject(animator.transform, "Set Television to Spawn Transform");
            animator.SetToSpawnTransform();
        }

        if (GUILayout.Button("スタート位置", GUILayout.Height(24)))
        {
            Undo.RecordObject(animator.transform, "Set Television to Start Transform");
            animator.SetToStartTransform();
        }

        if (GUILayout.Button("ゴール位置", GUILayout.Height(24)))
        {
            Undo.RecordObject(animator.transform, "Set Television to End Transform");
            animator.SetToEndTransform();
        }

        if (GUILayout.Button("収納位置", GUILayout.Height(24)))
        {
            Undo.RecordObject(animator.transform, "Set Television to Stow Transform");
            animator.SetToStowTransform();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("テスト再生", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("テスト: 1. アクセス時 (出現 → スタート)", GUILayout.Height(30)))
        {
            animator.PlayEnterAnimation();
        }

        if (GUILayout.Button("テスト: 2. 全コイン完了時 (スタート → ゴール)", GUILayout.Height(30)))
        {
            animator.PlayCoinAnimation();
        }

        if (GUILayout.Button("テスト: 3. キー3押下時 (ゴール → 収納)", GUILayout.Height(30)))
        {
            animator.PlayStowAnimation();
        }

        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }
}
