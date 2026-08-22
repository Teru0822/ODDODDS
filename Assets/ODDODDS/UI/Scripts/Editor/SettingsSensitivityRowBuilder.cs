using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OddOdds.UI.Settings.EditorTools
{
    /// <summary>
    /// 「操作」タブに マウス感度 の行（スライダー + 数値入力）を追加する。
    ///
    /// 既存の音量スライダー行を複製して作るので、見た目と大きさが他の行と揃う。
    /// 生成後は SettingUIManager の Sensitivity Slider / Sensitivity Input に自動で繋ぐ。
    ///
    /// 一度実行すれば十分。もう一度実行しても既にあれば作り直さない。
    /// </summary>
    public static class SettingsSensitivityRowBuilder
    {
        private const string PrefabPath = "Assets/Resources/Prefab/SettingCanvas.prefab";
        private const string RowName    = "SensitivityRow";
        private const string PageName   = "KeyBindContent";
        private const string SourceRow  = "BGMslider";

        [MenuItem("ODD ODDS/UI/操作タブに感度設定を追加", false, 25)]
        public static void Build()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null)
            {
                Debug.LogError("[感度設定] " + PrefabPath + " を読み込めませんでした。");
                return;
            }

            try
            {
                var page = FindByName(root, PageName);
                if (page == null)
                {
                    Debug.LogError("[感度設定] " + PageName + " が見つかりません。");
                    return;
                }

                if (FindByName(root, RowName) != null)
                {
                    Debug.Log("[感度設定] 既に " + RowName + " があるので何もしませんでした。");
                    return;
                }

                var source = FindByName(root, SourceRow);
                if (source == null)
                {
                    Debug.LogError("[感度設定] 複製元の " + SourceRow + " が見つかりません。");
                    return;
                }

                var row = Object.Instantiate(source.gameObject, page);
                row.name = RowName;

                var slider = PrepareRow(row);
                if (slider == null)
                {
                    Debug.LogError("[感度設定] 複製した行に Slider がありませんでした。");
                    return;
                }

                var input = CreateNumberInput(row.transform, slider);
                WireToManager(root, slider, input);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[感度設定] 「操作」タブに感度の行を追加しました。\n" +
                          "見た目を整えたい場合はリスキンを実行してください。");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>複製した行から不要なものを外し、ラベルを差し替える。</summary>
        private static Slider PrepareRow(GameObject row)
        {
            // 試聴ボタンは感度には要らない
            var playButton = row.transform.Find("PlaySoundButton");
            if (playButton != null) Object.DestroyImmediate(playButton.gameObject);

            // ラベル。複製元のローカライズが付いたままだと「BGM」に戻されるので外す
            var label = row.transform.Cast<Transform>()
                           .Select(t => t.GetComponent<TMP_Text>())
                           .FirstOrDefault(t => t != null && !t.name.StartsWith("__"));
            if (label != null)
            {
                var localize = label.GetComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();
                if (localize != null) Object.DestroyImmediate(localize);
                label.text = "感度";
            }

            var slider = row.GetComponentInChildren<Slider>(true);
            if (slider == null) return null;

            // 音量スライダーの配線が残っていると音量まで動いてしまう
            var so = new SerializedObject(slider);
            var calls = so.FindProperty("m_OnValueChanged.m_PersistentCalls.m_Calls");
            if (calls != null) { calls.ClearArray(); so.ApplyModifiedPropertiesWithoutUndo(); }

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = 0.5f;

            // 数値入力を右に置くぶん、スライダーを縮める
            var rt = slider.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(rt.sizeDelta.x - 140f, rt.sizeDelta.y);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x - 70f, rt.anchoredPosition.y);

            return slider;
        }

        /// <summary>スライダーの右に数値入力欄を作る。</summary>
        private static TMP_InputField CreateNumberInput(Transform parent, Slider slider)
        {
            var sliderRect = slider.GetComponent<RectTransform>();

            var go = new GameObject("SensitivityInput", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = sliderRect.anchorMin;
            rt.anchorMax = sliderRect.anchorMax;
            rt.pivot = sliderRect.pivot;
            rt.sizeDelta = new Vector2(120f, Mathf.Max(40f, sliderRect.sizeDelta.y));
            rt.anchoredPosition = new Vector2(
                sliderRect.anchoredPosition.x + sliderRect.sizeDelta.x * 0.5f + 70f,
                sliderRect.anchoredPosition.y);

            var background = go.GetComponent<Image>();
            background.sprite = null;
            background.color = new Color32(0x08, 0x08, 0x08, 0xFF);

            // TMP_InputField は「表示領域」と「文字」を別オブジェクトで持つ必要がある
            var viewport = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(rt, false);
            var vrt = (RectTransform)viewport.transform;
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = new Vector2(8f, 4f);
            vrt.offsetMax = new Vector2(-8f, -4f);

            var placeholder = CreateText(vrt, "Placeholder", "0");
            placeholder.color = new Color(1f, 1f, 1f, 0.4f);

            var text = CreateText(vrt, "Text", "50");

            var input = go.AddComponent<TMP_InputField>();
            input.textViewport = vrt;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.targetGraphic = background;
            input.contentType = TMP_InputField.ContentType.DecimalNumber;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 6;
            input.text = "50";

            // 枠線。リスキンと同じ描き方なので後から色や太さを揃えられる
            var border = go.AddComponent<UIRectBorder>();
            border.Thickness = 1f;
            border.Color = Color.white;
            border.Rebuild();

            return input;
        }

        private static TextMeshProUGUI CreateText(RectTransform parent, string name, string content)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.fontSize = 26f;
            return text;
        }

        /// <summary>SettingUIManager の感度用フィールドへ繋ぐ。</summary>
        private static void WireToManager(GameObject root, Slider slider, TMP_InputField input)
        {
            var manager = root.GetComponentInChildren<SettingUIManager>(true);
            if (manager == null)
            {
                Debug.LogWarning("[感度設定] SettingUIManager が見つからないため、参照を繋げませんでした。");
                return;
            }

            var so = new SerializedObject(manager);
            var sliderProp = so.FindProperty("_sensitivitySlider");
            var inputProp = so.FindProperty("_sensitivityInput");

            if (sliderProp == null || inputProp == null)
            {
                Debug.LogWarning("[感度設定] SettingUIManager に感度用のフィールドがありません。" +
                                 "スクリプトのコンパイルが終わってから実行してください。");
                return;
            }

            sliderProp.objectReferenceValue = slider;
            inputProp.objectReferenceValue = input;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform FindByName(GameObject root, string name)
            => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    }
}
