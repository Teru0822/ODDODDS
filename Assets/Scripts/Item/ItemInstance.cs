using UnityEngine;

/// <summary>
/// ランタイム上のデータを保存するためのクラス。ItemDataをマスターデータとして扱い、ここにはランタイム上で変化する情報を記載する。Jsonファイルに保存するため、変数の型には注意すること
/// </summary>
[System.Serializable]
public class ItemSaveData
{
    public int id;
    public int count;
}

/// <summary>
/// ItemPanelManagerで扱うアイテム情報を管理するクラス
/// </summary>
public class ItemInstance
{
    public ItemData master;
    public int Count { get; set; }
    public int Id => master.id;
    public string[] ItemName => master.itemName;
    public Sprite ItemIcon => master.iconImage;
    public string[] ItemDescription => master.description;
    public string ItemDescription_en => master.description_en;
    public GameObject PrefabData=> master.prefabData;
    public ItemType ItemType=> master.itemType;
    public ItemCategory ItemCategory=> master.itemCategory;

    public ItemSaveData CreateItemSaveData()
    {
        ItemSaveData savedata = new ItemSaveData();
        savedata.id = Id;
        savedata.count = Count;

        return savedata;
    }
}
