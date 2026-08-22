using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OddOdds.UI.Settings
{
    /// <summary>
    /// 設定画面の見た目を決める値をすべてここに集約する。
    /// 色・サイズ・フォントを変えたいときは、このアセット 1 つだけを編集すればよい。
    /// 変更後は SettingsScreen の右クリックメニュー「テーマを適用」で階層全体に反映される。
    /// </summary>
    [CreateAssetMenu(menuName = "OddOdds/UI/Settings Theme", fileName = "SettingsTheme")]
    public class SettingsTheme : ScriptableObject
    {
        // ------------------------------------------------------------------
        // 色（白黒 + 不透明度のみで構成する。色数を増やさないのがこのデザインの肝）
        // ------------------------------------------------------------------
        [Header("基本色")]
        [Tooltip("画面全体の背景。完全な黒(#000000)より少し明るくすると高級感が出る")]
        public Color screenBackground = new Color32(0x08, 0x08, 0x08, 0xFF);

        [Tooltip("パネルなど一段手前の面")]
        public Color panelBackground = new Color32(0x10, 0x10, 0x10, 0xFF);

        [Tooltip("設定行の背景")]
        public Color rowBackground = new Color32(0x0A, 0x0A, 0x0A, 0xFF);

        [Tooltip("枠線・文字の基本色")]
        public Color line = Color.white;

        [Tooltip("補足文などの弱い文字色")]
        public Color textDim = new Color32(0xAA, 0xAA, 0xAA, 0xFF);

        [Tooltip("無効状態")]
        public Color disabled = new Color32(0x55, 0x55, 0x55, 0xFF);

        [Header("不透明度")]
        [Range(0f, 1f)] public float rowBorderAlpha = 0.7f;
        [Range(0f, 1f)] public float decorLineAlpha = 0.6f;

        // ------------------------------------------------------------------
        // ボタンの色遷移（Unity 標準の Color Tint をそのまま使う）
        // ------------------------------------------------------------------
        [Header("ボタン Color Tint（黒地の部品）")]
        public Color btnNormal      = new Color32(0x08, 0x08, 0x08, 0xFF);
        public Color btnHighlighted = new Color32(0x22, 0x22, 0x22, 0xFF);
        [Tooltip("押した瞬間だけ白背景に反転させると「押した感」が出る")]
        public Color btnPressed     = Color.white;
        public Color btnSelected    = new Color32(0x22, 0x22, 0x22, 0xFF);
        public Color btnDisabled    = new Color32(0x33, 0x33, 0x33, 0xFF);
        [Range(0f, 1f)] public float btnFadeDuration = 0.08f;

        [Header("Color Tint（白いつまみ）")]
        [Tooltip("スライダー・スクロールバーのつまみ用。黒地の設定を流用すると白いつまみが黒く潰れる")]
        public Color handleNormal      = Color.white;
        public Color handleHighlighted = Color.white;
        public Color handlePressed     = new Color32(0xAA, 0xAA, 0xAA, 0xFF);
        public Color handleDisabled    = new Color32(0x55, 0x55, 0x55, 0xFF);

        // ------------------------------------------------------------------
        // 既存の SettingUIManager がスクリプトから直接塗る色。
        // Color Tint ではなく .color を直接代入しているため、ここで別に持つ。
        // ------------------------------------------------------------------
        [Header("タブ（SettingUIManager が制御）")]
        [Tooltip("選択中タブの塗り")]
        public Color tabSelectedFill = Color.white;
        [Tooltip("選択中タブの文字。白塗りの上に乗るので黒系にする")]
        public Color tabSelectedText = new Color32(0x08, 0x08, 0x08, 0xFF);
        [Tooltip("非選択タブの塗り")]
        public Color tabDeselectedFill = new Color32(0x08, 0x08, 0x08, 0xFF);
        [Tooltip("非選択タブの文字")]
        public Color tabDeselectedText = Color.white;

        [Header("キーバインド（SettingUIManager が制御）")]
        [Tooltip("通常時のキーボタンの塗り")]
        public Color keybindIdleFill = Color.white;
        [Tooltip("通常時のキー文字。白塗りの上に乗るので黒系にする")]
        public Color keybindIdleText = new Color32(0x08, 0x08, 0x08, 0xFF);
        [Tooltip("キー入力待ちの塗り")]
        public Color keybindRebindingFill = new Color32(0xC8, 0x28, 0x28, 0xFF);

        [Header("Color Tint（ドロップダウンの項目）")]
        [Tooltip("非選択の項目。リストの地の色に溶け込ませる")]
        public Color itemNormal      = new Color32(0x0A, 0x0A, 0x0A, 0xFF);
        [Tooltip("カーソルが乗った項目。白へ反転させると選択位置が一目で分かる")]
        public Color itemHighlighted = Color.white;
        public Color itemPressed     = Color.white;
        public Color itemSelected    = new Color32(0x22, 0x22, 0x22, 0xFF);

        // ------------------------------------------------------------------
        // パーツ色
        // ------------------------------------------------------------------
        [Header("スライダー")]
        public Color sliderBackground = new Color32(0x22, 0x22, 0x22, 0xFF);
        public Color sliderFill       = Color.white;
        public Color sliderHandle     = Color.white;

        [Header("スクロールバー")]
        public Color scrollbarBackground = new Color32(0x33, 0x33, 0x33, 0xFF);
        public Color scrollbarHandle     = Color.white;

        // ------------------------------------------------------------------
        // 寸法（1920x1080 基準）
        // ------------------------------------------------------------------
        [Header("寸法 / タブ")]
        public Vector2 tabSize = new Vector2(300f, 70f);

        [Header("寸法 / 設定行")]
        public Vector2 rowSize = new Vector2(1100f, 80f);
        [Tooltip("行の左端からラベルまでの余白")]
        public float rowLabelPadding = 50f;
        [Tooltip("行の右端からコントロールまでの余白")]
        public float rowControlPadding = 30f;
        public Vector2 dropdownSize = new Vector2(320f, 48f);
        public Vector2 sliderSize   = new Vector2(320f, 24f);
        public float   toggleBoxSize = 34f;
        public Vector2 keybindButtonSize = new Vector2(240f, 48f);

        [Header("寸法 / 線の太さ")]
        [Tooltip("大きな枠。この統一感がデザインの完成度を決める")]
        public float borderThick = 2f;
        [Tooltip("小さな枠・装飾線")]
        public float borderThin = 1f;
        public float sliderThickness = 3f;
        public float scrollbarTrackWidth = 4f;
        public float scrollbarHandleWidth = 6f;

        [Header("寸法 / 装飾線")]
        public float decorLineWidth = 1500f;
        public float decorDiamondSize = 8f;

        [Header("装飾 / 画面四隅")]
        [Tooltip("四隅に L 字の飾りを付ける")]
        public bool showCornerOrnaments = true;
        [Tooltip("L 字の一辺の長さ")]
        public float cornerLength = 60f;
        [Tooltip("L 字の線の太さ")]
        public float cornerThickness = 2f;
        [Tooltip("画面端からの余白")]
        public float cornerMargin = 32f;
        [Range(0f, 1f)] public float cornerAlpha = 0.85f;

        [Header("装飾 / タブと項目欄の間の区切り")]
        public bool showTabDivider = true;
        [Tooltip("区切り線の幅。0 以下なら親いっぱいに広げる")]
        public float tabDividerWidth = 1400f;
        [Tooltip("タブ下端からの距離（下方向が正）")]
        public float tabDividerOffsetY = 18f;

        [Header("閉じるボタン")]
        [Tooltip("上に大きな × を出し、その下に文字を置く")]
        public bool closeButtonUseGlyph = true;
        public float closeGlyphSize = 44f;
        [Tooltip("× と文字の高さの割り当て比率（× 側）")]
        [Range(0.3f, 0.9f)] public float closeGlyphRatio = 0.62f;

        [Header("ドロップダウンの開いたときの大きさ")]
        [Tooltip("Template の高さ。項目がこれより少なければ縮み、多ければスクロールする。\n" +
                 "0 にすると変更しない（Unity 側で個別に設定した値をそのまま使う）")]
        public float dropdownListHeight = 0f;

        [Tooltip("リスト 1 項目の高さ。0 にすると変更しない")]
        public float dropdownItemHeight = 0f;

        [Tooltip("開いたリストを、項目が全部見える高さに実行時へ詰め直す。\n" +
                 "項目数が少ないのに途中までしか見えない場合はこれをオンにする")]
        public bool dropdownFitListToItems = true;

        [Tooltip("詰め直したときの高さの上限。0 で上限なし（項目数ぶん必ず全部表示する）")]
        public float dropdownFitMaxHeight = 0f;

        [Tooltip("開いたリストを Canvas 直下へ逃がし、ScrollView のマスクで切られないようにする")]
        public bool dropdownEscapeMask = true;

        [Header("ドロップダウンの選択マーカー")]
        [Tooltip("選択中の項目の左に置くマーカー枠を用意する。\n" +
                 "枠だけ作るので、ダイヤなどの Image をその子オブジェクトに入れて使う")]
        public bool dropdownUseSelectionMark = true;
        [Tooltip("マーカー枠の一辺")]
        public float dropdownMarkSize = 18f;
        [Tooltip("項目の左端からマーカーまでの余白")]
        public float dropdownMarkPadding = 12f;
        [Tooltip("マーカーの分だけ項目名を右へずらす量")]
        public float dropdownLabelIndent = 40f;

        [Header("設定項目名")]
        [Tooltip("項目名を左寄せにする")]
        public bool leftAlignRowLabels = true;
        [Tooltip("項目名が占める横幅の割合")]
        [Range(0.2f, 0.9f)] public float rowLabelWidthRatio = 0.5f;

        [Header("レイアウト")]
        public float contentSpacing = 16f;
        public float contentTopPadding = 40f;
        public Vector2 contentViewSize = new Vector2(1500f, 700f);

        // ------------------------------------------------------------------
        // 文字
        // ------------------------------------------------------------------
        // ------------------------------------------------------------------
        // フォント
        //
        // 【重要】TMP のフォントアセットは「アトラスに焼かれた文字」しか描画できない。
        // 日本語グリフを持たないアセットを指定すると、すべて □（豆腐）になる。
        // fallbackFont に日本語を網羅したアセットを入れておけば、
        // 足りない文字はそちらで描画されるので豆腐を防げる。
        // ------------------------------------------------------------------
        [Header("フォント")]
        [Tooltip("見出し用。明朝系を想定（Zen Old Mincho）")]
        public TMP_FontAsset displayFont;

        [Tooltip("本文・設定項目用。ゴシック系を想定（Noto Sans JP）")]
        public TMP_FontAsset bodyFont;

        [Tooltip("数値・キー名など等幅で見せたい箇所用。未設定なら bodyFont を使う")]
        public TMP_FontAsset monoFont;

        [Tooltip("上記フォントに無い文字の代替。日本語を網羅したアセットを指定する。\n" +
                 "これを設定しておくと、見出しに日本語グリフの無いフォントを選んでも豆腐にならない")]
        public TMP_FontAsset fallbackFont;

        [Tooltip("リスキン時に、使用フォントへ fallbackFont の登録と\n" +
                 "Multi Atlas Textures の有効化を自動で行う（豆腐対策）")]
        public bool autoFixFontFallback = true;

        /// <summary>役割に応じたフォントを返す。未設定なら null。</summary>
        public TMP_FontAsset GetFont(FontRole role)
        {
            switch (role)
            {
                case FontRole.Display: return displayFont != null ? displayFont : bodyFont;
                case FontRole.Mono:    return monoFont != null ? monoFont : bodyFont;
                default:               return bodyFont;
            }
        }

        [Header("文字サイズ")]
        public float sizeTitle  = 52f;
        public float sizeTab    = 30f;
        public float sizeLabel  = 28f;
        public float sizeValue  = 26f;
        public float sizeButton = 30f;
        public float sizeSmall  = 18f;

        [Tooltip("見出しの字間。少し広げると落ち着く")]
        public float titleCharacterSpacing = 8f;

        [Header("既存UIのリスキン時の適用範囲")]
        [Tooltip("既存レイアウトを崩さないため、既定では文字サイズは変更しない。" +
                 "オンにするとリスキン時に上のサイズも流し込む")]
        public bool applyFontSizesOnRestyle = false;

        [Tooltip("見出しに使う GameObject 名の判定キーワード")]
        public string[] headerNameKeywords = { "Header" };

        // ------------------------------------------------------------------
        // 適用
        // ------------------------------------------------------------------

        /// <summary>役割に応じた色をこの Graphic に適用する。</summary>
        public void Apply(ThemeRole role, Graphic graphic)
        {
            if (graphic == null || role == ThemeRole.None) return;

            switch (role)
            {
                case ThemeRole.ScreenBackground:     graphic.color = screenBackground; break;
                case ThemeRole.PanelBackground:      graphic.color = panelBackground; break;
                case ThemeRole.RowBackground:        graphic.color = rowBackground; break;
                case ThemeRole.RowBorder:            graphic.color = WithAlpha(line, rowBorderAlpha); break;
                case ThemeRole.BoxBorder:            graphic.color = line; break;
                case ThemeRole.BoxInner:             graphic.color = screenBackground; break;
                case ThemeRole.DecorLine:            graphic.color = WithAlpha(line, decorLineAlpha); break;
                case ThemeRole.DecorDiamond:         graphic.color = line; break;
                case ThemeRole.SliderBackground:     graphic.color = sliderBackground; break;
                case ThemeRole.SliderFill:           graphic.color = sliderFill; break;
                case ThemeRole.SliderHandle:         graphic.color = sliderHandle; break;
                case ThemeRole.ScrollbarBackground:  graphic.color = scrollbarBackground; break;
                case ThemeRole.ScrollbarHandle:      graphic.color = scrollbarHandle; break;

                case ThemeRole.TextTitle:  ApplyText(graphic, displayFont, sizeTitle,  line,    titleCharacterSpacing); break;
                case ThemeRole.TextTab:    ApplyText(graphic, bodyFont,    sizeTab,    line,    0f); break;
                case ThemeRole.TextLabel:  ApplyText(graphic, bodyFont,    sizeLabel,  line,    0f); break;
                case ThemeRole.TextValue:  ApplyText(graphic, bodyFont,    sizeValue,  line,    0f); break;
                case ThemeRole.TextButton: ApplyText(graphic, bodyFont,    sizeButton, line,    0f); break;
                case ThemeRole.TextSmall:  ApplyText(graphic, bodyFont,    sizeSmall,  textDim, 0f); break;
                case ThemeRole.TextOnLight:ApplyText(graphic, bodyFont,    sizeLabel,  screenBackground, 0f); break;
            }
        }

        /// <summary>黒地の部品として Color Tint を設定する。</summary>
        public void ApplyColors(Selectable selectable) => ApplyTint(selectable, TintMode.Dark);

        /// <summary>
        /// Color Tint をテーマに合わせる。
        /// Color Tint は Image の色を「上書き」するため、白い部品に黒地用の設定を当てると潰れる。
        /// そのため部品の性格ごとに 3 種類を用意している。
        /// </summary>
        public void ApplyTint(Selectable selectable, TintMode mode)
        {
            if (selectable == null) return;

            selectable.transition = Selectable.Transition.ColorTint;
            var colors = selectable.colors;

            switch (mode)
            {
                case TintMode.Handle:
                    colors.normalColor      = handleNormal;
                    colors.highlightedColor = handleHighlighted;
                    colors.pressedColor     = handlePressed;
                    colors.selectedColor    = handleNormal;
                    colors.disabledColor    = handleDisabled;
                    break;

                case TintMode.DropdownItem:
                    colors.normalColor      = itemNormal;
                    colors.highlightedColor = itemHighlighted;
                    colors.pressedColor     = itemPressed;
                    colors.selectedColor    = itemSelected;
                    colors.disabledColor    = btnDisabled;
                    break;

                default:
                    colors.normalColor      = btnNormal;
                    colors.highlightedColor = btnHighlighted;
                    colors.pressedColor     = btnPressed;
                    colors.selectedColor    = btnSelected;
                    colors.disabledColor    = btnDisabled;
                    break;
            }

            colors.colorMultiplier = 1f;
            colors.fadeDuration = btnFadeDuration;
            selectable.colors = colors;
        }

        private static void ApplyText(Graphic graphic, TMP_FontAsset font, float size, Color color, float spacing)
        {
            if (graphic is not TMP_Text text) return;

            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = color;
            text.characterSpacing = spacing;
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }

    /// <summary>フォントの使い分け。</summary>
    public enum FontRole
    {
        /// <summary>本文・設定項目名・ボタン</summary>
        Body = 0,
        /// <summary>見出し</summary>
        Display,
        /// <summary>数値・キー名など</summary>
        Mono,
    }

    /// <summary>
    /// 「この UI 部品はテーマ上どういう役割か」を表す。
    /// ThemedElement に付けておくと、テーマ変更時に一括で色が更新される。
    /// </summary>
    public enum ThemeRole
    {
        None = 0,

        ScreenBackground,
        PanelBackground,
        RowBackground,
        RowBorder,
        /// <summary>白い外枠（この中に BoxInner を重ねて枠線を表現する）</summary>
        BoxBorder,
        /// <summary>BoxBorder の内側に敷く黒。2 枚重ねが「枠線」の正体</summary>
        BoxInner,
        DecorLine,
        DecorDiamond,

        SliderBackground,
        SliderFill,
        SliderHandle,
        ScrollbarBackground,
        ScrollbarHandle,

        TextTitle,
        TextTab,
        TextLabel,
        TextValue,
        TextButton,
        TextSmall,
        /// <summary>白背景の上に乗る黒文字（選択中タブ・押下中ボタン）</summary>
        TextOnLight,
    }
}
