using TMPro;
using UnityEngine;

/// <summary>
/// UFOキャッチャーの残り時間を7セグメント風フォントで表示する。
///
/// 表示フォーマット: MM:SS.C（例: "01:30.5"）
/// 残り時間が warningThreshold 秒以下になると警告時の色に切り替わる。
/// ちらつき（A）と Glow 揺らぎ（C）のノイズ演出付き。
/// hideUntilStart を有効にするとプレイ開始まで非表示になる。
/// </summary>
public class TimerDisplay : MonoBehaviour
{
    [Header("表示")]
    [Tooltip("タイマーを表示する TextMeshProUGUI コンポーネント")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("表示タイミング")]
    [Tooltip("プレイセッションが開始するまでタイマーを非表示にする")]
    [SerializeField] private bool hideUntilStart = true;
    [Tooltip("表示/非表示の切り替え対象 GameObject（Canvas や Panel など）。\n" +
             "未設定の場合は timerText の親 GameObject を自動使用。")]
    [SerializeField] private GameObject displayRoot;

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

    [Header("ノイズ演出 — ちらつき")]
    [Tooltip("ちらつき演出を有効にする")]
    [SerializeField] private bool enableFlicker = true;

    [Tooltip("1秒あたりの平均ちらつき回数")]
    [SerializeField, Min(0f)] private float flickerRate = 2f;

    [Tooltip("ちらつき時の最小輝度（0=完全消灯、1=変化なし）")]
    [SerializeField, Range(0f, 1f)] private float flickerMinBrightness = 0.05f;

    [Tooltip("ちらつきの持続時間（秒）。実際はこの値を中心にランダムにばらつく")]
    [SerializeField, Min(0f)] private float flickerDuration = 0.07f;

    [Header("ノイズ演出 — Glow 揺らぎ")]
    [Tooltip("Glow 揺らぎ演出を有効にする")]
    [SerializeField] private bool enableGlowFluctuation = true;

    [Tooltip("Glow 揺らぎの速さ。大きいほど激しく変動する")]
    [SerializeField, Min(0f)] private float glowFluctuationSpeed = 1.2f;

    [Tooltip("Glow Outer の基準値。マテリアルの Glow Outer 設定値に合わせる")]
    [SerializeField, Min(0f)] private float baseGlowOuter = 0.4f;

    [Tooltip("Glow 揺らぎの振れ幅（0=揺らぎなし、0.5=基準値の±50%）")]
    [SerializeField, Range(0f, 1f)] private float glowFluctuationAmount = 0.3f;

    [Header("非プレイ中の表示")]
    [Tooltip("UFOCameraController が存在しない・プレイ中でないときに表示するテキスト")]
    [SerializeField] private string idleText = "00:00.0";

    // -----------------------------------------------------------------------
    // 内部状態
    // -----------------------------------------------------------------------

    private float        _flickerTimer;
    private float        _noiseOffset;
    private CanvasGroup  _canvasGroup;
    private bool         _lastSessionState;

    // -----------------------------------------------------------------------
    // Unity ライフサイクル
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (timerText == null)
            Debug.LogWarning("[TimerDisplay] timerText が未設定です。Inspector で TextMeshProUGUI を割り当ててください。");

        _noiseOffset = Random.Range(0f, 100f);

        // displayRoot が未設定の場合は timerText の親を使う
        if (displayRoot == null && timerText != null)
            displayRoot = timerText.transform.parent.gameObject;

        // CanvasGroup で alpha 制御（SetActive より安全: Update が止まらない）
        if (displayRoot != null)
        {
            _canvasGroup = displayRoot.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = displayRoot.AddComponent<CanvasGroup>();
        }

        _lastSessionState = IsSessionCurrentlyActive();
        if (hideUntilStart)
            ApplyVisibility(_lastSessionState);
    }

    private void Update()
    {
        // 表示タイミング制御
        if (hideUntilStart)
        {
            bool current = IsSessionCurrentlyActive();
            if (current != _lastSessionState)
            {
                _lastSessionState = current;
                ApplyVisibility(current);
            }
        }

        if (timerText == null) return;

        UpdateFlicker();

        float remaining = GetRemainingTime();
        bool isWarning;

        if (remaining < 0f)
        {
            timerText.text = idleText;
            isWarning = false;
        }
        else
        {
            remaining = Mathf.Max(0f, remaining);
            timerText.text = FormatTime(remaining);
            isWarning = remaining <= warningThreshold;
        }

        ApplyColor(isWarning);
    }

    // -----------------------------------------------------------------------
    // 表示制御
    // -----------------------------------------------------------------------

    private void ApplyVisibility(bool visible)
    {
        if (_canvasGroup == null) return;
        _canvasGroup.alpha          = visible ? 1f : 0f;
        _canvasGroup.blocksRaycasts = visible;
    }

    private static bool IsSessionCurrentlyActive()
    {
        var ctrl = UFOCameraController.Instance;
        return ctrl != null && UFOCameraController.IsPlaySessionActive;
    }

    // -----------------------------------------------------------------------
    // ちらつき
    // -----------------------------------------------------------------------

    private void UpdateFlicker()
    {
        if (!enableFlicker) return;

        if (_flickerTimer > 0f)
        {
            _flickerTimer -= Time.deltaTime;
        }
        else if (Random.value < flickerRate * Time.deltaTime)
        {
            // 持続時間をランダムにばらつかせてより自然に見せる
            _flickerTimer = Random.Range(flickerDuration * 0.5f, flickerDuration * 1.5f);
        }
    }

    // -----------------------------------------------------------------------
    // 色・Glow 制御
    // -----------------------------------------------------------------------

    private void ApplyColor(bool isWarning)
    {
        Color faceColor = isWarning ? warningFaceColor : normalFaceColor;
        Color glowColor = isWarning ? warningGlowColor : normalGlowColor;

        // ちらつき：輝度を下げる
        if (enableFlicker && _flickerTimer > 0f)
        {
            float b = flickerMinBrightness;
            faceColor = new Color(faceColor.r * b, faceColor.g * b, faceColor.b * b, faceColor.a);
            glowColor = new Color(glowColor.r * b, glowColor.g * b, glowColor.b * b, glowColor.a * b);
        }

        timerText.color = faceColor;
        timerText.fontMaterial.SetColor(ShaderUtilities.ID_GlowColor, glowColor);

        // Glow 揺らぎ：Perlin ノイズで Glow Outer を変動させる
        if (enableGlowFluctuation)
        {
            float noise       = Mathf.PerlinNoise(Time.time * glowFluctuationSpeed, _noiseOffset);
            float fluctuation = (noise * 2f - 1f) * glowFluctuationAmount;
            float glowOuter   = Mathf.Max(0f, baseGlowOuter * (1f + fluctuation));
            timerText.fontMaterial.SetFloat(ShaderUtilities.ID_GlowOuter, glowOuter);
        }
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
