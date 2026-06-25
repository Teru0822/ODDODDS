using TMPro;
using UnityEngine;

/// <summary>
/// UFOキャッチャーの残り時間を7セグメント風フォントで表示する。
///
/// 表示フォーマット: MM:SS.C（例: "01:30.5"）
/// 残り時間が warningThreshold 秒以下になると Glow 色が warningColor に変わる。
/// changeFaceColorOnWarning を有効にすると文字の中の色も変わる。
/// </summary>
public class TimerDisplay : MonoBehaviour
{
    [Header("表示")]
    [Tooltip("タイマーを表示する TextMeshProUGUI コンポーネント")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("色設定")]
    [Tooltip("通常時の文字色・Glow 色")]
    [SerializeField] private Color normalColor = new Color(0f, 1f, 0.27f, 1f);

    [Tooltip("警告時の Glow 色（残り時間が warningThreshold 以下になったとき）")]
    [SerializeField] private Color warningColor = new Color(1f, 0.15f, 0.1f, 1f);

    [Tooltip("警告色に切り替わる残り時間（秒）")]
    [SerializeField, Min(0f)] private float warningThreshold = 10f;

    [Tooltip("ON: 警告時に文字の中の色も warningColor に変更する\nOFF: Glow 色のみ変更し、文字の中は normalColor のまま")]
    [SerializeField] private bool changeFaceColorOnWarning = true;

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

    /// <summary>
    /// isWarning に応じて Face Color と Glow Color を切り替える。
    /// changeFaceColorOnWarning が OFF のとき、Face Color は常に normalColor のまま。
    /// </summary>
    private void ApplyColor(bool isWarning)
    {
        Color glowColor = isWarning ? warningColor : normalColor;
        Color faceColor = (isWarning && changeFaceColorOnWarning) ? warningColor : normalColor;

        timerText.color = faceColor;
        timerText.fontMaterial.SetColor(ShaderUtilities.ID_GlowColor, glowColor);
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
