using TMPro;
using UnityEngine;

/// <summary>
/// UFOキャッチャーの残り時間を7セグメント風フォントで表示する。
///
/// 表示フォーマット: MM:SS.C（例: "01:30.5"）
/// 残り時間が warningThreshold 秒以下になると文字色・Glow 色が warningColor に変わる。
///
/// セットアップ:
///   1. World Space Canvas の子に TextMeshProUGUI を置き、DSEG7 フォントを割り当てる
///   2. このスクリプトを任意の GameObject にアタッチし timerText を Inspector で設定する
///   3. UFOCameraController が存在しない間は idleText を表示する
/// </summary>
public class TimerDisplay : MonoBehaviour
{
    [Header("表示")]
    [Tooltip("タイマーを表示する TextMeshProUGUI コンポーネント")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("色設定")]
    [Tooltip("通常時の文字色・Glow 色")]
    [SerializeField] private Color normalColor = new Color(0f, 1f, 0.27f, 1f);

    [Tooltip("警告時の文字色・Glow 色（残り時間が warningThreshold 以下になったとき）")]
    [SerializeField] private Color warningColor = new Color(1f, 0.15f, 0.1f, 1f);

    [Tooltip("警告色に切り替わる残り時間（秒）")]
    [SerializeField, Min(0f)] private float warningThreshold = 10f;

    [Header("非プレイ中の表示")]
    [Tooltip("UFOCameraController が存在しない・プレイ中でないときに表示するテキスト")]
    [SerializeField] private string idleText = "00:00.0";

    // -----------------------------------------------------------------------
    // 内部状態
    // -----------------------------------------------------------------------

    private bool _isWarning = false;

    // -----------------------------------------------------------------------
    // Unity ライフサイクル
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (timerText == null)
        {
            Debug.LogWarning("[TimerDisplay] timerText が未設定です。Inspector で TextMeshProUGUI を割り当ててください。");
            return;
        }
        // fontMaterial はインスタンスを生成するので Start で一度呼ぶことで他に影響しない
        ApplyColor(normalColor);
    }

    private void Update()
    {
        if (timerText == null) return;

        float remaining = GetRemainingTime();

        if (remaining < 0f)
        {
            timerText.text = idleText;
            SetWarning(false);
            return;
        }

        remaining = Mathf.Max(0f, remaining);
        timerText.text = FormatTime(remaining);
        SetWarning(remaining <= warningThreshold);
    }

    // -----------------------------------------------------------------------
    // 色制御
    // -----------------------------------------------------------------------

    private void SetWarning(bool warning)
    {
        if (_isWarning == warning) return;
        _isWarning = warning;
        ApplyColor(warning ? warningColor : normalColor);
    }

    /// <summary>Vertex Color と Glow Color を同時に変更する。</summary>
    private void ApplyColor(Color color)
    {
        // Vertex Color（文字の塗り色）
        timerText.color = color;

        // Glow Color（発光色）— fontMaterial はインスタンスを返すので他の TMP に影響しない
        timerText.fontMaterial.SetColor(ShaderUtilities.ID_GlowColor, color);
    }

    // -----------------------------------------------------------------------
    // 内部ロジック
    // -----------------------------------------------------------------------

    private static float GetRemainingTime()
    {
        var ctrl = UFOCameraController.Instance;
        if (ctrl == null) return -1f;
        if (!UFOCameraController.IsPlaySessionActive) return -1f;
        return ctrl.RemainingTime;
    }

    private static string FormatTime(float t)
    {
        int minutes = (int)(t / 60f);
        int seconds = (int)(t % 60f);
        int tenths  = (int)(t * 10f) % 10;
        return $"{minutes:00}:{seconds:00}.{tenths}";
    }
}
