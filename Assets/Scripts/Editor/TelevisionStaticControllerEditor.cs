using UnityEditor;
using UnityEngine;

/// <summary>
/// TelevisionStaticController 用のカスタム Inspector エディタ。
/// 3つの Canvas (Left, Right, Back) と砂嵐再生テストボタンを提供します。
/// </summary>
[CustomEditor(typeof(TelevisionStaticController))]
[CanEditMultipleObjects]
public class TelevisionStaticControllerEditor : Editor
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
        EditorGUILayout.LabelField("テスト & プレビュー表示", EditorStyles.boldLabel);

        TelevisionStaticController controller = target as TelevisionStaticController;
        if (controller == null) return;

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("砂嵐再生 (0.5s)", GUILayout.Height(28)))
        {
            controller.TestPlayStatic();
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Left Canvas 表示", GUILayout.Height(26)))
        {
            controller.TestShowLeft();
        }

        if (GUILayout.Button("Right Canvas 表示", GUILayout.Height(26)))
        {
            controller.TestShowRight();
        }

        if (GUILayout.Button("Back Canvas 表示", GUILayout.Height(26)))
        {
            controller.TestShowBack();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Play Canvas 表示", GUILayout.Height(26)))
        {
            controller.ShowPlayCanvas();
        }

        if (controller.IsCameraSwitchingEnabled)
        {
            if (GUILayout.Button("カメラ切り替え: 無効化", GUILayout.Height(26)))
            {
                controller.DisableCameraSwitching();
            }
        }
        else
        {
            if (GUILayout.Button("カメラ切り替え: 有効化", GUILayout.Height(26)))
            {
                controller.EnableCameraSwitching();
            }
        }

        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }
}
