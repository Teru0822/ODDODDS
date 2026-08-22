using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace App.Audio
{
    /// <summary>
    /// BGM / SE / ボイスの音量を、シーン上の AudioSource すべてに反映させる。
    ///
    /// AudioMixer を使わずに済ませているのは、既存の AudioSource が
    /// 多数のシーン・プレハブに散らばっており、全部を Mixer Group へ繋ぎ直すのが現実的でないため。
    /// 代わりに各 AudioSource の「元の音量」を覚えておき、
    ///   実際の音量 = 元の音量 × その分類のつまみ × マスター
    /// として毎回計算し直す。元の音量を保持しているので、何度反映しても音が痩せていかない。
    ///
    /// 【どの音がどの分類かを決める順番】
    ///   1. AudioSource と同じ GameObject の AudioCategoryTag
    ///   2. 親をたどって見つかった AudioCategoryTag（applyToChildren が有効なもの）
    ///   3. 下の 名前による振り分け
    ///   4. 既定の分類
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioVolumeController : MonoBehaviour
    {
        public static AudioVolumeController Instance { get; private set; }

        [Header("音量 (0〜1)")]
        [Range(0f, 1f)] [SerializeField] private float _master = 1f;
        [Range(0f, 1f)] [SerializeField] private float _bgm = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float _se = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float _voice = 0.5f;

        [Header("分類の指定")]
        [Tooltip("ここに入れた AudioSource は BGM として扱う。AudioCategoryTag より優先される")]
        [SerializeField] private List<AudioSource> _bgmSources = new List<AudioSource>();

        [Tooltip("ここに入れた AudioSource は SE として扱う")]
        [SerializeField] private List<AudioSource> _seSources = new List<AudioSource>();

        [Tooltip("ここに入れた AudioSource はボイスとして扱う")]
        [SerializeField] private List<AudioSource> _voiceSources = new List<AudioSource>();

        [Header("名前による振り分け")]
        [Tooltip("GameObject 名にこの文字列を含むものを BGM とみなす（大文字小文字は区別しない）")]
        [SerializeField] private string[] _bgmNameKeywords = { "BGM", "Music", "Ambient" };

        [Tooltip("ボイスとみなす名前のキーワード")]
        [SerializeField] private string[] _voiceNameKeywords = { "Voice", "Serifu", "Dialogue" };

        [Tooltip("どれにも当てはまらない AudioSource の分類")]
        [SerializeField] private AudioCategory _defaultCategory = AudioCategory.Se;

        [Header("追従")]
        [Tooltip("新しく現れた AudioSource を拾い直す間隔(秒)。0 で自動再スキャンなし")]
        [SerializeField] private float _rescanInterval = 2f;

        [Tooltip("シーンが読み込まれた時に拾い直す")]
        [SerializeField] private bool _rescanOnSceneLoaded = true;

        [Header("デバッグ")]
        [SerializeField] private bool _logOnScan = false;

        /// <summary>AudioSource ごとの「設定を掛ける前の音量」。</summary>
        private readonly Dictionary<AudioSource, float> _baseVolumes = new Dictionary<AudioSource, float>();

        /// <summary>反映後の音量。外から音量を書き換えられた時に気付くために覚えておく。</summary>
        private readonly Dictionary<AudioSource, float> _appliedVolumes = new Dictionary<AudioSource, float>();

        private float _rescanTimer;

        public float Master { get => _master; set { _master = Mathf.Clamp01(value); ApplyAll(); } }
        public float Bgm   { get => _bgm;   set { _bgm   = Mathf.Clamp01(value); ApplyAll(); } }
        public float Se    { get => _se;    set { _se    = Mathf.Clamp01(value); ApplyAll(); } }
        public float Voice { get => _voice; set { _voice = Mathf.Clamp01(value); ApplyAll(); } }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            if (_rescanOnSceneLoaded) SceneManager.sceneLoaded += HandleSceneLoaded;
            Rescan();
        }

        private void OnDisable()
        {
            if (_rescanOnSceneLoaded) SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Rescan();

        private void Update()
        {
            if (_rescanInterval <= 0f) return;

            _rescanTimer += Time.unscaledDeltaTime;
            if (_rescanTimer < _rescanInterval) return;
            _rescanTimer = 0f;
            Rescan();
        }

        /// <summary>シーン上の AudioSource を集め直して音量を反映する。</summary>
        [ContextMenu("AudioSource を拾い直す")]
        public void Rescan()
        {
            var sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // 消えたものを片付ける
            var gone = new List<AudioSource>();
            foreach (var pair in _baseVolumes)
            {
                if (pair.Key == null) gone.Add(pair.Key);
            }
            foreach (var g in gone)
            {
                _baseVolumes.Remove(g);
                _appliedVolumes.Remove(g);
            }

            int added = 0;
            foreach (var source in sources)
            {
                if (source == null) continue;
                if (!_baseVolumes.ContainsKey(source))
                {
                    _baseVolumes[source] = source.volume;
                    added++;
                }
            }

            ApplyAll();

            if (_logOnScan && added > 0)
                Debug.Log($"[AudioVolumeController] AudioSource を {added} 件追加（合計 {_baseVolumes.Count} 件）", this);
        }

        /// <summary>覚えている全 AudioSource へ音量を反映する。</summary>
        public void ApplyAll()
        {
            foreach (var pair in _baseVolumes)
            {
                var source = pair.Key;
                if (source == null) continue;

                float baseVolume = pair.Value;

                // 前回こちらが書いた値と違うなら、他のスクリプトが音量を変えたということ。
                // その値を新しい「元の音量」として扱い、設定と喧嘩しないようにする
                if (_appliedVolumes.TryGetValue(source, out float applied)
                    && !Mathf.Approximately(source.volume, applied))
                {
                    float scale = CategoryScale(Classify(source));
                    baseVolume = scale > 0.0001f ? source.volume / scale : source.volume;
                    _baseVolumes[source] = baseVolume;
                }

                float volume = baseVolume * CategoryScale(Classify(source));
                source.volume = volume;
                _appliedVolumes[source] = volume;
            }
        }

        /// <summary>指定した分類の音量を設定する。</summary>
        public void SetVolume(AudioCategory category, float value)
        {
            switch (category)
            {
                case AudioCategory.Bgm:   Bgm = value; break;
                case AudioCategory.Se:    Se = value; break;
                case AudioCategory.Voice: Voice = value; break;
            }
        }

        /// <summary>その分類に掛ける倍率。</summary>
        public float CategoryScale(AudioCategory category)
        {
            switch (category)
            {
                case AudioCategory.Bgm:   return _bgm * _master;
                case AudioCategory.Se:    return _se * _master;
                case AudioCategory.Voice: return _voice * _master;
                default:                  return 1f;   // Unmanaged は設定の影響を受けない
            }
        }

        /// <summary>この AudioSource がどの分類か決める。</summary>
        public AudioCategory Classify(AudioSource source)
        {
            if (source == null) return _defaultCategory;

            // 1. Inspector で明示的に指定されたもの
            if (_bgmSources.Contains(source)) return AudioCategory.Bgm;
            if (_seSources.Contains(source)) return AudioCategory.Se;
            if (_voiceSources.Contains(source)) return AudioCategory.Voice;

            // 2. 自分に付いたタグ
            var own = source.GetComponent<AudioCategoryTag>();
            if (own != null) return own.category;

            // 3. 親のタグ（配下に適用する設定のもの）
            var parentTag = source.GetComponentInParent<AudioCategoryTag>(true);
            if (parentTag != null && parentTag.applyToChildren) return parentTag.category;

            // 4. 名前で振り分け
            string name = source.gameObject.name;
            if (ContainsAny(name, _bgmNameKeywords)) return AudioCategory.Bgm;
            if (ContainsAny(name, _voiceNameKeywords)) return AudioCategory.Voice;

            return _defaultCategory;
        }

        private static bool ContainsAny(string name, string[] keywords)
        {
            if (keywords == null) return false;
            foreach (var keyword in keywords)
            {
                if (string.IsNullOrEmpty(keyword)) continue;
                if (name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) ApplyAll();
        }
#endif
    }
}
