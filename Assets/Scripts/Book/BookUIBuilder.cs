using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 本のページ UI を実行時に組み立てるための共通ヘルパー。
/// プレハブを手で作らずに済むよう、Reticle や RewardSelectionUI と同じく
/// コード側でヒエラルキーを生成する方式に揃えている。
/// </summary>
public static class BookUIBuilder
{
    /// <summary>親いっぱいに広がる空の RectTransform を作る。</summary>
    public static RectTransform Panel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Stretch(rt);
        return rt;
    }

    /// <summary>背景色付きのパネル。</summary>
    public static Image ColorPanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Stretch(rt);

        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    public static TextMeshProUGUI Text(Transform parent, string name, string content,
                                       float fontSize, TextAlignmentOptions alignment,
                                       Color color, TMP_FontAsset font = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        if (font != null) text.font = font;

        return text;
    }

    public static Image Sprite(Transform parent, string name, UnityEngine.Sprite sprite, bool preserveAspect = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = preserveAspect;
        img.raycastTarget = false;
        // スプライト未設定のスロットが白い四角として残らないようにする
        img.enabled = sprite != null;
        return img;
    }

    /// <summary>枠線だけの矩形。選択中アイテムのアウトライン表現に使う。</summary>
    public static Image OutlineFrame(Transform parent, string name, Color color, float thickness)
    {
        var root = Panel(parent, name);

        var img = root.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        img.type = Image.Type.Sliced;

        // 中央をくり抜いて枠に見せる。9-slice 用スプライトを用意しなくて済むよう
        // 内側に背景色のパネルを重ねる方式にする
        var inner = ColorPanel(root, "Inner", new Color(0f, 0f, 0f, 0f));
        inner.raycastTarget = false;
        inner.rectTransform.offsetMin = new Vector2(thickness, thickness);
        inner.rectTransform.offsetMax = new Vector2(-thickness, -thickness);

        return img;
    }

    public static Button Clickable(GameObject target, Action onClick)
    {
        var img = target.GetComponent<Image>();
        if (img == null)
        {
            // Button はレイキャスト対象のグラフィックが要るので透明な板を敷く
            img = target.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
        }
        img.raycastTarget = true;

        var button = target.GetComponent<Button>();
        if (button == null) button = target.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.RemoveAllListeners();
        if (onClick != null) button.onClick.AddListener(() => onClick());

        return button;
    }

    /// <summary>文字ラベル付きのボタンを作る。ページ送りの矢印や有効化ボタン用。</summary>
    public static Button LabelButton(Transform parent, string name, string label, Vector2 size,
                                     float fontSize, Color textColor, Color backColor, Action onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = size;

        var back = go.GetComponent<Image>();
        back.color = backColor;
        back.raycastTarget = true;

        var text = Text(rt, "Label", label, fontSize, TextAlignmentOptions.Center, textColor);
        Stretch(text.rectTransform);
        // "<" は TextMeshPro がタグの開始と解釈してしまうため、装飾機能を切って literal 扱いにする
        text.richText = false;

        var button = go.AddComponent<Button>();
        // 押した手応えが無いと壊れているように見えるので、色の変化を付ける
        button.targetGraphic = back;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 1.2f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.fadeDuration = 0.05f;
        button.colors = colors;
        if (onClick != null) button.onClick.AddListener(() => onClick());

        return button;
    }

    /// <summary>
    /// マウスホイールでスクロールできる領域を作る。
    /// 戻り値の content にコンテンツを並べる。
    /// </summary>
    public static RectTransform ScrollArea(Transform parent, string name, float scrollSensitivity,
                                           out ScrollRect scrollRect)
    {
        var viewport = Panel(parent, name);
        viewport.gameObject.AddComponent<RectMask2D>();

        // ScrollRect がホイール入力を拾うにはレイキャスト対象が必要
        var blocker = viewport.gameObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0f);
        blocker.raycastTarget = true;

        var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.spacing = 12f;

        scrollRect = viewport.gameObject.AddComponent<ScrollRect>();
        scrollRect.content = content;
        scrollRect.viewport = viewport;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = scrollSensitivity;

        return content;
    }

    /// <summary>アイテムを並べるグリッドを作る。</summary>
    public static RectTransform Grid(Transform parent, string name, Vector2 cellSize, Vector2 spacing, int columns)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        var grid = go.AddComponent<GridLayoutGroup>();
        grid.cellSize = cellSize;
        grid.spacing = spacing;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columns);
        grid.childAlignment = TextAnchor.UpperLeft;

        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return rt;
    }

    /// <summary>
    /// 親の中の相対位置（0..1）に、指定サイズで置く。
    /// RectTransform は既定のアンカーが左下なので、明示しないと
    /// anchoredPosition が親の左下基準になり画面外へ飛ぶ。
    /// </summary>
    public static void Place(RectTransform rt, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = offset;
    }

    /// <summary>親いっぱいに広げる。</summary>
    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>上端からの相対位置で配置する（0..1 は親に対する割合）。</summary>
    public static void AnchorRect(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
