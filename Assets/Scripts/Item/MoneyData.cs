using UnityEngine;
using System;

[Serializable]
[CreateAssetMenu(menuName = "Data/MoneyData")]
public class MoneyData : ScriptableObject
{
    public int price;//金額
    public Sprite iconImage;//アイテムの画像
    public GameObject prefabData;//生成するオブジェクト
}
