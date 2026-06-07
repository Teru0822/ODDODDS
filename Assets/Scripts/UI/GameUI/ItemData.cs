using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[CreateAssetMenu(menuName = "ItemData")]
public class ItemData : ScriptableObject
{
    public int id; 
    public string itemName;
    public string description;
    public Sprite iconImage;
    public GameObject prefabData;
}

