using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World Space Canvas に配置したタイマーにブラウン管（CRT）風の演出を追加する。
///
/// 効果:
///   スキャンライン  — 横縞の半透明ライン
///   スキャン波      — 明るい帯が上から下へ流れる（走査線）
///   ビネット        — 画面端が暗くなる
///   フォスファー    — 画面全体に微細な色味（ブラウン管のリン光色）
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
    [SerializeField] private bool enableScanlines = true;
    [Tooltip("スキャンラインの不透明度")]
    [SerializeField, Range(0f, 1f)] private float scanlineAlpha = 0.25f;
    [Tooltip("スキャンラインの間隔（Canvas ピクセル単位）")]
    [SerializeField, Min(2)] private int scanlineSpacing = 4;

    [Header("スキャン波（走査線）")]
    [SerializeField] private bool enableScanWave = true;
    [Tooltip("帯の移動速度（大きいほど速い）")]
    [SerializeField, Min(0f)] private float scanWaveSpeed = 0.25f;
    [Tooltip("帯の明るさ")]
    [SerializeField, Range(0f, 1f)] private float scanWaveAlpha = 0.07f;
    [Tooltip("帯の縦幅（パネル高さに対する割合）")]
    [SerializeField, Range(0.05f, 0.6f)] private float scanWaveWidth = 0.25f;

    [Header("ビネット（画面端の暗さ）")]
    [SerializeField] private bool enableVignette = true;
    [Tooltip("ビネットの強さ")]
    [SerializeField, Range(0f, 1f)] private float vignetteAlpha = 0.55f;
    [Tooltip("明るい領域の広さ（0=全体暗い、1=端だけ暗い）")]
    [SerializeField, Range(0.1f, 0.95f)] private float vignetteRadius = 0.65f;

    [Header("フォスファー色味")]
    [Tooltip("ブラウン管のリン光による色味オーバーレイを有効にする")]
    [SerializeField] private bool enablePhosphor = true;
    [Tooltip("フォスファー色（緑みがかった白が定番）")]
    [SerializeField] private Color phosphorColor = new Color(0.6f, 1f, 0.7f, 1f);
    [Tooltip("フォスファー色味の強さ（薄めが自然）")]
    [SerializeField, Range(0f, 0.15f)] private float phosphorAlpha = 0.04f;

    [Header("ジッター（微細な位置ぶれ）")]
    [SerializeField] private bool enableJitter = true;
    [Tooltip("ぶれの最大量（Canvas ピクセル単位）")]
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
    private float    _panelH;
    private float    _waveBandH;

    // -----------------------------------------------------------------------
    // Unity ライフサイクル
    // -----------------------------------------------------------------------

    private IEnumerator Start()
    {
        // Canvas のレイアウト完了を 1 フレーム待つ
        yield return null;

        if (displayPanel == null)
        {
            Debug.LogWarning("[CRTDisplayEffect] displayPanel が未設定です。");
            yield break;
        }

        _panelH = displayPanel.rect.height;

        if (textTransform != null)
            _baseTextPosition = textTransform.anchoredPosition;

        if (enableScanlines) BuildScanlineOverlay();
        if (enableVignette)  BuildVignetteOverlay();
        if (enablePhosphor)  BuildPhosphorOverlay();
        if (enableScanWave)  BuildScanWaveOverlay();
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

        int texH = scanlineSpacing * 2;
        var tex  = new Texture2D(2, texH, TextureFormat.RGBA32, false);
        tex.wrapMode   = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;

        for (int y = 0; y < texH; y++)
        {
            Color c = (y % scanlineSpacing == 0) ? new Color(0f, 0f, 0f, 1f) : Color.clear;
            tex.SetPixel(0, y, c);
            tex.SetPixel(1, y, c);
        }
        tex.Apply();

        _scanlineImage.texture = tex;
        _scanlineImage.color   = new Color(1f, 1f, 1f, scanlineAlpha);
        // パネル高さ / ライン間隔 = 必要なタイル数
        float tilingY = _panelH / scanlineSpacing;
        _scanlineImage.uvRect = new Rect(0f, 0f, 1f, tilingY);
    }

    // -----------------------------------------------------------------------
    // スキャン波
    // -----------------------------------------------------------------------

    private void BuildScanWaveOverlay()
    {
        _waveBandH = _panelH * scanWaveWidth;

        var go = new GameObject("ScanWaveOverlay", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(displayPanel, false);

        // 帯は全幅・固定高さ。anchoredPosition で上→下に動かす
        var rt        = go.GetComponent<RectTransform>();
        rt.anchorMin  = new Vector2(0f, 0.5f);
        rt.anchorMax  = new Vector2(1f, 0.5f);
        rt.pivot      = new Vector2(0.5f, 0.5f);
        rt.sizeDelta  = new Vector2(0f, _waveBandH);
        rt.SetAsLastSibling();

        // 縦方向ガウシアングラジエント（中央が最も明るい）
        int texH = 64;
        var tex  = new Texture2D(2, texH, TextureFormat.RGBA32, false);
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < texH; y++)
        {
            float t     = y / (float)(texH - 1);
            float alpha = Mathf.Exp(-Mathf.Pow((t - 0.5f) / 0.2f, 2f));
            tex.SetPixel(0, y, new Color(1f, 1f, 1f, alpha));
            tex.SetPixel(1, y, new Color(1f, 1f, 1f, alpha));
        }
        tex.Apply();

        _scanWaveImage        = go.GetComponent<RawImage>();
        _scanWaveImage.texture = tex;
        _scanWaveImage.color   = new Color(1f, 1f, 1f, scanWaveAlpha);
    }

    private void UpdateScanWave()
    {
        if (_scanWaveImage == null) return;

        float t    = (Time.time * scanWaveSpeed) % 1f;
        // パネル上端 (+panelH/2) → 下端 (-panelH/2) へ移動
        float posY = Mathf.Lerp(_panelH * 0.5f + _waveBandH, -_panelH * 0.5f - _waveBandH, t);
        _scanWaveImage.rectTransform.anchoredPosition = new Vector2(0f, posY);
    }

    // -----------------------------------------------------------------------
    // ビネット
    // -----------------------------------------------------------------------

    private void BuildVignetteOverlay()
    {
        var img = CreateOverlayImage("VignetteOverlay");
        img.transform.SetSiblingIndex(0); // 一番後ろ

        int size = 128;
        var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx    = (x / (float)(size - 1)) * 2f - 1f;
                float ny    = (y / (float)(size - 1)) * 2f - 1f;
                float dist  = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = Mathf.Clamp01(
                    Mathf.SmoothStep(vignetteRadius, 1.4f, dist)) * vignetteAlpha;
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
        }
        tex.Apply();

        img.texture = tex;
        img.color   = Color.white;
    }

    // -----------------------------------------------------------------------
    // フォスファー色味
    // -----------------------------------------------------------------------

    private void BuildPhosphorOverlay()
    {
        var img   = CreateOverlayImage("PhosphorOverlay");
        img.transform.SetSiblingIndex(1);
        img.texture = null;

        Color c   = phosphorColor;
        c.a       = phosphorAlpha;
        img.color = c;
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
            textTransform.anchoredPosition = _baseTextPosition;
            _jittering   = false;
            _jitterTimer = Random.Range(0.05f, 0.2f);
        }
        else if (Random.value < jitterRate * Time.deltaTime * 5f)
        {
            float jx = Random.Range(-jitterAmount, jitterAmount);
            float jy = Random.Range(-jitterAmount, jitterAmount);
            textTransform.anchoredPosition = _baseTextPosition + new Vector2(jx, jy);
            _jittering   = true;
            _jitterTimer = Random.Range(0.03f, 0.09f);
        }
        else
        {
            _jitterTimer = Random.Range(0.02f, 0.05f);
        }
    }

    // -----------------------------------------------------------------------
    // ユーティリティ
    // -----------------------------------------------------------------------

    private RawImage CreateOverlayImage(string goName)
    {
        var go = new GameObject(goName, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(displayPanel, false);

        var rt       = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.SetAsLastSibling();

        return go.GetComponent<RawImage>();
    }
}
