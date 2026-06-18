using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Multi-Scene 構成の起動ローダー。「ルート（常駐）シーン」に1つだけ配置する。
///
/// 役割:
///   起動時に、設定したサブシーン群を加法(Additive)でロードする。
///   エディタで既にそのサブシーンを開いている場合は二重ロードしない（Play 時の重複防止）。
///   全ロード後、指定したシーンを Active Scene にする（ライティング基準・新規 Instantiate 先になる）。
///
/// 使い方:
///   1. サブシーンを Build Settings (File > Build Profiles > Scene List) に登録する。
///   2. ルートシーン（例: MainScene）にこのコンポーネントを付けた空オブジェクトを置く。
///   3. subScenes にロードしたいサブシーン名を並べる。
///   4. activeSceneAfterLoad に、アクティブにしたいシーン名（通常はルートシーン or 環境シーン）を入れる。
///
/// 注意（重要）: シーンをまたぐ参照（Inspector で別シーンのオブジェクトを直接ドラッグ参照）は
///   保存時に Unity が null にする。サブシーンへ分割する際は、相互参照するオブジェクトは同じ
///   サブシーンにまとめるか、実行時の検索（FindAnyObjectByType / シングルトン / イベント）に切り替えること。
///   詳細は Docs/01_Rules_and_Policies/03_Workflows/MultiSceneSetup.md を参照。
/// </summary>
public class MultiSceneLoader : MonoBehaviour
{
    [System.Serializable]
    public struct SubScene
    {
        [Tooltip("Build Settings に登録したシーン名（拡張子・パスなし）")]
        public string sceneName;

        [Tooltip("チェックすると起動時にロードしない。【既定（オフ）= ロードする】。デバッグで一時的に外したい時だけオンにする。")]
        public bool disableOnStart;
    }

    [Tooltip("起動時に加法ロードするサブシーン群。すべて Build Settings に登録しておくこと。")]
    [SerializeField] private List<SubScene> subScenes = new List<SubScene>();

    [Tooltip("全サブシーンのロード後にアクティブにするシーン名。空ならこのシーンのまま。")]
    [SerializeField] private string activeSceneAfterLoad = "";

    private IEnumerator Start()
    {
        foreach (var s in subScenes)
        {
            if (s.disableOnStart || string.IsNullOrEmpty(s.sceneName)) continue;

            // 既にエディタで加法表示している/ロード済みなら二重に読まない
            if (SceneManager.GetSceneByName(s.sceneName).isLoaded) continue;

            var op = SceneManager.LoadSceneAsync(s.sceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError($"[MultiSceneLoader] シーン '{s.sceneName}' をロードできません。" +
                               "名前の綴りと Build Settings への登録を確認してください。");
                continue;
            }
            yield return op;
        }

        if (!string.IsNullOrEmpty(activeSceneAfterLoad))
        {
            var scene = SceneManager.GetSceneByName(activeSceneAfterLoad);
            if (scene.IsValid() && scene.isLoaded)
                SceneManager.SetActiveScene(scene);
            else
                Debug.LogWarning($"[MultiSceneLoader] アクティブ化対象 '{activeSceneAfterLoad}' が見つかりません。");
        }
    }
}
