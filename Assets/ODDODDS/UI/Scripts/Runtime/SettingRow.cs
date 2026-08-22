using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OddOdds.UI.Settings
{
    /// <summary>
    /// 設定項目 1 行分の骨組み。「枠 + ラベル + コントロール」だけを持つ。
    ///
    /// 設定項目を増やすときは、Prefabs/ 配下の SettingRow_*.prefab を複製して
    /// Label を変え、Control の中身を差し替えるだけでよい。
    /// このクラス自体はロジックを持たない（値の読み書きは呼び出し側の責務）。
    /// </summary>
    public class SettingRow : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private Image _border;
        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _label;
        [Tooltip("Dropdown / Toggle / Slider などを入れる枠")]
        [SerializeField] private RectTransform _control;

        [Header("表示")]
        [SerializeField] private string _labelText = "設定項目";

        public Image Border => _border;
        public Image Background => _background;
        public TMP_Text Label => _label;
        public RectTransform Control => _control;

        /// <summary>ラベル文字列。ローカライズ運用なら TMP 側を直接差し替えてもよい。</summary>
        public string LabelText
        {
            get => _labelText;
            set
            {
                _labelText = value;
                if (_label != null) _label.text = value;
            }
        }

        /// <summary>Control 直下の最初のコンポーネントを取り出すヘルパー。</summary>
        public T GetControl<T>() where T : Component
            => _control != null ? _control.GetComponentInChildren<T>(true) : null;

        /// <summary>行ごと有効/無効を切り替える。無効時は文字を暗くする。</summary>
        public void SetInteractable(bool interactable, SettingsTheme theme)
        {
            if (_control != null)
            {
                foreach (var selectable in _control.GetComponentsInChildren<Selectable>(true))
                    selectable.interactable = interactable;
            }

            if (_label != null && theme != null)
                _label.color = interactable ? theme.line : theme.disabled;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_label != null && !string.IsNullOrEmpty(_labelText)) _label.text = _labelText;
        }
#endif
    }
}
