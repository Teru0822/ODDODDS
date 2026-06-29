using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// UFOキャッチャーのタイマーをゲーム情報ディスプレイとして表示する。
///
/// 表示ステート:
///   Hidden       — UFO モード外（非表示）
///   RoundDisplay — ラウンド番号 (ROUND X / Y)
///   PlayInfo     — プレイ回数 (PLAY Z / W)
///   LimitTime    — 制限時間プレビュー (LIMIT TIME / 0:SS) ↔ PlayInfo 交互
///   Countdown    — アクティブなカウントダウン MM:SS.C
///   Finish       — セッション終了 (FINISH)
///
/// LabelText・RoundIndicator は未割り当て時に Awake() で自動生成する。
/// </summary>
public class TimerDisplay : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // 列挙体
    // -----------------------------------------------------------------------

    enum DisplayState { Hidden, RoundDisplay, PlayInfo, LimitTime, Countdown, Finish }

    // -----------------------------------------------------------------------
    // Inspector フィールド
    // -----------------------------------------------------------------------

    [Header("表示")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Tooltip("上段ラベル用 TMP（未設定時は Awake で自動生成）")]
    [SerializeField] private TextMeshProUGUI labelText;

    [Tooltip("ラベル用フォント（DSEG14 SDF など）。未設定時は timerText と同じフォントを使用")]
    [SerializeField] private TMP_FontAsset labelFont;

    [Tooltip("自動生成ラベルのフォントサイズ（0 = timerText.fontSizeMax の 30%）")]
    [SerializeField, Min(0f)] private float labelFontSize = 0f;

    [Tooltip("ラウンドインジケーター用 TMP（未設定時は Awake で自動生成）")]
    [SerializeField] private TextMeshProUGUI roundIndicatorText;

    [Header("ラウンド設定")]
    [Tooltip("ラウンドの最大数。0 にすると分母・インジケーターを非表示")]
    [SerializeField] private int maxRound = 3;

    [Tooltip("インジケーターに使う文字（labelFont が DSEG14 なら ~ など、DSEG7 なら 0, 8, - など）")]
    [SerializeField] private string indicatorChar = "~";

    [Tooltip("非アクティブなピップのアルファ（消灯セグメントの見え方）")]
    [SerializeField, Range(0f, 1f)] private float indicatorDimAlpha = 0.12f;

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
    const float FinishDuration       = 2.5f;
    const float AlternateDuration    = 2.5f;

    // -----------------------------------------------------------------------
    // 内部状態
    // -----------------------------------------------------------------------

    private DisplayState _state       = DisplayState.Hidden;
    private DisplayState _pendingState;
    private bool         _isFadingOut;
    private float        _fadeTimer;
    private float        _stateTimer;

    private float       _flickerTimer;
    private float       _noiseOffset;
    private CanvasGroup _canvasGroup;
    private GameObject  _displayCanvas; // TimerCanvas GameObjet（SetActive で表示制御）

    private bool  _lastIsPlayingUfo;
    private bool  _lastIsSessionActive;
    private int   _roundAtEntry;
    private bool  _limitTimePhase;
    private float _alternateTimer;
    private bool  _gameComplete;

    // labelText の Finish 前後でサイズ・位置を復元するための保存値
    private float   _labelNormalFontSize;
    private Vector2 _labelNormalSizeDelta;
    private Vector2 _labelNormalPosition;

    // -----------------------------------------------------------------------
    // Unity ライフサイクル
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (timerText == null) { Debug.LogWarning("[TimerDisplay] timerText が未設定です。"); return; }

        _noiseOffset = Random.Range(0f, 100f);

        _displayCanvas = timerText.transform.parent.gameObject; // TimerCanvas
        _canvasGroup = _displayCanvas.GetComponent<CanvasGroup>()
                       ?? _displayCanvas.AddComponent<CanvasGroup>();

        // SetActive で確実に非表示（CanvasGroup alpha だけでは不確実なため）
        _displayCanvas.SetActive(false);

        if (labelText == null)
            labelText = CreateLabel();

        // Finish モード遷移用に labelText の初期値を保存
        if (labelText != null)
        {
            _labelNormalFontSize  = labelText.fontSize;
            _labelNormalSizeDelta = labelText.rectTransform.sizeDelta;
            _labelNormalPosition  = labelText.rectTransform.anchoredPosition;
        }

        if (roundIndicatorText == null)
            roundIndicatorText = CreateRoundIndicator();

        _lastIsPlayingUfo    = UFOCameraController.IsPlayingUfo;
        _lastIsSessionActive = UFOCameraController.IsPlaySessionActive;
    }

    private void Update()
    {
        bool isPlayingUfo    = UFOCameraController.IsPlayingUfo;
        bool isSessionActive = UFOCameraController.IsPlaySessionActive;

        // UFO モード離脱
        if (_lastIsPlayingUfo && !isPlayingUfo)
        {
            _lastIsPlayingUfo    = false;
            _lastIsSessionActive = false;
            if (!_gameComplete)
                TransitionTo(DisplayState.Hidden);
            // gameComplete の場合は FINISH のまま維持
            return;
        }

        // UFO モード開始
        if (!_lastIsPlayingUfo && isPlayingUfo)
        {
            _lastIsPlayingUfo = true;
            _gameComplete     = false;
            var rm = RoundManager.Instance;
            _roundAtEntry = rm != null ? rm.currentRound : -1;
            TransitionTo(rm != null ? DisplayState.RoundDisplay : DisplayState.PlayInfo);
        }

        // セッション開始 → Countdown
        // エッジ検出でなくレベル検出にすることでフレームスキップによる取りこぼしを防ぐ。
        // フェード中に再発火しないよう「すでに Countdown へ向かっている」場合は除外する。
        bool alreadyGoingToCountdown = _isFadingOut && _pendingState == DisplayState.Countdown;
        if (!alreadyGoingToCountdown && isSessionActive
            && _state != DisplayState.Countdown
            && _state != DisplayState.Finish
            && _state != DisplayState.Hidden)
            TransitionTo(DisplayState.Countdown);

        // セッション終了 → Finish（エッジ検出）
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

        if (_state == DisplayState.Countdown)  UpdateFlicker();
        if (_state == DisplayState.LimitTime && _fadeTimer <= 0f) UpdateAlternate();
    }

    private void LateUpdate()
    {
        if (_state == DisplayState.Countdown && _fadeTimer <= 0f)
            UpdateCountdownText();
        if (_state != DisplayState.Hidden)
            UpdateRoundIndicator();
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
            // Hidden への/からの遷移はフェードなしで即時切替
            if (next != DisplayState.Hidden)
                _displayCanvas?.SetActive(true); // 非表示 → 表示: Canvas を先に有効化
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

        // FINISH モード以外では labelText を通常サイズに戻す
        if (next != DisplayState.Finish)
            RestoreLabel();

        switch (next)
        {
            case DisplayState.Hidden:
                ApplyAlpha(0f);
                SetTexts("", "");
                _displayCanvas?.SetActive(false); // Canvas を非アクティブにして確実に非表示
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
                _limitTimePhase = true;
                _alternateTimer = AlternateDuration;
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
                // FINISH は timerText (DSEG7) の N 表示が崩れるため
                // labelText (Liberation Sans) を拡大して表示する
                SetLabelToFinishMode();
                SetTexts("FINISH", "");
                // ゲームクリア済みなら永続表示（タイマーなし）
                _stateTimer = _gameComplete ? 0f : FinishDuration;
                break;
        }

        UpdateRoundIndicator();
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
            {
                if (_gameComplete) break; // ゲームクリア → FINISH を永続表示

                if (!UFOCameraController.IsPlayingUfo) { TransitionTo(DisplayState.Hidden); break; }

                // ゲームクリア判定（最終ラウンド & 全プレイ消費）
                var ctrl      = UFOCameraController.Instance;
                var rmF       = RoundManager.Instance;
                int nowRound  = rmF  != null ? rmF.currentRound    : _roundAtEntry;
                int payCount  = ctrl != null ? ctrl.PaymentCount   : 0;
                int maxPay    = ctrl != null ? ctrl.MaxPlayCount    : 0;
                bool allPlays = maxPay  > 0 && payCount  >= maxPay;
                bool lastRound = maxRound > 0 && nowRound >= maxRound;

                if (allPlays && lastRound)
                {
                    _gameComplete = true;
                    break; // FINISH のまま（次の OnAutoTransition は _stateTimer=0 で来ない）
                }

                // 通常遷移
                if (rmF != null && nowRound != _roundAtEntry)
                {
                    _roundAtEntry = nowRound;
                    TransitionTo(DisplayState.RoundDisplay);
                }
                else
                {
                    TransitionTo(DisplayState.PlayInfo);
                }
                break;
            }
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
            ApplyAlpha(Mathf.Clamp01(_fadeTimer / FadeDuration));
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
            ApplyAlpha(1f - Mathf.Clamp01(_fadeTimer / FadeDuration));
        }
    }

    // -----------------------------------------------------------------------
    // テキスト更新
    // -----------------------------------------------------------------------

    void RefreshRoundDisplay()
    {
        var rm      = RoundManager.Instance;
        int current = rm != null ? rm.currentRound : 1;
        string main = maxRound > 0 ? $" {current} / {maxRound}" : $" {current}";
        SetTexts("ROUND", main);
    }

    void RefreshPlayInfo()
    {
        var ctrl = UFOCameraController.Instance;
        if (ctrl == null) { SetTexts("PLAY", " - / -"); return; }
        SetTexts("PLAY", $" {ctrl.PaymentCount} / {ctrl.MaxPlayCount}");
    }

    void RefreshLimitTime()
    {
        var ctrl  = UFOCameraController.Instance;
        float dur = ctrl != null ? ctrl.PlayDuration : 30f;
        int sec   = Mathf.RoundToInt(dur);
        int m = sec / 60, s = sec % 60;
        SetTexts("LIMIT  TIME", m > 0 ? $"{m}:{s:00}" : $" 0:{s:00}");
    }

    void UpdateCountdownText()
    {
        if (timerText == null) return;
        float remaining = GetRemainingTime();

        if (remaining < 0f)
        {
            timerText.text = idleText;
            ApplyCountdownColor(false);
        }
        else
        {
            remaining      = Mathf.Max(0f, remaining);
            timerText.text = FormatCountdown(remaining);
            ApplyCountdownColor(remaining <= warningThreshold);
        }
    }

    // -----------------------------------------------------------------------
    // LimitTime 交互表示
    // -----------------------------------------------------------------------

    void UpdateAlternate()
    {
        _alternateTimer -= Time.deltaTime;
        if (_alternateTimer > 0f) return;
        _alternateTimer = AlternateDuration;
        _limitTimePhase = !_limitTimePhase;

        if (_limitTimePhase) { ApplyColorForState(DisplayState.LimitTime); RefreshLimitTime(); }
        else                 { ApplyColorForState(DisplayState.PlayInfo);  RefreshPlayInfo(); }
    }

    // -----------------------------------------------------------------------
    // ラウンドインジケーター（●○ ドット）
    // -----------------------------------------------------------------------

    void UpdateRoundIndicator()
    {
        if (roundIndicatorText == null) return;
        if (maxRound <= 0 || _state == DisplayState.Hidden)
        {
            roundIndicatorText.text = "";
            return;
        }

        // PaymentCount = UFO セッションをプレイした回数（セッション終了で +1）
        // ゲームラウンド（RoundManager.currentRound）はUFO+ピンボール両方でしか進まないため
        // UFO 単体でも変化する PaymentCount でインジケーターを駆動する
        var ctrl    = UFOCameraController.Instance;
        int current = ctrl != null ? ctrl.PaymentCount : 0;
        string ch   = string.IsNullOrEmpty(indicatorChar) ? "~" : indicatorChar;

        // timerText.color = ApplyColorForState / ApplyCountdownColor が毎フレーム更新する現在色
        Color c       = timerText != null ? timerText.color : normalFaceColor;
        string dimHex = ColorUtility.ToHtmlStringRGBA(new Color(c.r, c.g, c.b, indicatorDimAlpha));

        var sb = new StringBuilder();
        for (int i = 1; i <= maxRound; i++)
        {
            if (i > 1) sb.Append(" ");
            if (i <= current)
                sb.Append(ch);
            else
                sb.Append($"<color=#{dimHex}>{ch}</color>");
        }
        roundIndicatorText.text  = sb.ToString();
        roundIndicatorText.color = c;
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

        Color face, label, glow;
        switch (state)
        {
            case DisplayState.LimitTime:
                face = label = glow = limitTimeColor; break;
            case DisplayState.Finish:
                face = label = glow = finishColor;    break;
            default:
                face  = normalFaceColor;
                label = normalFaceColor;
                glow  = normalGlowColor;
                break;
        }

        timerText.color = face;
        SetLabelGlow(label, glow);

        if (timerText.fontMaterial != null)
            timerText.fontMaterial.SetColor(ShaderUtilities.ID_GlowColor, glow);
    }

    void ApplyCountdownColor(bool isWarning)
    {
        if (timerText == null) return;

        Color face = isWarning ? warningFaceColor : normalFaceColor;
        Color glow = isWarning ? warningGlowColor : normalGlowColor;

        if (enableFlicker && _flickerTimer > 0f)
        {
            float b = flickerMinBrightness;
            face = new Color(face.r * b, face.g * b, face.b * b, face.a);
            glow = new Color(glow.r * b, glow.g * b, glow.b * b, glow.a * b);
        }

        timerText.color = face;

        if (timerText.fontMaterial != null)
        {
            timerText.fontMaterial.SetColor(ShaderUtilities.ID_GlowColor, glow);
            if (enableGlowFluctuation)
            {
                float noise = Mathf.PerlinNoise(Time.time * glowFluctuationSpeed, _noiseOffset);
                float outer = Mathf.Max(0f, baseGlowOuter * (1f + (noise * 2f - 1f) * glowFluctuationAmount));
                timerText.fontMaterial.SetFloat(ShaderUtilities.ID_GlowOuter, outer);
            }
        }
    }

    void SetLabelGlow(Color labelColor, Color glowColor)
    {
        if (labelText == null) return;
        labelText.color = labelColor;
        if (labelText.fontMaterial != null)
            labelText.fontMaterial.SetColor(ShaderUtilities.ID_GlowColor, glowColor);
    }

    // -----------------------------------------------------------------------
    // labelText の Finish モード切替
    // -----------------------------------------------------------------------

    void SetLabelToFinishMode()
    {
        if (labelText == null) return;
        var rt      = labelText.rectTransform;
        var timerRt = timerText.rectTransform;

        // timerText と同じ位置に拡大して FINISH を大きく表示
        // 幅はtimerTextの2.5倍にしてHが折り返されないようにする
        float bigFs     = timerText.fontSizeMax;
        float bigH      = bigFs * 1.8f;
        rt.sizeDelta            = new Vector2(timerRt.sizeDelta.x * 2.5f, bigH);
        rt.anchoredPosition     = new Vector2(timerRt.anchoredPosition.x, timerRt.anchoredPosition.y);
        labelText.fontSize      = bigFs;
        labelText.enableWordWrapping = false;
        labelText.overflowMode  = TextOverflowModes.Overflow;
        labelText.alignment     = TextAlignmentOptions.Center;
    }

    void RestoreLabel()
    {
        if (labelText == null) return;
        labelText.fontSize                      = _labelNormalFontSize;
        labelText.rectTransform.sizeDelta       = _labelNormalSizeDelta;
        labelText.rectTransform.anchoredPosition = _labelNormalPosition;
        labelText.alignment                     = TextAlignmentOptions.Center;
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
        _canvasGroup.alpha = alpha;
    }

    static float GetRemainingTime()
    {
        var ctrl = UFOCameraController.Instance;
        if (ctrl == null || !UFOCameraController.IsPlaySessionActive) return -1f;
        return ctrl.RemainingTime;
    }

    static string FormatCountdown(float t)
    {
        int m = (int)(t / 60f), s = (int)(t % 60f), d = (int)(t * 10f) % 10;
        return $"{m:00}:{s:00}.{d}";
    }

    // -----------------------------------------------------------------------
    // 動的生成: LabelText（上段ラベル — timerText と同じ DSEG7 + ホログラムマテリアル）
    // -----------------------------------------------------------------------

    TextMeshProUGUI CreateLabel()
    {
        var go = new GameObject("LabelText", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(timerText.transform.parent, false);
        go.transform.SetSiblingIndex(timerText.transform.GetSiblingIndex());

        var timerRt = timerText.GetComponent<RectTransform>();
        float scaleY  = timerRt.localScale.y;
        float visHalf = timerRt.sizeDelta.y * 0.5f * scaleY;
        float fs      = labelFontSize > 0f ? labelFontSize : Mathf.Max(8f, timerText.fontSizeMax * 0.3f);
        float labelH  = fs * 1.4f;

        var rt              = go.GetComponent<RectTransform>();
        rt.anchorMin        = timerRt.anchorMin;
        rt.anchorMax        = timerRt.anchorMax;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(timerRt.sizeDelta.x * 1.5f, labelH);
        rt.anchoredPosition = new Vector2(timerRt.anchoredPosition.x,
                                          timerRt.anchoredPosition.y + visHalf + 4f + labelH * 0.5f);

        var tmp                  = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize             = fs;
        tmp.enableAutoSizing     = false;
        tmp.enableWordWrapping   = false;
        tmp.overflowMode         = TextOverflowModes.Overflow;
        tmp.alignment            = TextAlignmentOptions.Center;
        tmp.color                = normalFaceColor;
        tmp.text                 = "";

        if (labelFont != null)
        {
            // DSEG14 等の別フォント: フォント自身のアトラスを使い、そこへ glow だけ追加
            tmp.font = labelFont;
            ApplyHologramToLabel(tmp);
        }
        else
        {
            // 同じ DSEG7: ホログラムマテリアルをそのままコピーしてアトラスも一致
            tmp.font         = timerText.font;
            tmp.fontMaterial = new Material(timerText.fontMaterial);
        }

        return tmp;
    }

    // -----------------------------------------------------------------------
    // 動的生成: RoundIndicator（下段 セグメントピップ — DSEG7 + ホログラムマテリアル）
    // -----------------------------------------------------------------------

    TextMeshProUGUI CreateRoundIndicator()
    {
        if (maxRound <= 0) return null;

        var go = new GameObject("RoundIndicator", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(timerText.transform.parent, false);

        var timerRt   = timerText.GetComponent<RectTransform>();
        float scaleY  = timerRt.localScale.y;
        float visHalf = timerRt.sizeDelta.y * 0.5f * scaleY;
        float fs      = Mathf.Max(6f, timerText.fontSizeMax * 0.28f); // 0.22 → 0.28 で少し大きく
        float indH    = fs * 1.5f;

        var rt              = go.GetComponent<RectTransform>();
        rt.anchorMin        = timerRt.anchorMin;
        rt.anchorMax        = timerRt.anchorMax;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(timerRt.sizeDelta.x * 1.5f, indH);
        rt.anchoredPosition = new Vector2(timerRt.anchoredPosition.x,
                                          timerRt.anchoredPosition.y - visHalf - 4f - indH * 0.5f);

        var tmp                = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize           = fs;
        tmp.enableAutoSizing   = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode       = TextOverflowModes.Overflow;
        tmp.richText           = true;
        tmp.alignment          = TextAlignmentOptions.Center;
        tmp.color              = normalFaceColor;
        tmp.text               = "";

        if (labelFont != null)
        {
            // DSEG14 など labelFont が設定されている場合: ~ など 14 セグメント文字を使える
            tmp.font = labelFont;
            ApplyHologramToLabel(tmp);
        }
        else
        {
            // フォールバック: DSEG7 + ホログラムマテリアル
            tmp.font         = timerText.font;
            tmp.fontMaterial = new Material(timerText.fontMaterial);
        }

        return tmp;
    }

    // 別フォント（DSEG14 等）にホログラムシェーダー設定を移植する。
    // フォント自身のアトラスを保持しつつ timerText のシェーダーとグロー設定をコピーする。
    void ApplyHologramToLabel(TextMeshProUGUI tmp)
    {
        if (tmp == null || timerText?.fontMaterial == null || tmp.font == null) return;

        var src  = timerText.fontMaterial;
        var font = tmp.font;

        // timerText のシェーダーをベースにマテリアルを作成
        var mat = new Material(src);

        // フォントアトラスを labelFont のものに差し替える（これが表示されない原因の核心）
        mat.mainTexture = font.atlasTexture;
        mat.SetFloat(Shader.PropertyToID("_TextureWidth"),  font.atlasWidth);
        mat.SetFloat(Shader.PropertyToID("_TextureHeight"), font.atlasHeight);
        mat.SetFloat(Shader.PropertyToID("_GradientScale"), font.atlasPadding + 1);

        tmp.fontMaterial = mat;
    }

}
