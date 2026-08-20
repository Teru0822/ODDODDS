using UnityEngine;

/// <summary>
/// 床/壁/障害物等の「面」の音響プロファイル。木/金属/ガラス等の素材ごとに 1 つ作る。
///
/// ボール素材 (BallMaterialProfile.index) ごとに別の AudioClip を持てる構造：
///   rollLoops[ballIndex]   = そのボール素材で転がる時の音
///   impactClips[ballIndex] = そのボール素材で衝突する時の音
///
/// 配列長が足りない / 該当インデックスが null の場合は無音 (フォールバックなし)。
/// </summary>
[CreateAssetMenu(menuName = "OddOdds/Audio/Surface Profile", fileName = "Surface_New")]
public class SurfaceProfile : ScriptableObject
{
    [Tooltip("デバッグ識別用 (Wood / Metal / Glass 等)")]
    public string id;

    [Header("衝突音 (ボール素材ごと)")]
    [Tooltip("ボール素材インデックス順の衝突 AudioClip。null は無音。複数指定でランダム選択したい場合は impactClipsAlt を併用")]
    public AudioClip[] impactClips;

    [Tooltip("ボール素材ごとの追加衝突 AudioClip 候補。各セルに 1～N 個入れておくとランダムに選ばれる。null/空ならメインの impactClips のみ使用")]
    public ImpactVariants[] impactClipsAlt;

    [Range(0f, 1f)]
    [Tooltip("衝突音の基本音量 (実際は速度カーブで減衰)")]
    public float impactVolumeMax = 0.7f;

    [Range(0f, 0.5f)]
    [Tooltip("衝突音のピッチ揺らぎ ±幅")]
    public float impactPitchVariance = 0.05f;

    [Min(0f)]
    [Tooltip("この相対速度未満の衝突は鳴らさない (微小衝突カット)")]
    public float impactMinSpeed = 0.5f;

    [Tooltip("衝突音量を 法線方向相対速度 [m/s] から決めるカーブ (X=速度, Y=0~1)")]
    public AnimationCurve impactVolumeByNormalSpeed = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.5f, 0.1f),
        new Keyframe(5f, 1f));

    [Header("転がり音 (ボール素材ごとのループ)")]
    [Tooltip("ボール素材インデックス順の転がりループ AudioClip。null は無音")]
    public AudioClip[] rollLoops;

    [Range(0f, 1f)]
    [Tooltip("転がり音の最大音量")]
    public float rollVolumeMax = 0.4f;

    [Tooltip("転がり音量カーブ (X=ボール速度 m/s, Y=0~1)")]
    public AnimationCurve rollVolumeBySpeed = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.2f, 0f),
        new Keyframe(3f, 1f));

    [Tooltip("転がりピッチカーブ (X=ボール速度 m/s, Y=pitch)")]
    public AnimationCurve rollPitchBySpeed = new AnimationCurve(
        new Keyframe(0f, 0.85f),
        new Keyframe(3f, 1.15f),
        new Keyframe(8f, 1.3f));

    /// <summary>ボール素材インデックスから衝突 AudioClip を取得 (variants があればランダム)。</summary>
    public AudioClip GetImpactClip(int ballIndex)
    {
        if (impactClipsAlt != null && ballIndex >= 0 && ballIndex < impactClipsAlt.Length)
        {
            var v = impactClipsAlt[ballIndex];
            if (v != null && v.clips != null && v.clips.Length > 0)
            {
                var picked = v.clips[Random.Range(0, v.clips.Length)];
                if (picked != null) return picked;
            }
        }
        if (impactClips != null && ballIndex >= 0 && ballIndex < impactClips.Length)
            return impactClips[ballIndex];
        return null;
    }

    /// <summary>ボール素材インデックスから転がり AudioClip を取得。</summary>
    public AudioClip GetRollLoop(int ballIndex)
    {
        if (rollLoops != null && ballIndex >= 0 && ballIndex < rollLoops.Length)
            return rollLoops[ballIndex];
        return null;
    }

    [System.Serializable]
    public class ImpactVariants
    {
        [Tooltip("このボール素材でのランダム選択候補 (空ならメイン impactClips を使用)")]
        public AudioClip[] clips;
    }
}
