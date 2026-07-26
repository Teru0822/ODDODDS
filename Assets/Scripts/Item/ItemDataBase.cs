using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[CreateAssetMenu(menuName = "DataBase/ItemDataBase")]
public class ItemDataBase : ScriptableObject
{
    public List<ItemData> itemDataBase;
}
