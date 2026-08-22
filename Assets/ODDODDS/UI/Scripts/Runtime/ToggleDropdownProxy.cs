using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace OddOdds.UI.Settings
{
    /// <summary>
    /// 「排他的に並んだ複数の Toggle」をドロップダウン 1 つで操作できるようにする橋渡し。
    ///
    /// 既存の SettingUIManager は Toggle を直接参照して設定を反映しているため、
    /// Toggle を消してドロップダウンに置き換えると機能が壊れる。
    /// そこで Toggle は残したまま非表示にし、ドロップダウンの選択を Toggle に流し込む。
    /// これで見た目だけドロップダウンにでき、設定処理には一切手を入れずに済む。
    ///
    /// 選択肢の文言は Toggle のラベルから読む。ラベルはローカライズされているので、
    /// 言語が切り替わったら選択肢を作り直して追従させる。
    /// </summary>
    [DisallowMultipleComponent]
    public class ToggleDropdownProxy : MonoBehaviour
    {
        [Tooltip("操作用のドロップダウン")]
        [SerializeField] private TMP_Dropdown _dropdown;

        [Tooltip("実体となる Toggle。並び順がドロップダウンの選択肢の順番になる")]
        [SerializeField] private List<Toggle> _toggles = new List<Toggle>();

        [Tooltip("Toggle を見えなくする（機能は残したまま見た目だけ消す）")]
        [SerializeField] private bool _hideToggles = true;

        [Tooltip("言語切替時に、Toggle のラベルから選択肢を作り直す")]
        [SerializeField] private bool _followLocale = true;

        private readonly List<TMP_Text> _labels = new List<TMP_Text>();
        private bool _suppress;
        private Coroutine _pendingRebuild;

        private void Awake()
        {
            if (_dropdown == null || _toggles.Count == 0) return;

            CacheLabels();

            if (_hideToggles) HideToggles();

            _dropdown.onValueChanged.AddListener(OnDropdownChanged);
            foreach (var toggle in _toggles)
                if (toggle != null) toggle.onValueChanged.AddListener(_ => SyncFromToggles());

            if (_followLocale) SubscribeLocale();

            RebuildOptions();
            SyncFromToggles();
        }

        private void OnDestroy()
        {
            if (_dropdown != null) _dropdown.onValueChanged.RemoveListener(OnDropdownChanged);
            if (_followLocale) UnsubscribeLocale();
        }

        private void OnEnable()
        {
            // 別ページを開いている間に言語が変わると、この行は非アクティブで
            // 作り直しを受け取れない。戻ってきた時に必ず読み直す。
            RebuildOptions();
            SyncFromToggles();

            // ラベル側のローカライズ更新と前後する可能性があるので、1 フレーム後にもう一度
            RequestRebuild();
        }

        /// <summary>
        /// Toggle を見えなくする。
        ///
        /// SetActive(false) は使えない。ラベルに付いている LocalizeStringEvent は
        /// GameObject が非アクティブだと言語切替を受け取れず、
        /// 選択肢の文言が古い言語のまま固まってしまうため。
        /// CanvasGroup で透明にすれば、アクティブなまま見えなくできる。
        /// </summary>
        private void HideToggles()
        {
            foreach (var toggle in _toggles)
            {
                if (toggle == null) continue;

                var group = toggle.GetComponent<CanvasGroup>();
                if (group == null) group = toggle.gameObject.AddComponent<CanvasGroup>();

                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }

        // ------------------------------------------------------------------
        // 選択の受け渡し
        // ------------------------------------------------------------------

        private void OnDropdownChanged(int index)
        {
            if (_suppress) return;
            _suppress = true;

            // 先に false を切ってから true にしないと、ToggleGroup 無しの構成で
            // 複数同時 ON になることがある
            for (int i = 0; i < _toggles.Count; i++)
            {
                if (_toggles[i] != null && i != index) _toggles[i].isOn = false;
            }
            if (index >= 0 && index < _toggles.Count && _toggles[index] != null)
                _toggles[index].isOn = true;

            _suppress = false;
        }

        /// <summary>Toggle 側の状態をドロップダウンの表示に反映する。</summary>
        public void SyncFromToggles()
        {
            if (_suppress || _dropdown == null) return;

            for (int i = 0; i < _toggles.Count; i++)
            {
                if (_toggles[i] == null || !_toggles[i].isOn) continue;
                if (_dropdown.value == i) return;

                _suppress = true;
                _dropdown.SetValueWithoutNotify(i);
                _dropdown.RefreshShownValue();
                _suppress = false;
                return;
            }
        }

        // ------------------------------------------------------------------
        // 言語への追従
        // ------------------------------------------------------------------

        private void CacheLabels()
        {
            _labels.Clear();
            foreach (var toggle in _toggles)
            {
                if (toggle == null) { _labels.Add(null); continue; }

                var label = toggle.transform.Find("Label")?.GetComponent<TMP_Text>();
                if (label == null) label = toggle.GetComponentInChildren<TMP_Text>(true);
                _labels.Add(label);
            }
        }

        private void SubscribeLocale()
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

            // ラベル側のローカライズ更新が終わった瞬間が一番確実なタイミング
            foreach (var label in _labels)
            {
                if (label == null) continue;
                var localize = label.GetComponent<LocalizeStringEvent>();
                if (localize != null) localize.OnUpdateString.AddListener(OnLabelUpdated);
            }
        }

        private void UnsubscribeLocale()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;

            foreach (var label in _labels)
            {
                if (label == null) continue;
                var localize = label.GetComponent<LocalizeStringEvent>();
                if (localize != null) localize.OnUpdateString.RemoveListener(OnLabelUpdated);
            }
        }

        private void OnLabelUpdated(string _) => RequestRebuild();

        private void OnLocaleChanged(UnityEngine.Localization.Locale _) => RequestRebuild();

        /// <summary>
        /// 何度も呼ばれるので、フレーム末にまとめて 1 回だけ作り直す。
        /// ローカライズの適用順に左右されないよう、1 フレーム待ってから読む。
        /// </summary>
        private void RequestRebuild()
        {
            if (!isActiveAndEnabled) return;
            if (_pendingRebuild != null) StopCoroutine(_pendingRebuild);
            _pendingRebuild = StartCoroutine(RebuildAtEndOfFrame());
        }

        private IEnumerator RebuildAtEndOfFrame()
        {
            yield return null;
            _pendingRebuild = null;
            RebuildOptions();
        }

        /// <summary>Toggle のラベルから選択肢を作り直す。選択中の位置は保つ。</summary>
        public void RebuildOptions()
        {
            if (_dropdown == null || _toggles.Count == 0) return;
            if (_dropdown.IsExpanded) return;   // 開いている最中に作り直すと表示が乱れる

            if (_labels.Count != _toggles.Count) CacheLabels();

            var options = new List<string>(_toggles.Count);
            for (int i = 0; i < _toggles.Count; i++)
            {
                var text = _labels[i] != null ? _labels[i].text : null;
                options.Add(string.IsNullOrEmpty(text) ? "Option " + (i + 1) : text);
            }

            // 中身が同じなら触らない（毎フレーム作り直さないため）
            if (_dropdown.options.Count == options.Count)
            {
                bool same = true;
                for (int i = 0; i < options.Count; i++)
                {
                    if (_dropdown.options[i].text != options[i]) { same = false; break; }
                }
                if (same) return;
            }

            int selected = _dropdown.value;
            _suppress = true;
            _dropdown.ClearOptions();
            _dropdown.AddOptions(options);
            _dropdown.SetValueWithoutNotify(Mathf.Clamp(selected, 0, options.Count - 1));
            _dropdown.RefreshShownValue();
            _suppress = false;
        }

#if UNITY_EDITOR
        /// <summary>リスキンツールからの配線用。</summary>
        public void Bind(TMP_Dropdown dropdown, List<Toggle> toggles, bool hideToggles = true)
        {
            _dropdown = dropdown;
            _toggles = toggles;
            _hideToggles = hideToggles;
            CacheLabels();
        }
#endif
    }
}
