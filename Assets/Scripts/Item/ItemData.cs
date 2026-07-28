using System;
using UnityEngine;

[Serializable]
[CreateAssetMenu(menuName = "Data/ItemData")]
public class ItemData : ScriptableObject
{
    public int id; //アイテムのID
    public string itemName;//名前
    public string description;//説明
    public Sprite iconImage;//アイテムの画像
    public GameObject prefabData;//生成するオブジェクト
    public ItemType itemType;
    public ItemCategory itemCategory;
}

