using System.Collections;
using System.Collections.Generic;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
#endif
using UnityEngine;

/// <summary>
/// 起動時に、ゲームウィンドウを「ショートカットをダブルクリックしたモニター」へ移動させる。
///
/// Unity は前回の表示モニター（レジストリの UnitySelectMonitor）や既定でプライマリへ
/// ウィンドウを出すため、マルチモニタ環境では別の画面のショートカットから起動しても
/// 意図しないモニターに表示されてしまう。
/// 起動直後はマウスカーソルがダブルクリックした位置にあるので、
/// カーソルが乗っているモニターを調べてそこへウィンドウを移す。
///
/// Unity のディスプレイ情報を使わない理由:
/// 環境によって Screen.GetDisplayLayout() が全モニターに同じ workArea (0,0)-(幅,高さ) を
/// 返すことがあり、位置での突き合わせも Screen.mainWindowDisplayInfo による現在地判定も
/// 当てにならない。そのため移動先の判定・現在地の判定・移動そのものを Windows API で行い、
/// Unity の API は最後の手段としてのみ使う。
///
/// 配置は不要（起動時に自動生成）。エディタと Windows 以外では何もしない。
/// </summary>
[DisallowMultipleComponent]
public class StartupWindowPlacer : MonoBehaviour
{
    /// <summary>ウィンドウ移動を行うか。問題が出たらここを false にすれば既定動作へ戻る。</summary>
    private static readonly bool Enabled = true;

    /// <summary>ウィンドウの準備を待つフレーム数。起動直後は移動に失敗することがある。</summary>
    private const int WarmupFrames = 5;

    /// <summary>移動を要求してから位置が落ち着くまで待つフレーム数。</summary>
    private const int SettleFrames = 5;

    /// <summary>表示モード切替の反映を待つ最大フレーム数。</summary>
    private const int ModeChangeTimeoutFrames = 60;

    /// <summary>移動後、Unity 側に位置を戻されていないか見張るフレーム数。</summary>
    private const int WatchdogFrames = 60;

    /// <summary>
    /// 起動直後のカーソル位置（仮想デスクトップ座標）。
    /// ロード中にマウスを動かされても影響を受けないよう、できるだけ早い段階で押さえておく。
    /// </summary>
    private static Vector2Int? _startupCursor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void CaptureStartupCursor()
    {
        if (!Enabled) return;
        if (Application.isEditor) return;

        if (TryGetCursorPosition(out Vector2Int cursor)) _startupCursor = cursor;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Enabled) return;
        if (Application.isEditor) return;   // エディタのGameビューには効かない

        var go = new GameObject("[StartupWindowPlacer]");
        go.AddComponent<StartupWindowPlacer>();
        DontDestroyOnLoad(go);
    }

    private IEnumerator Start()
    {
        for (int i = 0; i < WarmupFrames; i++) yield return null;

        yield return PlaceWindowOnCursorMonitor();

        // 一度動かせば役目は終わり
        Destroy(gameObject);
    }

    private IEnumerator PlaceWindowOnCursorMonitor()
    {
        // 起動時に押さえた位置を使う。取れていなければ今の位置で代用する
        Vector2Int cursor;
        if (_startupCursor.HasValue)
        {
            cursor = _startupCursor.Value;
        }
        else if (!TryGetCursorPosition(out cursor))
        {
            Debug.Log("[StartupWindowPlacer] カーソル位置を取得できないため、ウィンドウ移動を行いません");
            yield break;
        }

        if (!TryPrepareTargetMonitor(cursor, out RectInt bounds, out string device))
        {
            Debug.Log($"[StartupWindowPlacer] カーソル{Format(cursor)} のモニターを特定できないため、ウィンドウ移動を行いません");
            yield break;
        }

        Debug.Log($"[StartupWindowPlacer] カーソル{Format(cursor)} → モニター '{device}' " +
                  $"({bounds.xMin},{bounds.yMin})-({bounds.xMax},{bounds.yMax})");
        Debug.Log($"[StartupWindowPlacer] {DescribeWindow()}");

        if (IsWindowOnTargetMonitor())
        {
            Debug.Log("[StartupWindowPlacer] 既に目的のモニターに表示されているため移動しません");
            yield break;
        }

        // 1) 表示モードを変えずにそのまま移す。ボーダーレス全画面ならこれで足りる
        yield return MoveDirectly(bounds);
        if (IsWindowOnTargetMonitor())
        {
            yield return ConfirmStaysOnTargetMonitor(bounds, "そのまま移動");
            yield break;
        }

        // 2) 全画面に固定されている場合に備え、一度ウィンドウモードへ落としてから移す
        yield return MoveViaWindowedMode(bounds);
        if (IsWindowOnTargetMonitor())
        {
            yield return ConfirmStaysOnTargetMonitor(bounds, "ウィンドウモード経由で移動");
            yield break;
        }

        // 3) Windows API での移動が効かない場合の最後の手段として Unity の API を総当たりする
        yield return MoveUsingUnityDisplays(device);
        if (IsWindowOnTargetMonitor())
        {
            yield return ConfirmStaysOnTargetMonitor(bounds, "Unity の MoveMainWindowTo で移動");
            yield break;
        }

        Debug.LogWarning($"[StartupWindowPlacer] モニター '{device}' へ移動できませんでした / {DescribeWindow()}");
    }

    /// <summary>表示モードを保ったままウィンドウを移す。</summary>
    private IEnumerator MoveDirectly(RectInt bounds)
    {
        if (!TrySetWindowBounds(bounds))
        {
            Debug.LogWarning("[StartupWindowPlacer] ウィンドウの移動要求が失敗しました");
            yield break;
        }

        yield return WaitFrames(SettleFrames);

        // 解像度が違うモニターへ移した場合は Unity 側の描画サイズも合わせる
        if (Screen.width != bounds.width || Screen.height != bounds.height)
        {
            Screen.SetResolution(bounds.width, bounds.height, Screen.fullScreenMode);
            yield return WaitFrames(SettleFrames);
        }
    }

    /// <summary>全画面を一旦解除してから移し、元の表示モードへ戻す。</summary>
    private IEnumerator MoveViaWindowedMode(RectInt bounds)
    {
        FullScreenMode originalMode = Screen.fullScreenMode;
        if (originalMode == FullScreenMode.Windowed)
        {
            // 既にウィンドウモードなら 1) と同じことになるので試す意味がない
            yield break;
        }

        Debug.Log($"[StartupWindowPlacer] {originalMode} のままでは移動できないため、ウィンドウモードを経由します");

        Screen.fullScreenMode = FullScreenMode.Windowed;
        yield return WaitForFullScreenMode(FullScreenMode.Windowed);

        TrySetWindowBounds(bounds);
        yield return WaitFrames(SettleFrames);

        Screen.SetResolution(bounds.width, bounds.height, originalMode);
        yield return WaitForFullScreenMode(originalMode);
        yield return WaitFrames(SettleFrames);
    }

    /// <summary>
    /// Unity の MoveMainWindowTo で移す。どの DisplayInfo が目的のモニターに対応するか
    /// 分からないため、名前が一致するものを優先しつつ順に試し、実際に移動できたかで判断する。
    /// </summary>
    private IEnumerator MoveUsingUnityDisplays(string device)
    {
        var layout = new List<DisplayInfo>();
        Screen.GetDisplayLayout(layout);
        LogLayout(layout);

        foreach (int index in OrderCandidates(layout, device))
        {
            Debug.Log($"[StartupWindowPlacer] Display {index + 1} ('{layout[index].name}') へ移動を試みます");

            AsyncOperation move = Screen.MoveMainWindowTo(layout[index], Vector2Int.zero);
            yield return move;
            yield return WaitFrames(SettleFrames);

            if (IsWindowOnTargetMonitor()) yield break;
        }
    }

    /// <summary>名前が一致する DisplayInfo を先頭に、それ以外を後ろに並べた順序を返す。</summary>
    private static IEnumerable<int> OrderCandidates(List<DisplayInfo> layout, string device)
    {
        for (int i = 0; i < layout.Count; i++)
        {
            if (IsSameDevice(layout[i].name, device)) yield return i;
        }
        for (int i = 0; i < layout.Count; i++)
        {
            if (!IsSameDevice(layout[i].name, device)) yield return i;
        }
    }

    /// <summary>Unity と Windows で同じモニターを指す名前か。表記揺れに備えて部分一致も見る。</summary>
    private static bool IsSameDevice(string unityName, string device)
    {
        if (string.IsNullOrEmpty(unityName) || string.IsNullOrEmpty(device)) return false;
        return unityName == device || unityName.Contains(device) || device.Contains(unityName);
    }

    /// <summary>移動後に Unity 側へ位置を戻されることがあるため、しばらく見張って必要なら戻す。</summary>
    private IEnumerator ConfirmStaysOnTargetMonitor(RectInt bounds, string how)
    {
        Debug.Log($"[StartupWindowPlacer] {how}しました / {DescribeWindow()}");

        for (int i = 0; i < WatchdogFrames; i++)
        {
            yield return null;
            if (IsWindowOnTargetMonitor()) continue;

            Debug.Log("[StartupWindowPlacer] 位置が戻されたため、もう一度移動します");
            TrySetWindowBounds(bounds);
            yield return WaitFrames(SettleFrames);
        }

        Debug.Log($"[StartupWindowPlacer] 配置完了 / {DescribeWindow()}");
    }

    /// <summary>表示モードの切替が実際に反映されるまで待つ。切替は1フレームでは終わらない。</summary>
    private static IEnumerator WaitForFullScreenMode(FullScreenMode mode)
    {
        for (int i = 0; i < ModeChangeTimeoutFrames; i++)
        {
            if (Screen.fullScreenMode == mode)
            {
                // 反映直後はウィンドウ位置が安定していないので1フレーム余裕を持たせる
                yield return null;
                yield break;
            }
            yield return null;
        }

        Debug.LogWarning($"[StartupWindowPlacer] 表示モード {mode} への切替が {ModeChangeTimeoutFrames} フレーム以内に完了しませんでした");
    }

    private static IEnumerator WaitFrames(int count)
    {
        for (int i = 0; i < count; i++) yield return null;
    }

    /// <summary>判定がずれたときに原因を追えるよう、Unity が認識しているモニターを残す。</summary>
    private static void LogLayout(List<DisplayInfo> layout)
    {
        Debug.Log($"[StartupWindowPlacer] Unity が認識しているモニター数: {layout.Count}");
        for (int i = 0; i < layout.Count; i++)
        {
            RectInt area = layout[i].workArea;
            Debug.Log($"[StartupWindowPlacer]   Display {i + 1}: '{layout[i].name}' {layout[i].width}x{layout[i].height} " +
                      $"workArea=({area.xMin},{area.yMin})-({area.xMax},{area.yMax})");
        }
    }

    private static string Format(Vector2Int value) => $"({value.x},{value.y})";

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    /// <summary>移動先モニターのハンドル。ハンドルで比べるので座標系の違いに影響されない。</summary>
    private static IntPtr _targetMonitor = IntPtr.Zero;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeMonitorInfoEx
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Device;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref NativeMonitorInfoEx info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    /// <summary>
    /// 仮想デスクトップ座標でのカーソル位置を取得する。
    /// Input.mousePosition はゲームウィンドウ基準なので、移動先の判定には使えない。
    /// </summary>
    private static bool TryGetCursorPosition(out Vector2Int position)
    {
        if (GetCursorPos(out NativePoint point))
        {
            position = new Vector2Int(point.X, point.Y);
            return true;
        }

        position = default;
        return false;
    }

    /// <summary>カーソルが乗っているモニターを調べ、以後の比較用に覚えておく。</summary>
    private static bool TryPrepareTargetMonitor(Vector2Int cursor, out RectInt bounds, out string device)
    {
        bounds = default;
        device = string.Empty;

        IntPtr monitor = MonitorFromPoint(new NativePoint { X = cursor.x, Y = cursor.y }, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero) return false;

        var info = new NativeMonitorInfoEx { Size = Marshal.SizeOf(typeof(NativeMonitorInfoEx)) };
        if (!GetMonitorInfo(monitor, ref info)) return false;

        _targetMonitor = monitor;
        bounds = new RectInt(info.Monitor.Left, info.Monitor.Top,
                             info.Monitor.Right - info.Monitor.Left,
                             info.Monitor.Bottom - info.Monitor.Top);
        device = info.Device ?? string.Empty;
        return true;
    }

    /// <summary>ゲームウィンドウが目的のモニターに乗っているか。ハンドルの一致で判定する。</summary>
    private static bool IsWindowOnTargetMonitor()
    {
        if (_targetMonitor == IntPtr.Zero) return false;
        if (!TryGetGameWindow(out IntPtr window)) return false;

        return MonitorFromWindow(window, MONITOR_DEFAULTTONEAREST) == _targetMonitor;
    }

    /// <summary>ゲームウィンドウを指定の矩形へ移す。</summary>
    private static bool TrySetWindowBounds(RectInt bounds)
    {
        if (!TryGetGameWindow(out IntPtr window)) return false;

        return SetWindowPos(window, IntPtr.Zero, bounds.xMin, bounds.yMin, bounds.width, bounds.height,
                            SWP_NOZORDER | SWP_NOACTIVATE);
    }

    /// <summary>
    /// 自プロセスのゲームウィンドウを得る。
    /// 読み込み中に他のアプリをクリックされると GetActiveWindow が取れないため、
    /// その場合は前面ウィンドウが自分のものであれば採用する。
    /// </summary>
    private static bool TryGetGameWindow(out IntPtr window)
    {
        window = GetActiveWindow();
        if (window != IntPtr.Zero) return true;

        window = GetForegroundWindow();
        if (window == IntPtr.Zero) return false;

        GetWindowThreadProcessId(window, out uint processId);
        if (processId == GetCurrentProcessId()) return true;

        window = IntPtr.Zero;
        return false;
    }

    /// <summary>ログ用に、OS から見たウィンドウの現在位置を文字列化する。</summary>
    private static string DescribeWindow()
    {
        if (!TryGetGameWindow(out IntPtr window)) return "OS から見たウィンドウ: 取得できません";

        IntPtr monitor = MonitorFromWindow(window, MONITOR_DEFAULTTONEAREST);
        string monitorText = $"monitor=0x{monitor.ToInt64():X}";
        if (monitor != IntPtr.Zero)
        {
            var info = new NativeMonitorInfoEx { Size = Marshal.SizeOf(typeof(NativeMonitorInfoEx)) };
            if (GetMonitorInfo(monitor, ref info)) monitorText = $"monitor='{info.Device}'";
        }

        if (!GetWindowRect(window, out NativeRect rect)) return $"OS から見たウィンドウ: {monitorText}";

        return $"OS から見たウィンドウ: ({rect.Left},{rect.Top})-({rect.Right},{rect.Bottom}) {monitorText}";
    }
#else
    private static bool TryGetCursorPosition(out Vector2Int position)
    {
        // Windows 以外ではカーソルの絶対座標を取得しないため、移動は行わない
        position = default;
        return false;
    }

    private static bool TryPrepareTargetMonitor(Vector2Int cursor, out RectInt bounds, out string device)
    {
        bounds = default;
        device = string.Empty;
        return false;
    }

    private static bool IsWindowOnTargetMonitor() => false;

    private static bool TrySetWindowBounds(RectInt bounds) => false;

    private static string DescribeWindow() => "OS から見たウィンドウ: この環境では取得しません";
#endif
}
