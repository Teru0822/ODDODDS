using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 2 見開き目。
///   左ページ … これまでにアンロックしたアイテムをカテゴリー別に並べる。
///               未アンロックはシルエット画像、アンロック済みはカラー画像。
///               1 ページに収まらなければマウスホイールでスクロールする。
///   右ページ … 選択中アイテムの画像を大きく表示し、その下に名前と説明を出す。
/// </summary>
public class BookItemsPage : IBookPage
{
    private readonly BookPagePalette _palette;
    private Language _language = Language.JP;

    private RectTransform _scrollContent;
    private readonly List<Slot> _slots = new List<Slot>();

    private Image _detailImage;
    private TextMeshProUGUI _detailName;
    private TextMeshProUGUI _detailCategory;
    private TextMeshProUGUI _detailDescription;

    private ItemData _selected;

    public void SetLocalize(Language language)
    {
        _language = language;
        Refresh();
    }

    /// <summary>グリッド上の 1 マス。</summary>
    private class Slot
    {
        public ItemData Data;
        public Image Icon;
        public GameObject Outline;
    }

    public BookItemsPage(BookPagePalette palette)
    {
        _palette = palette;
    }

    public void Build(RectTransform left, RectTransform right)
    {
        BuildLeft(left);
        BuildRight(right);
    }

    private void BuildLeft(RectTransform page)
    {
        var title = BookUIBuilder.Text(page, "Title", "Items", 56f,
                                       TextAlignmentOptions.TopLeft, _palette.Accent, _palette.Font, true,"BookUITable","ItemDetail",true,60,18);
        BookUIBuilder.AnchorRect(title.rectTransform, 0f, 0.90f, 1f, 1f);

        var area = BookUIBuilder.Panel(page, "ScrollArea");
        BookUIBuilder.AnchorRect(area, 0f, 0f, 1f, 0.89f);

        _scrollContent = BookUIBuilder.ScrollArea(area, "Viewport", _palette.ScrollSensitivity, out _);
    }

    private void BuildRight(RectTransform page)
    {
        var title = BookUIBuilder.Text(page, "Title", "Detail", 48f,
                                       TextAlignmentOptions.Top, _palette.Accent, _palette.Font, true,"BookUITable","ItemDetail",true,60,18);
        BookUIBuilder.AnchorRect(title.rectTransform, 0f, 0.92f, 1f, 1f);

        _detailImage = BookUIBuilder.Sprite(page, "DetailImage", null);
        BookUIBuilder.AnchorRect(_detailImage.rectTransform, 0.15f, 0.55f, 0.85f, 0.90f);

        _detailName = BookUIBuilder.Text(page, "DetailName", "", 44f,
                                         TextAlignmentOptions.Top, _palette.Text, _palette.Font);
        BookUIBuilder.AnchorRect(_detailName.rectTransform, 0f, 0.46f, 1f, 0.54f);

        _detailCategory = BookUIBuilder.Text(page, "DetailCategory", "", 28f,
                                             TextAlignmentOptions.Top, _palette.Accent, _palette.Font);
        BookUIBuilder.AnchorRect(_detailCategory.rectTransform, 0f, 0.40f, 1f, 0.46f);

        _detailDescription = BookUIBuilder.Text(page, "DetailDescription", "", 30f,
                                                TextAlignmentOptions.TopLeft, _palette.Text, _palette.Font, false,"","",true,60,18);
        BookUIBuilder.AnchorRect(_detailDescription.rectTransform, 0f, 0.02f, 1f, 0.38f);

        ShowDetail(null);
    }

    public void Refresh()
    {
        RebuildGrid();
        ShowDetail(_selected);
    }

    /// <summary>カテゴリー見出し＋グリッドを組み直す。所持状況が変わるので開くたびに作り直す。</summary>
    private void RebuildGrid()
    {
        if (_scrollContent == null) return;

        for (int i = _scrollContent.childCount - 1; i >= 0; i--)
        {
            Object.Destroy(_scrollContent.GetChild(i).gameObject);
        }
        _slots.Clear();

        var manager = ItemPanelManager.Instance;
        var database = manager != null ? manager.ItemDatabase : null;
        if (database == null)
        {
            BookUIBuilder.Text(_scrollContent, "Empty", "Item database not found", 28f,
                               TextAlignmentOptions.Top, _palette.Text, _palette.Font);
            return;
        }

        // 図鑑なので、所持していないものも含めて全件を対象にする
        var all = new List<ItemData>();
        if (database.itemDataBase != null) all.AddRange(database.itemDataBase.Where(d => d != null));
        if (database.craneItemDataBase != null)
        {
            all.AddRange(database.craneItemDataBase.Where(d => d != null && !all.Contains(d)));
        }

        AddCategory("Exchange", all.Where(d => d.itemCategory == ItemCategory.Exchange));
        AddCategory("Consumable", all.Where(d => d.itemCategory == ItemCategory.Consume));
        AddCategory("Permanent", all.Where(d => d.itemCategory == ItemCategory.Important));
    }

    private void AddCategory(string label, IEnumerable<ItemData> items)
    {
        var list = items.ToList();
        if (list.Count == 0) return;

        var heading = BookUIBuilder.Text(_scrollContent, $"Heading_{label}", label, 34f,
                                         TextAlignmentOptions.Left, _palette.Accent, _palette.Font);
        var headingElement = heading.gameObject.AddComponent<LayoutElement>();
        headingElement.preferredHeight = 48f;

        var grid = BookUIBuilder.Grid(_scrollContent, $"Grid_{label}",
                                      new Vector2(96f, 96f), new Vector2(12f, 12f), 5);

        foreach (var data in list) AddSlot(grid, data);
    }

    private void AddSlot(RectTransform grid, ItemData data)
    {
        var slotGo = new GameObject($"Slot_{data.id}", typeof(RectTransform));
        var slotRt = slotGo.GetComponent<RectTransform>();
        slotRt.SetParent(grid, false);

        bool owned = IsOwned(data);

        // 未アンロックはシルエット。シルエット未設定ならカラー画像を暗く落として代用する
        Sprite sprite = owned ? data.iconImage : (data.silhouetteImage != null ? data.silhouetteImage : data.iconImage);
        var icon = BookUIBuilder.Sprite(slotRt, "Icon", sprite);
        BookUIBuilder.Stretch(icon.rectTransform);
        if (!owned && data.silhouetteImage == null)
        {
            icon.color = new Color(0f, 0f, 0f, 0.55f);
        }

        var outline = BookUIBuilder.OutlineFrame(slotRt, "Outline", _palette.Selection, 4f);
        outline.gameObject.SetActive(false);

        var slot = new Slot { Data = data, Icon = icon, Outline = outline.gameObject };
        _slots.Add(slot);

        // アンロック済みのものだけ選択できる
        if (owned)
        {
            BookUIBuilder.Clickable(slotGo, () => Select(slot));
        }
    }

    private static bool IsOwned(ItemData data)
    {
        var manager = ItemPanelManager.Instance;
        return manager != null && manager.IsItemOwned(data.id);
    }

    private void Select(Slot slot)
    {
        _selected = slot.Data;

        foreach (var s in _slots)
        {
            if (s.Outline != null) s.Outline.SetActive(s == slot);
        }

        ShowDetail(_selected);
    }

    private void ShowDetail(ItemData data)
    {
        if (_detailImage == null) return;

        if (data == null)
        {
            _detailImage.sprite = null;
            _detailImage.enabled = false;
            if (_detailName != null) _detailName.text = "";
            if (_detailCategory != null) _detailCategory.text = "";
            if (_detailDescription != null) _detailDescription.text = "Select an item.";
            return;
        }

        _detailImage.sprite = data.iconImage;
        _detailImage.enabled = data.iconImage != null;

        if (_detailName != null) _detailName.text = data.itemName != null && data.itemName.Length > (int)_language ? data.itemName[(int)_language] : "";
        if (_detailCategory != null) _detailCategory.text = CategoryLabel(data.itemCategory);
        if (_detailDescription != null) _detailDescription.text = data.description != null && data.description.Length > (int)_language ? data.description[(int)_language] : "";
    }

    private static string CategoryLabel(ItemCategory category)
    {
        switch (category)
        {
            case ItemCategory.Exchange: return "Exchange";
            case ItemCategory.Consume: return "Consumable";
            case ItemCategory.Important: return "Permanent";
            default: return "";
        }
    }
}
