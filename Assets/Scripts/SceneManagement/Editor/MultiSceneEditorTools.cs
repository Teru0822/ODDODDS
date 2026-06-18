using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Multi-Scene 編集の補助ツール（エディタ専用）。
/// 「ルートシーン + 指定フォルダ内の全サブシーン」を一括で加法(Additive)に開く。
/// 毎回 Hierarchy で手動オープンする手間と、開き忘れによる事故を防ぐ。
///
/// サブシーンは <see cref="SubSceneFolder"/> に置く前提。フォルダを変える場合は定数を編集する。
/// </summary>
public static class MultiSceneEditorTools
{
    // ルート（常駐）シーン
    private const string RootScenePath = "Assets/Scenes/MainScene.unity";

    // サブシーンを置くフォルダ（この中の *.unity をすべて加法で開く）
    private const string SubSceneFolder = "Assets/Scenes/Additive";

    [MenuItem("Tools/Multi-Scene/ルート + 全サブシーンを開く")]
    public static void OpenAll()
    {
        // 未保存があれば保存を促す（保存忘れ事故の防止）
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var root = EditorSceneManager.OpenScene(RootScenePath, OpenSceneMode.Single);
        if (root.IsValid()) SceneManager.SetActiveScene(root);

        if (!AssetDatabase.IsValidFolder(SubSceneFolder))
        {
            Debug.LogWarning($"[MultiSceneEditorTools] サブシーンフォルダ '{SubSceneFolder}' が存在しません。" +
                             "フォルダを作り、分割したサブシーンを置いてください。");
            return;
        }

        int opened = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { SubSceneFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            opened++;
        }
        Debug.Log($"[MultiSceneEditorTools] ルート + サブシーン {opened} 個を加法で開きました。");
    }

    [MenuItem("Tools/Multi-Scene/ルートシーンのみ開く")]
    public static void OpenRootOnly()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(RootScenePath, OpenSceneMode.Single);
    }
}
