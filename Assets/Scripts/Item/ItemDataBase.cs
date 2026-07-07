using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[CreateAssetMenu(menuName = "ItemDataBase")]
public class ItemDataBase : ScriptableObject
{
    public List<ItemData> itemDataBase;
}
