using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World Space Canvas に配置したタイマーにブラウン管（CRT）風の演出を追加する。
///
/// 効果:
///   スキャンライン  — 横縞の半透明ラインを重ねる
///   スキャン波      — 明るい帯が画面をゆっくり流れる（走査線）
///   ジッター        — テキストが微細にランダムぶれする
///
/// セットアップ:
///   1. TimerObject（または TimerCanvas）にアタッチ
///   2. displayPanel に TimerCanvas の RectTransform を設定
///   3. textTransform に TimerText の RectTransform を設定（ジッター用）
/// </summary>
public class CRTDisplayEffect : MonoBehaviour
{
    [Header("対象")]
    [Tooltip("CRT エフェクトを重ねるパネルの RectTransform（TimerCanvas 等）")]
    [SerializeField] private RectTransform displayPanel;

    [Tooltip("ジッターをかけるテキストの RectTransform（TimerText 等）")]
    [SerializeField] private RectTransform textTransform;

    [Header("スキャンライン")]
    [Tooltip("横縞ラインのオーバーレイを有効にする")]
    [SerializeField] private bool enableScanlines = true;

    [Tooltip("スキャンラインの不透明度（0=非表示、1=完全に黒）")]
    [SerializeField, Range(0f, 1f)] private float scanlineAlpha = 0.25f;

    [Tooltip("スキャンラインの間隔（ピクセル）。小さいほど細かい縞になる")]
    [SerializeField, Min(2)] private int scanlineSpacing = 4;

    [Header("スキャン波（走査線）")]
    [Tooltip("明るい帯が流れる走査線エフェクトを有効にする")]
    [SerializeField] private bool enableScanWave = true;

    [Tooltip("走査線の移動速度（大きいほど速く流れる）")]
    [SerializeField, Min(0f)] private float scanWaveSpeed = 0.3f;

    [Tooltip("走査線の明るさ（0=なし、1=真っ白）")]
    [SerializeField, Range(0f, 1f)] private float scanWaveAlpha = 0.06f;

    [Tooltip("走査線の幅（パネル高さに対する割合）")]
    [SerializeField, Range(0.05f, 0.8f)] private float scanWaveWidth = 0.3f;

    [Header("ジッター（微細な位置ぶれ）")]
    [Tooltip("テキストの微細なぶれを有効にする")]
    [SerializeField] private bool enableJitter = true;

    [Tooltip("ぶれの最大量（ピクセル単位）")]
    [SerializeField, Min(0f)] private float jitterAmount = 0.8f;

    [Tooltip("1秒あたりのぶれ発生回数")]
    [SerializeField, Min(0f)] private float jitterRate = 4f;

    // -----------------------------------------------------------------------
    // 内部状態
    // -----------------------------------------------------------------------

    private RawImage _scanlineImage;
    private RawImage _scanWaveImage;
    private Vector2  _baseTextPosition;
    private float    _jitterTimer;
    private bool     _jittering;

    // -----------------------------------------------------------------------
    // Unity ライフサイクル
    // -----------------------------------------------------------------------

    private void Start()
    {
        if (textTransform != null)
            _baseTextPosition = textTransform.anchoredPosition;

        if (displayPanel == null)
        {
            Debug.LogWarning("[CRTDisplayEffect] displayPanel が未設定です。");
            return;
        }

        if (enableScanlines)  BuildScanlineOverlay();
        if (enableScanWave)   BuildScanWaveOverlay();
    }

    private void Update()
    {
        if (enableJitter)   UpdateJitter();
        if (enableScanWave) UpdateScanWave();
    }

    // -----------------------------------------------------------------------
    // スキャンライン
    // -----------------------------------------------------------------------

    private void BuildScanlineOverlay()
    {
        _scanlineImage = CreateOverlayImage("ScanlineOverlay");

        // 1ライン分だけ黒、残りは透明なテクスチャをタイリング表示
        int texH = scanlineSpacing * 2;
        var tex  = new Texture2D(2, texH, TextureFormat.RGBA32, false);
        tex.wrapMode   = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;

        for (int y = 0; y < texH; y++)
        {
            Color c = (y % scanlineSpacing == 0)
                ? new Color(0f, 0f, 0f, 1f)
                : Color.clear;
            tex.SetPixel(0, y, c);
            tex.SetPixel(1, y, c);
        }
        tex.Apply();

        _scanlineImage.texture = tex;
        _scanlineImage.color   = new Color(1f, 1f, 1f, scanlineAlpha);

        // パネル高さ分タイリング
        float tilingY = displayPanel.rect.height / scanlineSpacing;
        _scanlineImage.uvRect = new Rect(0f, 0f, 1f, tilingY);
    }

    // -----------------------------------------------------------------------
    // スキャン波
    // -----------------------------------------------------------------------

    private void BuildScanWaveOverlay()
    {
        _scanWaveImage = CreateOverlayImage("ScanWaveOverlay");

        // 縦方向に白→透明のグラジエント（帯の形）
        int size = 64;
        var tex  = new Texture2D(2, size, TextureFormat.RGBA32, false);
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            float t     = y / (float)(size - 1);
            // ガウス曲線で中央が最も明るい帯を作る
            float alpha = Mathf.Exp(-Mathf.Pow((t - 0.5f) / (scanWaveWidth * 0.5f), 2f));
            Color c     = new Color(1f, 1f, 1f, alpha);
            tex.SetPixel(0, y, c);
            tex.SetPixel(1, y, c);
        }
        tex.Apply();

        _scanWaveImage.texture = tex;
        _scanWaveImage.color   = new Color(1f, 1f, 1f, scanWaveAlpha);

        // 帯の高さをパネルの 50% に設定
        var rt = _scanWaveImage.rectTransform;
        rt.anchorMin  = new Vector2(0f, 0f);
        rt.anchorMax  = new Vector2(1f, 0f);
        rt.pivot      = new Vector2(0.5f, 0.5f);
        rt.sizeDelta  = new Vector2(0f, displayPanel.rect.height * 0.5f);
    }

    private void UpdateScanWave()
    {
        if (_scanWaveImage == null || displayPanel == null) return;

        float panelH = displayPanel.rect.height;
        // -half ~ +half の範囲を scanWaveSpeed で周回
        float t   = (Time.time * scanWaveSpeed) % 1f;
        float posY = Mathf.Lerp(-panelH * 0.5f, panelH * 0.5f, t);
        _scanWaveImage.rectTransform.anchoredPosition = new Vector2(0f, posY);
    }

    // -----------------------------------------------------------------------
    // ジッター
    // -----------------------------------------------------------------------

    private void UpdateJitter()
    {
        if (textTransform == null) return;

        _jitterTimer -= Time.deltaTime;
        if (_jitterTimer > 0f) return;

        if (_jittering)
        {
            // ぶれを元に戻す
            textTransform.anchoredPosition = _baseTextPosition;
            _jittering   = false;
            _jitterTimer = Random.Range(0.02f, 1f / Mathf.Max(jitterRate, 0.1f));
        }
        else if (Random.value < jitterRate * Time.deltaTime * 5f)
        {
            // ぶれを発生させる
            float jx = Random.Range(-jitterAmount, jitterAmount);
            float jy = Random.Range(-jitterAmount, jitterAmount);
            textTransform.anchoredPosition = _baseTextPosition + new Vector2(jx, jy);
            _jittering   = true;
            _jitterTimer = Random.Range(0.03f, 0.1f);
        }
        else
        {
            _jitterTimer = Random.Range(0.02f, 0.05f);
        }
    }

    // -----------------------------------------------------------------------
    // ユーティリティ
    // -----------------------------------------------------------------------

    /// <summary>displayPanel の子に全面を覆う RawImage を生成して返す。</summary>
    private RawImage CreateOverlayImage(string goName)
    {
        var go = new GameObject(goName, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(displayPanel, false);

        var rt        = go.GetComponent<RectTransform>();
        rt.anchorMin  = Vector2.zero;
        rt.anchorMax  = Vector2.one;
        rt.sizeDelta  = Vector2.zero;
        rt.SetAsLastSibling();

        return go.GetComponent<RawImage>();
    }
}
