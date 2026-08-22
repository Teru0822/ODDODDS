using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static OddOdds.UI.Settings.EditorTools.SettingsUIFactory;

namespace OddOdds.UI.Settings.EditorTools
{
    /// <summary>
    /// 設定画面の骨組みを一発で生成するツール。
    ///
    /// 生成後は普通の GameObject なので、Inspector で自由に位置・サイズを調整できる。
    /// 色やフォントを変えたいときは SettingsTheme.asset を編集して
    /// SettingsScreen の右クリック →「テーマを適用」を実行する。
    /// </summary>
    public static class SettingsUIBuilder
    {
        private const string RootFolder   = "Assets/ODDODDS/UI";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string ThemePath    = RootFolder + "/SettingsTheme.asset";

        private const string DisplayFontPath = "Assets/Fonts/Zen_Old_Mincho/ZenOldMincho-Medium SDF.asset";
        private const string BodyFontPath    = "Assets/Resources/Font/Noto_Sans_JP/NotoSansJP-Medium SDF.asset";

        private static readonly string[] TabLabels = { "グラフィック", "サウンド", "キーバインド", "その他" };

        // ==================================================================
        [MenuItem("ODD ODDS/UI/設定画面の骨組みを生成", false, 0)]
        public static void Build()
        {
            var theme = LoadOrCreateTheme();

            var canvasGo = new GameObject("SettingsCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "設定画面を生成");

            ConfigureCanvas(canvasGo);

            var canvasRect = (RectTransform)canvasGo.transform;

            // 開閉対象。Canvas 自体ではなくこの Root を SetActive する
            var root = NewRect("Root", canvasRect);
            Stretch(root);

            var background = NewImage("Background", root, ThemeRole.ScreenBackground);
            Stretch(background.rectTransform);
            background.raycastTarget = true;   // 背面クリックが後ろへ抜けないようにする

            var tabs   = BuildHeader(root, theme, out var closeButton);
            var pages  = BuildContent(root, theme, out var content);
            BuildLeftSide(root, theme);
            BuildFooter(root, theme);

            var screen = canvasGo.AddComponent<SettingsScreen>();
            WireScreen(screen, theme, root.gameObject, tabs, pages, closeButton);

            SavePrefabs(content, theme);

            screen.ApplyTheme();

            Selection.activeGameObject = canvasGo;
            EditorGUIUtility.PingObject(canvasGo);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvasGo.scene);

            Debug.Log("[SettingsUIBuilder] 設定画面の骨組みを生成しました。\n" +
                      "Escape を割り当てるには SettingsScreen の Open Action に " +
                      "InputSystem_Actions の OpenSetting を設定してください。", canvasGo);
        }

        [MenuItem("ODD ODDS/UI/選択中の設定画面にテーマを再適用", false, 1)]
        public static void ReapplyTheme()
        {
            var screen = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<SettingsScreen>()
                : Object.FindFirstObjectByType<SettingsScreen>();

            if (screen == null)
            {
                Debug.LogWarning("[SettingsUIBuilder] SettingsScreen が見つかりません。");
                return;
            }
            screen.ApplyTheme();
        }

        // ==================================================================
        // テーマ
        // ==================================================================

        private static SettingsTheme LoadOrCreateTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<SettingsTheme>(ThemePath);
            if (theme != null) return theme;

            EnsureFolder(RootFolder);
            theme = ScriptableObject.CreateInstance<SettingsTheme>();
            theme.displayFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayFontPath);
            theme.bodyFont    = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);

            if (theme.displayFont == null) Debug.LogWarning($"[SettingsUIBuilder] 見出しフォントが見つかりません: {DisplayFontPath}");
            if (theme.bodyFont == null)    Debug.LogWarning($"[SettingsUIBuilder] 本文フォントが見つかりません: {BodyFontPath}");

            AssetDatabase.CreateAsset(theme, ThemePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SettingsUIBuilder] テーマを作成しました: {ThemePath}", theme);
            return theme;
        }

        // ==================================================================
        // Canvas
        // ==================================================================

        private static void ConfigureCanvas(GameObject go)
        {
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // 0.5 にすると縦横どちらに伸びても破綻しにくい
            scaler.matchWidthOrHeight = 0.5f;
        }

        // ==================================================================
        // ヘッダー（タブ + 閉じる + 装飾線）
        // ==================================================================

        private static List<SettingsTabButton> BuildHeader(RectTransform parent, SettingsTheme theme, out Button closeButton)
        {
            var header = NewRect("Header", parent);
            Anchor(header, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -60f), new Vector2(1920f, 160f));

            // --- タブ ---
            var tabsRoot = NewRect("Tabs", header);
            Anchor(tabsRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(theme.tabSize.x * TabLabels.Length, theme.tabSize.y));

            var layout = tabsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 0f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var tabs = new List<SettingsTabButton>();
            foreach (var label in TabLabels)
                tabs.Add(CreateTab(tabsRoot, theme, label));

            // --- 閉じるボタン ---
            closeButton = CreateButton(header, theme, "CloseButton", "✕", new Vector2(70f, 70f));
            Anchor(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-160f, 0f), new Vector2(70f, 70f));

            // --- 装飾線 ---
            var decor = CreateDecorLine(header, theme, "TopLine");
            Anchor(decor, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -theme.tabSize.y - 30f),
                new Vector2(theme.decorLineWidth, theme.decorDiamondSize));

            return tabs;
        }

        // ==================================================================
        // 本体（スクロール領域 + 4 ページ）
        // ==================================================================

        private static List<GameObject> BuildContent(RectTransform parent, SettingsTheme theme, out RectTransform content)
        {
            var scrollGo = NewRect("ScrollView", parent);
            Anchor(scrollGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -30f), theme.contentViewSize);

            // --- Viewport ---
            var viewport = NewRect("Viewport", scrollGo);
            Stretch(viewport);
            viewport.offsetMax = new Vector2(-(theme.scrollbarHandleWidth + 20f), 0f);

            // マスク形状に画像を使わずに済む RectMask2D を使う
            viewport.gameObject.AddComponent<RectMask2D>();

            // --- Content ---
            content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);

            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = theme.contentSpacing;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.padding = new RectOffset(0, 0, (int)theme.contentTopPadding, 40);

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // --- スクロールバー ---
            var scrollbar = CreateScrollbar(scrollGo, theme);
            var sbRect = scrollbar.GetComponent<RectTransform>();
            sbRect.anchorMin = new Vector2(1f, 0f);
            sbRect.anchorMax = new Vector2(1f, 1f);
            sbRect.pivot = new Vector2(1f, 1f);
            // 本体の幅はつまみ幅。溝は CreateScrollbar 側で内側に絞ってある
            sbRect.offsetMin = new Vector2(-theme.scrollbarHandleWidth, 0f);
            sbRect.offsetMax = new Vector2(0f, 0f);

            // --- ScrollRect ---
            var scrollRect = scrollGo.gameObject.AddComponent<ScrollRect>();
            scrollRect.content = content;
            scrollRect.viewport = viewport;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 40f;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            // --- ページ ---
            var pages = new List<GameObject>();
            for (int i = 0; i < TabLabels.Length; i++)
                pages.Add(BuildPage(content, theme, TabLabels[i], i));

            return pages;
        }

        private static GameObject BuildPage(RectTransform parent, SettingsTheme theme, string title, int index)
        {
            var page = NewRect("Page_" + title, parent);

            var layout = page.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = theme.contentSpacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = page.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var titleText = NewText("Title", page, ThemeRole.TextTitle, title);
            titleText.alignment = TextAlignmentOptions.Center;
            var titleLayout = titleText.gameObject.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 90f;
            titleLayout.preferredWidth = theme.rowSize.x;

            // 1 ページ目だけサンプル行を入れておく。残りは複製して作る想定
            if (index == 0) BuildSampleRows(page, theme);

            return page.gameObject;
        }

        /// <summary>各コントロール型の見本を 1 つずつ作る。複製元として使う。</summary>
        private static void BuildSampleRows(RectTransform page, SettingsTheme theme)
        {
            // トグル
            var modeRow = CreateRow(page, theme, "Row_ScreenMode", "スクリーンモード");
            CreateToggle(modeRow.Control, theme, "ウィンドウ");

            // ドロップダウン
            var resolutionRow = CreateRow(page, theme, "Row_Resolution", "解像度");
            CreateDropdown(resolutionRow.Control, theme,
                new List<string> { "1920 x 1080", "2560 x 1440", "1600 x 900", "1280 x 720" });

            var frameRateRow = CreateRow(page, theme, "Row_FrameRate", "フレームレート");
            CreateDropdown(frameRateRow.Control, theme,
                new List<string> { "60 FPS", "120 FPS", "144 FPS", "無制限" });

            // スライダー
            var brightnessRow = CreateRow(page, theme, "Row_Brightness", "明るさ");
            CreateSlider(brightnessRow.Control, theme);

            // ボタン（キーバインド行の見本）
            var keybindRow = CreateRow(page, theme, "Row_Keybind", "前進");
            var keyButton = CreateButton(keybindRow.Control, theme, "KeyButton", "W", theme.keybindButtonSize);
            Anchor(keyButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                Vector2.zero, theme.keybindButtonSize);
        }

        // ==================================================================
        // 左側の所持品表示（枠だけ用意する）
        // ==================================================================

        /// <summary>
        /// コイン数・アイテム数を並べる領域。ここは設定値ではなくゲームの状態を映すので、
        /// 実データとの接続は別途行う想定。行の見本だけ置いてある。
        /// </summary>
        private static void BuildLeftSide(RectTransform parent, SettingsTheme theme)
        {
            var side = NewRect("LeftSide", parent);
            Anchor(side, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(120f, 0f), new Vector2(240f, 400f));

            var layout = side.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = side.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var amount = NewText("CoinAmount", side, ThemeRole.TextTitle, "8500");
            amount.alignment = TextAlignmentOptions.Left;
            amount.fontSize = 44f;
            var amountLayout = amount.gameObject.AddComponent<LayoutElement>();
            amountLayout.preferredHeight = 56f;

            var caption = NewText("CoinCaption", side, ThemeRole.TextSmall, "未浄化コイン所持数");
            caption.alignment = TextAlignmentOptions.Left;
            var captionLayout = caption.gameObject.AddComponent<LayoutElement>();
            captionLayout.preferredHeight = 26f;

            // Layout Group 配下ではサイズを Layout Element で指示する
            var divider = CreateDecorLine(side, theme, "Divider");
            var dividerLayout = divider.gameObject.AddComponent<LayoutElement>();
            dividerLayout.preferredWidth = 200f;
            dividerLayout.preferredHeight = theme.decorDiamondSize;

            var sampleItem = NewText("Item_Sample", side, ThemeRole.TextLabel, "x 6");
            sampleItem.alignment = TextAlignmentOptions.Left;
            var itemLayout = sampleItem.gameObject.AddComponent<LayoutElement>();
            itemLayout.preferredHeight = 36f;
        }

        // ==================================================================
        // フッター
        // ==================================================================

        private static void BuildFooter(RectTransform parent, SettingsTheme theme)
        {
            var footer = NewRect("Footer", parent);
            Anchor(footer, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 60f), new Vector2(1000f, 90f));

            var layout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 80f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var size = new Vector2(400f, 80f);
            foreach (var (name, label) in new[] { ("ResetButton", "リセット"), ("TitleButton", "タイトル") })
            {
                var button = CreateButton(footer, theme, name, label, size);
                var element = button.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = size.x;
                element.preferredHeight = size.y;
            }
        }

        // ==================================================================
        // ドロップダウン（Unity 標準の生成メニューを使ってから塗り直す）
        // ==================================================================

        private static TMP_Dropdown CreateDropdown(RectTransform parent, SettingsTheme theme, List<string> options)
        {
            var previousSelection = Selection.activeGameObject;
            Selection.activeGameObject = parent.gameObject;

            if (!EditorApplication.ExecuteMenuItem("GameObject/UI/Dropdown - TextMeshPro"))
            {
                Selection.activeGameObject = previousSelection;
                Debug.LogWarning("[SettingsUIBuilder] TMP Dropdown を生成できませんでした。手動で追加してください。");
                return null;
            }

            var dropdown = Selection.activeGameObject.GetComponent<TMP_Dropdown>();
            Selection.activeGameObject = previousSelection;
            if (dropdown == null) return null;

            RestyleDropdown(dropdown, theme, options);
            return dropdown;
        }

        /// <summary>標準の丸みのある見た目を、白黒の平らな矩形に作り替える。</summary>
        private static void RestyleDropdown(TMP_Dropdown dropdown, SettingsTheme theme, List<string> options)
        {
            var rt = dropdown.GetComponent<RectTransform>();
            Anchor(rt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, theme.dropdownSize);

            // 子は必ず親の Image より手前に描かれる。そのため
            // 「本体の Image = 白枠」「その手前に一回り小さい黒を敷く」という順序にする。
            var body = dropdown.GetComponent<Image>();
            if (body != null)
            {
                body.sprite = null;
                body.type = Image.Type.Simple;
                Tag(body.gameObject, ThemeRole.BoxBorder);
            }

            var inner = NewImage("Background", rt, ThemeRole.BoxInner);
            Stretch(inner.rectTransform, theme.borderThin);
            inner.raycastTarget = true;
            inner.transform.SetAsFirstSibling();   // Label / Arrow より後ろへ

            // Color Tint はこの黒地に効かせる（白枠は塗り替えない）
            dropdown.targetGraphic = inner;
            theme.ApplyColors(dropdown);

            if (dropdown.captionText != null)
            {
                Tag(dropdown.captionText.gameObject, ThemeRole.TextValue);
                dropdown.captionText.alignment = TextAlignmentOptions.Left;
                var capRect = dropdown.captionText.rectTransform;
                capRect.offsetMin = new Vector2(16f, 0f);
                capRect.offsetMax = new Vector2(-40f, 0f);
            }

            // 矢印：画像の代わりに TMP の「▼」で描く
            var arrow = rt.Find("Arrow");
            if (arrow != null)
            {
                Object.DestroyImmediate(arrow.gameObject);
                var arrowText = NewText("Arrow", rt, ThemeRole.TextValue, "▼");
                Anchor(arrowText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-14f, 0f), new Vector2(24f, 24f));
                arrowText.alignment = TextAlignmentOptions.Center;
                arrowText.fontSize = theme.sizeSmall;
            }

            RestyleDropdownTemplate(dropdown, theme);

            if (options != null && options.Count > 0)
            {
                dropdown.ClearOptions();
                dropdown.AddOptions(options);
            }
        }

        private static void RestyleDropdownTemplate(TMP_Dropdown dropdown, SettingsTheme theme)
        {
            var template = dropdown.template;
            if (template == null) return;

            template.sizeDelta = new Vector2(template.sizeDelta.x, 200f);

            // 本体と同じ考え方。Template の Image を白枠にして、内側に黒を敷く
            var templateImage = template.GetComponent<Image>();
            if (templateImage != null)
            {
                templateImage.sprite = null;
                Tag(templateImage.gameObject, ThemeRole.BoxBorder);
            }

            var templateInner = NewImage("Background", template, ThemeRole.BoxInner);
            Stretch(templateInner.rectTransform, theme.borderThin);
            templateInner.transform.SetAsFirstSibling();

            // Viewport は Mask の型抜きにしか使わないので、絵は出さない
            var viewport = template.Find("Viewport");
            if (viewport != null)
            {
                var viewportImage = viewport.GetComponent<Image>();
                if (viewportImage != null)
                {
                    viewportImage.sprite = null;
                    viewportImage.color = Color.white;   // Mask はアルファで抜くので不透明のまま
                }
                var mask = viewport.GetComponent<Mask>();
                if (mask != null) mask.showMaskGraphic = false;
            }

            // 展開リスト内のスクロールバー
            var scrollbar = template.Find("Scrollbar");
            if (scrollbar != null)
            {
                var track = scrollbar.GetComponent<Image>();
                if (track != null)
                {
                    track.sprite = null;
                    Tag(track.gameObject, ThemeRole.ScrollbarBackground);
                }
                var handle = scrollbar.Find("Sliding Area/Handle")?.GetComponent<Image>();
                if (handle != null)
                {
                    handle.sprite = null;
                    Tag(handle.gameObject, ThemeRole.ScrollbarHandle);
                }
            }

            var item = template.Find("Viewport/Content/Item");
            if (item == null) return;

            // 項目の背景。選択/ホバー時に白へ反転させるのは Color Tint に任せる
            var itemBackground = item.Find("Item Background")?.GetComponent<Image>();
            if (itemBackground != null)
            {
                itemBackground.sprite = null;
                Tag(itemBackground.gameObject, ThemeRole.BoxInner);
            }

            var itemToggle = item.GetComponent<Toggle>();
            if (itemToggle != null)
            {
                itemToggle.targetGraphic = itemBackground;
                item.gameObject.AddComponent<SelectableTint>().mode = TintMode.DropdownItem;
                theme.ApplyTint(itemToggle, TintMode.DropdownItem);
            }

            // 標準のチェックマーク画像は使わない（選択中は背景の反転で示す）
            var checkmark = item.Find("Item Checkmark");
            if (checkmark != null) Object.DestroyImmediate(checkmark.gameObject);

            if (dropdown.itemText != null)
            {
                Tag(dropdown.itemText.gameObject, ThemeRole.TextValue);
                dropdown.itemText.alignment = TextAlignmentOptions.Left;
                dropdown.itemText.rectTransform.offsetMin = new Vector2(16f, 0f);

                // 背景が白へ反転するので、カーソルが乗っている間は文字を黒にする
                var invert = item.gameObject.AddComponent<ButtonLabelInvert>();
                invert.Bind(dropdown.itemText, theme.screenBackground, onPress: false, onHover: true);
            }
        }

        // ==================================================================
        // 配線 / Prefab 化
        // ==================================================================

        private static void WireScreen(SettingsScreen screen, SettingsTheme theme, GameObject root,
            List<SettingsTabButton> tabs, List<GameObject> pages, Button closeButton)
        {
            var so = new SerializedObject(screen);
            so.FindProperty("_theme").objectReferenceValue = theme;
            so.FindProperty("_root").objectReferenceValue = root;
            so.FindProperty("_closeButton").objectReferenceValue = closeButton;
            so.FindProperty("_defaultPageIndex").intValue = 0;

            var pagesProperty = so.FindProperty("_pages");
            pagesProperty.arraySize = Mathf.Min(tabs.Count, pages.Count);
            for (int i = 0; i < pagesProperty.arraySize; i++)
            {
                var element = pagesProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("tab").objectReferenceValue = tabs[i];
                element.FindPropertyRelative("panel").objectReferenceValue = pages[i];
                element.FindPropertyRelative("note").stringValue = TabLabels[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>複製元として使える Prefab を書き出す。</summary>
        private static void SavePrefabs(RectTransform content, SettingsTheme theme)
        {
            EnsureFolder(PrefabFolder);

            var page = content.Find("Page_" + TabLabels[0]);
            if (page == null) return;

            SaveAsPrefab(page.Find("Row_ScreenMode"),  "SettingRow_Toggle");
            SaveAsPrefab(page.Find("Row_Resolution"),  "SettingRow_Dropdown");
            SaveAsPrefab(page.Find("Row_Brightness"),  "SettingRow_Slider");
            SaveAsPrefab(page.Find("Row_Keybind"),     "SettingRow_Keybind");
        }

        private static void SaveAsPrefab(Transform target, string prefabName)
        {
            if (target == null) return;

            var path = $"{PrefabFolder}/{prefabName}.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(target.gameObject, path, InteractionMode.AutomatedAction);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
