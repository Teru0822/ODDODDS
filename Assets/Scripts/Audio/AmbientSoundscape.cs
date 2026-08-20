using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BGMの代わりに、多数の効果音をそれぞれ別々の間隔で鳴らして環境音を組み立てるコンポーネント。
///
/// 1つのレイヤー = 1種類の音（時計、蛍光灯、水滴、床鳴り…）で、
/// 音量・間隔・ピッチ・定位をレイヤーごとに設定できる。
/// 「ずっと鳴らし続ける音（蛍光灯のジー音など）」はループに、
/// 「たまに鳴る音（水滴、軋み、遠くの物音）」はランダム間隔にする。
///
/// 使い方: シーンの空オブジェクトに付けて Layers を組み立てるだけ。
/// AudioSource はレイヤーごとに実行時へ自動生成される。
/// </summary>
[DisallowMultipleComponent]
public class AmbientSoundscape : MonoBehaviour
{
    /// <summary>環境音1種類ぶんの設定。</summary>
    [Serializable]
    public class Layer
    {
        [Tooltip("Inspector上の見出し。動作には影響しません")]
        public string label = "SE";

        [Tooltip("オフにするとこのレイヤーだけ鳴らなくなる。実行中に切り替えても反映されます")]
        public bool enabled = true;

        [Tooltip("鳴らすクリップ。複数入れると毎回ランダムに選ばれるので、単調さが減ります")]
        public AudioClip[] clips;

        [Header("鳴らし方")]
        [Tooltip("オンにすると鳴らしっぱなしにする（蛍光灯のジー音など）。オフならランダム間隔で単発再生")]
        public bool loop = false;

        [Tooltip("単発再生の間隔(秒)。この範囲でランダムに待ってから次を鳴らします")]
        public Vector2 intervalRange = new Vector2(8f, 25f);

        [Tooltip("再生開始までの初期待ち(秒)。レイヤーごとにずらすと、開始直後に音が重なりません")]
        public Vector2 startDelayRange = new Vector2(0f, 5f);

        [Header("音量・ピッチ")]
        [Tooltip("音量の範囲。単発は毎回この範囲でランダム。ループの場合は Min の値が使われます")]
        public Vector2 volumeRange = new Vector2(0.4f, 0.7f);

        [Tooltip("ピッチの範囲。少し散らすと同じ音の繰り返しに聞こえにくくなります")]
        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

        [Header("定位")]
        [Range(0f, 1f)]
        [Tooltip("0=どこでも同じ音量(2D) / 1=位置による立体音(3D)")]
        public float spatialBlend = 0f;

        [Tooltip("3Dで鳴らす時の発生源。未指定ならこのコンポーネントの位置から鳴ります")]
        public Transform anchor;

        [Tooltip("3D時、この距離までは減衰しない")]
        public float minDistance = 1f;

        [Tooltip("3D時、この距離で聞こえなくなる")]
        public float maxDistance = 25f;

        // --- 実行時 ---
        [NonSerialized] public AudioSource source;
        [NonSerialized] public Coroutine routine;
    }

    [Header("全体")]
    [Range(0f, 1f)]
    [Tooltip("全レイヤーに掛かる音量。実行中に変えても即反映されます")]
    [SerializeField] private float _masterVolume = 1f;

    [Tooltip("オフにすると環境音全体が止まります")]
    [SerializeField] private bool _play = true;

    [Header("再生開始条件")]
    [Tooltip("コインの生成と落下が終わってから鳴らし始める。ロード直後の重い時間帯を避けられます")]
    [SerializeField] private bool _waitForItemsToSettle = true;

    [Tooltip("生成が終わってから鳴らし始めるまでの追加待ち(秒)。コインが床で落ち着くのを待ちます")]
    [SerializeField] private float _settleDelay = 3f;

    [Tooltip("生成が始まるのを待つ最大秒数。時間内に始まらなければ見切って再生します")]
    [SerializeField] private float _spawnStartTimeout = 8f;

    [Header("環境音レイヤー")]
    [Tooltip("1要素 = 1種類の音。音量・間隔・定位を個別に設定します")]
    [SerializeField] private List<Layer> _layers = new List<Layer>();

    [Header("デバッグ")]
    [Tooltip("どのレイヤーがいつ鳴ったかをConsoleに出力します")]
    [SerializeField] private bool _logEvents = false;

    /// <summary>全体音量。設定UIなどから変更できます。</summary>
    public float MasterVolume
    {
        get => _masterVolume;
        set => _masterVolume = Mathf.Clamp01(value);
    }

    private void Reset()
    {
        _layers = new List<Layer> { new Layer() };
    }

    /// <summary>
    /// Inspector の「+」でリスト要素を追加すると、Unity は C# のフィールド初期化子を使わず
    /// 全フィールドを 0 で埋める。そのままだと enabled=false・音量0・ピッチ0 になり、
    /// 音を割り当てても鳴らないため、明らかに未設定のレイヤーだけ既定値へ直す。
    /// </summary>
    private void OnValidate()
    {
        if (_layers == null) return;

        foreach (var layer in _layers)
        {
            if (layer == null) continue;

            bool looksUninitialized = layer.intervalRange == Vector2.zero
                                   && layer.volumeRange == Vector2.zero
                                   && layer.pitchRange == Vector2.zero;

            if (looksUninitialized)
            {
                layer.enabled = true;
                layer.intervalRange = new Vector2(8f, 25f);
                layer.startDelayRange = new Vector2(0f, 5f);
                layer.volumeRange = new Vector2(0.4f, 0.7f);
                layer.pitchRange = new Vector2(0.95f, 1.05f);
                layer.minDistance = 1f;
                layer.maxDistance = 25f;
                if (string.IsNullOrEmpty(layer.label)) layer.label = "SE";
            }

            // ピッチ0は完全な無音になるので保険をかける
            if (layer.pitchRange == Vector2.zero) layer.pitchRange = Vector2.one;
            if (layer.maxDistance <= 0f) layer.maxDistance = 25f;
        }
    }

    // コインが落ち着くまでは鳴らさない。Update のループ再生もこのフラグで抑える
    private bool _ready;

    private IEnumerator Start()
    {
        // AudioSource だけ先に用意しておく（この時点では鳴らさない）
        BuildSources();

        if (_waitForItemsToSettle) yield return WaitUntilItemsSettled();

        _ready = true;
        StartAllLayers();

        if (_logEvents) Debug.Log("[AmbientSoundscape] 環境音の再生を開始します", this);
    }

    /// <summary>
    /// コインの生成が終わり、落下が落ち着くまで待つ。
    /// 生成中は物理演算で負荷が高く、環境音を重ねても埋もれてしまうため。
    /// </summary>
    private IEnumerator WaitUntilItemsSettled()
    {
        // UFOキャッチャーはサブシーン側にあるので、加法ロードの完了を待つ
        while (MultiSceneLoader.IsLoadingSubScenes) yield return null;

        // Awake/Start が走る猶予を与えてから ItemSpawner の有無を判断する
        yield return null;

        if (ItemSpawner.Instance != null)
        {
            // 生成が始まるのを待つ。始まらないまま時間切れになっても先へ進む
            float limit = Time.unscaledTime + Mathf.Max(0f, _spawnStartTimeout);
            while (!ItemSpawner.IsSpawning && Time.unscaledTime < limit) yield return null;

            // 生成が終わるまで待つ
            while (ItemSpawner.IsSpawning) yield return null;
        }

        // 生成直後はまだ空中にあるので、床で落ち着くまで待つ
        if (_settleDelay > 0f) yield return new WaitForSeconds(_settleDelay);
    }

    private void Update()
    {
        if (!_ready) return;

        // 実行中のInspector操作（マスター音量・レイヤーのオンオフ）を反映する
        foreach (var layer in _layers)
        {
            if (layer?.source == null) continue;

            bool shouldPlay = _play && layer.enabled && HasClip(layer);

            if (layer.loop)
            {
                layer.source.volume = layer.volumeRange.x * _masterVolume;

                if (shouldPlay && !layer.source.isPlaying) layer.source.Play();
                else if (!shouldPlay && layer.source.isPlaying) layer.source.Pause();
            }
            else if (!shouldPlay && layer.source.isPlaying)
            {
                layer.source.Stop();
            }
        }
    }

    /// <summary>レイヤーごとに AudioSource を作る。</summary>
    private void BuildSources()
    {
        foreach (var layer in _layers)
        {
            if (layer == null) continue;

            var go = new GameObject($"Ambient_{layer.label}");
            go.transform.SetParent(layer.anchor != null ? layer.anchor : transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = layer.loop;
            source.spatialBlend = layer.spatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = layer.minDistance;
            source.maxDistance = layer.maxDistance;

            layer.source = source;

            if (layer.loop && HasClip(layer))
            {
                // ループは開始時にクリップを決め打ちしておく
                source.clip = PickClip(layer);
                source.volume = layer.volumeRange.x * _masterVolume;
                source.pitch = UnityEngine.Random.Range(layer.pitchRange.x, layer.pitchRange.y);
            }
        }
    }

    private void StartAllLayers()
    {
        foreach (var layer in _layers)
        {
            if (layer == null || layer.loop) continue;   // ループは Update 側で面倒を見る
            layer.routine = StartCoroutine(PlayLayerRoutine(layer));
        }
    }

    /// <summary>単発レイヤーを、ランダム間隔で鳴らし続ける。</summary>
    private IEnumerator PlayLayerRoutine(Layer layer)
    {
        // レイヤーごとに開始をずらして、頭で音が団子にならないようにする
        yield return new WaitForSeconds(RandomInRange(layer.startDelayRange));

        while (true)
        {
            if (_play && layer.enabled && HasClip(layer) && layer.source != null)
            {
                AudioClip clip = PickClip(layer);
                // ピッチ0だと無音のまま止まってしまうので下限を設ける
                layer.source.pitch = Mathf.Max(0.01f, RandomInRange(layer.pitchRange));
                layer.source.PlayOneShot(clip, RandomInRange(layer.volumeRange) * _masterVolume);

                if (_logEvents) Debug.Log($"[AmbientSoundscape] {layer.label}: {clip.name}", this);
            }

            // 間隔は毎回引き直す。Inspectorで範囲を変えれば次の待ちから反映される
            yield return new WaitForSeconds(Mathf.Max(0.05f, RandomInRange(layer.intervalRange)));
        }
    }

    private static bool HasClip(Layer layer)
    {
        if (layer.clips == null) return false;
        foreach (var c in layer.clips) if (c != null) return true;
        return false;
    }

    private static AudioClip PickClip(Layer layer)
    {
        // null を除いた中からランダムに選ぶ
        int count = 0;
        foreach (var c in layer.clips) if (c != null) count++;

        int target = UnityEngine.Random.Range(0, count);
        foreach (var c in layer.clips)
        {
            if (c == null) continue;
            if (target-- == 0) return c;
        }
        return null;
    }

    private static float RandomInRange(Vector2 range)
    {
        return UnityEngine.Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }

    /// <summary>ラベルを指定してレイヤーのオン/オフを切り替える。</summary>
    public void SetLayerEnabled(string label, bool enabled)
    {
        foreach (var layer in _layers)
        {
            if (layer != null && layer.label == label) layer.enabled = enabled;
        }
    }

    /// <summary>環境音全体の再生/停止。</summary>
    public void SetPlaying(bool play) => _play = play;
}
