using UnityEngine;

/// <summary>
/// 指定したディスプレイをアプリ起動時 (タイトルシーン Awake) に有効化する。
/// マルチモニタビルドで Display 4 等を表示先として確保しておきたい時にタイトルへ配置する。
/// Display 1 (index 0) は常に有効なので、それ以外 (index 1~7) を列挙する。
///
/// 注意: Unity Editor の Game view は `Display.Activate()` で自動切替されない。
/// エディタ確認時は Window → General → Game で 2 つ目の Game view を開き、
/// Display ドロップダウンを目的の Display に切り替えてください。
/// </summary>
public class DisplayBootstrap : MonoBehaviour
{
    [Tooltip("起動時に Activate するディスプレイのインデックス (0=Display1 は既に有効、3=Display4 など)")]
    public int[] displaysToActivate = new[] { 3 };

    [Tooltip("シーン遷移後も同インスタンスを維持して Display を有効化したまま保つ")]
    public bool dontDestroyOnLoad = true;

    [Tooltip("Activate ログを Console に出力")]
    public bool logEvents = true;

    private void Awake()
    {
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

        if (displaysToActivate == null) return;
        for (int i = 0; i < displaysToActivate.Length; i++)
        {
            int idx = displaysToActivate[i];
            if (idx < 0 || idx >= Display.displays.Length)
            {
                if (logEvents) Debug.LogWarning($"[DisplayBootstrap] Display[{idx}] は範囲外 (Length={Display.displays.Length})。マルチモニタ環境かビルド設定を確認してください", this);
                continue;
            }
            var disp = Display.displays[idx];
            if (disp.active)
            {
                if (logEvents) Debug.Log($"[DisplayBootstrap] Display[{idx}] は既に有効", this);
                continue;
            }
            disp.Activate();
            if (logEvents) Debug.Log($"[DisplayBootstrap] Display[{idx}] を Activate しました", this);
        }
    }
}
