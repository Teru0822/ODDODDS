using TMPro;
using UnityEngine;

/// <summary>
/// UFOキャッチャーのタイマーをゲーム情報ディスプレイとして表示する。
///
/// 表示ステート:
///   Hidden       — UFO モード外（非表示）
///   RoundDisplay — ラウンド番号 (ROUND X / Y)
///   PlayInfo     — プレイ回数 (PLAY Z / W)
///   LimitTime    — 制限時間プレビュー (LIMIT TIME / 0:SS)
///   Countdown    — アクティブなカウントダウン MM:SS.C
///   Finish       — セッション終了 (FINISH)
///
/// 上段ラベル (LabelText) は未割り当て時に Awake() で自動生成する。
/// </summary>
public class TimerDisplay : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // 列挙体
    // -----------------------------------------------------------------------

    enum DisplayState
    {
        Hidden,
        RoundDisplay,
        PlayInfo,
        LimitTime,
        Countdown,
        Finish,
    }

    // -----------------------------------------------------------------------
    // Inspector フィールド
    // -----------------------------------------------------------------------

    [Header("表示")]
    [Tooltip("タイマーを表示する TextMeshProUGUI コンポーネント（下段メイン）")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Tooltip("上段ラベル用 TMP（未設定時は Awake で自動生成）")]
    [SerializeField] private TextMeshProUGUI labelText;

    [Header("ラウンド設定")]
    [Tooltip("ラウンドの最大数。0 にすると分母を非表示")]
    [SerializeField] private int maxRound = 3;

    [Header("通常時の色")]
    [SerializeField] private Color normalFaceColor = new Color(0f, 1f, 0.27f, 1f);
    [SerializeField] private Color normalGlowColor = new Color(0f, 1f, 0.27f, 1f);

    [Header("警告時の色（残り warningThreshold 秒以下）")]
    [SerializeField] private Color warningFaceColor = Color.black;
    [SerializeField] private Color warningGlowColor = new Color(1f, 0.15f, 0.1f, 1f);
    [SerializeField, Min(0f)] private float warningThreshold = 10f;

    [Header("制限時間表示の色")]
    [SerializeField] private Color limitTimeColor = new Color(1f, 0.7f, 0.1f, 1f);

    [Header("FINISH の色")]
    [SerializeField] private Color finishColor = new Color(0.8f, 1f, 1f, 1f);

    [Header("ノイズ演出 — ちらつき")]
    [SerializeField] private bool enableFlicker = true;
    [SerializeField, Min(0f)] private float flickerRate = 2f;
    [SerializeField, Range(0f, 1f)] private float flickerMinBrightness = 0.05f;
    [SerializeField, Min(0f)] private float flickerDuration = 0.07f;

    [Header("ノイズ演出 — Glow 揺らぎ")]
    [SerializeField] private bool enableGlowFluctuation = true;
    [SerializeField, Min(0f)] private float glowFluctuationSpeed = 1.2f;
    [SerializeField, Min(0f)] private float baseGlowOuter = 0.4f;
    [SerializeField, Range(0f, 1f)] private float glowFluctuationAmount = 0.3f;

    [Header("非プレイ中の表示")]
    [SerializeField] private string idleText = "00:00.0";

    // -----------------------------------------------------------------------
    // 定数
    // -----------------------------------------------------------------------

    const float FadeDuration         = 0.15f;
    const float RoundDisplayDuration = 1.5f;
    const float PlayInfoDuration     = 1.5f;
    const float FinishDuration       = 2.0f;

    // -----------------------------------------------------------------------
    // 内部状態
    // -----------------------------------------------------------------------

    private DisplayState _state       = DisplayState.Hidden;
    private DisplayState _pendingState;
    private bool         _isFadingOut = false;
    private float        _fadeTimer   = 0f;
    private float        _stateTimer  = 0f;

    private float       _flickerTimer;
    private float       _noiseOffset;
    private CanvasGroup _canvasGroup;
    private GameObject  _displayRoot;

    private bool _lastIsPlayingUfo;
    private bool _lastIsSessionActive;

    // -----------------------------------------------------------------------
    // Unity ライフサイクル
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (timerText == null)
        {
            Debug.LogWarning("[TimerDisplay] timerText が未設定です。");
            return;
        }

        _noiseOffset = Random.Range(0f, 100f);
        _displayRoot = timerText.transform.parent.gameObject;

        _canvasGroup = _displayRoot.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = _displayRoot.AddComponent<CanvasGroup>();

        ApplyAlpha(0f);

        if (labelText == null)
            labelText = CreateLabelText();

        _lastIsPlayingUfo    = UFOCameraController.IsPlayingUfo;
        _lastIsSessionActive = UFOCameraController.IsPlaySessionActive;
    }

    private void Update()
    {
        bool isPlayingUfo    = UFOCameraController.IsPlayingUfo;
        bool isSessionActive = UFOCameraController.IsPlaySessionActive;

        // UFO モード離脱 → 即 Hidden
        if (_lastIsPlayingUfo && !isPlayingUfo)
        {
            _lastIsPlayingUfo    = false;
            _lastIsSessionActive = false;
            TransitionTo(DisplayState.Hidden);
            return;
        }

        // UFO モード開始 → RoundDisplay へ
        if (!_lastIsPlayingUfo && isPlayingUfo)
        {
            _lastIsPlayingUfo = true;
            TransitionTo(DisplayState.RoundDisplay);
        }

        // セッション開始（LimitTime・PlayInfo 等どこからでも）→ Countdown へ
        if (_state != DisplayState.Countdown && _state != DisplayState.Finish && _state != DisplayState.Hidden
            && !_lastIsSessionActive && isSessionActive)
            TransitionTo(DisplayState.Countdown);

        // Countdown 中にセッション終了 → Finish へ
        if (_state == DisplayState.Countdown && _lastIsSessionActive && !isSessionActive)
            TransitionTo(DisplayState.Finish);

        _lastIsSessionActive = isSessionActive;
        _lastIsPlayingUfo    = isPlayingUfo;

        UpdateFade();

        if (_stateTimer > 0f)
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f)
                OnAutoTransition();
        }

        if (_state == DisplayState.Countdown)
            UpdateFlicker();
    }

    private void LateUpdate()
    {
        if (_state == DisplayState.Countdown && _fadeTimer <= 0f)
            UpdateCountdownText();
    }

    // -----------------------------------------------------------------------
    // ステート遷移
    // -----------------------------------------------------------------------

    void TransitionTo(DisplayState next)
    {
        if (_state == next && next != DisplayState.RoundDisplay) return;

        _pendingState = next;

        if (_state == DisplayState.Hidden || next == DisplayState.Hidden)
        {
            ApplyState(next);
        }
        else
        {
            _isFadingOut = true;
            _fadeTimer   = FadeDuration;
        }
    }

    void ApplyState(DisplayState next)
    {
        _state      = next;
        _stateTimer = 0f;

        switch (next)
        {
            case DisplayState.Hidden:
                ApplyAlpha(0f);
                SetTexts("", "");
                break;

            case DisplayState.RoundDisplay:
                ApplyAlpha(1f);
                ApplyColorForState(next);
                RefreshRoundDisplay();
                _stateTimer = RoundDisplayDuration;
                break;

            case DisplayState.PlayInfo:
                ApplyAlpha(1f);
                ApplyColorForState(next);
                RefreshPlayInfo();
                _stateTimer = PlayInfoDuration;
                break;

            case DisplayState.LimitTime:
                ApplyAlpha(1f);
                ApplyColorForState(next);
                RefreshLimitTime();
                break;

            case DisplayState.Countdown:
                ApplyAlpha(1f);
                SetTexts("", idleText);
                UpdateCountdownText();
                break;

            case DisplayState.Finish:
                ApplyAlpha(1f);
                ApplyColorForState(next);
                SetTexts("", "FINISH");
                _stateTimer = FinishDuration;
                break;
        }
    }

    void OnAutoTransition()
    {
        switch (_state)
        {
            case DisplayState.RoundDisplay:
                TransitionTo(DisplayState.PlayInfo);
                break;

            case DisplayState.PlayInfo:
                TransitionTo(DisplayState.LimitTime);
                break;

            case DisplayState.Finish:
                if (UFOCameraController.IsPlayingUfo)
                    TransitionTo(DisplayState.RoundDisplay);
                else
                    TransitionTo(DisplayState.Hidden);
                break;
        }
    }

    // -----------------------------------------------------------------------
    // フェードアニメーション
    // -----------------------------------------------------------------------

    void UpdateFade()
    {
        if (_fadeTimer <= 0f) return;

        _fadeTimer -= Time.deltaTime;

        if (_isFadingOut)
        {
            float t = Mathf.Clamp01(_fadeTimer / FadeDuration);
            ApplyAlpha(t);

            if (_fadeTimer <= 0f)
            {
                ApplyState(_pendingState);
                if (_pendingState != DisplayState.Hidden)
                {
                    _isFadingOut = false;
                    _fadeTimer   = FadeDuration;
                }
            }
        }
        else
        {
            float t = 1f - Mathf.Clamp01(_fadeTimer / FadeDuration);
            ApplyAlpha(t);
        }
    }

    // -----------------------------------------------------------------------
    // テキスト更新
    // -----------------------------------------------------------------------

    void RefreshRoundDisplay()
    {
        var round   = RoundManager.Instance;
        int current = round != null ? round.currentRound : 1;
        string main = maxRound > 0 ? $" {current} / {maxRound}" : $" {current}";
        SetTexts("ROUND", main);
    }

    void RefreshPlayInfo()
    {
        var ctrl = UFOCameraController.Instance;
        if (ctrl == null) { SetTexts("PLAY", " - / -"); return; }
        int played = ctrl.PaymentCount;
        int max    = ctrl.MaxPlayCount;
        SetTexts("PLAY", $" {played} / {max}");
    }

    void RefreshLimitTime()
    {
        var ctrl      = UFOCameraController.Instance;
        float duration = ctrl != null ? ctrl.PlayDuration : 30f;
        int totalSec   = Mathf.RoundToInt(duration);
        int m = totalSec / 60;
        int s = totalSec % 60;
        string timeStr = m > 0 ? $"{m}:{s:00}" : $" 0:{s:00}";
        SetTexts("LIMIT  TIME", timeStr);
    }

    void UpdateCountdownText()
    {
        if (timerText == null) return;

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
            timerText.text = FormatCountdown(remaining);
            isWarning = remaining <= warningThreshold;
        }

        ApplyCountdownColor(isWarning);
    }

    // -----------------------------------------------------------------------
    // ちらつき（Countdown 専用）
    // -----------------------------------------------------------------------

    void UpdateFlicker()
    {
        if (!enableFlicker) return;

        if (_flickerTimer > 0f)
            _flickerTimer -= Time.deltaTime;
        else if (Random.value < flickerRate * Time.deltaTime)
            _flickerTimer = Random.Range(flickerDuration * 0.5f, flickerDuration * 1.5f);
    }

    // -----------------------------------------------------------------------
    // 色・Glow 制御
    // -----------------------------------------------------------------------

    void ApplyColorForState(DisplayState state)
    {
        if (timerText == null) return;

        Color faceColor, labelColor;
        Color glowColor;

        switch (state)
        {
            case DisplayState.LimitTime:
                faceColor  = limitTimeColor;
                labelColor = limitTimeColor;
                glowColor  = limitTimeColor;
                break;
            case DisplayState.Finish:
                faceColor  = finishColor;
                labelColor = finishColor;
                glowColor  = finishColor;
                break;
            default:
                faceColor  = normalFaceColor;
                labelColor = normalFaceColor;
                glowColor  = normalGlowColor;
                break;
        }

        timerText.color = faceColor;
        if (labelText != null) labelText.color = labelColor;

        if (timerText.fontMaterial != null)
            timerText.fontMaterial.SetColor(ShaderUtilities.ID_GlowColor, glowColor);
    }

    void ApplyCountdownColor(bool isWarning)
    {
        if (timerText == null) return;

        Color faceColor = isWarning ? warningFaceColor : normalFaceColor;
        Color glowColor = isWarning ? warningGlowColor : normalGlowColor;

        if (enableFlicker && _flickerTimer > 0f)
        {
            float b = flickerMinBrightness;
            faceColor = new Color(faceColor.r * b, faceColor.g * b, faceColor.b * b, faceColor.a);
            glowColor = new Color(glowColor.r * b, glowColor.g * b, glowColor.b * b, glowColor.a * b);
        }

        timerText.color = faceColor;

        if (timerText.fontMaterial != null)
        {
            timerText.fontMaterial.SetColor(ShaderUtilities.ID_GlowColor, glowColor);

            if (enableGlowFluctuation)
            {
                float noise       = Mathf.PerlinNoise(Time.time * glowFluctuationSpeed, _noiseOffset);
                float fluctuation = (noise * 2f - 1f) * glowFluctuationAmount;
                float glowOuter   = Mathf.Max(0f, baseGlowOuter * (1f + fluctuation));
                timerText.fontMaterial.SetFloat(ShaderUtilities.ID_GlowOuter, glowOuter);
            }
        }
    }

    // -----------------------------------------------------------------------
    // ユーティリティ
    // -----------------------------------------------------------------------

    void SetTexts(string label, string main)
    {
        if (labelText != null) labelText.text = label;
        if (timerText  != null) timerText.text  = main;
    }

    void ApplyAlpha(float alpha)
    {
        if (_canvasGroup == null) return;
        _canvasGroup.alpha          = alpha;
        _canvasGroup.blocksRaycasts = alpha > 0.5f;
    }

    static float GetRemainingTime()
    {
        var ctrl = UFOCameraController.Instance;
        if (ctrl == null || !UFOCameraController.IsPlaySessionActive) return -1f;
        return ctrl.RemainingTime;
    }

    static string FormatCountdown(float t)
    {
        int minutes = (int)(t / 60f);
        int seconds = (int)(t % 60f);
        int tenths  = (int)(t * 10f) % 10;
        return $"{minutes:00}:{seconds:00}.{tenths}";
    }

    TextMeshProUGUI CreateLabelText()
    {
        var go = new GameObject("LabelText", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(timerText.transform.parent, false);
        go.transform.SetSiblingIndex(timerText.transform.GetSiblingIndex());

        var rt       = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.6f);
        rt.anchorMax = new Vector2(1f, 1.0f);
        rt.sizeDelta = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var tmp       = go.GetComponent<TextMeshProUGUI>();
        tmp.font      = timerText.font;
        tmp.fontSize  = timerText.fontSize * 0.4f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = timerText.color;
        tmp.text      = "";

        // timerText を下 60 % 領域に収める
        var mainRt       = timerText.GetComponent<RectTransform>();
        mainRt.anchorMin = new Vector2(0f, 0f);
        mainRt.anchorMax = new Vector2(1f, 0.65f);
        mainRt.offsetMin = Vector2.zero;
        mainRt.offsetMax = Vector2.zero;

        return tmp;
    }
}
