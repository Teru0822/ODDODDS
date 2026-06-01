using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class SceneTool : MonoBehaviour
{
    [MenuItem("Scenes/Play First Scene", priority = 0)]
    public static void PlayFirstScene()
    {
        // 既に再生中の場合は何もしない.
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Editor is Playing.");
            return;
        }

        // Build Settingsにシーンが登録されていない場合は何もしない.
        if (EditorBuildSettings.scenes.Length <= 0)
        {
            Debug.LogWarning("Not Exist Scene.");
            return;
        }

        // 現在のシーンが編集中の場合、保存/非保存を聞く.キャンセルの場合はなにもしない.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        // 最初のシーンを開く.
        EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);
        // Unity Editorを再生させる.
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Scenes/Title", priority = 11)]
    public static void OpenTitleScene()
    {
        // 現在のシーンが編集中の場合、保存/非保存を聞く.キャンセルの場合はなにもしない.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }
        EditorSceneManager.OpenScene("Assets/Scenes/3D_Title_Sample.unity", OpenSceneMode.Single);
    }

    [MenuItem("Scenes/Game", priority = 12)]
    public static void OpenGameScene()
    {
        // 現在のシーンが編集中の場合、保存/非保存を聞く.キャンセルの場合はなにもしない.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity", OpenSceneMode.Single);
    }
}
