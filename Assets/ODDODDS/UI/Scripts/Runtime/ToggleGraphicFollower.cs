using UnityEngine;
using UnityEngine.UI;

namespace OddOdds.UI.Settings
{
    /// <summary>
    /// Toggle の graphic は 1 つしか指定できないため、
    /// ON のときだけ出したいオブジェクト（チェックの「✓」文字など）をここで追従させる。
    /// </summary>
    [RequireComponent(typeof(Toggle))]
    public class ToggleGraphicFollower : MonoBehaviour
    {
        [Tooltip("ON のときだけ表示するオブジェクト")]
        [SerializeField] private GameObject[] _showWhenOn = new GameObject[0];

        [Tooltip("OFF のときだけ表示するオブジェクト")]
        [SerializeField] private GameObject[] _showWhenOff = new GameObject[0];

        private Toggle _toggle;

        private void Awake()
        {
            _toggle = GetComponent<Toggle>();
            _toggle.onValueChanged.AddListener(Refresh);
            Refresh(_toggle.isOn);
        }

        private void OnDestroy()
        {
            if (_toggle != null) _toggle.onValueChanged.RemoveListener(Refresh);
        }

        private void Refresh(bool isOn)
        {
            foreach (var go in _showWhenOn)
                if (go != null) go.SetActive(isOn);

            foreach (var go in _showWhenOff)
                if (go != null) go.SetActive(!isOn);
        }

#if UNITY_EDITOR
        /// <summary>生成ツールからの配線用。</summary>
        public void Bind(Toggle toggle, params GameObject[] showWhenOn)
        {
            _toggle = toggle;
            _showWhenOn = showWhenOn;
        }

        private void OnValidate()
        {
            var toggle = GetComponent<Toggle>();
            if (toggle != null && !Application.isPlaying) Refresh(toggle.isOn);
        }
#endif
    }
}
