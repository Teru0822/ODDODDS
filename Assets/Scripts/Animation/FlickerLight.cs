using UnityEngine;

/// <summary>
/// 切れかけの蛍光灯のようにホラーゲーム的にバチバチと点滅する Light コントローラ。
/// 状態マシン: Stable (通常点灯) / Burst (激しい不規則点滅) / Dead (消灯)。
/// Burst 中は SparkBurstVFX に火花を散らさせ、AudioClip でクラックル音を再生する。
/// </summary>
[DisallowMultipleComponent]
public class FlickerLight : MonoBehaviour
{
    [Header("対象")]
    [Tooltip("制御する Light。null なら自身/子から自動取得")]
    public Light targetLight;

    [Tooltip("通常点灯時の Intensity")]
    public float baseIntensity = 1.5f;

    [Header("状態時間 (秒、min/max)")]
    [Tooltip("Stable (通常点灯) を維持する時間範囲")]
    public Vector2 stableDuration = new Vector2(2f, 5f);

    [Tooltip("Burst (激しい点滅) を維持する時間範囲")]
    public Vector2 burstDuration = new Vector2(0.3f, 1.2f);

    [Tooltip("Dead (消灯) を維持する時間範囲")]
    public Vector2 deadDuration = new Vector2(0.4f, 2.0f);

    [Header("状態確率")]
    [Tooltip("Stable に遷移する確率 (Burst/Dead と合算で 1)")]
    [Range(0f, 1f)] public float stableProb = 0.6f;

    [Tooltip("Dead に遷移する確率")]
    [Range(0f, 1f)] public float deadProb = 0.15f;
    // burst = 1 - stable - dead

    [Header("Burst の点滅パラメータ")]
    [Tooltip("Burst 中、次の Intensity 更新までの間隔 (秒)")]
    public Vector2 burstFlickerInterval = new Vector2(0.02f, 0.12f);

    [Tooltip("Burst 中の Intensity 倍率範囲 (baseIntensity に対する倍率)。0 で完全消灯、>1 でブースト")]
    public Vector2 burstIntensityRange = new Vector2(0f, 2.5f);

    [Header("火花 VFX")]
    [Tooltip("Burst 中に火花を出す SparkBurstVFX。null なら自身/子から自動取得")]
    public SparkBurstVFX sparkVFX;

    [Tooltip("Burst 中、火花を散らす間隔 (秒)")]
    public Vector2 sparkInterval = new Vector2(0.05f, 0.25f);

    [Tooltip("1 回の火花で出す粒子数")]
    public Vector2Int sparksPerEmit = new Vector2Int(3, 8);

    [Header("クラックル音 (バチッ)")]
    [Tooltip("再生用 AudioSource。null なら自身に AddComponent")]
    public AudioSource audioSource;

    [Tooltip("Burst 開始時に再生する音 (複数ならランダム選択)")]
    public AudioClip[] crackleClips;

    [Tooltip("クラックル音ピッチ範囲")]
    public Vector2 cracklePitchRange = new Vector2(0.9f, 1.2f);

    [Tooltip("クラックル音ボリューム (1超でブースト可)")]
    [Range(0f, 5f)]
    public float crackleVolume = 1f;

    [Tooltip("AudioSource の空間ブレンド (0=2D, 1=3D)")]
    [Range(0f, 1f)]
    public float audioSpatialBlend = 1f;

    [Header("起動時")]
    [Tooltip("起動時にランダム位相で開始 (複数ライトを同期させないため)")]
    public bool randomStartState = true;

    private enum State { Stable, Burst, Dead }
    private State _state;
    private float _stateEndTime;
    private float _nextFlickerTime;
    private float _nextSparkTime;

    private void Awake()
    {
        if (targetLight == null) targetLight = GetComponentInChildren<Light>(true);
        if (sparkVFX == null) sparkVFX = GetComponentInChildren<SparkBurstVFX>(true);
        EnsureAudioSource();
        EnterState(randomStartState ? PickNextState() : State.Stable);
    }

    private void EnsureAudioSource()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        audioSource.spatialBlend = audioSpatialBlend;
    }

    private State PickNextState()
    {
        float r = Random.value;
        if (r < stableProb) return State.Stable;
        if (r < stableProb + Mathf.Max(0f, 1f - stableProb - deadProb)) return State.Burst;
        return State.Dead;
    }

    private void EnterState(State s)
    {
        _state = s;
        Vector2 dur = s switch
        {
            State.Stable => stableDuration,
            State.Burst => burstDuration,
            _ => deadDuration,
        };
        _stateEndTime = Time.time + Random.Range(dur.x, dur.y);
        _nextFlickerTime = 0f;
        _nextSparkTime = Time.time;

        if (s == State.Burst)
        {
            PlayCrackle();
        }
    }

    private void Update()
    {
        if (targetLight == null) return;

        switch (_state)
        {
            case State.Stable:
                targetLight.intensity = baseIntensity;
                break;

            case State.Burst:
                if (Time.time >= _nextFlickerTime)
                {
                    float mul = Random.Range(burstIntensityRange.x, burstIntensityRange.y);
                    targetLight.intensity = baseIntensity * mul;
                    _nextFlickerTime = Time.time + Random.Range(burstFlickerInterval.x, burstFlickerInterval.y);
                }
                if (sparkVFX != null && Time.time >= _nextSparkTime)
                {
                    int c = Random.Range(sparksPerEmit.x, sparksPerEmit.y + 1);
                    sparkVFX.Emit(c);
                    _nextSparkTime = Time.time + Random.Range(sparkInterval.x, sparkInterval.y);
                }
                break;

            case State.Dead:
                targetLight.intensity = 0f;
                break;
        }

        if (Time.time >= _stateEndTime)
        {
            EnterState(PickNextState());
        }
    }

    private void PlayCrackle()
    {
        if (audioSource == null || crackleClips == null || crackleClips.Length == 0) return;
        var clip = crackleClips[Random.Range(0, crackleClips.Length)];
        if (clip == null) return;
        audioSource.pitch = Random.Range(cracklePitchRange.x, cracklePitchRange.y);
        audioSource.PlayOneShot(clip, crackleVolume);
    }

    /// <summary>外部から強制的に状態を切り替える (デバッグ用)</summary>
    public void ForceBurst() => EnterState(State.Burst);
    public void ForceDead() => EnterState(State.Dead);
    public void ForceStable() => EnterState(State.Stable);
}
