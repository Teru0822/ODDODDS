using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Multi-Scene 構成の登録状況を一覧し、まとめて同期するエディタウィンドウ（エディタ専用）。
///
/// サブシーンを新規追加したとき、次の2箇所への登録忘れが起きやすい:
///   1. Build Settings … 未登録だと実行時に SceneManager.LoadSceneAsync が失敗する。
///   2. ルートシーンの <see cref="MultiSceneLoader"/>.subScenes … 未登録だと起動時にロードされない。
///
/// このウィンドウは <see cref="SubSceneFolder"/> 内のシーンを走査し、両者の現在状態を
/// チェックボックスで見せて一括適用する。既存の設定は保持され、変更した行だけが反映される。
///
/// 「起動時にロード」を外した行は subScenes から削除せず disableOnStart を立てるだけなので、
/// 再度チェックすれば元に戻せる（デバッグで一時的に外す用途と同じ扱い）。
/// </summary>
public class MultiSceneSetupWindow : EditorWindow
{
    // ルート（常駐）シーン。MultiSceneLoader はこのシーンに置く前提。
    private const string RootScenePath = "Assets/Scenes/MainScene.unity";

    // サブシーンを置くフォルダ（MultiSceneEditorTools と揃えること）
    private const string SubSceneFolder = "Assets/Scenes/Additive";

    private class Row
    {
        public string Path;
        public string SceneName;

        // 走査時点の状態
        public bool WasInBuildSettings;
        public bool WasLoadedOnStart;

        // ユーザーが指定した適用後の状態
        public bool WantInBuildSettings;
        public bool WantLoadOnStart;

        public bool IsNew;      // Build Settings 未登録 = 新規追加されたサブシーン
        public bool IsChanged => WantInBuildSettings != WasInBuildSettings || WantLoadOnStart != WasLoadedOnStart;
    }

    private readonly List<Row> _rows = new List<Row>();

    // subScenes に載っているが Additive フォルダに実体が無い名前（シーンのリネーム/削除後の取り残し）
    private readonly List<string> _staleEntries = new List<string>();

    private MultiSceneLoader _loader;
    private string _error;
    private Vector2 _scroll;

    [MenuItem("Tools/Multi-Scene/サブシーン構成を同期...")]
    public static void Open()
    {
        var window = GetWindow<MultiSceneSetupWindow>(true, "Multi-Scene 構成の同期");
        window.minSize = new Vector2(560f, 300f);
        window.Refresh();
        window.Show();
    }

    // 走査し直すとチェックボックスの編集内容が消えるため、OnFocus では自動更新しない。
    // シーンを増やした直後などは「再読み込み」ボタンを押すこと。
    private void Refresh()
    {
        _rows.Clear();
        _staleEntries.Clear();
        _error = null;
        _loader = null;

        if (!AssetDatabase.IsValidFolder(SubSceneFolder))
        {
            _error = $"サブシーンフォルダ '{SubSceneFolder}' が存在しません。";
            return;
        }

        // ルートシーンが開かれていないと MultiSceneLoader を編集できない
        var loaders = Object.FindObjectsByType<MultiSceneLoader>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (loaders.Length == 0)
        {
            _error = $"MultiSceneLoader が見つかりません。ルートシーン '{Path.GetFileName(RootScenePath)}' を開いてください。";
            return;
        }
        if (loaders.Length > 1)
        {
            _error = $"MultiSceneLoader が {loaders.Length} 個見つかりました。ルートシーンに1つだけ置いてください。";
            return;
        }
        _loader = loaders[0];

        // 現在の subScenes を読み取る（sceneName -> 起動時にロードされるか）
        var loaderState = new Dictionary<string, bool>();
        var serialized = new SerializedObject(_loader);
        var list = serialized.FindProperty("subScenes");
        for (int i = 0; i < list.arraySize; i++)
        {
            var element = list.GetArrayElementAtIndex(i);
            var name = element.FindPropertyRelative("sceneName").stringValue;
            if (string.IsNullOrEmpty(name)) continue;
            loaderState[name] = !element.FindPropertyRelative("disableOnStart").boolValue;
        }

        var buildScenes = EditorBuildSettings.scenes;

        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { SubSceneFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var sceneName = Path.GetFileNameWithoutExtension(path);
            var entry = buildScenes.FirstOrDefault(s => s.path == path);

            bool inBuild = entry != null && entry.enabled;
            bool onStart = loaderState.TryGetValue(sceneName, out var enabled) && enabled;
            bool isNew = entry == null;

            _rows.Add(new Row
            {
                Path = path,
                SceneName = sceneName,
                WasInBuildSettings = inBuild,
                WasLoadedOnStart = onStart,
                // 新規サブシーンは「登録して起動時にロードする」を既定にする。
                // 既存シーンは現状維持（意図的に外してあるものを勝手に有効化しない）。
                WantInBuildSettings = inBuild || isNew,
                WantLoadOnStart = onStart || isNew,
                IsNew = isNew,
            });
            loaderState.Remove(sceneName);
        }

        _rows.Sort((a, b) => string.CompareOrdinal(a.SceneName, b.SceneName));
        _staleEntries.AddRange(loaderState.Keys);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField($"サブシーンフォルダ: {SubSceneFolder}", EditorStyles.miniLabel);

        if (_error != null)
        {
            EditorGUILayout.HelpBox(_error, MessageType.Error);
            if (GUILayout.Button("ルート + 全サブシーンを開く"))
            {
                MultiSceneEditorTools.OpenAll();
                Refresh();
            }
            return;
        }

        EditorGUILayout.HelpBox(
            "「起動時にロード」は MultiSceneLoader.subScenes への登録です。チェックすると Build Settings にも自動で登録されます。\n" +
            "新しく追加されたサブシーン (NEW) は既定で両方オンになっています。",
            MessageType.Info);

        EditorGUILayout.Space(2f);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUILayout.LabelField("シーン", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("Build Settings", EditorStyles.miniBoldLabel, GUILayout.Width(100f));
            EditorGUILayout.LabelField("起動時にロード", EditorStyles.miniBoldLabel, GUILayout.Width(100f));
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (var row in _rows)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var label = row.IsNew ? $"{row.SceneName}   (NEW)" : row.SceneName;
                var style = row.IsChanged ? EditorStyles.boldLabel : EditorStyles.label;
                EditorGUILayout.LabelField(label, style);

                // 起動時にロードするなら Build Settings 登録は必須なので、その場合は固定表示にする
                using (new EditorGUI.DisabledScope(row.WantLoadOnStart))
                {
                    row.WantInBuildSettings = EditorGUILayout.Toggle(
                        row.WantLoadOnStart || row.WantInBuildSettings, GUILayout.Width(100f));
                }
                row.WantLoadOnStart = EditorGUILayout.Toggle(row.WantLoadOnStart, GUILayout.Width(100f));
            }
        }
        EditorGUILayout.EndScrollView();

        if (_staleEntries.Count > 0)
        {
            EditorGUILayout.HelpBox(
                "MultiSceneLoader に登録されていますが、フォルダに実体が無いシーン名があります。" +
                "リネーム・削除の取り残しの可能性があるため Inspector で確認してください:\n  " +
                string.Join(", ", _staleEntries),
                MessageType.Warning);
        }

        var changed = _rows.Where(r => r.IsChanged).ToList();
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(
            changed.Count == 0 ? "変更はありません。" : $"変更 {changed.Count} 件: {string.Join(", ", changed.Select(r => r.SceneName))}",
            EditorStyles.wordWrappedMiniLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("再読み込み")) Refresh();
            using (new EditorGUI.DisabledScope(changed.Count == 0))
            {
                if (GUILayout.Button("適用")) Apply();
            }
        }
        EditorGUILayout.Space(4f);
    }

    private void Apply()
    {
        // --- 1. Build Settings ---
        var scenes = EditorBuildSettings.scenes.ToList();
        foreach (var row in _rows)
        {
            bool want = row.WantInBuildSettings || row.WantLoadOnStart;
            int index = scenes.FindIndex(s => s.path == row.Path);

            if (want && index < 0) scenes.Add(new EditorBuildSettingsScene(row.Path, true));
            else if (index >= 0) scenes[index].enabled = want;
        }
        EditorBuildSettings.scenes = scenes.ToArray();

        // --- 2. MultiSceneLoader.subScenes ---
        var serialized = new SerializedObject(_loader);
        var list = serialized.FindProperty("subScenes");

        foreach (var row in _rows)
        {
            int index = IndexOfSceneName(list, row.SceneName);
            if (row.WantLoadOnStart)
            {
                if (index < 0)
                {
                    index = list.arraySize;
                    list.InsertArrayElementAtIndex(index);
                    // InsertArrayElementAtIndex は直前の要素を複製するため、両フィールドを必ず明示的に設定する
                    list.GetArrayElementAtIndex(index).FindPropertyRelative("sceneName").stringValue = row.SceneName;
                }
                list.GetArrayElementAtIndex(index).FindPropertyRelative("disableOnStart").boolValue = false;
            }
            else if (index >= 0)
            {
                list.GetArrayElementAtIndex(index).FindPropertyRelative("disableOnStart").boolValue = true;
            }
        }

        // ApplyModifiedProperties は Undo 登録も行う
        serialized.ApplyModifiedProperties();

        var rootScene = _loader.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(rootScene);

        var summary = string.Join("\n", _rows.Where(r => r.IsChanged).Select(r =>
            $"  {r.SceneName}: Build Settings={Mark(r.WantInBuildSettings || r.WantLoadOnStart)} / 起動時ロード={Mark(r.WantLoadOnStart)}"));
        Debug.Log($"[MultiSceneSetupWindow] 構成を同期しました。\n{summary}");

        if (EditorUtility.DisplayDialog(
                "Multi-Scene 構成の同期",
                $"Build Settings を更新し、'{rootScene.name}' の MultiSceneLoader を書き換えました。\n\n" +
                $"{summary}\n\nシーンの変更を確定するには '{rootScene.name}' の保存が必要です。今すぐ保存しますか？",
                "保存する", "あとで保存する"))
        {
            EditorSceneManager.SaveScene(rootScene);
        }

        Refresh();
    }

    private static string Mark(bool value) => value ? "ON" : "OFF";

    private static int IndexOfSceneName(SerializedProperty list, string sceneName)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).FindPropertyRelative("sceneName").stringValue == sceneName)
                return i;
        }
        return -1;
    }
}
