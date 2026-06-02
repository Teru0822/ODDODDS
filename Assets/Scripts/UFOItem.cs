using UnityEngine;

public enum UFOItemType
{
    CopperCoin,
    SilverCoin,
    GoldCoin,
    Watch
}

/// <summary>
/// 各アイテムのプレハブ（銅・銀・金・時計）にアタッチするクラス。
/// 自身の価値や種類を定義し、床や壁に衝突した際に効果音を鳴らします。
/// </summary>
public class UFOItem : MonoBehaviour
{
    [Tooltip("アイテムの種類")]
    public UFOItemType itemType;

    [Tooltip("このアイテムが落とし口に入った時に貰える基本金額")]
    public float baseValue = 100f;

    [Header("衝突効果音の設定")]
    [Tooltip("床や他のオブジェクトに衝突した際の効果音")]
    [SerializeField] private AudioClip hitSound;

    [Tooltip("効果音を鳴らす最小の衝突速度（小さすぎる擦れ音などを防ぎます）")]
    [SerializeField] private float minVelocityThreshold = 0.5f;

    [Tooltip("効果音の最大音量 (1.0より大きい値で音量増幅可能)")]
    [Range(0f, 10f)]
    [SerializeField] private float soundVolume = 0.8f;

    [Tooltip("効果音のピッチ（高低）のランダム幅（じゃらじゃら感を出すために使用）")]
    [SerializeField] private Vector2 pitchRange = new Vector2(0.85f, 1.15f);

    private AudioSource _audioSource;

    void Start()
    {
        // 自身にアタッチされているAudioSourceを取得（ピッチ調整など高度な再生に必要）
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D音響にして距離減衰を無視する
        }
    }

    /// <summary>
    /// 物理衝突検知
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (hitSound == null) return;

        // 衝突した相対速度の大きさを取得
        float impactVelocity = collision.relativeVelocity.magnitude;

        // 一定以上の強さで衝突した場合のみ音を鳴らす
        if (impactVelocity > minVelocityThreshold)
        {
            PlayCollisionSound(impactVelocity);
        }
    }

    /// <summary>
    /// 衝突速度とランダムピッチを加味して効果音を再生する
    /// </summary>
    private void PlayCollisionSound(float velocity)
    {
        // 衝突の強さに比例して音量を変化させる（ただし最大設定音量でクランプ）
        // 速度が10以上のときに最大音量になります
        float volume = Mathf.Min(velocity * 0.1f * soundVolume, soundVolume);

        // ピッチ（音の高さ）を少しだけランダムに変えることで、金属特有の「じゃらじゃら」感を表現
        float randomPitch = Random.Range(pitchRange.x, pitchRange.y);

        if (_audioSource != null)
        {
            _audioSource.pitch = randomPitch;
            _audioSource.PlayOneShot(hitSound, volume);
        }
        else
        {
            // AudioSourceがない場合の最終フォールバック（3D音響としてワールド座標で再生）
            AudioSource.PlayClipAtPoint(hitSound, transform.position, volume);
        }
    }
}
