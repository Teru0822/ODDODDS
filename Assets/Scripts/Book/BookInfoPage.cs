using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 1 見開き目。
///   左ページ … 「Information」。所持金(DC)と各コインの所持枚数をアイコン付きで並べる。
///   右ページ … 「Next Debt Collection」。次回の取り立て金額と、取り立てまでの残りターン。
/// </summary>
public class BookInfoPage : IBookPage
{
    private readonly BookPagePalette _palette;
    private readonly Sprite _goldIcon;
    private readonly Sprite _silverIcon;
    private readonly Sprite _bronzeIcon;
    private readonly Sprite _diamondIcon;

    private TextMeshProUGUI _moneyValue;
    private TextMeshProUGUI _goldValue;
    private TextMeshProUGUI _silverValue;
    private TextMeshProUGUI _bronzeValue;
    private TextMeshProUGUI _diamondValue;

    private TextMeshProUGUI _quotaValue;
    private TextMeshProUGUI _turnValue;

    public BookInfoPage(BookPagePalette palette, Sprite gold, Sprite silver, Sprite bronze, Sprite diamond)
    {
        _palette = palette;
        _goldIcon = gold;
        _silverIcon = silver;
        _bronzeIcon = bronze;
        _diamondIcon = diamond;
    }

    public void Build(RectTransform left, RectTransform right)
    {
        BuildLeft(left);
        BuildRight(right);
    }

    private void BuildLeft(RectTransform page)
    {
        // タイトルは左上
        var title = BookUIBuilder.Text(page, "Title", "Information", 56f,
                                       TextAlignmentOptions.TopLeft, _palette.Accent, _palette.Font);
        BookUIBuilder.AnchorRect(title.rectTransform, 0f, 0.88f, 1f, 1f);

        var money = BookUIBuilder.Text(page, "MoneyLabel", "Money", 30f,
                                       TextAlignmentOptions.TopLeft, _palette.Text, _palette.Font);
        BookUIBuilder.AnchorRect(money.rectTransform, 0f, 0.78f, 1f, 0.86f);

        _moneyValue = BookUIBuilder.Text(page, "MoneyValue", "0 DC", 52f,
                                         TextAlignmentOptions.TopRight, _palette.Text, _palette.Font);
        BookUIBuilder.AnchorRect(_moneyValue.rectTransform, 0f, 0.70f, 1f, 0.80f);

        var coinsLabel = BookUIBuilder.Text(page, "CoinsLabel", "Coins", 30f,
                                            TextAlignmentOptions.TopLeft, _palette.Text, _palette.Font);
        BookUIBuilder.AnchorRect(coinsLabel.rectTransform, 0f, 0.60f, 1f, 0.68f);

        var list = new GameObject("CoinList", typeof(RectTransform)).GetComponent<RectTransform>();
        list.SetParent(page, false);
        BookUIBuilder.AnchorRect(list, 0f, 0.10f, 1f, 0.60f);

        var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 16f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        _goldValue = CoinRow(list, "Gold", "Gold Coin", _goldIcon);
        _silverValue = CoinRow(list, "Silver", "Silver Coin", _silverIcon);
        _bronzeValue = CoinRow(list, "Bronze", "Bronze Coin", _bronzeIcon);
        _diamondValue = CoinRow(list, "Diamond", "Black Diamond", _diamondIcon);
    }

    /// <summary>アイコン＋名前＋枚数の 1 行。</summary>
    private TextMeshProUGUI CoinRow(RectTransform parent, string name, string label, Sprite icon)
    {
        var row = new GameObject(name + "Row", typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(parent, false);
        row.sizeDelta = new Vector2(0f, 72f);

        var element = row.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 72f;

        var image = BookUIBuilder.Sprite(row, "Icon", icon);
        image.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        image.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        image.rectTransform.pivot = new Vector2(0f, 0.5f);
        image.rectTransform.anchoredPosition = Vector2.zero;
        image.rectTransform.sizeDelta = new Vector2(64f, 64f);

        var text = BookUIBuilder.Text(row, "Label", label, 32f,
                                      TextAlignmentOptions.Left, _palette.Text, _palette.Font);
        BookUIBuilder.AnchorRect(text.rectTransform, 0f, 0f, 0.7f, 1f);
        text.rectTransform.offsetMin = new Vector2(80f, 0f);

        var value = BookUIBuilder.Text(row, "Value", "0", 38f,
                                       TextAlignmentOptions.Right, _palette.Text, _palette.Font);
        BookUIBuilder.AnchorRect(value.rectTransform, 0.6f, 0f, 1f, 1f);

        return value;
    }

    private void BuildRight(RectTransform page)
    {
        // こちらのタイトルは中央上
        var title = BookUIBuilder.Text(page, "Title", "Next Debt Collection", 48f,
                                       TextAlignmentOptions.Top, _palette.Accent, _palette.Font);
        BookUIBuilder.AnchorRect(title.rectTransform, 0f, 0.86f, 1f, 1f);

        var quotaLabel = BookUIBuilder.Text(page, "QuotaLabel", "Amount Due", 30f,
                                            TextAlignmentOptions.Center, _palette.Text, _palette.Font);
        BookUIBuilder.AnchorRect(quotaLabel.rectTransform, 0f, 0.66f, 1f, 0.74f);

        _quotaValue = BookUIBuilder.Text(page, "QuotaValue", "0 DC", 72f,
                                         TextAlignmentOptions.Center, _palette.Accent, _palette.Font);
        BookUIBuilder.AnchorRect(_quotaValue.rectTransform, 0f, 0.54f, 1f, 0.66f);

        var turnLabel = BookUIBuilder.Text(page, "TurnLabel", "Turns Remaining", 30f,
                                           TextAlignmentOptions.Center, _palette.Text, _palette.Font);
        BookUIBuilder.AnchorRect(turnLabel.rectTransform, 0f, 0.38f, 1f, 0.46f);

        _turnValue = BookUIBuilder.Text(page, "TurnValue", "0", 64f,
                                        TextAlignmentOptions.Center, _palette.Text, _palette.Font);
        BookUIBuilder.AnchorRect(_turnValue.rectTransform, 0f, 0.26f, 1f, 0.38f);
    }

    public void Refresh()
    {
        var money = MoneyManager.Instance;
        if (money != null)
        {
            if (_moneyValue != null) _moneyValue.text = $"{Mathf.FloorToInt(money.CurrentMoney)} DC";
            if (_quotaValue != null) _quotaValue.text = $"{money.GetQuotaThisTime()} DC";
            if (_turnValue != null) _turnValue.text = money.NextDebtCollectionTurnCount.ToString();
        }

        var wallet = PlayerWallet.Local;
        if (wallet != null)
        {
            if (_goldValue != null) _goldValue.text = wallet.GoldCoins.ToString();
            if (_silverValue != null) _silverValue.text = wallet.SilverCoins.ToString();
            if (_bronzeValue != null) _bronzeValue.text = wallet.BronzeCoins.ToString();
            if (_diamondValue != null) _diamondValue.text = wallet.BlackDiamonds.ToString();
        }
    }
}
