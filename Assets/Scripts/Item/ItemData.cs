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
    public string itemName;//名前
    public string description;//説明
    public Sprite iconImage;//アイテムの画像（入手済み・カラー）
    public Sprite silhouetteImage;//アイテムの画像（未入手・シルエット）
    public GameObject prefabData;//生成するオブジェクト
    public Rate priority;//アイテムのレア度
    public int rate;//UFOキャッチャー内での出現確率
    public ItemType itemType;
    public ItemCategory itemCategory;
}

