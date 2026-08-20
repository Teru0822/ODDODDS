using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[CreateAssetMenu(menuName = "DataBase/ItemDataBase")]
public class ItemDataBase : ScriptableObject
{
    public List<ItemData> itemDataBase;

    [Tooltip("クレーンゲーム（Devilキャッチャー）用アイテム一覧。ItemSpawner・DevilItemPickupDisplay等が個別に持つ" +
             "リストと同じ ItemData アセットをここにも並べておくことで、一覧性を確保する（参照を共有するだけで、" +
             "既存スクリプト側のリストはそのまま引き続き使われる）")]
    public List<ItemData> craneItemDataBase;
}
