using System;
using UnityEngine;

/// <summary>
/// UFOキャッチャー(ItemSpawner)のアイテム排出設定を、1アイテム1アセットで管理するためのデータ。
/// ItemSpawner.ItemSpawnSettings と同じ項目を持ち、ToRuntimeSettings() でランタイム用のインスタンスに変換される。
/// </summary>
[Serializable]
[CreateAssetMenu(menuName = "Data/ItemSpawnData")]
public class ItemSpawnData : ScriptableObject
{
    [Tooltip("識別用ID（将来的な解放システム等との連携用。未使用でも問題ありません）")]
    public int id;

    public string itemName;
    public GameObject prefab;

    [Header("排出設定")]
    [Tooltip("アクティブ枠の入れ替え判定に使う優先度")]
    public int priority;
    [Tooltip("排出確率（比率）")]
    public float rate;

    [Header("獲得時の変換設定")]
    [Tooltip("落とし口に入ったときに獲得できる金貨の枚数")]
    public int goldConvertCount = 0;
    [Tooltip("落とし口に入ったときに獲得できる銀貨の枚数")]
    public int silverConvertCount = 0;
    [Tooltip("落とし口に入ったときに獲得できる銅貨の枚数")]
    public int bronzeConvertCount = 0;

    /// <summary>ItemSpawner が実際に使うランタイム用の ItemSpawnSettings（ミュータブルな複製）に変換する</summary>
    public ItemSpawnSettings ToRuntimeSettings()
    {
        return new ItemSpawnSettings(itemName, prefab, priority, rate, goldConvertCount, silverConvertCount, bronzeConvertCount);
    }
}
