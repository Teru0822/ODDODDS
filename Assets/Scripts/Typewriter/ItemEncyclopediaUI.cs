using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// タイプライターUIのアイテムタブに表示するアイテム図鑑。
/// _itemTabStub のルートに追加して使用する。
/// アイテムをクリックすると RewardSelectionUI の既存プレビュー枠・説明欄に情報を表示する。
/// セルサイズはパネル幅から自動計算する。
/// </summary>
[DisallowMultipleComponent]
public class ItemEncyclopediaUI : MonoBehaviour
{
    [Header("データ")]
    [Tooltip("図鑑に表示するアイテムデータベース。未設定なら ItemPanelManager から自動取得")]
    [SerializeField] private ItemDataBase _itemDataBase;

    [Header("グリッドレイアウト")]
    [Tooltip("1行あたりの列数")]
    [SerializeField] private int _columnCount = 4;
    [Tooltip("セル間のスペース (px)")]
    [SerializeField] private float _cellSpacing = 8f;
    [Tooltip("グリッド外周のパディング (px)")]
    [SerializeField] private int _gridPadding = 10;
    [Tooltip("セルの縦横比（1.0 = 正方形）")]
    [SerializeField, Range(0.5f, 2f)] private float _cellAspect = 1f;

    [Header("セルカラー")]
    [SerializeField] private Color _bgUnknown  = new Color(0.12f, 0.10f, 0.16f, 1f);
    [SerializeField] private Color _bgOwned    = new Color(0.18f, 0.14f, 0.28f, 1f);
    [SerializeField] private Color _bgSelected = new Color(0.28f, 0.20f, 0.44f, 1f);
    [Tooltip("未入手セルの内枠色")]
    [SerializeField] private Color _rimUnknown  = new Color(0.22f, 0.18f, 0.30f, 1f);
    [Tooltip("入手済みセルの内枠色（淡い紫）")]
    [SerializeField] private Color _rimOwned    = new Color(0.50f, 0.35f, 0.80f, 0.9f);
    [Tooltip("選択中セルの内枠色（明るい紫）")]
    [SerializeField] private Color _rimSelected = new Color(0.85f, 0.70f, 1.00f, 1f);
    [Tooltip("未入手アイコンを暗くする色（silhouetteImage 未設定時）")]
    [SerializeField] private Color _silhouetteColor = new Color(0.07f, 0.05f, 0.10f, 1f);

    // ────────────────────────────────────────────────
    // ランタイム
    // ────────────────────────────────────────────────
    private class EntryView
    {
        public ItemData data;
        public Image    bgImage;
        public Image    rimImage;
        public Image    iconImage;
        public Button   button;
        public bool     isOwned;
    }

    private readonly List<EntryView> _entries = new List<EntryView>();
    private RectTransform   _gridContent;
    private GridLayoutGroup _gridLayout;
    private ItemPanelManager   _itemPanelManager;
    private RewardSelectionUI  _rewardSelectionUI;
    private EntryView _selected;

    // ────────────────────────────────────────────────
    // ライフサイクル
    // ────────────────────────────────────────────────

    private void Awake()
    {
        ItemPanelManager.OnItemObtained += OnItemObtained;
        if (_rewardSelectionUI == null)
            _rewardSelectionUI = GetComponentInParent<RewardSelectionUI>(true);
    }

    private void OnDestroy()
    {
        ItemPanelManager.OnItemObtained -= OnItemObtained;
    }

    private void OnEnable()
    {
        // RectTransform のサイズ確定後に Refresh するため 1 フレーム待つ
        StartCoroutine(RefreshNextFrame());
    }

    private IEnumerator RefreshNextFrame()
    {
        yield return null;
        Refresh();
    }

    // ────────────────────────────────────────────────
    // 公開 API
    // ────────────────────────────────────────────────

    public void Refresh()
    {
        if (_itemPanelManager == null)
            _itemPanelManager = FindFirstObjectByType<ItemPanelManager>(FindObjectsInactive.Include);
        if (_itemDataBase == null && _itemPanelManager != null)
            _itemDataBase = _itemPanelManager.ItemDatabase;
        if (_rewardSelectionUI == null)
            _rewardSelectionUI = GetComponentInParent<RewardSelectionUI>(true);

        if (_itemDataBase == null || _itemDataBase.itemDataBase == null)
        {
            Debug.LogWarning("[ItemEncyclopediaUI] ItemDataBase が設定されていません。", this);
            return;
        }

        EnsureScrollView();
        RecalcCellSize();

        var all = _itemDataBase.itemDataBase;
        if (_entries.Count != all.Count)
            BuildEntries(all);

        for (int i = 0; i < _entries.Count; i++)
        {
            bool owned = _itemPanelManager != null && _itemPanelManager.IsItemOwned(all[i].id);
            ApplyVisual(_entries[i], owned);
        }
    }

    // ────────────────────────────────────────────────
    // 内部処理
    // ────────────────────────────────────────────────

    private void OnItemObtained(int id)
    {
        foreach (var e in _entries)
            if (e.data != null && e.data.id == id) { ApplyVisual(e, true); break; }
    }

    /// <summary>スクロールビューを初回のみ生成する</summary>
    private void EnsureScrollView()
    {
        if (_gridContent != null) return;

        var sv = new GameObject("Scroll", typeof(ScrollRect));
        sv.transform.SetParent(transform, false);
        Stretch(sv.GetComponent<RectTransform>());

        var vp = new GameObject("Viewport", typeof(RectMask2D));
        vp.transform.SetParent(sv.transform, false);
        Stretch(vp.GetComponent<RectTransform>());

        var ct = new GameObject("Content", typeof(RectTransform));
        ct.transform.SetParent(vp.transform, false);
        _gridContent = ct.GetComponent<RectTransform>();
        _gridContent.anchorMin = new Vector2(0, 1);
        _gridContent.anchorMax = new Vector2(1, 1);
        _gridContent.pivot     = new Vector2(0.5f, 1f);
        _gridContent.offsetMin = Vector2.zero;
        _gridContent.offsetMax = Vector2.zero;

        _gridLayout = ct.AddComponent<GridLayoutGroup>();
        _gridLayout.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        _gridLayout.constraintCount = _columnCount;
        _gridLayout.childAlignment  = TextAnchor.UpperCenter;
        _gridLayout.spacing         = new Vector2(_cellSpacing, _cellSpacing);
        _gridLayout.padding         = new RectOffset(_gridPadding, _gridPadding, _gridPadding, _gridPadding);

        var csf = ct.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        var sr = sv.GetComponent<ScrollRect>();
        sr.content          = _gridContent;
        sr.viewport         = vp.GetComponent<RectTransform>();
        sr.horizontal       = false;
        sr.vertical         = true;
        sr.scrollSensitivity = 30f;
        sr.movementType     = ScrollRect.MovementType.Clamped;
    }

    /// <summary>パネル幅からセルサイズを計算して GridLayoutGroup に反映する</summary>
    private void RecalcCellSize()
    {
        if (_gridLayout == null) return;
        Canvas.ForceUpdateCanvases();
        float panelW = GetComponent<RectTransform>().rect.width;
        if (panelW <= 0) return;
        float usable = panelW - _gridPadding * 2f - (_columnCount - 1) * _cellSpacing;
        float w = Mathf.Max(usable / _columnCount, 40f);
        _gridLayout.cellSize = new Vector2(w, w * _cellAspect);
    }

    private void BuildEntries(List<ItemData> items)
    {
        foreach (var e in _entries)
            if (e.button != null) Destroy(e.button.gameObject);
        _entries.Clear();
        _selected = null;

        foreach (var item in items)
        {
            if (item == null) continue;

            // ── セル外側：背景色 ──────────────────────────────
            var cell = new GameObject($"Cell_{item.id}", typeof(Image), typeof(Button));
            cell.transform.SetParent(_gridContent, false);
            var bg  = cell.GetComponent<Image>();
            bg.color = _bgUnknown;
            var btn = cell.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;

            // ── 内枠（リム）：2 px インセット ─────────────────
            var rim = MakeFullRect("Rim", cell.transform, 2f);
            rim.color = _rimUnknown;

            // ── アイコン：内側に余白を確保 ──────────────────────
            var iconGo = new GameObject("Icon", typeof(Image));
            iconGo.transform.SetParent(cell.transform, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            float pad = Mathf.Max(8f, _gridLayout.cellSize.x * 0.10f);
            iconRect.offsetMin = new Vector2(pad, pad);
            iconRect.offsetMax = new Vector2(-pad, -pad);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.sprite        = item.iconImage;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget  = false;

            var entry = new EntryView
            {
                data      = item,
                bgImage   = bg,
                rimImage  = rim,
                iconImage = iconImg,
                button    = btn,
            };
            _entries.Add(entry);

            var cap = entry;
            btn.onClick.AddListener(() => OnCellClicked(cap));
        }
    }

    private void ApplyVisual(EntryView e, bool owned)
    {
        e.isOwned = owned;
        bool sel  = _selected == e;

        if (owned)
        {
            e.iconImage.sprite = e.data.iconImage;
            e.iconImage.color  = Color.white;
            e.bgImage.color    = sel ? _bgSelected : _bgOwned;
            e.rimImage.color   = sel ? _rimSelected : _rimOwned;
        }
        else
        {
            if (e.data.silhouetteImage != null)
            {
                e.iconImage.sprite = e.data.silhouetteImage;
                e.iconImage.color  = Color.white;
            }
            else
            {
                e.iconImage.sprite = e.data.iconImage;
                e.iconImage.color  = _silhouetteColor;
            }
            e.bgImage.color  = sel ? _bgSelected : _bgUnknown;
            e.rimImage.color = sel ? _rimSelected : _rimUnknown;
        }
    }

    private void OnCellClicked(EntryView e)
    {
        // 前の選択を解除
        var prev = _selected;
        _selected = e;
        if (prev != null) ApplyVisual(prev, prev.isOwned);
        ApplyVisual(e, e.isOwned);

        if (_rewardSelectionUI == null) return;
        if (e.isOwned)
            _rewardSelectionUI.ShowItemPreview(e.data.iconImage, e.data.itemName, e.data.description);
        else
            _rewardSelectionUI.ShowItemPreviewUnknown();
    }

    // ────────────────────────────────────────────────
    // ユーティリティ
    // ────────────────────────────────────────────────

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>親の RectTransform を inset だけ内側に縮小した Image を生成する</summary>
    private static Image MakeFullRect(string name, Transform parent, float inset)
    {
        var go   = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var rt   = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        go.GetComponent<Image>().raycastTarget = false;
        return go.GetComponent<Image>();
    }
}
