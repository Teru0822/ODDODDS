using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OddOdds.UI.Settings.EditorTools
{
    /// <summary>
    /// 既存の SettingCanvas.prefab の「見た目だけ」を白黒デザインに差し替える。
    ///
    /// SettingUIManager が持つ参照（ボタン・ドロップダウン・スライダー等）には一切触らない。
    /// 変更するのは色・スプライト・フォントと、枠線用の子オブジェクトの追加だけなので、
    /// 機能はそのまま保たれる。
    ///
    /// 枠線は UIRectBorder（上下左右 4 本の細い Image）で描く。
    /// 「白い矩形の上に黒を重ねる」方式は既存の塗りを覆ってしまい、
    /// SettingUIManager がタブ選択時に targetGraphic を白く塗る処理と衝突するため使えない。
    /// </summary>
    public static class SettingsCanvasRestyler
    {

        /// <summary>
        /// コンポーネントを取得し、無ければ付ける。
        ///
        /// GetComponent&lt;T&gt;() ?? AddComponent&lt;T&gt;() と書いてはいけない。
        /// Unity のオブジェクトは「破棄済み/未存在でも C# 的には null でない」ため
        /// ?? が右辺に落ちず、存在しないコンポーネントに触って例外になる。
        /// 判定は必ず Unity がオーバーロードした == を通す。
        /// </summary>
        private static T Ensure<T>(GameObject go) where T : Component
        {
            if (go == null) return null;
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static int _failures;

        /// <summary>1 工程を実行する。失敗しても他の工程は続ける。</summary>
        private static void Step(string name, System.Action action)
        {
            try
            {
                action();
            }
            catch (System.Exception e)
            {
                _failures++;
                Debug.LogError($"[Restyler] 「{name}」で失敗しました。\n" +
                               $"{e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }
        private const string PrefabPath = "Assets/Resources/Prefab/SettingCanvas.prefab";
        private const string ThemePath  = "Assets/ODDODDS/UI/SettingsTheme.asset";

        private const string DisplayFontPath = "Assets/Fonts/Zen_Old_Mincho/ZenOldMincho-Medium SDF.asset";
        private const string BodyFontPath    = "Assets/Resources/Font/Noto_Sans_JP/NotoSansJP-Medium SDF.asset";

        /// <summary>SettingUIManager が _settingButtons に登録しているタブ。色はスクリプト側が制御する。</summary>
        private static readonly string[] TabNames =
            { "GraphicButton", "SoundButton", "KeyBindButton", "OtherButton" };

        /// <summary>枠 + 黒地 + 白文字にする汎用ボタン。</summary>
        private static readonly string[] PlainButtonNames =
            { "CloseButton", "ResetButton", "BackTitleButton", "YesButton", "NoButton", "PlaySoundButton" };

        // ==================================================================

        [MenuItem("ODD ODDS/UI/既存の設定画面をリスキン", false, 20)]
        public static void Restyle()
        {
            var theme = LoadOrCreateTheme();
            if (theme == null) return;

            if (!EditorUtility.DisplayDialog(
                    "設定画面のリスキン",
                    "SettingCanvas.prefab の見た目を SettingsTheme.asset の内容で塗り替えます。\n\n" +
                    "■ 保たれるもの\n" +
                    "・機能（SettingUIManager の参照）\n" +
                    "・自分で割り当てた画像（Unity標準スプライトだけを外します）\n" +
                    "・追加した子オブジェクトそのもの\n" +
                    "・KeepManualLayout を付けた要素の位置とサイズ\n" +
                    "・Auto Layout をオフにした枠線の位置と太さ\n\n" +
                    "■ 元に戻るもの\n" +
                    "・手で動かした枠線（Auto Layout がオンのまま）\n" +
                    "・項目名・閉じるボタン・スライダー・四隅などの配置\n" +
                    "・テーマで管理している色\n\n" +
                    "文字サイズの適用: " + (theme.applyFontSizesOnRestyle ? "する" : "しない") + "\n\n" +
                    "調整済みの箇所があるなら、先にコミットしておくことを勧めます。\n" +
                    "実行しますか？",
                    "実行する", "やめる"))
            {
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null)
            {
                Debug.LogError("[Restyler] " + PrefabPath + " を読み込めませんでした。");
                return;
            }

            try
            {
                int borders = 0;
                _failures = 0;

                // 各工程は独立して失敗させる。1 箇所の例外で全部が止まり、
                // 何も保存されないまま終わる事故を防ぐため
                Step("「その他」タブの改名", () => RetargetOtherTabToLanguage(root));
                Step("スクリーンモードのドロップダウン化", () => ConvertScreenModeToDropdown(root, theme));

                Step("スプライトの除去", () => FlattenAllImages(root));
                Step("外枠・パネル", () => StyleFrame(root, theme));
                Step("タブ", () => borders += StyleTabs(root, theme));
                Step("ボタン", () => borders += StylePlainButtons(root, theme));
                Step("設定行", () => borders += StyleRows(root, theme));
                Step("ドロップダウン", () => borders += StyleDropdowns(root, theme));
                Step("トグル", () => StyleToggles(root, theme));
                Step("スライダー", () => StyleSliders(root, theme));
                Step("スクロールバー", () => StyleScrollbars(root, theme));
                Step("キーバインド", () => StyleKeybindButtons(root, theme));

                Step("四隅の飾り", () => StyleCorners(root, theme));
                Step("タブ下の区切り線", () => StyleTabDivider(root, theme));
                Step("閉じるボタン", () => StyleCloseButton(root, theme));
                Step("項目名の左寄せ", () => AlignRowLabels(root, theme));
                Step("スクロールバーの出し分け", () => SetupScrollbarPerPage(root, theme));

                // フォントは最後。装飾で追加したテキストにも同じ設定を行き渡らせる
                Step("フォントの代替設定", () => FixFontFallbacks(theme));
                Step("フォントの適用", () => StyleTexts(root, theme));
                Step("テーマの受け渡し", () => BindThemeToManager(root, theme));

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

                if (_failures == 0)
                {
                    Debug.Log($"[Restyler] リスキンしました（枠線 {borders} 箇所）。\n" +
                              "色を変えたいときは SettingsTheme.asset を編集して、もう一度このメニューを実行してください。");
                }
                else
                {
                    Debug.LogWarning($"[Restyler] {_failures} 個の工程が失敗しましたが、" +
                                     "残りは適用して保存しました。上のエラーを確認してください。");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
        }

        // ==================================================================
        // テーマ
        // ==================================================================

        private static SettingsTheme LoadOrCreateTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<SettingsTheme>(ThemePath);
            if (theme != null)
            {
                // 既存アセットに代替フォントが無いと、日本語グリフを持たない
                // 見出しフォントを指定したときに □ になってしまう
                if (theme.fallbackFont == null)
                {
                    theme.fallbackFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
                    if (theme.fallbackFont != null)
                    {
                        EditorUtility.SetDirty(theme);
                        Debug.Log($"[Restyler] 代替フォントに {theme.fallbackFont.name} を設定しました。", theme);
                    }
                }
                return theme;
            }

            var dir = System.IO.Path.GetDirectoryName(ThemePath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Debug.LogError("[Restyler] " + dir + " がありません。");
                return null;
            }

            theme = ScriptableObject.CreateInstance<SettingsTheme>();
            theme.displayFont  = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayFontPath);
            theme.bodyFont     = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
            theme.fallbackFont = theme.bodyFont;
            AssetDatabase.CreateAsset(theme, ThemePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Restyler] テーマを作成しました: " + ThemePath, theme);
            return theme;
        }

        // ==================================================================
        // 共通処理
        // ==================================================================

        /// <summary>枠線用に自前で足した子かどうか。塗り替え対象から外すために使う。</summary>
        private static bool IsBorderEdge(Component c)
            => c != null && c.gameObject.name.StartsWith("__Border_");

        /// <summary>位置・サイズを手で決めた宣言がされているか。されていれば RectTransform を触らない。</summary>
        private static bool LayoutLocked(Component c) => KeepManualLayout.IsLocked(c);

        /// <summary>リスキンから完全に除外されているか。</summary>
        private static bool Excluded(Component c) => KeepManualLayout.IsFullyExcluded(c);

        /// <summary>
        /// Unity 標準のスプライト（UISprite / Background / Knob など）だけを外して単色の矩形にする。
        ///
        /// 自分で割り当てた画像は残す。ここで無条件に sprite = null していたため、
        /// 手で追加したダイヤなどの画像がリスキンのたびに消えていた。
        /// </summary>
        private static void FlattenAllImages(GameObject root)
        {
            int kept = 0;
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (IsBorderEdge(image) || Excluded(image) || IsKeepNamed(image)) continue;

                if (image.sprite != null && !IsUnityBuiltinSprite(image.sprite))
                {
                    // ユーザーが割り当てた画像。9-slice などの設定ごと残す
                    kept++;
                    continue;
                }

                image.sprite = null;
                image.type = Image.Type.Simple;
            }

            if (kept > 0)
                Debug.Log($"[Restyler] 独自に割り当てられた画像 {kept} 件はそのまま残しました。");
        }

        /// <summary>「__Keep_」で始まる名前は、リスキンの塗り替え対象から外す取り決め。</summary>
        private static bool IsKeepNamed(Component c)
            => c != null && c.gameObject.name.StartsWith("__Keep_");

        /// <summary>Unity 組み込みのスプライトか（UISprite / Background / Knob など）。</summary>
        private static bool IsUnityBuiltinSprite(Sprite sprite)
        {
            var path = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrEmpty(path)) return true;   // 判定できないものは従来どおり外す

            return path.StartsWith("Resources/unity_builtin_extra")
                || path.StartsWith("Library/unity default resources")
                || path.StartsWith("Library/unity editor resources");
        }

        /// <summary>枠線を付ける（既にあれば設定だけ更新する）。</summary>
        private static int AddBorder(GameObject go, float thickness, Color color)
        {
            if (go == null) return 0;
            var border = Ensure<UIRectBorder>(go);
            // 手動調整モードの枠は色だけ揃え、太さと配置はユーザーの設定を尊重する
            if (!border.AutoLayout || LayoutLocked(go.transform))
            {
                border.Color = color;
                return 1;
            }
            border.Thickness = thickness;
            border.Color = color;
            border.Rebuild();
            return 1;
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        private static Transform Find(GameObject root, string name)
            => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        private static IEnumerable<Transform> FindAll(GameObject root, string name)
            => root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name);

        private static void SetImage(GameObject go, Color color)
        {
            if (go == null) return;
            var image = go.GetComponent<Image>();
            if (image != null) image.color = color;
        }

        // ==================================================================
        // 各パーツ
        // ==================================================================

        private static void StyleFrame(GameObject root, SettingsTheme theme)
        {
            SetImage(Find(root, "BackGround")?.gameObject, theme.screenBackground);
            SetImage(Find(root, "ScrollView")?.gameObject, theme.panelBackground);
            SetImage(Find(root, "CheckPanel")?.gameObject, theme.panelBackground);
            AddBorder(Find(root, "CheckPanel")?.gameObject, theme.borderThick, theme.line);

            // Viewport は Mask の型抜きにしか使わないので絵を出さない
            foreach (var viewport in FindAll(root, "Viewport"))
            {
                var image = viewport.GetComponent<Image>();
                if (image != null) image.color = Color.white;
                var mask = viewport.GetComponent<Mask>();
                if (mask != null) mask.showMaskGraphic = false;
            }
        }

        private static int StyleTabs(GameObject root, SettingsTheme theme)
        {
            int n = 0;
            foreach (var tabName in TabNames)
            {
                var tab = Find(root, tabName);
                if (tab == null) continue;

                SetImage(tab.gameObject, theme.tabDeselectedFill);

                // 選択色は SettingUIManager が targetGraphic に直接代入する。
                // Color Tint を残すとホバー/離脱のたびに上書きされて選択表示が消えるため切る。
                var button = tab.GetComponent<Button>();
                if (button != null) button.transition = Selectable.Transition.None;

                n += AddBorder(tab.gameObject, theme.borderThick, theme.line);

                // GetChild(0) が文字である前提のコードがあるので、順番は変えない
                var label = tab.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.color = theme.tabDeselectedText;
            }
            return n;
        }

        private static int StylePlainButtons(GameObject root, SettingsTheme theme)
        {
            int n = 0;
            foreach (var name in PlainButtonNames)
            {
                foreach (var tr in FindAll(root, name))
                {
                    var button = tr.GetComponent<Button>();
                    if (button == null) continue;

                    SetImage(tr.gameObject, theme.btnNormal);
                    theme.ApplyTint(button, TintMode.Dark);
                    n += AddBorder(tr.gameObject, theme.borderThin, theme.line);

                    var label = tr.GetComponentInChildren<TMP_Text>(true);
                    if (label != null)
                    {
                        label.color = theme.line;
                        // 押下中は背景が白へ反転するので文字を黒へ入れ替える
                        var invert = Ensure<ButtonLabelInvert>(tr.gameObject);
                        invert.Bind(label, theme.screenBackground);
                    }

                    if (tr.GetComponent<UIButtonScale>() == null) tr.gameObject.AddComponent<UIButtonScale>();
                }
            }
            return n;
        }

        /// <summary>各 Content 直下の設定行（ラベル＋コントロールを載せている帯）。</summary>
        private static int StyleRows(GameObject root, SettingsTheme theme)
        {
            int n = 0;
            // ドロップダウンの Template 内にも "Content" があるので除外する
            var contents = root.GetComponentsInChildren<Transform>(true)
                               .Where(t => t.name.EndsWith("Content"))
                               .Where(t => t.GetComponentInParent<TMP_Dropdown>(true) == null);

            foreach (var content in contents)
            {
                foreach (Transform row in content)
                {
                    var image = row.GetComponent<Image>();
                    if (image == null) continue;

                    // 見出し行は帯を敷かず、線だけで区切る
                    bool isHeader = theme.headerNameKeywords.Any(k => !string.IsNullOrEmpty(k) && row.name.Contains(k));
                    if (isHeader)
                    {
                        image.color = Color.clear;
                        var border = Ensure<UIRectBorder>(row.gameObject);
                        border.Thickness = theme.borderThin;
                        border.Color = WithAlpha(theme.line, theme.decorLineAlpha);
                        border.Rebuild();
                        // 見出しは下線だけにする
                        var so = new SerializedObject(border);
                        so.FindProperty("_top").boolValue = false;
                        so.FindProperty("_left").boolValue = false;
                        so.FindProperty("_right").boolValue = false;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        border.Apply();
                        n++;
                        continue;
                    }

                    image.color = theme.rowBackground;
                    n += AddBorder(row.gameObject, theme.borderThin, WithAlpha(theme.line, theme.rowBorderAlpha));
                }
            }
            return n;
        }

        private static int StyleDropdowns(GameObject root, SettingsTheme theme)
        {
            int n = 0;
            foreach (var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
            {
                SetImage(dropdown.gameObject, theme.btnNormal);
                theme.ApplyTint(dropdown, TintMode.Dark);
                n += AddBorder(dropdown.gameObject, theme.borderThin, theme.line);

                // 開いた時に項目が全部見えるようにする（実行時に高さを詰め直す）
                var fitter = Ensure<DropdownListFitter>(dropdown.gameObject);
                fitter.Configure(theme.dropdownFitListToItems, theme.dropdownFitMaxHeight, theme.dropdownEscapeMask);

                if (dropdown.captionText != null) dropdown.captionText.color = theme.line;

                // 矢印は画像をやめて「▼」の文字で描く
                var arrow = dropdown.transform.Find("Arrow");
                if (arrow != null) ConvertImageToText(arrow.gameObject, "▼", theme, theme.line, theme.sizeSmall);

                var template = dropdown.template;
                if (template == null) continue;

                SetImage(template.gameObject, theme.screenBackground);
                n += AddBorder(template.gameObject, theme.borderThin, theme.line);

                // 展開したリストの高さ。0 なら Unity 側の設定をそのまま使う
                if (theme.dropdownListHeight > 0f && !LayoutLocked(template))
                    template.sizeDelta = new Vector2(template.sizeDelta.x, theme.dropdownListHeight);

                var item = template.Find("Viewport/Content/Item");
                if (item == null) continue;

                // 1 項目の高さ。0 なら変更しない
                if (theme.dropdownItemHeight > 0f && !LayoutLocked(item))
                {
                    var itemRect = item.GetComponent<RectTransform>();
                    if (itemRect != null)
                        itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, theme.dropdownItemHeight);
                }

                var itemBackground = item.Find("Item Background")?.GetComponent<Image>();
                var itemToggle = item.GetComponent<Toggle>();
                if (itemBackground != null)
                {
                    itemBackground.color = theme.itemNormal;
                    if (itemToggle != null)
                    {
                        itemToggle.targetGraphic = itemBackground;
                        var tint = Ensure<SelectableTint>(item.gameObject);
                        tint.mode = TintMode.DropdownItem;
                        theme.ApplyTint(itemToggle, TintMode.DropdownItem);
                    }
                }

                SetupSelectionMark(item, itemToggle, theme);

                if (dropdown.itemText != null)
                {
                    dropdown.itemText.color = theme.line;

                    // マーカーの分だけ項目名を右へずらす
                    if (theme.dropdownUseSelectionMark && !LayoutLocked(dropdown.itemText))
                        dropdown.itemText.rectTransform.offsetMin =
                            new Vector2(theme.dropdownLabelIndent, dropdown.itemText.rectTransform.offsetMin.y);

                    // ハイライト時は背景が白になるので文字を黒へ
                    var invert = Ensure<ButtonLabelInvert>(item.gameObject);
                    invert.Bind(dropdown.itemText, theme.screenBackground, onPress: false, onHover: true);
                }
            }
            return n;
        }

        private static void StyleToggles(GameObject root, SettingsTheme theme)
        {
            foreach (var toggle in root.GetComponentsInChildren<Toggle>(true))
            {
                // ドロップダウンのリスト項目は別扱い済みなので飛ばす。
                // Template は非アクティブなので includeInactive を付けないと親を辿れない
                if (toggle.GetComponentInParent<TMP_Dropdown>(true) != null) continue;

                var background = toggle.transform.Find("Background");
                if (background != null)
                {
                    SetImage(background.gameObject, theme.screenBackground);
                    AddBorder(background.gameObject, theme.borderThin, theme.line);
                }

                // Toggle.graphic はチェック表示の本体。ここを白い塗りにして ON を表す
                if (toggle.graphic is Image check)
                {
                    check.color = theme.line;
                    var rt = check.rectTransform;
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = new Vector2(theme.borderThin * 2f, theme.borderThin * 2f);
                    rt.offsetMax = new Vector2(-theme.borderThin * 2f, -theme.borderThin * 2f);

                    // 白塗りの上に黒い「✓」を重ねる。ON/OFF は親ごと切り替わるので追従する
                    EnsureCheckGlyph(check.gameObject, theme);
                }

                var label = toggle.transform.Find("Label")?.GetComponent<TMP_Text>();
                if (label != null) label.color = theme.line;
            }
        }

        private static void StyleSliders(GameObject root, SettingsTheme theme)
        {
            foreach (var slider in root.GetComponentsInChildren<Slider>(true))
            {
                var background = slider.transform.Find("Background");
                if (background != null)
                {
                    SetImage(background.gameObject, theme.sliderBackground);
                    // 溝は細く。これだけで一気に洗練される
                    var rt = LayoutLocked(background) ? null : background.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = new Vector2(rt.anchorMin.x, 0.5f);
                        rt.anchorMax = new Vector2(rt.anchorMax.x, 0.5f);
                        rt.sizeDelta = new Vector2(rt.sizeDelta.x, theme.sliderThickness);
                    }
                }

                if (slider.fillRect != null)
                {
                    SetImage(slider.fillRect.gameObject, theme.sliderFill);
                    var parent = slider.fillRect.parent as RectTransform;
                    if (parent != null && !LayoutLocked(parent))
                    {
                        parent.anchorMin = new Vector2(parent.anchorMin.x, 0.5f);
                        parent.anchorMax = new Vector2(parent.anchorMax.x, 0.5f);
                        parent.sizeDelta = new Vector2(parent.sizeDelta.x, theme.sliderThickness);
                    }
                }

                if (slider.handleRect != null)
                    SetImage(slider.handleRect.gameObject, theme.sliderHandle);

                var tint = Ensure<SelectableTint>(slider.gameObject);
                tint.mode = TintMode.Handle;
                theme.ApplyTint(slider, TintMode.Handle);
            }
        }

        private static void StyleScrollbars(GameObject root, SettingsTheme theme)
        {
            foreach (var scrollbar in root.GetComponentsInChildren<Scrollbar>(true))
            {
                SetImage(scrollbar.gameObject, theme.scrollbarBackground);
                if (scrollbar.handleRect != null)
                    SetImage(scrollbar.handleRect.gameObject, theme.scrollbarHandle);

                var tint = Ensure<SelectableTint>(scrollbar.gameObject);
                tint.mode = TintMode.Handle;
                theme.ApplyTint(scrollbar, TintMode.Handle);
            }
        }

        /// <summary>
        /// キーバインドのキーボタン。SettingUIManager が通常時=白 / 入力待ち=赤 を直接代入するため、
        /// Color Tint は切って塗りをスクリプトに任せ、文字だけ黒にして読めるようにする。
        /// </summary>
        private static void StyleKeybindButtons(GameObject root, SettingsTheme theme)
        {
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            {
                // 対象はキー割り当ての行（ForwardKeyBind 等）だけ。
                // タブの KeyBindButton やページの KeyBindContent まで巻き込まないよう末尾一致にする
                if (!tr.name.EndsWith("KeyBind")) continue;

                foreach (var button in tr.GetComponentsInChildren<Button>(true))
                {
                    button.transition = Selectable.Transition.None;
                    SetImage(button.gameObject, theme.keybindIdleFill);

                    var label = button.GetComponentInChildren<TMP_Text>(true);
                    if (label != null) label.color = theme.keybindIdleText;

                    // 白塗りなので枠線は付けない（見えないうえに二重線になる）
                    var border = button.GetComponent<UIRectBorder>();
                    if (border != null) Object.DestroyImmediate(border, true);
                }
            }
        }

        private static bool IsHeader(Transform t, SettingsTheme theme)
            => theme.headerNameKeywords.Any(
                k => !string.IsNullOrEmpty(k) &&
                     (t.name.Contains(k) || (t.parent != null && t.parent.name.Contains(k))));

        private static void StyleTexts(GameObject root, SettingsTheme theme)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                bool isHeader = IsHeader(text.transform, theme);
                var font = theme.GetFont(isHeader ? FontRole.Display : FontRole.Body);
                if (font != null) text.font = font;

                // "__" で始まるものはリスキンが作った飾り（× や ✓）。サイズは個別に決めてある
                if (theme.applyFontSizesOnRestyle && !text.name.StartsWith("__"))
                    text.fontSize = isHeader ? theme.sizeTitle : theme.sizeLabel;
            }
        }

        /// <summary>
        /// 使用フォントに fallback を登録し、アトラスを増やせるようにする。
        ///
        /// TMP はアトラスに焼かれた文字しか描けない。日本語グリフを持たないアセットを
        /// 見出しに指定すると全部 □ になる（今回の不具合の原因がこれ）。
        /// fallback を入れておけば、足りない文字はそちらで描画されるので豆腐にならない。
        /// </summary>
        private static void FixFontFallbacks(SettingsTheme theme)
        {
            if (!theme.autoFixFontFallback || theme.fallbackFont == null) return;

            var targets = new[] { theme.displayFont, theme.bodyFont, theme.monoFont }
                          .Where(f => f != null && f != theme.fallbackFont)
                          .Distinct();

            foreach (var font in targets)
            {
                bool dirty = false;

                if (font.fallbackFontAssetTable == null)
                    font.fallbackFontAssetTable = new List<TMP_FontAsset>();

                if (!font.fallbackFontAssetTable.Contains(theme.fallbackFont))
                {
                    font.fallbackFontAssetTable.Add(theme.fallbackFont);
                    dirty = true;
                    Debug.Log($"[Restyler] {font.name} に代替フォント {theme.fallbackFont.name} を登録しました。", font);
                }

                // 動的フォントはアトラス 1 枚が埋まると新しい文字を焼けなくなる。
                // 複数アトラスを許可しておくと日本語でも足りなくならない
                var so = new SerializedObject(font);
                var multi = so.FindProperty("m_IsMultiAtlasTexturesEnabled");
                if (multi != null && !multi.boolValue)
                {
                    multi.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    dirty = true;
                    Debug.Log($"[Restyler] {font.name} の Multi Atlas Textures を有効にしました。", font);
                }

                if (dirty) EditorUtility.SetDirty(font);
            }
        }

        /// <summary>
        /// タブ選択色とキーバインド色は SettingUIManager がスクリプトから直接塗るため、
        /// テーマ参照を渡しておかないとリスキン後も旧来の色のままになる。
        /// </summary>
        private static void BindThemeToManager(GameObject root, SettingsTheme theme)
        {
            var manager = root.GetComponentInChildren<SettingUIManager>(true);
            if (manager == null)
            {
                Debug.LogWarning("[Restyler] SettingUIManager が見つからないため、テーマを渡せませんでした。");
                return;
            }

            var so = new SerializedObject(manager);
            var property = so.FindProperty("_theme");
            if (property == null)
            {
                Debug.LogWarning("[Restyler] SettingUIManager に _theme がありません。");
                return;
            }

            property.objectReferenceValue = theme;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 選択中の項目の左に置くマーカー枠を用意する。
        ///
        /// 既存の "Item Checkmark" を、絵を持たない入れ物として作り替える。
        /// ここへダイヤなどの Image を子オブジェクトとして入れれば、
        /// 選択中の項目でだけ表示される。
        ///
        /// Toggle.graphic は「その Graphic の透明度」しか変えず子には効かないため、
        /// graphic の割り当てを外して ToggleGraphicFollower で
        /// GameObject ごと ON/OFF する方式にしている（＝子も一緒に出入りする）。
        /// </summary>
        private static void SetupSelectionMark(Transform item, Toggle itemToggle, SettingsTheme theme)
        {
            var mark = item.Find("Item Checkmark");
            if (mark == null || !theme.dropdownUseSelectionMark) return;

            // Toggle の透明度制御から外す（子オブジェクトごと出し入れしたいため）
            if (itemToggle != null && itemToggle.graphic == mark.GetComponent<Graphic>())
                itemToggle.graphic = null;

            // 入れ物自身は絵を持たない。中に入れた Image だけが見える
            var image = mark.GetComponent<Image>();
            if (image != null) image.enabled = false;

            if (!LayoutLocked(mark))
            {
                var rt = mark.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(theme.dropdownMarkPadding, 0f);
                rt.sizeDelta = Vector2.one * theme.dropdownMarkSize;
            }

            // 選択中だけ表示する
            if (itemToggle != null)
            {
                var follower = Ensure<ToggleGraphicFollower>(item.gameObject);
                follower.Bind(itemToggle, mark.gameObject);
            }
        }

        // ==================================================================
        // 装飾
        // ==================================================================

        /// <summary>画面四隅に L 字の飾りを置く。線だけなので画像はいらない。</summary>
        private static void StyleCorners(GameObject root, SettingsTheme theme)
        {
            var parent = Find(root, "BackGround") ?? root.transform;
            var holder = EnsureChild(parent, "__CornerOrnaments");
            if (LayoutLocked(holder)) return;
            Stretch(holder);

            if (!theme.showCornerOrnaments)
            {
                holder.gameObject.SetActive(false);
                return;
            }
            holder.gameObject.SetActive(true);

            // (アンカー, 横線の向き, 縦線の向き)
            var corners = new[]
            {
                ("TL", new Vector2(0f, 1f), new Vector2(0f, 1f), 1f, -1f),
                ("TR", new Vector2(1f, 1f), new Vector2(1f, 1f), -1f, -1f),
                ("BL", new Vector2(0f, 0f), new Vector2(0f, 0f), 1f, 1f),
                ("BR", new Vector2(1f, 0f), new Vector2(1f, 0f), -1f, 1f),
            };

            var color = WithAlpha(theme.line, theme.cornerAlpha);
            foreach (var (tag, anchor, pivot, sx, sy) in corners)
            {
                var corner = EnsureChild(holder, "__Corner_" + tag);
                corner.anchorMin = corner.anchorMax = anchor;
                corner.pivot = pivot;
                corner.anchoredPosition = new Vector2(sx * theme.cornerMargin, sy * theme.cornerMargin);
                corner.sizeDelta = new Vector2(theme.cornerLength, theme.cornerLength);

                var h = EnsureImage(corner, "H", color);
                h.rectTransform.anchorMin = h.rectTransform.anchorMax = pivot;
                h.rectTransform.pivot = pivot;
                h.rectTransform.anchoredPosition = Vector2.zero;
                h.rectTransform.sizeDelta = new Vector2(theme.cornerLength, theme.cornerThickness);

                var v = EnsureImage(corner, "V", color);
                v.rectTransform.anchorMin = v.rectTransform.anchorMax = pivot;
                v.rectTransform.pivot = pivot;
                v.rectTransform.anchoredPosition = Vector2.zero;
                v.rectTransform.sizeDelta = new Vector2(theme.cornerThickness, theme.cornerLength);
            }
        }

        /// <summary>タブと設定項目欄の間に、細い線と中央の菱形を置く。</summary>
        private static void StyleTabDivider(GameObject root, SettingsTheme theme)
        {
            var tabs = Find(root, "ButtonParents");
            if (tabs == null) return;

            var divider = EnsureChild(tabs, "__TabDivider");
            if (LayoutLocked(divider)) return;
            if (!theme.showTabDivider)
            {
                divider.gameObject.SetActive(false);
                return;
            }
            divider.gameObject.SetActive(true);

            divider.anchorMin = new Vector2(0.5f, 0f);
            divider.anchorMax = new Vector2(0.5f, 0f);
            divider.pivot = new Vector2(0.5f, 1f);
            divider.anchoredPosition = new Vector2(0f, -theme.tabDividerOffsetY);
            divider.sizeDelta = new Vector2(
                theme.tabDividerWidth > 0f ? theme.tabDividerWidth : theme.decorLineWidth,
                theme.decorDiamondSize);

            var line = EnsureImage(divider, "Line", WithAlpha(theme.line, theme.decorLineAlpha));
            var lr = line.rectTransform;
            lr.anchorMin = new Vector2(0f, 0.5f);
            lr.anchorMax = new Vector2(1f, 0.5f);
            lr.pivot = new Vector2(0.5f, 0.5f);
            lr.offsetMin = new Vector2(0f, -theme.borderThin * 0.5f);
            lr.offsetMax = new Vector2(0f, theme.borderThin * 0.5f);

            // 45度回した正方形＝菱形
            var diamond = EnsureImage(divider, "Diamond", theme.line);
            var dr = diamond.rectTransform;
            dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 0.5f);
            dr.pivot = new Vector2(0.5f, 0.5f);
            dr.anchoredPosition = Vector2.zero;
            dr.sizeDelta = Vector2.one * theme.decorDiamondSize;
            dr.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        /// <summary>閉じるボタンを「大きな × ＋ 下に文字」の 2 段構成にする。</summary>
        private static void StyleCloseButton(GameObject root, SettingsTheme theme)
        {
            var close = Find(root, "CloseButton");
            if (close == null || !theme.closeButtonUseGlyph) return;

            // 既存の「閉じる」テキストはローカライズが付いているので消さずに下段へ移す
            var label = close.GetComponentsInChildren<TMP_Text>(true)
                             .FirstOrDefault(t => !t.name.StartsWith("__"));
            if (label != null && !LayoutLocked(label))
            {
                var lr = label.rectTransform;
                lr.anchorMin = new Vector2(0f, 0f);
                lr.anchorMax = new Vector2(1f, 1f - theme.closeGlyphRatio);
                lr.offsetMin = Vector2.zero;
                lr.offsetMax = Vector2.zero;
                label.alignment = TextAlignmentOptions.Center;
                label.color = theme.line;
            }

            var glyph = EnsureText(close, "__CloseGlyph", "×", theme);
            if (!LayoutLocked(glyph))
            {
                var gr = glyph.rectTransform;
                gr.anchorMin = new Vector2(0f, 1f - theme.closeGlyphRatio);
                gr.anchorMax = new Vector2(1f, 1f);
                gr.offsetMin = Vector2.zero;
                gr.offsetMax = Vector2.zero;
            }
            glyph.fontSize = theme.closeGlyphSize;
            glyph.color = theme.line;
            glyph.alignment = TextAlignmentOptions.Center;

            // 押下時は背景が白へ反転するので、× も一緒に黒へ入れ替える
            var invert = close.GetComponent<ButtonLabelInvert>();
            if (invert != null) invert.Bind(glyph, theme.screenBackground);
        }

        /// <summary>設定項目名を左寄せにする。</summary>
        private static void AlignRowLabels(GameObject root, SettingsTheme theme)
        {
            if (!theme.leftAlignRowLabels) return;

            var contents = root.GetComponentsInChildren<Transform>(true)
                               .Where(t => t.name.EndsWith("Content"))
                               .Where(t => t.GetComponentInParent<TMP_Dropdown>(true) == null);

            foreach (var content in contents)
            {
                foreach (Transform row in content)
                {
                    if (IsHeader(row, theme)) continue;

                    // 行の直下にある最初のテキスト＝項目名
                    var label = row.Cast<Transform>()
                                   .Select(t => t.GetComponent<TMP_Text>())
                                   .FirstOrDefault(t => t != null && !t.name.StartsWith("__"));
                    if (label == null || LayoutLocked(label)) continue;

                    var rt = label.rectTransform;
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(Mathf.Clamp01(theme.rowLabelWidthRatio), 1f);
                    rt.offsetMin = new Vector2(theme.rowLabelPadding, 0f);
                    rt.offsetMax = Vector2.zero;
                    label.alignment = TextAlignmentOptions.Left;
                }
            }
        }

        /// <summary>ページごとにスクロールバーを出し分ける設定を仕込む。</summary>
        private static void SetupScrollbarPerPage(GameObject root, SettingsTheme theme)
        {
            var scrollView = Find(root, "ScrollView");
            if (scrollView == null) return;

            var scrollRect = scrollView.GetComponent<ScrollRect>();
            var scrollbar = scrollRect != null ? scrollRect.verticalScrollbar : null;
            if (scrollbar == null) return;

            // スクロールバーを出すのはグラフィックだけ、という指定
            var wanted = new Dictionary<string, bool>
            {
                { "GraphicContent", true },
                { "SoundContent", false },
                { "KeyBindContent", false },
                { "OtherContent", false },
            };

            var rules = new List<SettingsScrollbarPerPage.PageRule>();
            foreach (var pair in wanted)
            {
                var page = Find(root, pair.Key);
                if (page == null) continue;
                rules.Add(new SettingsScrollbarPerPage.PageRule
                {
                    page = page.gameObject,
                    showScrollbar = pair.Value,
                });
            }
            if (rules.Count == 0) return;

            var controller = Ensure<SettingsScrollbarPerPage>(scrollView.gameObject);
            controller.Bind(scrollbar, rules.ToArray());
        }

        /// <summary>
        /// 「その他」タブを「言語」にする。
        /// 表示文字はローカライズ管理なので、TMP を直接書き換えるのではなく
        /// 参照するキーを既存の Language エントリへ差し替える（英語表示も同時に正しくなる）。
        /// </summary>
        private static void RetargetOtherTabToLanguage(GameObject root)
        {
            const long LanguageKeyId = 17221989507072L;   // SettingMenuTable の "Language"

            var tab = Find(root, "OtherButton");
            if (tab == null) return;

            var localize = tab.GetComponentsInChildren<
                UnityEngine.Localization.Components.LocalizeStringEvent>(true).FirstOrDefault();
            if (localize == null)
            {
                Debug.LogWarning("[Restyler] OtherButton に LocalizeStringEvent が無いため、タブ名を変更できませんでした。");
                return;
            }

            var so = new SerializedObject(localize);
            var keyId = so.FindProperty("m_StringReference.m_TableEntryReference.m_KeyId");
            var keyName = so.FindProperty("m_StringReference.m_TableEntryReference.m_Key");
            if (keyId == null)
            {
                Debug.LogWarning("[Restyler] ローカライズ参照の構造が想定と違うため、タブ名を変更できませんでした。");
                return;
            }

            keyId.longValue = LanguageKeyId;
            if (keyName != null) keyName.stringValue = "Language";
            so.ApplyModifiedPropertiesWithoutUndo();

            // エディタ上の見た目も合わせておく（実行時はローカライズが上書きする）
            var text = tab.GetComponentInChildren<TMP_Text>(true);
            if (text != null && !text.name.StartsWith("__")) text.text = "言語";

            Debug.Log("[Restyler] 「その他」タブを「言語」に変更しました。");
        }

        /// <summary>
        /// ウィンドウ / フルスクリーンの選択をドロップダウンにする。
        ///
        /// SettingUIManager は Toggle を直接参照しているので、Toggle は残したまま
        /// 非表示にし、ドロップダウンから操作する形にする（設定処理には手を入れない）。
        /// </summary>
        private static void ConvertScreenModeToDropdown(GameObject root, SettingsTheme theme)
        {
            var row = Find(root, "ScreenSize");
            if (row == null) return;

            var toggles = row.GetComponentsInChildren<Toggle>(true)
                             .Where(t => t.GetComponentInParent<TMP_Dropdown>(true) == null)
                             .ToList();
            if (toggles.Count < 2) return;

            var dropdown = row.GetComponentInChildren<TMP_Dropdown>(true);
            if (dropdown == null)
            {
                // 既存のドロップダウンを複製して使う。ゼロから組むより確実で見た目も揃う
                var source = Find(root, "ScreenResolution")?.GetComponentInChildren<TMP_Dropdown>(true);
                if (source == null)
                {
                    Debug.LogWarning("[Restyler] 複製元のドロップダウンが見つからないため、スクリーンモードは変換しませんでした。");
                    return;
                }

                var clone = Object.Instantiate(source.gameObject, row);
                clone.name = "ScreenModeDropdown";
                dropdown = clone.GetComponent<TMP_Dropdown>();

                // 複製元に付いていた配線が残らないようにする
                var so = new SerializedObject(dropdown);
                var calls = so.FindProperty("m_OnValueChanged.m_PersistentCalls.m_Calls");
                if (calls != null) { calls.ClearArray(); so.ApplyModifiedPropertiesWithoutUndo(); }
            }

            // 選択肢は Toggle のラベルから作る（ローカライズ済みの文言をそのまま使える）
            // ?.GetComponent<T>()?.text と繋いではいけない。GetComponent が返す
            // 「Unity 的には null」は ?. をすり抜けるため、null チェックは == で行う
            var options = new List<string>(toggles.Count);
            for (int i = 0; i < toggles.Count; i++)
            {
                var labelTr = toggles[i].transform.Find("Label");
                var label = labelTr != null ? labelTr.GetComponent<TMP_Text>() : null;
                var text = label != null ? label.text : null;
                options.Add(string.IsNullOrEmpty(text) ? "Option " + (i + 1) : text);
            }
            dropdown.ClearOptions();
            dropdown.AddOptions(options);

            // 行の右側に寄せる（手動配置が宣言されていれば触らない）
            var rt = dropdown.GetComponent<RectTransform>();
            if (!LayoutLocked(rt))
            {
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(-theme.rowControlPadding, 0f);
                rt.sizeDelta = theme.dropdownSize;
            }

            var proxy = Ensure<ToggleDropdownProxy>(row.gameObject);
            proxy.Bind(dropdown, toggles);

            // エディタ上でも Toggle が見えないようにしておく。
            // SetActive(false) にしないのは、ラベルの LocalizeStringEvent が
            // 非アクティブだと言語切替を受け取れなくなるため（選択肢が古い言語で固まる）
            foreach (var toggle in toggles)
            {
                var group = Ensure<CanvasGroup>(toggle.gameObject);
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            Debug.Log("[Restyler] スクリーンモードをドロップダウンに置き換えました（Toggle は非表示で残しています）。");
        }

        // ==================================================================
        // 補助
        // ==================================================================

        private static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>名前で子を探し、無ければ作る。</summary>
        private static RectTransform EnsureChild(Transform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null) return existing;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image EnsureImage(Transform parent, string name, Color color)
        {
            var rt = EnsureChild(parent, name);
            var image = Ensure<Image>(rt.gameObject);
            image.sprite = null;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI EnsureText(Transform parent, string name, string content, SettingsTheme theme)
        {
            var rt = EnsureChild(parent, name);
            var text = Ensure<TextMeshProUGUI>(rt.gameObject);
            text.text = content;
            text.raycastTarget = false;
            var font = theme.GetFont(FontRole.Body);
            if (font != null) text.font = font;
            return text;
        }

        /// <summary>Image を捨てて TextMeshPro の記号に置き換える（矢印など、参照されていない飾りに使う）。</summary>
        private static void ConvertImageToText(GameObject go, string glyph, SettingsTheme theme, Color color, float size)
        {
            var image = go.GetComponent<Image>();
            if (image != null) Object.DestroyImmediate(image, true);

            var text = Ensure<TextMeshProUGUI>(go);
            text.text = glyph;
            text.color = color;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            if (theme.bodyFont != null) text.font = theme.bodyFont;
        }

        /// <summary>白い塗りの上に黒い「✓」を置く。既にあれば作らない。</summary>
        private static void EnsureCheckGlyph(GameObject fill, SettingsTheme theme)
        {
            const string GlyphName = "__CheckGlyph";
            var existing = fill.transform.Find(GlyphName);
            TextMeshProUGUI text;
            if (existing != null)
            {
                text = existing.GetComponent<TextMeshProUGUI>();
                if (text == null) text = existing.gameObject.AddComponent<TextMeshProUGUI>();
            }
            else
            {
                var go = new GameObject(GlyphName, typeof(RectTransform));
                go.transform.SetParent(fill.transform, false);
                text = go.AddComponent<TextMeshProUGUI>();
            }

            var rt = text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            text.text = "✓";
            text.color = theme.screenBackground;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = 8f;
            text.fontSizeMax = 48f;
            if (theme.bodyFont != null) text.font = theme.bodyFont;
        }
    }
}
