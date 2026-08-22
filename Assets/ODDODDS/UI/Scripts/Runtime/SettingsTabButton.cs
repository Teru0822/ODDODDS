using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OddOdds.UI.Settings
{
    /// <summary>
    /// タブ 1 個分。非選択は「黒背景 + 白枠 + 白文字」、選択中は「白背景 + 黒文字」に反転する。
    ///
    /// 枠線は画像を使わず、白い Border の上に一回り小さい黒 Background を重ねて表現している。
    /// 枠の太さを変えたいときは 2 枚のサイズ差（= theme.borderThick）を変える。
    ///
    /// 選択中の白背景は Button の Color Tint とぶつかるため、専用の SelectedFill を
    /// 重ねて表示/非表示するかたちにしている（Color Tint に色を上書きされないようにするため）。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SettingsTabButton : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private Image _border;
        [SerializeField] private Image _background;
        [Tooltip("選択中だけ表示する白い塗り")]
        [SerializeField] private Image _selectedFill;
        [SerializeField] private TMP_Text _label;

        [Header("表示")]
        [SerializeField] private string _labelText = "タブ";

        private Button _button;
        private SettingsTheme _theme;
        private bool _selected;

        public Button Button => _button != null ? _button : _button = GetComponent<Button>();
        public bool IsSelected => _selected;

        /// <summary>ラベル文字列。ローカライズを使う場合は TMP 側を直接差し替えてよい。</summary>
        public string LabelText
        {
            get => _labelText;
            set
            {
                _labelText = value;
                if (_label != null) _label.text = value;
            }
        }

        public void Initialize(SettingsTheme theme)
        {
            _theme = theme;
            if (_label != null && !string.IsNullOrEmpty(_labelText)) _label.text = _labelText;
            Refresh();
        }

        /// <summary>選択状態を切り替える。実際の切り替え判断は SettingsScreen が行う。</summary>
        public void SetSelected(bool selected)
        {
            _selected = selected;
            Refresh();
        }

        private void Refresh()
        {
            if (_selectedFill != null) _selectedFill.gameObject.SetActive(_selected);

            if (_theme != null)
            {
                if (_border != null) _border.color = _theme.line;
                if (_selectedFill != null) _selectedFill.color = _theme.line;
                if (_label != null) _label.color = _selected ? _theme.screenBackground : _theme.line;
            }

            // 選択中のタブは押せなくする（白背景のまま固定される）
            if (Button != null) Button.interactable = !_selected;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_label != null && !string.IsNullOrEmpty(_labelText)) _label.text = _labelText;
        }
#endif
    }
}
