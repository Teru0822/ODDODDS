using TMPro;
using UnityEngine;

/// <summary>
/// UFOキャッチャーの残り時間を7セグメント風フォントで表示する。
///
/// 表示フォーマット: MM:SS.C（例: "01:30.5"）
/// 残り時間が warningThreshold 秒以下になると文字色が warningColor に変わる。
///
/// セットアップ:
///   1. World Space Canvas の子に TextMeshProUGUI を置き、DSEG7 フォントを割り当てる
///   2. このスクリプトを任意の GameObject にアタッチし timerText を Inspector で設定する
///   3. UFOCameraController が存在しない間は "--:--.--" を表示する
/// </summary>
public class TimerDisplay : MonoBehaviour
{
    [Header("表示")]
    [Tooltip("タイマーを表示する TextMeshProUGUI コンポーネント")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("色設定")]
    [Tooltip("通常時の文字色（発光感を出すには明るい緑を推奨）")]
    [SerializeField] private Color normalColor = new Color(0f, 1f, 0.27f, 1f);

    [Tooltip("警告時の文字色（残り時間が warningThreshold 以下になったとき）")]
    [SerializeField] private Color warningColor = new Color(1f, 0.15f, 0.1f, 1f);

    [Tooltip("警告色に切り替わる残り時間（秒）")]
    [SerializeField, Min(0f)] private float warningThreshold = 10f;

    [Header("非プレイ中の表示")]
    [Tooltip("UFOCameraController が存在しない・プレイ中でないときに表示するテキスト")]
    [SerializeField] private string idleText = "--:--.--";

    // -----------------------------------------------------------------------
    // Unity ライフサイクル
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (timerText == null)
            Debug.LogWarning("[TimerDisplay] timerText が未設定です。Inspector で TextMeshProUGUI を割り当ててください。");
    }

    private void Update()
    {
        if (timerText == null) return;

        float remaining = GetRemainingTime();

        if (remaining < 0f)
        {
            // UFOCameraController が存在しない or 非プレイ中
            timerText.text  = idleText;
            timerText.color = normalColor;
            return;
        }

        remaining = Mathf.Max(0f, remaining);

        timerText.text  = FormatTime(remaining);
        timerText.color = remaining <= warningThreshold ? warningColor : normalColor;
    }

    // -----------------------------------------------------------------------
    // 内部ロジック
    // -----------------------------------------------------------------------

    /// <summary>
    /// UFOCameraController から残り時間を取得する。
    /// Instance が null またはプレイ中でない場合は -1 を返す。
    /// </summary>
    private static float GetRemainingTime()
    {
        var ctrl = UFOCameraController.Instance;
        if (ctrl == null) return -1f;
        if (!UFOCameraController.IsPlaySessionActive) return -1f;
        return ctrl.RemainingTime;
    }

    /// <summary>float 秒を "MM:SS.C" 形式の文字列に変換する。</summary>
    private static string FormatTime(float t)
    {
        int minutes  = (int)(t / 60f);
        int seconds  = (int)(t % 60f);
        int tenths   = (int)(t * 10f) % 10;
        return $"{minutes:00}:{seconds:00}.{tenths}";
    }
}
