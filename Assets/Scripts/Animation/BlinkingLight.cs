using UnityEngine;

/// <summary>
/// 一定周期で Light の Intensity を On/Off 値で点滅させる汎用スクリプト。
/// smooth=true なら境界をフェードで補間 (柔らかいパルス)、false ならパキッと矩形波。
/// </summary>
[DisallowMultipleComponent]
public class BlinkingLight : MonoBehaviour
{
    [Header("対象")]
    [Tooltip("制御する Light。null なら自身/子から自動取得")]
    public Light targetLight;

    [Header("点滅パターン")]
    [Tooltip("On 状態の長さ (秒)")]
    public float onDuration = 0.5f;

    [Tooltip("Off 状態の長さ (秒)")]
    public float offDuration = 0.5f;

    [Tooltip("On 時の Intensity")]
    public float onIntensity = 1.5f;

    [Tooltip("Off 時の Intensity (0 で完全消灯)")]
    public float offIntensity = 0f;

    [Header("補間")]
    [Tooltip("On/Off の境界を滑らかに補間する (柔らかいパルス)")]
    public bool smooth = false;

    [Tooltip("smooth=true 時、On に立ち上がる時間 (秒)。onDuration の一部")]
    public float fadeInDuration = 0.08f;

    [Tooltip("smooth=true 時、Off に落ちる時間 (秒)。offDuration の一部")]
    public float fadeOutDuration = 0.08f;

    [Header("位相")]
    [Tooltip("起動時の位相オフセット (秒)。同種ライトを微妙にずらすのに使う")]
    public float phaseOffset = 0f;

    [Tooltip("起動時にランダム位相をかける (同種ライトを完全にバラす)")]
    public bool randomPhase = false;

    private float _runtimePhase;

    private void Awake()
    {
        if (targetLight == null) targetLight = GetComponentInChildren<Light>(true);
        _runtimePhase = randomPhase ? Random.Range(0f, Mathf.Max(0.001f, onDuration + offDuration)) : phaseOffset;
    }

    private void Update()
    {
        if (targetLight == null) return;
        float period = onDuration + offDuration;
        if (period <= 0f) return;

        float t = (Time.time + _runtimePhase) % period;
        float intensity;
        if (!smooth)
        {
            intensity = (t < onDuration) ? onIntensity : offIntensity;
        }
        else
        {
            if (t < fadeInDuration)
            {
                // Off → On
                float u = t / Mathf.Max(0.0001f, fadeInDuration);
                intensity = Mathf.Lerp(offIntensity, onIntensity, u);
            }
            else if (t < onDuration)
            {
                intensity = onIntensity;
            }
            else if (t < onDuration + fadeOutDuration)
            {
                // On → Off
                float u = (t - onDuration) / Mathf.Max(0.0001f, fadeOutDuration);
                intensity = Mathf.Lerp(onIntensity, offIntensity, u);
            }
            else
            {
                intensity = offIntensity;
            }
        }
        targetLight.intensity = intensity;
    }
}
