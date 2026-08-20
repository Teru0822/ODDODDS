using System;
using UnityEngine;

public enum Rate
{
    common,
    uncommon,
    rare,
    superRare,
    SuperSpecialRare
}

[Serializable]
[CreateAssetMenu(menuName = "Data/ItemData")]
public class ItemData : ScriptableObject
{
    public int id; //アイテムのID
    [Tooltip("0:日本語, 1:英語, 2:中国語")]
    public string[] itemName;//名前
    [Tooltip("0:日本語, 1:英語, 2:中国語")]
    public string[] description;//説明
    public string description_en;//説明(英語)
    public Sprite iconImage;//アイテムの画像（入手済み・カラー）
    public Sprite silhouetteImage;//アイテムの画像（未入手・シルエット）
    public GameObject prefabData;//生成するオブジェクト
    public Rate priority;//アイテムのレア度
    public int rate;//UFOキャッチャー内での出現確率
    public ItemType itemType;
    public ItemCategory itemCategory;

    [Header("クレーンゲーム排出設定（旧ItemSpawnData統合分）")]
    [Tooltip("ItemSpawnerのアクティブ枠の入れ替え判定に使う優先度")]
    public int spawnPriority;
    [Tooltip("ItemSpawnerでの排出確率（比率）")]
    public float spawnRate;

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
        return new ItemSpawnSettings(itemName, prefabData, spawnPriority, spawnRate, goldConvertCount, silverConvertCount, bronzeConvertCount);
    }
}

