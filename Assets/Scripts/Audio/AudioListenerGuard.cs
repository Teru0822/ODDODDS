using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// シーン内の AudioListener を常にちょうど1つだけ有効に保つ。
///
/// Multi-Scene 構成では各サブシーンや実行時生成のプレハブがそれぞれ AudioListener を
/// 持ち込むため、Unity が「There are N audio listeners in the scene.」を
/// <b>毎フレーム</b>出力する。警告はスタックトレース収集を伴うため非常に重く、
/// 放置するとフレームレートが崩壊する（実際にほぼフリーズする）。
///
/// この警告はエンジン側が出すものなのでコードから消せず、
/// 有効な AudioListener を1つにする以外に止める方法がない。
///
/// 配置は不要。実行時に自動生成される。
/// </summary>
[DisallowMultipleComponent]
public class AudioListenerGuard : MonoBehaviour
{
    [Tooltip("優先して残す AudioListener。未指定なら Camera.main のものを使います")]
    [SerializeField] private AudioListener _preferred;

    [Tooltip("再スキャンの間隔(秒)。実行中に生成されるプレハブへの対策。0以下で定期スキャンなし")]
    [SerializeField] private float _rescanInterval = 2f;

    [Tooltip("どれを残していくつ無効化したかをConsoleに出力します")]
    [SerializeField] private bool _logEvents = false;

    private static AudioListenerGuard _instance;

    /// <summary>シーン配置なしで動くよう、起動時に自動生成する。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;

        var go = new GameObject("[AudioListenerGuard]");
        go.AddComponent<AudioListenerGuard>();
        DontDestroyOnLoad(go);
    }

    /// <summary>外部から任意のタイミングで整理し直したい場合に呼ぶ。</summary>
    public static void Refresh()
    {
        if (_instance != null) _instance.Apply();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = this;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private IEnumerator Start()
    {
        // サブシーンの加法ロードが終わってから最初の整理を行う
        while (MultiSceneLoader.IsLoadingSubScenes) yield return null;
        Apply();

        // 実行中に生成されるプレハブ（UFOキャッチャー等）が持ち込む分を拾うため低頻度で見張る
        while (_rescanInterval > 0f)
        {
            yield return new WaitForSeconds(_rescanInterval);
            Apply();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

    private void OnSceneUnloaded(Scene scene) => Apply();

    /// <summary>残す1つ以外の AudioListener を無効化する。</summary>
    private void Apply()
    {
        var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (listeners.Length == 0) return;

        AudioListener keep = PickListener(listeners);
        if (keep == null) return;

        int disabledCount = 0;
        foreach (var listener in listeners)
        {
            if (listener == null) continue;

            bool shouldBeEnabled = listener == keep;
            if (listener.enabled == shouldBeEnabled) continue;

            listener.enabled = shouldBeEnabled;
            if (!shouldBeEnabled) disabledCount++;
        }

        if (_logEvents && disabledCount > 0)
        {
            Debug.Log($"[AudioListenerGuard] '{keep.gameObject.name}' を残して {disabledCount} 個を無効化しました", this);
        }
    }

    /// <summary>残すべき AudioListener を決める。</summary>
    private AudioListener PickListener(AudioListener[] listeners)
    {
        // 1. Inspector で明示指定されたもの
        if (IsUsable(_preferred)) return _preferred;

        // 2. メインカメラのもの（カメラが動いても音の定位が付いてくる）
        Camera main = Camera.main;
        if (main != null)
        {
            var onMain = main.GetComponent<AudioListener>();
            if (IsUsable(onMain)) return onMain;
        }

        // 3. すでに有効なもの（切り替えによる音の途切れを避ける）
        foreach (var listener in listeners)
        {
            if (IsUsable(listener) && listener.enabled) return listener;
        }

        // 4. 最後の手段として先頭のもの
        foreach (var listener in listeners)
        {
            if (IsUsable(listener)) return listener;
        }

        return null;
    }

    private static bool IsUsable(AudioListener listener)
    {
        return listener != null && listener.gameObject.activeInHierarchy;
    }
}
