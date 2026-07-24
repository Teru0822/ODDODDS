using System.Collections;
using UnityEngine;

namespace App.ATM
{
    /// <summary>
    /// ATMの物理キーパッドボタンにアタッチし、クリック時やキー入力時の沈み込み挙動を制御する。
    /// </summary>
    [DisallowMultipleComponent]
    public class ATMPhysicalButton : MonoBehaviour
    {
        [Header("沈み込み設定")]
        [Tooltip("沈み込むローカル方向")]
        [SerializeField] private Vector3 pressDirection = Vector3.back;

        [Tooltip("沈み込む量 (メートル)")]
        [SerializeField] private float pressDistance = 0.003f;

        [Tooltip("沈むのにかかる時間（秒）")]
        [SerializeField] private float pressDuration = 0.08f;

        [Tooltip("戻るのにかかる時間（秒）")]
        [SerializeField] private float releaseDuration = 0.12f;

        [Header("効果音")]
        [Tooltip("クリック音 (任意)")]
        [SerializeField] private AudioClip clickSound;

        private Vector3 _originalLocalPosition;
        private Coroutine _pressCoroutine;

        public AudioClip ClickSound => clickSound;

        private void Awake()
        {
            _originalLocalPosition = transform.localPosition;
        }

        /// <summary>
        /// ボタンの沈み込みアニメーションをトリガーします。
        /// </summary>
        /// <param name="audioSource">再生用のAudioSource（オプション）</param>
        public void Press(AudioSource audioSource = null)
        {
            if (audioSource != null && clickSound != null)
            {
                audioSource.PlayOneShot(clickSound);
            }

            if (_pressCoroutine != null)
            {
                StopCoroutine(_pressCoroutine);
                transform.localPosition = _originalLocalPosition; // 前の位置からスムーズにリセット
            }

            _pressCoroutine = StartCoroutine(PressCoroutine());
        }

        private IEnumerator PressCoroutine()
        {
            Vector3 targetPosition = _originalLocalPosition + pressDirection.normalized * pressDistance;
            
            // 1. 沈み込む (押し下げ)
            float elapsed = 0f;
            while (elapsed < pressDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / pressDuration);
                float rate = Mathf.Sin(t * Mathf.PI * 0.5f); // EaseOut
                transform.localPosition = Vector3.Lerp(_originalLocalPosition, targetPosition, rate);
                yield return null;
            }
            transform.localPosition = targetPosition;

            // 2. 元に戻る (リリース)
            elapsed = 0f;
            while (elapsed < releaseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / releaseDuration);
                float rate = Mathf.Sin(t * Mathf.PI * 0.5f); // EaseOut
                transform.localPosition = Vector3.Lerp(targetPosition, _originalLocalPosition, rate);
                yield return null;
            }
            transform.localPosition = _originalLocalPosition;
            _pressCoroutine = null;
        }
    }
}
