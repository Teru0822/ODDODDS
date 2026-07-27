using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;

namespace App.ATM
{
    /// <summary>
    /// ATMScreens.yaml を読み込み、画面 id とトークン辞書から
    /// (1) 3D TextMeshPro に流し込むリッチテキストの生成
    /// (2) WorldSpace Canvas 上への画像オーバーレイの生成/破棄
    /// を行うレンダラ。ATMController から利用されるプレーンな C# クラス。
    /// </summary>
    public class ATMScreenRenderer
    {
        private readonly TextMeshPro _screenText;
        private readonly Transform _imageContainer;
        private readonly ATMScreenLibrary _library;
        private readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

        private string _currentImageScreen = null;

        public bool IsLoaded => _library != null && _library.Count > 0;

        // 文字間インライン画像用に実行時生成する TMP Sprite Asset
        private TMP_SpriteAsset _inlineSpriteAsset;
        private readonly HashSet<string> _inlineSpriteNames = new HashSet<string>();
        private static readonly Regex InlineSpriteRegex = new Regex(@"\[img:([^\]]+)\]", RegexOptions.Compiled);

        public ATMScreenRenderer(TextMeshPro screenText, Transform imageContainer)
        {
            _screenText = screenText;
            _imageContainer = imageContainer;
            _library = LoadLibrary();
            BuildInlineSpriteAsset();
        }

        private static string ScreensDirectory => Path.Combine(Application.streamingAssetsPath, "ATM");

        private static ATMScreenLibrary LoadLibrary()
        {
            try
            {
                string path = Path.Combine(ScreensDirectory, "ATMScreens.yaml");
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[ATMScreenRenderer] YAML が見つかりません: {path}");
                    return new ATMScreenLibrary();
                }
                string text = File.ReadAllText(path, Encoding.UTF8);
                return ATMScreenLibrary.Parse(text);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ATMScreenRenderer] YAML 読み込みに失敗しました: {e.Message}");
                return new ATMScreenLibrary();
            }
        }

        /// <summary>指定画面の存在確認。</summary>
        public bool HasScreen(string screenId)
        {
            return _library.TryGet(screenId, out _);
        }

        /// <summary>
        /// 画面を切り替える。画像オーバーレイを再構築し、テキストも更新する。
        /// 画面遷移時に一度だけ呼ぶこと（画像の作り直しを毎フレーム行わないため）。
        /// </summary>
        public void SetScreen(string screenId, IDictionary<string, string> tokens)
        {
            RebuildImages(screenId);
            UpdateText(screenId, tokens);
        }

        /// <summary>
        /// テキストのみ更新する（タイピング/プログレス/カウントアップ演出などの毎フレーム更新用）。
        /// </summary>
        public void UpdateText(string screenId, IDictionary<string, string> tokens)
        {
            if (_screenText == null) return;
            if (!_library.TryGet(screenId, out var elements)) return;

            var sb = new StringBuilder(256);
            bool first = true;
            foreach (var el in elements)
            {
                if (el.IsImage) continue;
                if (!first) sb.Append('\n');
                first = false;
                sb.Append(WrapText(ExpandInlineSprites(Substitute(el.Text, tokens)), el));
            }
            _screenText.text = sb.ToString();
        }

        /// <summary>すべての画像オーバーレイを破棄する（ATM 消灯時など）。</summary>
        public void ClearImages()
        {
            if (_imageContainer == null) return;
            for (int i = _imageContainer.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(_imageContainer.GetChild(i).gameObject);
            }
            _currentImageScreen = null;
        }

        private void RebuildImages(string screenId)
        {
            if (_imageContainer == null) return;
            if (_currentImageScreen == screenId) return; // 同一画面なら作り直さない
            _currentImageScreen = screenId;

            for (int i = _imageContainer.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(_imageContainer.GetChild(i).gameObject);
            }

            if (!_library.TryGet(screenId, out var elements)) return;

            int idx = 0;
            foreach (var el in elements)
            {
                if (!el.IsImage || string.IsNullOrEmpty(el.Sprite)) continue;
                Sprite sprite = LoadSprite(el.Sprite);
                if (sprite == null) continue;

                var go = new GameObject($"Image_{idx++}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_imageContainer, false);

                var img = go.GetComponent<Image>();
                img.sprite = sprite;
                img.raycastTarget = false;
                img.preserveAspect = true;

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(el.X, el.Y);
                float w = el.Width > 0f ? el.Width : sprite.rect.width;
                float h = el.Height > 0f ? el.Height : sprite.rect.height;
                rt.sizeDelta = new Vector2(w, h);
            }
        }

        private Sprite LoadSprite(string relativePath)
        {
            if (_spriteCache.TryGetValue(relativePath, out var cached)) return cached;

            Sprite sprite = null;
            try
            {
                string full = Path.Combine(ScreensDirectory, relativePath);
                if (File.Exists(full))
                {
                    byte[] bytes = File.ReadAllBytes(full);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes))
                    {
                        sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    }
                }
                else
                {
                    Debug.LogWarning($"[ATMScreenRenderer] 画像が見つかりません: {full}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ATMScreenRenderer] 画像読み込み失敗 ({relativePath}): {e.Message}");
            }

            _spriteCache[relativePath] = sprite; // null もキャッシュして再試行を抑制
            return sprite;
        }

        // --- 文字間インライン画像 (TMP Sprite Asset を実行時生成) ---

        /// <summary>
        /// 全画面テキスト中の [img:NAME] を走査し、StreamingAssets/ATM/images の PNG から
        /// TMP Sprite Asset を1つ生成して atmScreenText の既定スプライトアセットに設定する。
        /// これにより text 内に [img:NAME] と書くだけで文字と同じ行に画像を差し込める。
        /// </summary>
        private void BuildInlineSpriteAsset()
        {
            if (_screenText == null || _library == null) return;

            // 1) [img:NAME] を収集（拡張子は無視して名前に統一）
            var names = new List<string>();
            var seen = new HashSet<string>();
            foreach (var el in _library.AllElements())
            {
                if (el == null || string.IsNullOrEmpty(el.Text)) continue;
                foreach (Match m in InlineSpriteRegex.Matches(el.Text))
                {
                    string spriteName = Path.GetFileNameWithoutExtension(m.Groups[1].Value.Trim());
                    if (spriteName.Length > 0 && seen.Add(spriteName)) names.Add(spriteName);
                }
            }
            if (names.Count == 0) return; // インライン画像を使っていない

            // 2) 各 PNG を読み込む
            var texList = new List<Texture2D>();
            var loadedNames = new List<string>();
            foreach (string n in names)
            {
                Texture2D tex = LoadTexture(n);
                if (tex != null) { texList.Add(tex); loadedNames.Add(n); }
            }
            if (texList.Count == 0) return;

            try
            {
                // 3) 1枚のアトラスにパッキング
                var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Rect[] uvRects = atlas.PackTextures(texList.ToArray(), 2, 4096);

                // 4) TMP_SpriteAsset を実行時生成
                var spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                spriteAsset.name = "ATMInlineSprites";

                // upgrade による table 消去を避けるため version を先に設定 (internal set のためリフレクション)
                var versionField = typeof(TMP_Asset).GetField("m_Version", BindingFlags.Instance | BindingFlags.NonPublic);
                versionField?.SetValue(spriteAsset, "1.1.0");

                spriteAsset.spriteSheet = atlas;

                for (int i = 0; i < loadedNames.Count; i++)
                {
                    Rect uv = uvRects[i];
                    int gx = Mathf.RoundToInt(uv.x * atlas.width);
                    int gy = Mathf.RoundToInt(uv.y * atlas.height);
                    int gw = Mathf.RoundToInt(uv.width * atlas.width);
                    int gh = Mathf.RoundToInt(uv.height * atlas.height);

                    // faceInfo 未設定のままにすると TMP がフォント高さに合わせて自動スケールする。
                    // metrics.height を実ピクセル高にしておくことがその計算の前提。
                    var glyph = new TMP_SpriteGlyph(
                        (uint)i,
                        new GlyphMetrics(gw, gh, 0f, gh, gw),
                        new GlyphRect(gx, gy, gw, gh),
                        1.0f, 0);
                    spriteAsset.spriteGlyphTable.Add(glyph);

                    var character = new TMP_SpriteCharacter((uint)(0xE000 + i), glyph)
                    {
                        name = loadedNames[i],
                        scale = 1.0f
                    };
                    spriteAsset.spriteCharacterTable.Add(character);

                    _inlineSpriteNames.Add(loadedNames[i]);
                }

                // 5) スプライト用マテリアル
                Shader spriteShader = Shader.Find("TextMeshPro/Sprite");
                if (spriteShader != null)
                {
                    var mat = new Material(spriteShader);
                    mat.SetTexture("_MainTex", atlas);
                    spriteAsset.material = mat;
                }

                spriteAsset.UpdateLookupTables();

                _inlineSpriteAsset = spriteAsset;
                _screenText.spriteAsset = spriteAsset; // 既定に設定 → <sprite name="..."> で解決
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ATMScreenRenderer] インライン用 SpriteAsset 生成に失敗: {e.Message}");
            }
        }

        private Texture2D LoadTexture(string spriteName)
        {
            try
            {
                string file = Path.HasExtension(spriteName) ? spriteName : spriteName + ".png";
                string full = Path.Combine(ScreensDirectory, "images", file);
                if (!File.Exists(full))
                {
                    Debug.LogWarning($"[ATMScreenRenderer] インライン画像が見つかりません: {full}");
                    return null;
                }
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(File.ReadAllBytes(full))) return tex;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ATMScreenRenderer] インライン画像読み込み失敗 ({spriteName}): {e.Message}");
            }
            return null;
        }

        /// <summary>text 中の [img:NAME] を TMP の &lt;sprite name="NAME"&gt; に展開する。</summary>
        private string ExpandInlineSprites(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("[img:", System.StringComparison.Ordinal) < 0) return text;
            return InlineSpriteRegex.Replace(text, m =>
            {
                string spriteName = Path.GetFileNameWithoutExtension(m.Groups[1].Value.Trim());
                if (_inlineSpriteNames.Contains(spriteName))
                    return $"<sprite name=\"{spriteName}\">";
                return ""; // 未登録の画像マーカーは除去し、生の [img:...] を表示しない
            });
        }

        // --- テキスト整形 ---

        private static string WrapText(string text, ATMScreenElement el)
        {
            string result = text ?? "";
            if (!string.IsNullOrEmpty(el.Font)) result = $"<font=\"{el.Font}\">{result}</font>";
            if (!string.IsNullOrEmpty(el.Color)) result = $"<color={el.Color}>{result}</color>";
            if (el.HasSize && !Mathf.Approximately(el.Size, 100f))
                result = $"<size={el.Size.ToString("0.###", CultureInfo.InvariantCulture)}%>{result}</size>";
            if (!string.IsNullOrEmpty(el.Align)) result = $"<align={el.Align}>{result}</align>";
            return result;
        }

        private static string Substitute(string text, IDictionary<string, string> tokens)
        {
            if (string.IsNullOrEmpty(text) || tokens == null || text.IndexOf('{') < 0) return text;
            var sb = new StringBuilder(text.Length + 32);
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '{')
                {
                    int end = text.IndexOf('}', i + 1);
                    if (end > i)
                    {
                        string key = text.Substring(i + 1, end - i - 1);
                        if (tokens.TryGetValue(key, out string val))
                        {
                            sb.Append(val);
                            i = end + 1;
                            continue;
                        }
                    }
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }
    }
}
