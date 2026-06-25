using TMPro;
using UnityEngine;

/// <summary>
/// UFOキャッチャーの残り時間を7セグメント風フォントで表示する。
///
/// 表示フォーマット: MM:SS.C（例: "01:30.5"）
/// 残り時間が warningThreshold 秒以下になると警告時の色に切り替わる。
/// 通常時・警告時それぞれの文字内色（Face）と発光色（Glow）を Inspector で個別に設定できる。
/// </summary>
public class TimerDisplay : MonoBehaviour
{
    [Header("表示")]
    [Tooltip("タイマーを表示する TextMeshProUGUI コンポーネント")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("通常時の色")]
    [Tooltip("通常時の文字の中の色")]
    [SerializeField] private Color normalFaceColor = new Color(0f, 1f, 0.27f, 1f);

    [Tooltip("通常時の発光（Glow）色")]
    [SerializeField] private Color normalGlowColor = new Color(0f, 1f, 0.27f, 1f);

    [Header("警告時の色（残り warningThreshold 秒以下）")]
    [Tooltip("警告時の文字の中の色")]
    [SerializeField] private Color warningFaceColor = Color.black;

    [Tooltip("警告時の発光（Glow）色")]
    [SerializeField] private Color warningGlowColor = new Color(1f, 0.15f, 0.1f, 1f);

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
        ApplyColor(false);
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
        ApplyColor(warning);
    }

    private void ApplyColor(bool isWarning)
    {
        timerText.color = isWarning ? warningFaceColor : normalFaceColor;
        timerText.fontMaterial.SetColor(ShaderUtilities.ID_GlowColor, isWarning ? warningGlowColor : normalGlowColor);
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
