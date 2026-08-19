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
/// 自身の価値や種類を定義し、床や他のコインに衝突した際は CoinImpactSoundManager へ通知するだけの
/// 薄い転送役に徹する（判定・音量計算・鳴らしすぎ防止などのロジックは全てCoinImpactSoundManager側に
/// 一元化しており、コインごとに個別のAudioSourceやロジックは持たない）。
/// </summary>
public class UFOItem : MonoBehaviour
{
    [Tooltip("アイテムの種類")]
    public UFOItemType itemType;

    [Tooltip("このアイテムが落とし口に入った時に貰える基本金額")]
    public float baseValue = 100f;

    private void OnCollisionEnter(Collision collision)
    {
        CoinImpactSoundManager.Instance?.NotifyImpact(collision, this);
    }
}
