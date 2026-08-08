using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 接続台数が足りないディスプレイを指しているカメラ／Canvas を、既定のディスプレイへ退避させる。
///
/// このプロジェクトは筐体を想定してカメラの Target Display を Display 4 / 5 に設定している。
/// モニタがそこまで無い環境で実行すると描画先が存在せず、ロゴの後に真っ暗な画面になる。
/// 起動時に実際の接続台数を見て、範囲外のカメラだけを Display 1 に振り直す。
///
/// 台数が足りている環境では何も変更しないため、筐体では従来どおり複数画面に出力される。
/// 配置は不要（実行時に自動生成）。Inspector で調整したい場合のみ手動アタッチする。
/// </summary>
[DisallowMultipleComponent]
public class DisplayFallback : MonoBehaviour
{
    [Tooltip("退避先のディスプレイ番号 (0 = Display 1)")]
    [SerializeField] private int _fallbackDisplayIndex = 0;

    [Tooltip("退避させないディスプレイ番号 (0 = Display 1)。デバッグ用カメラを置いているディスプレイを指定する。" +
             "既定は 4 = Display 5 (TypeWriterCamera / ConfirmEffectCamera)。" +
             "ここに含めたディスプレイのカメラは、存在しなくてもそのまま放置され描画されない")]
    [SerializeField] private int[] _debugDisplayIndices = { 4 };

    [Tooltip("再スキャンの間隔(秒)。実行中に生成されるカメラへの対策。0以下で定期スキャンなし")]
    [SerializeField] private float _rescanInterval = 2f;

    [Tooltip("振り直したカメラをConsoleに出力する")]
    [SerializeField] private bool _logEvents = true;

    private static DisplayFallback _instance;

    /// <summary>
    /// シーン配置なしで動くよう、起動時に自動生成する。
    /// 手動で配置されたインスタンスがあれば Awake 済みなので生成しない。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;

        var go = new GameObject("[DisplayFallback]");
        go.AddComponent<DisplayFallback>();
        DontDestroyOnLoad(go);
    }

    /// <summary>外部から任意のタイミングで振り直したい場合に呼ぶ。</summary>
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

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private IEnumerator Start()
    {
        // サブシーンのカメラも対象にするため、加法ロードの完了を待ってから最初の走査を行う
        while (MultiSceneLoader.IsLoadingSubScenes) yield return null;
        Apply();

        // 実行中に生成されるプレハブ（UFOキャッチャー等）のカメラを拾うため、低頻度で見張る
        while (_rescanInterval > 0f)
        {
            yield return new WaitForSeconds(_rescanInterval);
            Apply();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

    /// <summary>存在しないディスプレイを指している描画先を退避させる。</summary>
    private void Apply()
    {
        int displayCount = Mathf.Max(1, Display.displays.Length);

        foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cam == null) continue;

            // RenderTexture へ描くカメラ（ATM画面やテレビ）はディスプレイに出ないので対象外
            if (cam.targetTexture != null) continue;

            // デバッグ用ディスプレイのカメラは退避させない。
            // 存在しないディスプレイのまま放置することで、画面には出さないでおく
            if (IsDebugDisplay(cam.targetDisplay)) continue;

            if (cam.targetDisplay < displayCount)
            {
                ActivateIfNeeded(cam.targetDisplay);
                continue;
            }

            if (_logEvents)
            {
                Debug.Log($"[DisplayFallback] '{cam.name}' の Target Display {cam.targetDisplay + 1} は存在しないため " +
                          $"Display {_fallbackDisplayIndex + 1} へ退避しました (接続台数={displayCount})", cam);
            }
            cam.targetDisplay = _fallbackDisplayIndex;
        }

        // Screen Space - Overlay の Canvas はカメラに追従しないため個別に振り直す
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == null) continue;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            if (IsDebugDisplay(canvas.targetDisplay)) continue;
            if (canvas.targetDisplay < displayCount) continue;

            if (_logEvents)
            {
                Debug.Log($"[DisplayFallback] Canvas '{canvas.name}' を Display {_fallbackDisplayIndex + 1} へ退避しました", canvas);
            }
            canvas.targetDisplay = _fallbackDisplayIndex;
        }
    }

    /// <summary>デバッグ用として据え置くディスプレイか。</summary>
    private bool IsDebugDisplay(int index)
    {
        if (_debugDisplayIndices == null) return false;

        for (int i = 0; i < _debugDisplayIndices.Length; i++)
        {
            if (_debugDisplayIndices[i] == index) return true;
        }
        return false;
    }

    /// <summary>存在するが未使用のディスプレイを有効化する。Display 1 は常に有効なので対象外。</summary>
    private static void ActivateIfNeeded(int index)
    {
        if (index <= 0 || index >= Display.displays.Length) return;

        var display = Display.displays[index];
        if (!display.active) display.Activate();
    }
}
