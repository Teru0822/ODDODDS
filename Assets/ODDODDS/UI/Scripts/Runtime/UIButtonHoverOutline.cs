using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OddOdds.UI.Settings
{
    /// <summary>
    /// ボタン自身に直接付けた Outline コンポーネントを、カーソルが乗っている間だけ有効にする。
    /// ホバー音・選択音も、このスクリプトを付けたボタン自身に対して鳴らす。
    /// ボタンの GameObject にこのコンポーネントを付けるだけで動く（Outline は自動取得）。
    /// </summary>
    [DisallowMultipleComponent]
    public class UIButtonHoverOutline : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("アウトライン")]
        [Tooltip("同じ GameObject に付けた Outline コンポーネント。null なら自動取得する")]
        [SerializeField] private Outline _outline;

        [Header("SE")]
        [Tooltip("ホバー開始時に鳴らす音")]
        [SerializeField] private AudioClip _hoverClip;

        [Tooltip("選択(クリック)時に鳴らす音")]
        [SerializeField] private AudioClip _selectClip;

        [Tooltip("再生用 AudioSource。null なら自身に AddComponent して使う")]
        [SerializeField] private AudioSource _audioSource;

        [Range(0f, 1f)]
        [SerializeField] private float _volume = 1f;

        private Selectable _selectable;

        private bool IsInteractable => _selectable == null || _selectable.interactable;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            if (_outline == null) _outline = GetComponent<Outline>();
            if (_outline != null) _outline.enabled = false;

            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null && (_hoverClip != null || _selectClip != null))
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
            }
        }

        private void OnDisable()
        {
            // 非表示化された時にアウトラインが出しっぱなしにならないようにする
            if (_outline != null) _outline.enabled = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsInteractable) return;
            if (_outline != null) _outline.enabled = true;
            PlayClip(_hoverClip);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_outline != null) _outline.enabled = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsInteractable) return;
            PlayClip(_selectClip);
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip == null || _audioSource == null) return;
            _audioSource.PlayOneShot(clip, _volume);
        }
    }
}
