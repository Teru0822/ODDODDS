using System.Collections;
using System.Collections.Generic;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

/// <summary>
/// 起動時に、ゲームウィンドウを「ショートカットをダブルクリックしたモニター」へ移動させる。
///
/// Unity は既定でプライマリモニターにウィンドウを出すため、マルチモニタ環境では
/// 別の画面のショートカットから起動しても主モニターに表示されてしまう。
/// 起動直後はマウスカーソルがダブルクリックした位置にあるので、
/// カーソルが乗っているディスプレイを調べてそこへウィンドウを移す。
///
/// 配置は不要（起動時に自動生成）。エディタでは何もしない。
/// </summary>
[DisallowMultipleComponent]
public class StartupWindowPlacer : MonoBehaviour
{
    /// <summary>ウィンドウ移動を行うか。問題が出たらここを false にすれば既定動作へ戻る。</summary>
    private static readonly bool Enabled = true;

    /// <summary>ウィンドウの準備を待つフレーム数。起動直後は移動に失敗することがある。</summary>
    private const int WarmupFrames = 3;

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

        yield return MoveWindowToCursorDisplay();

        // 一度動かせば役目は終わり
        Destroy(gameObject);
    }

    private IEnumerator MoveWindowToCursorDisplay()
    {
        var layout = new List<DisplayInfo>();
        Screen.GetDisplayLayout(layout);
        Debug.Log($"[StartupWindowPlacer] 認識しているモニター数: {layout.Count}");

        // モニターが1台ならそもそも移動する先がない
        if (layout.Count <= 1)
        {
            Debug.Log("[StartupWindowPlacer] モニターが1台のため移動しません");
            yield break;
        }

        if (!TryGetCursorPosition(out Vector2Int cursor))
        {
            Debug.Log("[StartupWindowPlacer] カーソル位置を取得できないため、ウィンドウ移動を行いません");
            yield break;
        }

        int index = FindDisplayIndexContaining(layout, cursor);
        if (index < 0)
        {
            // 座標系がずれている場合の手掛かりとして、各モニターの領域も出しておく
            for (int i = 0; i < layout.Count; i++)
            {
                RectInt a = layout[i].workArea;
                Debug.Log($"[StartupWindowPlacer]   Display {i + 1}: workArea=({a.xMin},{a.yMin})-({a.xMax},{a.yMax})");
            }
            Debug.Log($"[StartupWindowPlacer] カーソル({cursor.x},{cursor.y}) を含むディスプレイが見つかりませんでした");
            yield break;
        }

        DisplayInfo target = layout[index];
        DisplayInfo current = Screen.mainWindowDisplayInfo;
        Debug.Log($"[StartupWindowPlacer] カーソル({cursor.x},{cursor.y}) → Display {index + 1} ({target.width}x{target.height})");

        // 既に目的のモニターに居るなら何もしない
        if (IsSameDisplay(current, target))
        {
            Debug.Log("[StartupWindowPlacer] 既に目的のモニターに表示されているため移動しません");
            yield break;
        }

        // 全画面のままだとウィンドウが表示中のモニターに固定され、移動要求が無視される。
        // 一度ウィンドウモードへ落としてから移動し、最後に元のモードへ戻す
        FullScreenMode originalMode = Screen.fullScreenMode;
        if (originalMode != FullScreenMode.Windowed)
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            yield return null;
            yield return null;
        }

        // 移動先ディスプレイの左上へ移す
        AsyncOperation move = Screen.MoveMainWindowTo(target, Vector2Int.zero);
        yield return move;

        // 移動後、そのモニターの解像度で元の表示モードに戻す
        Screen.SetResolution(target.width, target.height, originalMode);
        Debug.Log($"[StartupWindowPlacer] Display {index + 1} へ移動しました (mode={originalMode})");
    }

    /// <summary>仮想デスクトップ座標のカーソル位置を含むディスプレイを探す。</summary>
    private static int FindDisplayIndexContaining(List<DisplayInfo> layout, Vector2Int cursor)
    {
        for (int i = 0; i < layout.Count; i++)
        {
            RectInt area = layout[i].workArea;
            bool insideX = cursor.x >= area.xMin && cursor.x < area.xMax;
            bool insideY = cursor.y >= area.yMin && cursor.y < area.yMax;
            if (insideX && insideY) return i;
        }
        return -1;
    }

    /// <summary>同じディスプレイを指しているか。workArea の位置で判定する。</summary>
    private static bool IsSameDisplay(DisplayInfo a, DisplayInfo b)
    {
        return a.workArea.position == b.workArea.position
            && a.width == b.width
            && a.height == b.height;
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

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
#else
    private static bool TryGetCursorPosition(out Vector2Int position)
    {
        // Windows 以外ではカーソルの絶対座標を取得しないため、移動は行わない
        position = default;
        return false;
    }
#endif
}
