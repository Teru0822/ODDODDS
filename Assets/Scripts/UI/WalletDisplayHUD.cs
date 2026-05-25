using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// TAB キーを押し込んでいる間だけ、Player の所持金 (未洗浄金 / 洗浄金 / 徳ポイント) を画面に表示する HUD。
/// Player にアタッチ。Start で Canvas + Text を自動生成 (Reticle と同じパターン)。
/// </summary>
[DisallowMultipleComponent]
public class WalletDisplayHUD : MonoBehaviour
{
    [Header("データソース")]
    [Tooltip("参照する PlayerWallet。null なら同一 GameObject / 親 / シーンから自動取得")]
    public PlayerWallet wallet;

    [Header("入力 (押し込み中表示)")]
    [Tooltip("表示中にする押しっぱなしキー。デフォルト Tab")]
    public Key holdKey = Key.Tab;

    [Header("外観")]
    [Tooltip("使用するフォントアセット (日本語対応必須)。空なら Resources から Noto Sans JP を読み込み、失敗時は TMP デフォルト")]
    public TMP_FontAsset font;

    [Tooltip("Resources からのフォントパス (font 未指定時のフォールバック)。.asset 拡張子なし")]
    public string fallbackFontResourcePath = "Font/Noto_Sans_JP/NotoSansJP-Medium SDF";

    [Tooltip("背景色")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);

    [Tooltip("テキスト色")]
    public Color textColor = Color.white;

    [Tooltip("フォントサイズ (px)")]
    public int fontSize = 28;

    [Tooltip("画面端からの余白 (px)")]
    public Vector2 padding = new Vector2(24, 24);

    [Tooltip("パネル内の上下余白 (px)")]
    public float panelInnerPadding = 16f;

    [Tooltip("画面のどの隅に表示するか")]
    public ScreenAnchor anchor = ScreenAnchor.TopLeft;

    public enum ScreenAnchor { TopLeft, TopRight, BottomLeft, BottomRight, Center }

    [Header("Canvas 設定")]
    [Tooltip("表示するディスプレイ番号 (0=Display 1, 3=Display 4)")]
    [Range(0, 7)]
    public int targetDisplay = 3;

    [Tooltip("Sorting Order (UI 重ね順)")]
    public int sortingOrder = 32000;

    [Tooltip("シーン内の他 Canvas より自動的に上に表示")]
    public bool autoBringToFront = true;

    [Header("表示内容")]
    [Tooltip("徳ポイントも表示する")]
    public bool showVirtuePoints = true;

    [Tooltip("未洗浄金フォーマット ({0} に値)")]
    public string unwashedFormat = "未洗浄金: ¥{0:N0}";

    [Tooltip("洗浄金フォーマット")]
    public string washedFormat = "所持金: ¥{0:N0}";

    [Tooltip("徳ポイントフォーマット")]
    public string virtueFormat = "徳ポイント: {0}";

    [Header("デバッグ")]
    public bool logOnStart = true;

    private GameObject _autoCanvas;
    private GameObject _panel;
    private TextMeshProUGUI _text;
    private bool _shown;

    private void Start()
    {
        ResolveWallet();
        CreateUI();
        SetShown(false);
    }

    private void OnDestroy()
    {
        if (_autoCanvas != null) Destroy(_autoCanvas);
    }

    private void ResolveWallet()
    {
        if (wallet != null) return;
        wallet = GetComponentInParent<PlayerWallet>();
        if (wallet == null) wallet = GetComponentInChildren<PlayerWallet>();
        if (wallet == null) wallet = PlayerWallet.Local;
        if (wallet == null)
        {
            Debug.LogWarning($"[WalletDisplayHUD] '{name}': PlayerWallet が見つかりません。Player にアタッチされているか確認してください。", this);
        }
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        bool pressed = Keyboard.current[holdKey].isPressed;
        if (pressed != _shown) SetShown(pressed);
        if (pressed) RefreshText();
    }

    private void SetShown(bool show)
    {
        _shown = show;
        if (_panel != null) _panel.SetActive(show);
        if (show) RefreshText();
    }

    private void RefreshText()
    {
        if (_text == null || wallet == null) return;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Format(unwashedFormat, Mathf.FloorToInt(wallet.UnwashedAmount)));
        sb.Append(string.Format(washedFormat, Mathf.FloorToInt(wallet.WashedAmount)));
        if (showVirtuePoints)
        {
            sb.AppendLine();
            sb.Append(string.Format(virtueFormat, wallet.VirtuePoints));
        }
        _text.text = sb.ToString();
    }

    private void CreateUI()
    {
        // Canvas
        _autoCanvas = new GameObject(
            "WalletHUDCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        var canvas = _autoCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = ResolveSortingOrder();
        canvas.targetDisplay = targetDisplay;
        var scaler = _autoCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.referencePixelsPerUnit = 100f;

        // Panel (背景)
        _panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _panel.transform.SetParent(canvas.transform, false);
        var panelRt = _panel.GetComponent<RectTransform>();
        SetAnchor(panelRt, anchor);
        panelRt.sizeDelta = new Vector2(360f, 140f);
        var img = _panel.GetComponent<Image>();
        img.color = backgroundColor;
        img.raycastTarget = false;
        img.sprite = CreateWhiteSprite();

        // Text
        var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(_panel.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(panelInnerPadding, panelInnerPadding);
        textRt.offsetMax = new Vector2(-panelInnerPadding, -panelInnerPadding);
        _text = textGo.GetComponent<TextMeshProUGUI>();
        _text.color = textColor;
        _text.fontSize = fontSize;
        _text.alignment = TextAlignmentOptions.TopLeft;
        _text.raycastTarget = false;

        // 日本語表示できるフォントを適用 (TMP デフォルトの LiberationSans は日本語非対応)
        TMP_FontAsset useFont = font;
        if (useFont == null && !string.IsNullOrEmpty(fallbackFontResourcePath))
        {
            useFont = Resources.Load<TMP_FontAsset>(fallbackFontResourcePath);
            if (useFont == null)
            {
                Debug.LogWarning($"[WalletDisplayHUD] フォントが見つかりません: Resources/{fallbackFontResourcePath}.asset。Inspector で font を直接アサインしてください。", this);
            }
        }
        if (useFont != null) _text.font = useFont;

        if (logOnStart)
        {
            Debug.Log($"[WalletDisplayHUD] 生成完了: canvas='{canvas.name}' sortingOrder={canvas.sortingOrder} targetDisplay={canvas.targetDisplay} (=Display {canvas.targetDisplay + 1})", this);
        }
    }

    private void SetAnchor(RectTransform rt, ScreenAnchor a)
    {
        switch (a)
        {
            case ScreenAnchor.TopLeft:
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(padding.x, -padding.y);
                break;
            case ScreenAnchor.TopRight:
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-padding.x, -padding.y);
                break;
            case ScreenAnchor.BottomLeft:
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(padding.x, padding.y);
                break;
            case ScreenAnchor.BottomRight:
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-padding.x, padding.y);
                break;
            case ScreenAnchor.Center:
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                break;
        }
    }

    private int ResolveSortingOrder()
    {
        if (!autoBringToFront) return sortingOrder;
        int maxOrder = sortingOrder;
        var all = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in all)
        {
            if (c == null) continue;
            if (_autoCanvas != null && c.gameObject == _autoCanvas) continue;
            if (c.sortingOrder >= maxOrder) maxOrder = c.sortingOrder + 1;
        }
        return maxOrder;
    }

    private static Sprite CreateWhiteSprite()
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var pixels = new Color[] { Color.white, Color.white, Color.white, Color.white };
        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(tex, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f);
    }
}
