using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OddOdds.UI.Settings.EditorTools
{
    /// <summary>
    /// 設定画面を構成する UI 部品の組み立てヘルパー。
    /// 画像素材を一切使わず、Image（スプライトなし = 単色の矩形）と TextMeshPro だけで作る。
    ///
    /// 「枠線」は白い矩形の上に一回り小さい黒矩形を重ねて表現している。
    /// 枠の太さ = 2 枚のサイズ差なので、太さを変えたいときは inset 値だけ変えればよい。
    /// </summary>
    public static class SettingsUIFactory
    {
        // ------------------------------------------------------------------
        // 基本
        // ------------------------------------------------------------------

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            return rt;
        }

        public static Image NewImage(string name, Transform parent, ThemeRole role)
        {
            var rt = NewRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = null;               // スプライトなし = 単色の矩形
            image.raycastTarget = false;       // 装飾は当たり判定を持たせない
            Tag(rt.gameObject, role);
            return image;
        }

        public static TextMeshProUGUI NewText(string name, Transform parent, ThemeRole role, string content)
        {
            var rt = NewRect(name, parent);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Left;
            Tag(rt.gameObject, role);
            return text;
        }

        /// <summary>テーマ適用の対象として登録する。</summary>
        public static ThemedElement Tag(GameObject go, ThemeRole role)
        {
            if (role == ThemeRole.None) return null;
            var element = go.GetComponent<ThemedElement>() ?? go.AddComponent<ThemedElement>();
            element.role = role;
            return element;
        }

        // ------------------------------------------------------------------
        // RectTransform ヘルパー
        // ------------------------------------------------------------------

        /// <summary>親いっぱいに広げる。inset を入れると内側に縮む。</summary>
        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        /// <summary>アンカーを一点に固定して、位置とサイズを指定する。</summary>
        public static RectTransform Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        // ------------------------------------------------------------------
        // 枠つきの箱（白枠 + 黒地）
        // ------------------------------------------------------------------

        /// <summary>
        /// 白い枠と黒い内側の 2 枚重ねを作る。これがこの UI の「枠線」の作り方。
        /// </summary>
        public static void CreateBorderBox(Transform parent, float thickness,
            out Image border, out Image inner,
            ThemeRole borderRole = ThemeRole.BoxBorder, ThemeRole innerRole = ThemeRole.BoxInner)
        {
            border = NewImage("Border", parent, borderRole);
            Stretch(border.rectTransform);

            inner = NewImage("Background", parent, innerRole);
            Stretch(inner.rectTransform, thickness);
        }

        // ------------------------------------------------------------------
        // タブ
        // ------------------------------------------------------------------

        public static SettingsTabButton CreateTab(Transform parent, SettingsTheme theme, string label)
        {
            var rt = NewRect("Tab_" + label, parent);
            rt.sizeDelta = theme.tabSize;

            // 生成順 = 重なり順。Border(白) → Background(黒) → SelectedFill(白) → Text
            CreateBorderBox(rt, theme.borderThick, out var border, out var background);
            background.raycastTarget = true;   // クリックを受けるのは内側

            // 選択中だけ出す白い塗り。Color Tint と色を奪い合わないよう独立させている
            var selectedFill = NewImage("SelectedFill", rt, ThemeRole.BoxBorder);
            Stretch(selectedFill.rectTransform, theme.borderThick);
            selectedFill.gameObject.SetActive(false);

            var text = NewText("Text", rt, ThemeRole.TextTab, label);
            Stretch(text.rectTransform);
            text.alignment = TextAlignmentOptions.Center;

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            theme.ApplyColors(button);

            rt.gameObject.AddComponent<UIButtonScale>();

            var tab = rt.gameObject.AddComponent<SettingsTabButton>();
            SetPrivate(tab, "_border", border);
            SetPrivate(tab, "_background", background);
            SetPrivate(tab, "_selectedFill", selectedFill);
            SetPrivate(tab, "_label", text);
            SetPrivate(tab, "_labelText", label);

            var layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = theme.tabSize.x;
            layout.preferredHeight = theme.tabSize.y;

            return tab;
        }

        // ------------------------------------------------------------------
        // 汎用ボタン（枠 + 背景 + 文字）
        // ------------------------------------------------------------------

        public static Button CreateButton(Transform parent, SettingsTheme theme, string name, string label, Vector2 size)
        {
            var rt = NewRect(name, parent);
            rt.sizeDelta = size;

            CreateBorderBox(rt, theme.borderThin, out _, out var background);
            background.raycastTarget = true;

            var text = NewText("Text", rt, ThemeRole.TextButton, label);
            Stretch(text.rectTransform);
            text.alignment = TextAlignmentOptions.Center;

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            theme.ApplyColors(button);

            rt.gameObject.AddComponent<UIButtonScale>();

            // 押下中は背景が白へ反転するので、文字は黒へ入れ替える
            var invert = rt.gameObject.AddComponent<ButtonLabelInvert>();
            invert.Bind(text, theme.screenBackground);

            return button;
        }

        // ------------------------------------------------------------------
        // 装飾線（細い線 + 45度回転した菱形）
        // ------------------------------------------------------------------

        public static RectTransform CreateDecorLine(Transform parent, SettingsTheme theme, string name = "DecorLine")
        {
            var root = NewRect(name, parent);
            root.sizeDelta = new Vector2(theme.decorLineWidth, theme.decorDiamondSize);

            // 線は親の幅に追従させる。Layout Group の下に置いても破綻しないようにするため
            var line = NewImage("Line", root, ThemeRole.DecorLine);
            var lineRect = line.rectTransform;
            lineRect.anchorMin = new Vector2(0f, 0.5f);
            lineRect.anchorMax = new Vector2(1f, 0.5f);
            lineRect.pivot = new Vector2(0.5f, 0.5f);
            lineRect.offsetMin = new Vector2(0f, -theme.borderThin * 0.5f);
            lineRect.offsetMax = new Vector2(0f, theme.borderThin * 0.5f);

            // 45度回した正方形 = 菱形。画像素材なしで装飾感を出す
            var diamond = NewImage("Diamond", root, ThemeRole.DecorDiamond);
            Anchor(diamond.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.one * theme.decorDiamondSize);
            diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            return root;
        }

        // ------------------------------------------------------------------
        // 設定行
        // ------------------------------------------------------------------

        /// <summary>「枠 + ラベル + コントロール枠」だけの空の行を作る。</summary>
        public static SettingRow CreateRow(Transform parent, SettingsTheme theme, string name, string label)
        {
            var rt = NewRect(name, parent);
            rt.sizeDelta = theme.rowSize;

            var border = NewImage("Border", rt, ThemeRole.RowBorder);
            Stretch(border.rectTransform);

            var background = NewImage("Background", rt, ThemeRole.RowBackground);
            Stretch(background.rectTransform, theme.borderThin);

            var text = NewText("Label", rt, ThemeRole.TextLabel, label);
            Anchor(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(theme.rowLabelPadding, 0f), new Vector2(500f, theme.rowSize.y));
            text.alignment = TextAlignmentOptions.Left;

            // コントロールは右寄せ。中身は呼び出し側が入れる
            var control = NewRect("Control", rt);
            Anchor(control, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-theme.rowControlPadding, 0f), theme.dropdownSize);

            var row = rt.gameObject.AddComponent<SettingRow>();
            SetPrivate(row, "_border", border);
            SetPrivate(row, "_background", background);
            SetPrivate(row, "_label", text);
            SetPrivate(row, "_control", control);
            SetPrivate(row, "_labelText", label);

            var layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = theme.rowSize.x;
            layout.preferredHeight = theme.rowSize.y;

            return row;
        }

        // ------------------------------------------------------------------
        // コントロール類
        // ------------------------------------------------------------------

        /// <summary>チェックボックス。チェックマークは画像ではなく TMP の「✓」で描く。</summary>
        public static Toggle CreateToggle(Transform parent, SettingsTheme theme, string label)
        {
            var rt = NewRect("Toggle", parent);
            rt.sizeDelta = new Vector2(theme.dropdownSize.x, theme.toggleBoxSize);

            var boxRoot = NewRect("Box", rt);
            Anchor(boxRoot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, Vector2.one * theme.toggleBoxSize);

            // 生成順 = 重なり順。Border(白) → Background(黒) → OnFill(白) → Checkmark(黒文字)
            CreateBorderBox(boxRoot, theme.borderThin, out _, out var inner);
            inner.raycastTarget = true;

            // ON のとき白く塗りつぶし、その上に黒いチェックを出す
            var onFill = NewImage("OnFill", boxRoot, ThemeRole.BoxBorder);
            Stretch(onFill.rectTransform, theme.borderThin);

            var check = NewText("Checkmark", boxRoot, ThemeRole.TextOnLight, "✓");
            Stretch(check.rectTransform);
            check.alignment = TextAlignmentOptions.Center;

            var labelText = NewText("Label", rt, ThemeRole.TextValue, label);
            Anchor(labelText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(theme.toggleBoxSize + 14f, 0f),
                new Vector2(theme.dropdownSize.x - theme.toggleBoxSize - 14f, theme.toggleBoxSize));

            var toggle = rt.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = inner;
            toggle.graphic = onFill;                 // ON/OFF で切り替わる部分
            toggle.isOn = false;
            theme.ApplyColors(toggle);

            // graphic は 1 つしか指定できないので、チェック文字は追従用に別管理する
            var follower = rt.gameObject.AddComponent<ToggleGraphicFollower>();
            follower.Bind(toggle, check.gameObject);

            return toggle;
        }

        public static Slider CreateSlider(Transform parent, SettingsTheme theme)
        {
            var rt = NewRect("Slider", parent);
            rt.sizeDelta = theme.sliderSize;

            // 溝
            var background = NewImage("Background", rt, ThemeRole.SliderBackground);
            Anchor(background.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(theme.sliderSize.x, theme.sliderThickness));

            // 到達部分
            var fillArea = NewRect("Fill Area", rt);
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(0f, -theme.sliderThickness * 0.5f + theme.sliderSize.y * 0.5f);
            fillArea.offsetMax = new Vector2(0f, theme.sliderThickness * 0.5f - theme.sliderSize.y * 0.5f);

            var fill = NewImage("Fill", fillArea, ThemeRole.SliderFill);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            // つまみ
            var handleArea = NewRect("Handle Slide Area", rt);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(theme.sliderSize.y * 0.5f, 0f);
            handleArea.offsetMax = new Vector2(-theme.sliderSize.y * 0.5f, 0f);

            var handle = NewImage("Handle", handleArea, ThemeRole.SliderHandle);
            handle.raycastTarget = true;
            handle.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            handle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            handle.rectTransform.sizeDelta = Vector2.one * theme.sliderSize.y;

            var slider = rt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.5f;
            // つまみは白。黒地用の Color Tint を当てると潰れるので Handle 指定にする
            rt.gameObject.AddComponent<SelectableTint>().mode = TintMode.Handle;
            theme.ApplyTint(slider, TintMode.Handle);

            return slider;
        }

        public static Scrollbar CreateScrollbar(Transform parent, SettingsTheme theme)
        {
            var rt = NewRect("Scrollbar Vertical", parent);

            // 溝はつまみより細くする。この差が「細い線」の印象を作る。
            // Scrollbar 本体の幅 = つまみ幅、溝だけ内側に絞る
            var background = NewImage("Background", rt, ThemeRole.ScrollbarBackground);
            var inset = Mathf.Max(0f, (theme.scrollbarHandleWidth - theme.scrollbarTrackWidth) * 0.5f);
            Stretch(background.rectTransform);
            background.rectTransform.offsetMin = new Vector2(inset, 0f);
            background.rectTransform.offsetMax = new Vector2(-inset, 0f);
            background.raycastTarget = true;

            var slidingArea = NewRect("Sliding Area", rt);
            Stretch(slidingArea);

            var handle = NewImage("Handle", slidingArea, ThemeRole.ScrollbarHandle);
            handle.raycastTarget = true;
            Stretch(handle.rectTransform);

            var scrollbar = rt.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            rt.gameObject.AddComponent<SelectableTint>().mode = TintMode.Handle;
            theme.ApplyTint(scrollbar, TintMode.Handle);

            return scrollbar;
        }

        // ------------------------------------------------------------------
        // private フィールドへの代入（生成ツール専用）
        // ------------------------------------------------------------------

        /// <summary>
        /// SerializeField な private 変数に生成物を差し込む。
        /// 実行時コードからは使わない。あくまで生成ツールが配線するためのもの。
        /// </summary>
        public static void SetPrivate(Object target, string fieldName, object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning($"[SettingsUIFactory] {target.GetType().Name}.{fieldName} が見つかりません。");
                return;
            }

            if (value is string s) property.stringValue = s;
            else property.objectReferenceValue = value as Object;

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
