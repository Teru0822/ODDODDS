using UnityEngine;

/// <summary>
/// AudioSource を持つオブジェクト（BGM 音源など）にアタッチして使う。
/// プレイヤー（AudioListener）の「視点の向き」から見た音源の方向を計算し、
/// 音源が左にあれば左、右にあれば右から聞こえるように Stereo Pan を動的に設定する。
///
/// Unity の 3D 空間オーディオ（Spatial Blend = 3D）と違い、こちらは BGM のような
/// 2D 音源のまま「左右の定位」だけを付けられる（距離で音量が消えない）。距離減衰が
/// 欲しい場合は useDistanceVolume をオンにする。
///
/// 仕組み:
///   - listener.right（プレイヤーが向いている方向の右）への投影で左右を判定。
///     → プレイヤーが振り向くと、同じ音源でも聞こえる左右が入れ替わる。
///   - panStereo は -1(左) ～ +1(右)。前後（正面/真後ろ）はどちらも中央寄りになる。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SurroundBGMAudioSource : MonoBehaviour
{
    [Header("リスナー")]
    [Tooltip("プレイヤー視点。空なら AudioListener → MainCamera の順で自動取得する")]
    public Transform listener;

    [Header("左右パン")]
    [Tooltip("起動時に Spatial Blend を 2D(0) に強制する。panStereo は 2D 音源にしか効かないため、" +
             "これがオフ＆Spatial Blend が 3D 側だと音が定位しない（スライダーは動くが無音）。通常オン推奨")]
    public bool force2DForPanning = true;

    [Range(0f, 1f)]
    [Tooltip("左右の振りの強さ。0=振らない（中央固定）、1=最大")]
    public float panIntensity = 1f;

    [Min(0f)]
    [Tooltip("パンの追従を滑らかにする時間（秒）。0=即時。大きいほどゆっくり追従")]
    public float panSmoothTime = 0.08f;

    [Tooltip("上下方向は無視して水平面だけで左右を判定する（通常オン）")]
    public bool horizontalOnly = true;

    [Header("距離による音量")]
    [Tooltip("音源とプレイヤーの距離で音量を上下させる")]
    public bool useDistanceVolume = true;

    [Min(0f)]
    [Tooltip("この距離以内は最大音量")]
    public float fullVolumeDistance = 3f;

    [Min(0f)]
    [Tooltip("この距離以遠は無音")]
    public float silentDistance = 25f;

    [Range(0f, 1f)]
    [Tooltip("最大音量（近いときの音量）")]
    public float baseVolume = 1f;

    [Min(0f)]
    [Tooltip("音量変化を滑らかにする時間（秒）。0=即時。移動時の急な音量変化を防ぐ")]
    public float volumeSmoothTime = 0.15f;

    private AudioSource _source;
    private float _panVelocity;
    private float _volVelocity;

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        // panStereo は 2D 音源にしか効かない。3D 側だとスライダーは動くが無音になるため強制的に 2D にする。
        if (force2DForPanning) _source.spatialBlend = 0f;
        if (listener == null) ResolveListener();
    }

    private void ResolveListener()
    {
        var al = FindAnyObjectByType<AudioListener>();
        if (al != null) { listener = al.transform; return; }
        if (Camera.main != null) listener = Camera.main.transform;
    }

    // カメラ追従より後に計算したいので LateUpdate
    private void LateUpdate()
    {
        if (listener == null)
        {
            ResolveListener();
            if (listener == null) return;
        }

        Vector3 toSource = transform.position - listener.position;
        if (horizontalOnly) toSource = Vector3.ProjectOnPlane(toSource, Vector3.up);

        float targetPan = 0f;
        if (toSource.sqrMagnitude > 1e-6f)
        {
            Vector3 dir = toSource.normalized;
            // リスナー（視点）の右方向への成分: +で右, -で左
            float rightDot = Vector3.Dot(dir, listener.right);
            targetPan = Mathf.Clamp(rightDot * panIntensity, -1f, 1f);
        }

        _source.panStereo = (panSmoothTime > 0f)
            ? Mathf.SmoothDamp(_source.panStereo, targetPan, ref _panVelocity, panSmoothTime)
            : targetPan;

        if (useDistanceVolume)
        {
            float d = (transform.position - listener.position).magnitude;
            // near=1, far=0（silentDistance > fullVolumeDistance 前提）
            float t = Mathf.Clamp01(Mathf.InverseLerp(silentDistance, fullVolumeDistance, d));
            float targetVol = baseVolume * t;
            _source.volume = (volumeSmoothTime > 0f)
                ? Mathf.SmoothDamp(_source.volume, targetVol, ref _volVelocity, volumeSmoothTime)
                : targetVol;
        }
    }
}
