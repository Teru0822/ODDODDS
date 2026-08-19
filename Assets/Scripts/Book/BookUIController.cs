using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 本のページに載せる UI 全体の管理役。
/// 見開き 3 つ（Information / Items / Roguelike）を作り、下部の左右矢印で切り替える。
///
/// 【構成】
///   本のインスタンスの下に WorldSpace の Canvas を 2 枚（左ページ・右ページ）ぶら下げる。
///   本と一緒に動く／回るので、開く演出中もページが本に貼り付いたまま追従する。
///
/// 【置き場所】
///   BookOpenController と同じオブジェクトに付ける。本の生成を待って自動で組み立てる。
///
/// 【最初にやること】
///   Left / Right Page Local Position と Page Local Euler を、実際の本のページ面に合うよう調整する。
///   本のモデルの大きさに依存するので、既定値のままでは合わない。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BookOpenController))]
public class BookUIController : MonoBehaviour
{
    [Header("ページ配置（本のローカル座標）")]
    [Tooltip("左ページ中央の位置")]
    [SerializeField] private Vector3 _leftPageLocalPosition = new Vector3(-0.11f, 0.01f, 0f);

    [Tooltip("右ページ中央の位置")]
    [SerializeField] private Vector3 _rightPageLocalPosition = new Vector3(0.11f, 0.01f, 0f);

    [Tooltip("ページ面の向き。本を真上から見る形なら X=90 あたり")]
    [SerializeField] private Vector3 _pageLocalEuler = new Vector3(90f, 0f, 0f);

    [Tooltip("ページ 1 枚のワールドサイズ(m)")]
    [SerializeField] private Vector2 _pageWorldSize = new Vector2(0.2f, 0.28f);

    [Tooltip("ページの解像度(px)。大きいほど文字が精細になるがメモリを食う")]
    [SerializeField] private Vector2 _pageResolution = new Vector2(700f, 980f);

    [Header("見た目")]
    [SerializeField] private Color _pageBackColor = new Color(0f, 0f, 0f, 0f);
    [SerializeField] private Color _textColor = new Color(0.12f, 0.09f, 0.06f, 1f);
    [SerializeField] private Color _accentColor = new Color(0.65f, 0.15f, 0.12f, 1f);
    [SerializeField] private Color _selectionColor = new Color(0.2f, 0.55f, 1f, 1f);

    [Tooltip("ページ本文のフォント。未指定なら TMP の既定フォント")]
    [SerializeField] private TMP_FontAsset _font;

    [Header("コインのアイコン")]
    [SerializeField] private Sprite _goldCoinIcon;
    [SerializeField] private Sprite _silverCoinIcon;
    [SerializeField] private Sprite _bronzeCoinIcon;
    [SerializeField] private Sprite _blackDiamondIcon;

    [Header("ローグライク")]
    [Tooltip("説明動画・画像の登録アセット。RewardSelectionUI と同じものを指定してよい")]
    [SerializeField] private RoguelikePreviewRegistry _previewRegistry;

    [Header("動作")]
    [Tooltip("マウスホイールのスクロール量")]
    [SerializeField] private float _scrollSensitivity = 30f;

    [Tooltip("本が開き切ってからページを表示する。開く演出中は中身を出さない")]
    [SerializeField] private bool _showOnlyWhenFullyOpen = true;

    [Tooltip("本を開いている間、他の画面UI(Canvas)をすべて隠す")]
    [SerializeField] private bool _hideOtherCanvases = true;

    [Tooltip("ページ送りを押した時に Console へ出力する。反応を確認したい時に使う")]
    [SerializeField] private bool _logEvents = false;

    [Header("ページ送り")]
    [Tooltip("左右矢印ボタンの大きさ(px)")]
    [SerializeField] private Vector2 _navButtonSize = new Vector2(70f, 70f);

    [Tooltip("「<」を左ページに置く。オフなら右ページ")]
    [SerializeField] private bool _prevButtonOnLeftPage = false;

    [Tooltip("「<」の位置。ページ内の割合 (0,0)=左下 (1,1)=右上")]
    [SerializeField] private Vector2 _prevButtonAnchor = new Vector2(0.35f, 0.05f);

    [Tooltip("「>」を左ページに置く。オフなら右ページ")]
    [SerializeField] private bool _nextButtonOnLeftPage = false;

    [Tooltip("「>」の位置。ページ内の割合")]
    [SerializeField] private Vector2 _nextButtonAnchor = new Vector2(0.65f, 0.05f);

    [Tooltip("ページ番号を表示する")]
    [SerializeField] private bool _showPageNumber = true;

    [Tooltip("ページ番号を左ページに置く。オフなら右ページ")]
    [SerializeField] private bool _pageNumberOnLeftPage = false;

    [Tooltip("ページ番号の位置。ページ内の割合")]
    [SerializeField] private Vector2 _pageNumberAnchor = new Vector2(0.5f, 0.05f);

    private BookOpenController _bookController;

    private readonly List<BookSpread> _spreads = new List<BookSpread>();
    private int _spreadIndex;

    private Canvas _leftCanvas;
    private Canvas _rightCanvas;
    private CanvasGroup _leftGroup;
    private CanvasGroup _rightGroup;

    private TextMeshProUGUI _pageNumberText;
    private RectTransform _prevButtonRt;
    private RectTransform _nextButtonRt;
    private bool _built;

    // 本を開いている間だけ伏せた他の Canvas。閉じたら元に戻す
    private readonly List<Canvas> _hiddenCanvases = new List<Canvas>();

    // Canvas を disable しても GraphicRaycaster はクリックを拾い続けるため、こちらも止める
    private readonly List<GraphicRaycaster> _disabledRaycasters = new List<GraphicRaycaster>();

    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(8);
    private PointerEventData _pointerData;

    // Inspector の値を毎フレーム流し込むと余計なレイアウト再計算が走るので、
    // 変わった時だけ適用できるよう直前の値を覚えておく
    private Vector3 _appliedLeftPosition;
    private Vector3 _appliedRightPosition;
    private Vector3 _appliedEuler;
    private Vector2 _appliedWorldSize;
    private Vector2 _appliedResolution;
    private bool _layoutApplied;

    /// <summary>1 見開き分。左右それぞれのページ内容を持つ。</summary>
    private class BookSpread
    {
        public RectTransform Left;
        public RectTransform Right;
        public IBookPage Page;
    }

    private void Awake()
    {
        _bookController = GetComponent<BookOpenController>();
    }

    private void OnEnable()
    {
        if (_bookController != null)
        {
            _bookController.OnBookOpened += HandleOpened;
            _bookController.OnBookClosed += HandleClosed;
        }
    }

    private void OnDisable()
    {
        if (_bookController != null)
        {
            _bookController.OnBookOpened -= HandleOpened;
            _bookController.OnBookClosed -= HandleClosed;
        }
    }

    private void Update()
    {
        if (!_built) return;

        // Inspector で位置・角度・サイズを変えたらその場で反映する。
        // 本の大きさに合わせ込む作業を Play 中に行えるようにするため
        ApplyPageLayout();

        // 矢印は個別に動かせるようにしてあるので、こちらも毎フレーム追従させる
        ApplyNavLayout();

        if (_logEvents) LogPointerHits();

        // 開き切るまでページを伏せておくと、めくれ途中の面に文字が乗らない
        bool visible = !_showOnlyWhenFullyOpen || _bookController.IsFullyOpen;
        SetPagesVisible(visible);
    }

    /// <summary>ページ面の配置を Inspector の値に合わせる。値が変わった時だけ実際に書き込む。</summary>
    private void ApplyPageLayout()
    {
        if (_layoutApplied
            && _appliedLeftPosition == _leftPageLocalPosition
            && _appliedRightPosition == _rightPageLocalPosition
            && _appliedEuler == _pageLocalEuler
            && _appliedWorldSize == _pageWorldSize
            && _appliedResolution == _pageResolution)
        {
            return;
        }

        ApplyPageTransform(_leftCanvas, _leftPageLocalPosition);
        ApplyPageTransform(_rightCanvas, _rightPageLocalPosition);

        _appliedLeftPosition = _leftPageLocalPosition;
        _appliedRightPosition = _rightPageLocalPosition;
        _appliedEuler = _pageLocalEuler;
        _appliedWorldSize = _pageWorldSize;
        _appliedResolution = _pageResolution;
        _layoutApplied = true;
    }

    private void ApplyPageTransform(Canvas canvas, Vector3 localPosition)
    {
        if (canvas == null) return;

        var rt = (RectTransform)canvas.transform;
        rt.localPosition = localPosition;
        rt.localRotation = Quaternion.Euler(_pageLocalEuler);
        rt.sizeDelta = _pageResolution;
        rt.localScale = PageScale();

        // メインカメラが差し替わっても WorldSpace Canvas が迷子にならないようにする
        if (canvas.worldCamera == null || !canvas.worldCamera.isActiveAndEnabled)
        {
            canvas.worldCamera = Camera.main;
        }
    }

    /// <summary>解像度(px)とワールドサイズ(m)から Canvas のスケールを求める。</summary>
    private Vector3 PageScale()
    {
        float x = _pageResolution.x > 0f ? _pageWorldSize.x / _pageResolution.x : 0.0001f;
        float y = _pageResolution.y > 0f ? _pageWorldSize.y / _pageResolution.y : 0.0001f;
        return new Vector3(x, y, 1f);
    }

    private void HandleOpened()
    {
        EnsureBuilt();
        RefreshCurrentSpread();
        HideOtherCanvases();
    }

    private void HandleClosed()
    {
        SetPagesVisible(false);
        RestoreOtherCanvases();
    }

    /// <summary>本を読んでいる間は他の画面 UI を伏せる。ページと、モヤ・ロード画面は対象外。</summary>
    private void HideOtherCanvases()
    {
        if (!_hideOtherCanvases) return;

        RestoreOtherCanvases();

        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas == null || !canvas.enabled) continue;

            // 遷移中のモヤ・ロード画面は DontDestroyOnLoad 側に居るので触らない
            if (canvas.gameObject.scene.name == "DontDestroyOnLoad") continue;

            // 本のページ自身は当然残す
            if (_leftCanvas != null && canvas.transform.IsChildOf(_leftCanvas.transform)) continue;
            if (_rightCanvas != null && canvas.transform.IsChildOf(_rightCanvas.transform)) continue;

            // GraphicRaycaster は Canvas を disable しても当たり判定を返し続けるので個別に止める。
            // これを残すと、画面全体を覆う HUD が本のページへのクリックを吸ってしまう
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null && raycaster.enabled)
            {
                raycaster.enabled = false;
                _disabledRaycasters.Add(raycaster);
            }

            canvas.enabled = false;
            _hiddenCanvases.Add(canvas);
        }
    }

    private void RestoreOtherCanvases()
    {
        foreach (var canvas in _hiddenCanvases)
        {
            if (canvas != null) canvas.enabled = true;
        }
        _hiddenCanvases.Clear();

        foreach (var raycaster in _disabledRaycasters)
        {
            if (raycaster != null) raycaster.enabled = true;
        }
        _disabledRaycasters.Clear();
    }

    /// <summary>
    /// クリックが何に吸われているかを調べる診断。Log Events がオンの時だけ働く。
    /// 「ボタンを押してもログが出ない」時に、手前の何が邪魔しているかを特定するためのもの。
    /// </summary>
    private void LogPointerHits()
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        var eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            Debug.LogWarning("[BookUIController] EventSystem がシーンにありません。UI のクリックは一切届きません。", this);
            return;
        }

        if (_pointerData == null) _pointerData = new PointerEventData(eventSystem);
        _pointerData.Reset();
        _pointerData.position = mouse.position.ReadValue();

        _raycastResults.Clear();
        eventSystem.RaycastAll(_pointerData, _raycastResults);

        if (_raycastResults.Count == 0)
        {
            Debug.Log("[BookUIController] クリック位置に UI が1つも当たっていません。" +
                      "ページの Canvas に届いていないので、位置ずれか worldCamera の設定を疑ってください。", this);
            return;
        }

        var log = new StringBuilder("[BookUIController] クリックが当たった UI（手前から）:").AppendLine();
        for (int i = 0; i < Mathf.Min(6, _raycastResults.Count); i++)
        {
            var hit = _raycastResults[i];
            var canvas = hit.gameObject.GetComponentInParent<Canvas>();
            log.AppendLine($"  {i}: {hit.gameObject.name}  (canvas={(canvas != null ? canvas.name : "?")}, " +
                           $"mode={(canvas != null ? canvas.renderMode.ToString() : "?")})");
        }

        Debug.Log(log.ToString(), this);
    }

    private void EnsureBuilt()
    {
        if (_built) return;

        GameObject book = _bookController.BookInstance;
        if (book == null)
        {
            Debug.LogWarning("[BookUIController] 本のインスタンスがまだありません。ページの生成を見送ります。", this);
            return;
        }

        _leftCanvas = CreatePageCanvas(book.transform, "LeftPageCanvas", _leftPageLocalPosition, out _leftGroup);
        _rightCanvas = CreatePageCanvas(book.transform, "RightPageCanvas", _rightPageLocalPosition, out _rightGroup);

        BuildSpreads();
        BuildNavigation();

        _built = true;
        ShowSpread(0);
    }

    /// <summary>ページ 1 枚ぶんの WorldSpace Canvas を本の子として作る。</summary>
    private Canvas CreatePageCanvas(Transform bookRoot, string name, Vector3 localPosition, out CanvasGroup group)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(bookRoot, false);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        // 位置・角度・スケールは ApplyPageTransform に一本化して、
        // 生成時と Play 中の調整で必ず同じ計算を通るようにする
        ApplyPageTransform(canvas, localPosition);

        group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        BookUIBuilder.ColorPanel(rt, "Back", _pageBackColor);
        return canvas;
    }

    private void BuildSpreads()
    {
        var palette = new BookPagePalette
        {
            Text = _textColor,
            Accent = _accentColor,
            Selection = _selectionColor,
            Font = _font,
            ScrollSensitivity = _scrollSensitivity
        };

        AddSpread(new BookInfoPage(palette, _goldCoinIcon, _silverCoinIcon, _bronzeCoinIcon, _blackDiamondIcon));
        AddSpread(new BookItemsPage(palette));
        AddSpread(new BookRoguelikePage(palette, _previewRegistry));
    }

    private void AddSpread(IBookPage page)
    {
        var spread = new BookSpread
        {
            Left = BookUIBuilder.Panel(_leftCanvas.transform, $"Spread{_spreads.Count}_Left"),
            Right = BookUIBuilder.Panel(_rightCanvas.transform, $"Spread{_spreads.Count}_Right"),
            Page = page
        };

        // 端に文字が張り付かないよう内側に余白を取る
        Inset(spread.Left, 40f);
        Inset(spread.Right, 40f);

        page.Build(spread.Left, spread.Right);
        spread.Left.gameObject.SetActive(false);
        spread.Right.gameObject.SetActive(false);

        _spreads.Add(spread);
    }

    private static void Inset(RectTransform rt, float margin)
    {
        rt.offsetMin = new Vector2(margin, margin + 60f);   // 下側はページ送りのぶん多めに空ける
        rt.offsetMax = new Vector2(-margin, -margin);
    }

    /// <summary>ページ下部の左右矢印とページ番号。</summary>
    private void BuildNavigation()
    {
        var prev = BookUIBuilder.LabelButton(_rightCanvas.transform, "PrevButton", "<", _navButtonSize, 48f,
                                             _textColor, new Color(0f, 0f, 0f, 0.06f), () => Step(-1));
        _prevButtonRt = prev.GetComponent<RectTransform>();

        var next = BookUIBuilder.LabelButton(_rightCanvas.transform, "NextButton", ">", _navButtonSize, 48f,
                                             _textColor, new Color(0f, 0f, 0f, 0.06f), () => Step(1));
        _nextButtonRt = next.GetComponent<RectTransform>();

        _pageNumberText = BookUIBuilder.Text(_rightCanvas.transform, "PageNumber", "", 34f,
                                             TextAlignmentOptions.Center, _textColor, _font);

        ApplyNavLayout();
    }

    /// <summary>
    /// 左右矢印とページ番号を、それぞれ指定されたページ・位置へ置き直す。
    /// Play 中に Inspector で動かしながら詰められるよう毎フレーム呼ぶ。
    /// </summary>
    private void ApplyNavLayout()
    {
        PlaceNavItem(_prevButtonRt, _prevButtonOnLeftPage, _prevButtonAnchor, _navButtonSize);
        PlaceNavItem(_nextButtonRt, _nextButtonOnLeftPage, _nextButtonAnchor, _navButtonSize);

        if (_pageNumberText != null)
        {
            _pageNumberText.gameObject.SetActive(_showPageNumber);
            PlaceNavItem(_pageNumberText.rectTransform, _pageNumberOnLeftPage,
                         _pageNumberAnchor, new Vector2(220f, 70f));
        }
    }

    private void PlaceNavItem(RectTransform rt, bool onLeftPage, Vector2 anchor, Vector2 size)
    {
        if (rt == null) return;

        Transform host = (onLeftPage ? _leftCanvas : _rightCanvas)?.transform;
        if (host != null && rt.parent != host) rt.SetParent(host, false);

        // ページ本体より後ろに描かれるとクリックを取られるので、常に最前面に置く
        rt.SetAsLastSibling();

        BookUIBuilder.Place(rt, anchor, Vector2.zero, size);
    }

    private void Step(int delta)
    {
        if (_spreads.Count == 0) return;

        // 端で止めると「押しても無反応」に見えるので巡回させる
        int next = (_spreadIndex + delta + _spreads.Count) % _spreads.Count;

        if (_logEvents) Debug.Log($"[BookUIController] ページ送り {delta:+#;-#} : {_spreadIndex} -> {next}", this);
        if (next != _spreadIndex) ShowSpread(next);
    }

    private void ShowSpread(int index)
    {
        for (int i = 0; i < _spreads.Count; i++)
        {
            bool active = i == index;
            _spreads[i].Left.gameObject.SetActive(active);
            _spreads[i].Right.gameObject.SetActive(active);
        }

        _spreadIndex = index;
        if (_pageNumberText != null) _pageNumberText.text = $"{index + 1} / {_spreads.Count}";

        RefreshCurrentSpread();
    }

    private void RefreshCurrentSpread()
    {
        if (_spreadIndex < 0 || _spreadIndex >= _spreads.Count) return;

        _spreads[_spreadIndex].Page.Refresh();
    }

    private void SetPagesVisible(bool visible)
    {
        SetGroupVisible(_leftGroup, visible);
        SetGroupVisible(_rightGroup, visible);
    }

    private static void SetGroupVisible(CanvasGroup group, bool visible)
    {
        if (group == null) return;

        float target = visible ? 1f : 0f;
        if (!Mathf.Approximately(group.alpha, target))
        {
            group.alpha = target;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}

/// <summary>ページ共通の見た目設定。各ページに配って回る。</summary>
public class BookPagePalette
{
    public Color Text;
    public Color Accent;
    public Color Selection;
    public TMP_FontAsset Font;
    public float ScrollSensitivity;
}

/// <summary>見開き 1 つ分のページ内容。</summary>
public interface IBookPage
{
    /// <summary>UI を組み立てる。開いた時に一度だけ呼ばれる。</summary>
    void Build(RectTransform left, RectTransform right);

    /// <summary>表示内容を最新の値に更新する。ページを開くたびに呼ばれる。</summary>
    void Refresh();
}
