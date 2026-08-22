using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OddOdds.UI.Settings
{
    /// <summary>
    /// 押下中に背景が白へ反転する Color Tint と組み合わせて使う。
    /// 白背景に白文字だと読めなくなるので、押している間だけ文字色を黒へ入れ替える。
    /// </summary>
    [DisallowMultipleComponent]
    public class ButtonLabelInvert : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TMP_Text _label;

        [Tooltip("反転中の文字色。背景が白になるので黒系にする")]
        [SerializeField] private Color _invertedColor = new Color32(0x08, 0x08, 0x08, 0xFF);

        [Tooltip("押した瞬間に反転する（ボタン用）")]
        [SerializeField] private bool _onPress = true;

        [Tooltip("カーソルが乗った時点で反転する（ドロップダウンの項目用）")]
        [SerializeField] private bool _onHover;

        private Color _normalColor = Color.white;
        private bool _inverted;
        private Selectable _selectable;

        private void Awake()
        {
            if (_label == null) _label = GetComponentInChildren<TMP_Text>(true);
            if (_label != null) _normalColor = _label.color;
            _selectable = GetComponent<Selectable>();
        }

        private void OnDisable() => Restore();

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_onHover) Invert();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_onPress) Invert();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // ホバー反転が有効なら、指を離してもカーソルが乗っている間は反転を保つ
            if (!_onHover) Restore();
        }

        public void OnPointerExit(PointerEventData eventData) => Restore();

        private void Invert()
        {
            if (_inverted) return;
            if (_selectable != null && !_selectable.interactable) return;
            if (_label == null) return;

            _normalColor = _label.color;
            _label.color = _invertedColor;
            _inverted = true;
        }

        private void Restore()
        {
            if (!_inverted || _label == null) { _inverted = false; return; }
            _label.color = _normalColor;
            _inverted = false;
        }

#if UNITY_EDITOR
        /// <summary>生成ツールからの配線用。</summary>
        public void Bind(TMP_Text label, Color invertedColor, bool onPress = true, bool onHover = false)
        {
            _label = label;
            _invertedColor = invertedColor;
            _onPress = onPress;
            _onHover = onHover;
        }
#endif
    }
}
