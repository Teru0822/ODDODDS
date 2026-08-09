using UnityEngine;

namespace App.Player
{
    /// <summary>
    /// プレイヤーが歩いている間、一定間隔で足音をランダムに再生する。
    ///
    /// プレイヤー本体（CharacterController が付いているオブジェクト）にアタッチして、
    /// Clips に足音を並べるだけで動く。AudioSource は自動生成される。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerFootsteps : MonoBehaviour
    {
        [Header("足音")]
        [Tooltip("再生する足音。複数入れると毎回ランダムに選ばれます")]
        [SerializeField] private AudioClip[] _clips;

        [Tooltip("同じ音が2回続かないようにする")]
        [SerializeField] private bool _avoidSameClipTwice = true;

        [Header("間隔")]
        [Tooltip("足音を鳴らす間隔(秒)。小さいほど早足に聞こえます")]
        [Min(0.05f)]
        [SerializeField] private float _interval = 0.5f;

        [Tooltip("歩き出した瞬間に1歩目を鳴らす。オフだと Interval 経過後から鳴り始めます")]
        [SerializeField] private bool _playFirstStepImmediately = true;

        [Header("音量・ピッチ")]
        [Tooltip("音量の範囲。毎回この範囲でランダムに決まります")]
        [SerializeField] private Vector2 _volumeRange = new Vector2(0.7f, 0.9f);

        [Tooltip("ピッチの範囲。少し散らすと同じ音の繰り返しに聞こえにくくなります")]
        [SerializeField] private Vector2 _pitchRange = new Vector2(0.95f, 1.05f);

        [Range(0f, 1f)]
        [Tooltip("0=どこでも同じ音量(2D)。自分の足音なので通常は0のままで構いません")]
        [SerializeField] private float _spatialBlend = 0f;

        [Header("歩行判定")]
        [Tooltip("この速さ以上で移動していれば「歩いている」とみなす(m/s)")]
        [SerializeField] private float _moveThreshold = 0.6f;

        [Tooltip("接地している時だけ鳴らす。" +
                 "このプロジェクトでは地形によって接地判定が false のままになることがあるため既定はオフ")]
        [SerializeField] private bool _requireGrounded = false;

        private CharacterController _characterController;
        private FirstPersonController _fpController;
        private AudioSource _audioSource;
        private float _timer;
        private int _lastClipIndex = -1;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _fpController = GetComponent<FirstPersonController>();

            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = _spatialBlend;

            ResetTimer();
        }

        private void Update()
        {
            if (!IsWalking())
            {
                // 止まったらタイマーを戻し、歩き出した時にすぐ1歩目が出るようにする
                ResetTimer();
                return;
            }

            _timer += Time.deltaTime;
            if (_timer < _interval) return;

            PlayStep();
            _timer = 0f;
        }

        /// <summary>歩いていると判定できるか。</summary>
        private bool IsWalking()
        {
            // ATMやUFOキャッチャーの操作中はコントローラが無効化される。
            // その間 CharacterController.velocity は最後の値が残るため、ここで弾く
            if (_fpController != null && !_fpController.enabled) return false;
            if (_requireGrounded && !_characterController.isGrounded) return false;

            Vector3 horizontal = _characterController.velocity;
            horizontal.y = 0f;
            return horizontal.magnitude >= _moveThreshold;
        }

        private void PlayStep()
        {
            AudioClip clip = PickClip();
            if (clip == null) return;

            _audioSource.pitch = Random.Range(
                Mathf.Min(_pitchRange.x, _pitchRange.y),
                Mathf.Max(_pitchRange.x, _pitchRange.y));

            float volume = Random.Range(
                Mathf.Min(_volumeRange.x, _volumeRange.y),
                Mathf.Max(_volumeRange.x, _volumeRange.y));

            _audioSource.PlayOneShot(clip, volume);
        }

        /// <summary>直前と同じ音を避けつつ、ランダムに1つ選ぶ。</summary>
        private AudioClip PickClip()
        {
            if (_clips == null || _clips.Length == 0) return null;

            // null 要素があっても選べるように、有効なものだけを対象にする
            int valid = 0;
            foreach (var c in _clips) if (c != null) valid++;
            if (valid == 0) return null;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                int index = Random.Range(0, _clips.Length);
                if (_clips[index] == null) continue;
                if (_avoidSameClipTwice && valid > 1 && index == _lastClipIndex) continue;

                _lastClipIndex = index;
                return _clips[index];
            }

            // 抽選に失敗した場合は最初の有効なクリップを返す
            foreach (var c in _clips) if (c != null) return c;
            return null;
        }

        private void ResetTimer()
        {
            _timer = _playFirstStepImmediately ? _interval : 0f;
        }
    }
}
