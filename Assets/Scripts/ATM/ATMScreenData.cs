using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace App.ATM
{
    /// <summary>
    /// 1画面を構成する要素（テキスト or 画像）。ATMScreens.yaml の各 element に対応。
    /// </summary>
    public class ATMScreenElement
    {
        public string Type = "text";   // "text" or "image"

        // --- text 用 ---
        public string Text = "";
        public bool HasSize = false;
        public float Size = 100f;      // ％
        public string Color = null;    // "#RRGGBB" or null
        public string Align = null;    // "left" / "center" / "right" or null
        public string Font = null;     // TMP フォントアセット名（Resources/Fonts & Materials 配下）or null

        // --- image 用 ---
        public string Sprite = null;   // StreamingAssets/ATM/ からの相対パス
        public float Width = 0f;
        public float Height = 0f;
        public float X = 0f;
        public float Y = 0f;

        public bool IsImage => Type == "image";
    }

    /// <summary>
    /// 画面 id → 要素リスト の集合。ATMScreens.yaml をパースして生成する。
    /// 依存追加を避けるため、本スキーマ専用の軽量 YAML リーダを内蔵している。
    /// </summary>
    public class ATMScreenLibrary
    {
        private readonly Dictionary<string, List<ATMScreenElement>> _screens =
            new Dictionary<string, List<ATMScreenElement>>();

        public int Count => _screens.Count;

        public bool TryGet(string screenId, out List<ATMScreenElement> elements)
        {
            return _screens.TryGetValue(screenId, out elements);
        }

        /// <summary>全画面の全要素を列挙する（インライン画像スキャン等に使用）。</summary>
        public IEnumerable<ATMScreenElement> AllElements()
        {
            foreach (var list in _screens.Values)
                foreach (var el in list)
                    yield return el;
        }

        /// <summary>
        /// YAML テキストをパースして画面ライブラリを構築する。失敗しても例外を投げず、
        /// 解析できた分だけ返す（呼び出し側でフォールバック可能）。
        /// </summary>
        public static ATMScreenLibrary Parse(string yaml)
        {
            var lib = new ATMScreenLibrary();
            if (string.IsNullOrEmpty(yaml)) return lib;

            bool inScreens = false;
            List<ATMScreenElement> currentScreen = null;
            ATMScreenElement currentElement = null;

            string[] lines = yaml.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            foreach (string raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;

                int indent = CountIndent(raw);
                string trimmed = raw.Trim();
                if (trimmed.StartsWith("#")) continue; // 行コメント

                // ルート: screens:
                if (trimmed == "screens:")
                {
                    inScreens = true;
                    continue;
                }
                if (!inScreens) continue;

                // シーケンス要素の開始: "- ..." → 新しい element
                if (trimmed == "-" || trimmed.StartsWith("- "))
                {
                    if (currentScreen == null) continue;
                    currentElement = new ATMScreenElement();
                    currentScreen.Add(currentElement);

                    string remainder = trimmed.Length > 1 ? trimmed.Substring(1).Trim() : "";
                    if (remainder.Length > 0)
                    {
                        ApplyField(currentElement, remainder);
                    }
                    continue;
                }

                // "key: value" 形式
                int colon = trimmed.IndexOf(':');
                if (colon < 0) continue;
                string key = trimmed.Substring(0, colon).Trim();
                string value = trimmed.Substring(colon + 1).Trim();

                // 値が空 かつ 浅いインデント → 画面名
                if (value.Length == 0 && indent <= 2)
                {
                    currentScreen = new List<ATMScreenElement>();
                    currentElement = null;
                    lib._screens[key] = currentScreen;
                    continue;
                }

                // それ以外は現 element のフィールド
                if (currentElement != null)
                {
                    SetField(currentElement, key, value);
                }
            }

            return lib;
        }

        private static int CountIndent(string line)
        {
            int i = 0;
            while (i < line.Length && line[i] == ' ') i++;
            return i;
        }

        /// <summary>"key: value" 断片（"- " 直後の最初のフィールド）を element に適用。</summary>
        private static void ApplyField(ATMScreenElement el, string keyValue)
        {
            int colon = keyValue.IndexOf(':');
            if (colon < 0) return;
            string key = keyValue.Substring(0, colon).Trim();
            string value = keyValue.Substring(colon + 1).Trim();
            SetField(el, key, value);
        }

        private static void SetField(ATMScreenElement el, string key, string rawValue)
        {
            string value = Unquote(rawValue);
            switch (key)
            {
                case "type": el.Type = value; break;
                case "text": el.Text = value; break;
                case "color": el.Color = string.IsNullOrEmpty(value) ? null : value; break;
                case "align": el.Align = string.IsNullOrEmpty(value) ? null : value; break;
                case "font": el.Font = string.IsNullOrEmpty(value) ? null : value; break;
                case "size":
                    if (TryParseFloat(value, out float sz)) { el.Size = sz; el.HasSize = true; }
                    break;
                case "sprite": el.Sprite = string.IsNullOrEmpty(value) ? null : value; break;
                case "width": if (TryParseFloat(value, out float w)) el.Width = w; break;
                case "height": if (TryParseFloat(value, out float h)) el.Height = h; break;
                case "x": if (TryParseFloat(value, out float x)) el.X = x; break;
                case "y": if (TryParseFloat(value, out float y)) el.Y = y; break;
                default: break; // 未知キーは無視
            }
        }

        /// <summary>前後のダブルクオートを外し、簡単なエスケープを展開する。</summary>
        private static string Unquote(string s)
        {
            if (s == null) return "";
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
            {
                string inner = s.Substring(1, s.Length - 2);
                var sb = new StringBuilder(inner.Length);
                for (int i = 0; i < inner.Length; i++)
                {
                    if (inner[i] == '\\' && i + 1 < inner.Length)
                    {
                        char n = inner[i + 1];
                        if (n == 'n') { sb.Append('\n'); i++; continue; }
                        if (n == '"') { sb.Append('"'); i++; continue; }
                        if (n == '\\') { sb.Append('\\'); i++; continue; }
                    }
                    sb.Append(inner[i]);
                }
                return sb.ToString();
            }
            return s;
        }

        private static bool TryParseFloat(string s, out float result)
        {
            // 先頭トークンのみ（万一の後続文字対策）
            return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }
    }
}
