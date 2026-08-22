using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OddOdds.UI.Settings
{
    /// <summary>
    /// 押した瞬間に少しだけ縮ませる。色の反転だけより「押した感」が出る。
    /// Animator も DOTween も使わず、Update で補間するだけの軽い実装。
    /// </summary>
    [DisallowMultipleComponent]
    public class UIButtonScale : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Tooltip("押下中の倍率。0.96〜0.98 くらいが自然")]
        [SerializeField, Range(0.8f, 1f)] private float _pressedScale = 0.96f;

        [Tooltip("0 にすると補間なしで即座に切り替わる")]
        [SerializeField, Range(0f, 0.3f)] private float _duration = 0.06f;

        private Vector3 _defaultScale = Vector3.one;
        private bool _pressed;
        private Selectable _selectable;

        private void Awake()
        {
            _defaultScale = transform.localScale;
            _selectable = GetComponent<Selectable>();
        }

        private void OnDisable()
        {
            _pressed = false;
            transform.localScale = _defaultScale;
        }

        private void Update()
        {
            var target = _pressed ? _defaultScale * _pressedScale : _defaultScale;
            if (transform.localScale == target) return;

            transform.localScale = _duration <= 0f
                ? target
                // unscaledDeltaTime: 設定画面は Time.timeScale = 0 でも動く想定
                : Vector3.Lerp(transform.localScale, target, Time.unscaledDeltaTime / _duration);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.interactable) return;
            _pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData) => _pressed = false;
        public void OnPointerExit(PointerEventData eventData) => _pressed = false;
    }
}
