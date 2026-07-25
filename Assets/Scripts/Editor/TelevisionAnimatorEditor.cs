using UnityEditor;
using UnityEngine;

/// <summary>
/// TelevisionAnimator 用のカスタム Inspector エディタ。
/// Scene_UFOCatcher にある television オブジェクトの位置保存・プレビュー・テスト再生ボタンを配置します。
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

        if (GUILayout.Button("現在地を『スタート(出現)座標』に保存", GUILayout.Height(30)))
        {
            Undo.RecordObject(animator, "Save Television Start Transform");
            animator.SaveCurrentTransformAsStart();
            EditorUtility.SetDirty(animator);
        }

        if (GUILayout.Button("現在地を『ゴール(目標)座標』に保存", GUILayout.Height(30)))
        {
            Undo.RecordObject(animator, "Save Television End Transform");
            animator.SaveCurrentTransformAsEnd();
            EditorUtility.SetDirty(animator);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("プレビュー配置", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("スタート位置に移動", GUILayout.Height(25)))
        {
            Undo.RecordObject(animator.transform, "Set Television to Start Transform");
            animator.SetToStartTransform();
        }

        if (GUILayout.Button("ゴール位置に移動", GUILayout.Height(25)))
        {
            Undo.RecordObject(animator.transform, "Set Television to End Transform");
            animator.SetToEndTransform();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("テスト再生", EditorStyles.boldLabel);

        if (GUILayout.Button("テスト再生: コイン投入時アニメーション", GUILayout.Height(35)))
        {
            animator.PlayCoinAnimation();
        }

        serializedObject.ApplyModifiedProperties();
    }
}
