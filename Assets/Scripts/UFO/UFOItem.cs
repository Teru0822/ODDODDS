using UnityEngine;

public enum UFOItemType
{
    CopperCoin,
    SilverCoin,
    GoldCoin,
    Watch,
    RouletteItem,
    BlackDiamond
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

    [Header("衝突効果音の設定（個別AudioSource廃止のため現在は非アクティブ）")]
    [Tooltip("床や他のオブジェクトに衝突した際の効果音")]
    [SerializeField] private AudioClip hitSound;

    [Tooltip("効果音を鳴らす最小の衝突速度")]
    [SerializeField] private float minVelocityThreshold = 0.5f;

    [Tooltip("効果音の最大音量")]
    [Range(0f, 10f)]
    [SerializeField] private float soundVolume = 0.8f;

    [Tooltip("効果音のピッチ（高低）のランダム幅")]
    [SerializeField] private Vector2 pitchRange = new Vector2(0.85f, 1.15f);

    void Start()
    {
        // 負荷軽減のため、個別コインのAudioSourceの動的追加および取得処理は廃止されました
    }

    // 衝突時の個別再生処理（OnCollisionEnter）は、大量のコイン衝突による負荷を避けるため廃止されました
}
