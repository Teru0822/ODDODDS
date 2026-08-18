using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum ItemType
{
    /// <summary>
    /// 消費アイテム
    /// </summary>
    Consume,
    /// <summary>
    /// 恒常アイテム
    /// </summary>
    Permanent,
    /// <summary>
    /// クレーンゲーム（UFOキャッチャー）アイテム
    /// </summary>
    CraneItem,
}

public enum ItemCategory
{
    /// <summary>
    /// 交換アイテム
    /// </summary>
    Exchange,
    /// <summary>
    /// 消費アイテム
    /// </summary>
    Consume,
    /// <summary>
    /// 大事なもの
    /// </summary>
    Important
}
/// <summary>
/// アイテムパネルの表示およびアイテム所持状況の永続化を管理するクラス
/// </summary>
public class ItemPanelManager : MonoBehaviour, IsaveDataProvider
{
    public static ItemPanelManager Instance { get; private set; }

    /// <summary>AddItem でアイテムが追加されたときに発火。引数はアイテムID。</summary>
    public static event System.Action<int> OnItemObtained;

    [Header("アイテム表示用オブジェクト")]
    [SerializeField] private ItemDataBase _itemDataBase;

    public ItemDataBase ItemDatabase => _itemDataBase;
    [SerializeField] private Button _consumeButton;
    [SerializeField] private Button _permanentButton;
    [SerializeField] private List<Button> _itemButtons = new List<Button>();

    [Header("説明用オブジェクト")]
    [SerializeField] private TMP_Text _itemNameText;
    [SerializeField] private TMP_Text _categoryText;
    [SerializeField] private TMP_Text _countText;
    [SerializeField] private TMP_Text _detailDescriptionText;
    [SerializeField] private Image _itemIconImage;
    [SerializeField] private Button _useButton;

    ReactiveCollection<ItemInstance> _ownedItems = new ReactiveCollection<ItemInstance>();//恒常アイテムのIDを保持したもの
    ReactiveCollection<ItemInstance> _ownedConsumeItems = new ReactiveCollection<ItemInstance>();//消費アイテムのIDを保持したもの

    /*--- アイテムの処理で使うコンポーネントを記述 ---*/
    [SerializeField] private GameUIManager _gameUIManager;
    private ItemType _displayedType = ItemType.Consume;//表示させるアイテムの種類を決定する変数
    private ItemInstance _nowSelectedItem = null;

    //データ収集
    private int _getItemCount;//これまでアイテムを取得した数
    private int _useItemCount;//これまでアイテムを使用した数

    private void Awake()
    {
        Instance = this;

        _ownedItems
            .ObserveAdd()
            .Subscribe(index =>
            {
                if (index.Value == null)
                {
                    Debug.LogError("追加されたItemDataがnull");
                    return;
                }
                Debug.LogWarning(index.Value.ItemName + "をゲットしました。");
                UpdateUI();
            }).AddTo(this);

        _ownedConsumeItems
        .ObserveAdd()
        .Subscribe(index =>
        {
            if (index.Value == null)
                {
                    Debug.LogError("追加されたItemDataがnull");
                    return;
                }
            Debug.LogWarning(index.Value.ItemName + "をゲットしました。");
            UpdateUI();
        }).AddTo(this);

        _useButton.onClick.AddListener(() =>
        {
            UseItem();
        });

        _consumeButton.GetComponent<Image>().color = Color.gray;
        _permanentButton.onClick.AddListener(() =>
        {
            _displayedType = ItemType.Permanent;
            _permanentButton.GetComponent<Image>().color = Color.gray;
            _consumeButton.GetComponent<Image>().color = Color.white;
            UpdateUI();
            ClearExplainPanel();
        });
        _consumeButton.onClick.AddListener(() => 
        { 
            _displayedType = ItemType.Consume; 
            _permanentButton.GetComponent<Image>().color = Color.white;
            _consumeButton.GetComponent<Image>().color = Color.gray;
            UpdateUI();
            ClearExplainPanel();
        });

        ClearExplainPanel();
    }

    private void ClearExplainPanel()
    {
        _detailDescriptionText.gameObject.SetActive(false);
        _categoryText.gameObject.SetActive(false);
        _countText.gameObject.SetActive(false);
        _itemNameText.gameObject.SetActive(false);
        _itemIconImage.gameObject.SetActive(false);
        _useButton.gameObject.SetActive(false);
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Keyboard.current.iKey.wasPressedThisFrame)
        {
            foreach (var item in _ownedItems)
            {
                Debug.LogWarning(item.ItemName);
            }
            UpdateUI();
            Debug.Log("試しにイベント機能を使います。");
        }
#endif
    }

    public void WriteSaveData(RoguelikeSaveData saveData)
    {
        //ItemInstanceからItemSaveDataを作成し、セーブさせる
        List<ItemSaveData> permanentItemSaveData = new List<ItemSaveData>();
        List<ItemSaveData> consumeItemSaveData = new List<ItemSaveData>();

        foreach(var item in _ownedItems)
        {
            permanentItemSaveData.Add(item.CreateItemSaveData());
        }
        foreach(var item in _ownedConsumeItems)
        {
            consumeItemSaveData.Add(item.CreateItemSaveData());
        }

        saveData.ownedPermanentItems = permanentItemSaveData;
        saveData.ownedConsumeItems = consumeItemSaveData;
        saveData.getItemCount = _getItemCount;
        saveData.useItemCount = _useItemCount;
    }

    public void ReadSaveData(RoguelikeSaveData saveData)
    {
        _ownedItems.Clear();
        _ownedConsumeItems.Clear();

        if (saveData.ownedPermanentItems != null)
        {
            foreach (var itemSaveData in saveData.ownedPermanentItems)
            {
                ItemInstance instance = new ItemInstance();
                instance.master = GetItemDataById(itemSaveData.id);
                instance.Count = itemSaveData.count;
                _ownedItems.Add(instance);
            }
        }

        if (saveData.ownedConsumeItems != null)
        {
            foreach (var itemSaveData in saveData.ownedConsumeItems)
            {
                ItemInstance instance = new ItemInstance();
                instance.master = GetItemDataById(itemSaveData.id);
                instance.Count = itemSaveData.count;
                _ownedConsumeItems.Add(instance);
            }
        }
        _getItemCount = saveData.getItemCount;
        _useItemCount = saveData.useItemCount;

        Debug.LogWarning("[WriteSaveData] --- Owned Permanent Items Details ---");
        foreach (var item in _ownedItems)
        {
            if (item != null)
            {
                Debug.LogWarning($"[PermanentItem] ID: {item.Id}, Name: {item.ItemName}, Description: {item.ItemDescription}, Count: {item.Count}, Type: {item.ItemType}, Category: {item.ItemCategory}, Icon: {(item.ItemIcon != null ? item.ItemIcon.name : "null")}, Prefab: {(item.PrefabData != null ? item.PrefabData.name : "null")}");
            }
            else
            {
                Debug.LogWarning("[PermanentItem] Item is null");
            }
        }

        Debug.LogWarning("[WriteSaveData] --- Owned Consume Items Details ---");
        foreach (var item in _ownedConsumeItems)
        {
            if (item != null)
            {
                Debug.LogWarning($"[CounsumeItem] ID: {item.Id}, Name: {item.ItemName}, Description: {item.ItemDescription}, Count: {item.Count}, Type: {item.ItemType}, Category: {item.ItemCategory}, Icon: {(item.ItemIcon != null ? item.ItemIcon.name : "null")}, Prefab: {(item.PrefabData != null ? item.PrefabData.name : "null")}");
            }
            else
            {
                Debug.LogWarning("[ConsumeItem] Item is null");
            }
        }

        UpdateUI();
    }


    /// <summary>
    /// 所持アイテムのデータに合わせてScroll Viewのボタンを更新する
    /// </summary>
    public void UpdateUI()
    {
        List<ItemInstance> ownedItems = new List<ItemInstance>();
        var targetCollection = (_displayedType == ItemType.Permanent) ? _ownedItems : _ownedConsumeItems;

        foreach (var item in targetCollection)
        {
            if (item != null)
            {
                ownedItems.Add(item);
            }
        }

        if (_itemButtons == null || _itemButtons.Count == 0)
        {
            Debug.LogWarning("Scroll View内にbuttonがないです");
            return;
        }

        for (int i = 0; i < _itemButtons.Count; i++)
        {
            if (i < ownedItems.Count)
            {
                var item = ownedItems[i];
                _itemButtons[i].gameObject.SetActive(true);
                SetButtonUI(_itemButtons[i].gameObject, item.Count.ToString(),item.ItemIcon);

                _itemButtons[i].onClick.RemoveAllListeners();
                _itemButtons[i].onClick.AddListener(() => OnSelectItem(item));
            }
            else
            {
                _itemButtons[i].gameObject.SetActive(false);
            }
        }

        if(_countText.gameObject.activeSelf && _nowSelectedItem != null)
            _countText.text = "Count: " + _nowSelectedItem.Count.ToString();
    }

    /// <summary>
    /// ボタンのUIをアイテムに応じて変更させる
    /// </summary>
    private void SetButtonUI(GameObject btnObj, string text,Sprite icon)
    {
        TMP_Text tmpText = btnObj.GetComponentInChildren<TMP_Text>();
        Image image = btnObj.transform.Find("ItemIcon").GetComponent<Image>();
        if (tmpText != null)
            tmpText.text = text;

        if(image != null)
            image.sprite = icon;
    }

    /// <summary>
    /// IDからItemDataを取得する
    /// </summary>
    private ItemData GetItemDataById(int id)
    {
        if (_itemDataBase == null || _itemDataBase.itemDataBase == null)
        {
            return null;
        }
        return _itemDataBase.itemDataBase.Find(item => item.id == id);
    }

    /// <summary>
    /// アイテムが選択された際の処理
    /// </summary>
    private void OnSelectItem(ItemInstance item)
    {
        if (item == null) return;

        if (_detailDescriptionText != null)
        {
            _detailDescriptionText.text = item.ItemDescription;
            _detailDescriptionText.gameObject.SetActive(true);
        }

        if (_categoryText != null)
        {
            _categoryText.text = "Category: " + item.ItemCategory.ToString();
            _categoryText.gameObject.SetActive(true);
        }

        if (_countText != null)
        {
            _countText.text = "Count: " + item.Count.ToString();
            _countText.gameObject.SetActive(true);
        }

        if (_itemNameText != null)
        {
            _itemNameText.text = item.ItemName;
            _itemNameText.gameObject.SetActive(true);
        }

        if (_itemIconImage != null)
        {
            _itemIconImage.sprite = item.ItemIcon;
            _itemIconImage.gameObject.SetActive(true);
        }

        if(_useButton != null && item.ItemCategory == ItemCategory.Consume && item.Count > 0)
        {
            _useButton.gameObject.SetActive(true);
        }
        else
        {
            _useButton.gameObject.SetActive(false);
        }
        _nowSelectedItem = item;
    }

    /// <summary>
    /// アイテムを使用する際の処理
    /// </summary>
    private void UseItem()
    {
        int effectId = EffectManager.Instance.GetIdByItemName(_nowSelectedItem.ItemName);

        //アイテムを使用できるか確認し、アイテム処理を行う
        if(EffectManager.Instance.IsHasEffect(effectId))
        {
            //既に使用しているのでこのターンは使えません...と記載する
            Debug.LogError("既に使用しているのでこのターンは使えません");
        }
        else
        {
            Debug.LogError(_nowSelectedItem.ItemName + "使用");
            EffectManager.Instance.AddEffect(effectId);
            RemoveItem(_nowSelectedItem.Id,_nowSelectedItem.ItemType);
            _useItemCount++;   
        }
    }

    /// <summary>
    /// 指定したIDのアイテムを一度でも入手したことがあるかどうか（図鑑表示用）
    /// </summary>
    public bool IsItemOwned(int id)
    {
        foreach (var item in _ownedItems)
            if (item != null && item.Id == id) return true;
        foreach (var item in _ownedConsumeItems)
            if (item != null && item.Id == id) return true;
        return false;
    }

    /// <summary>
    /// 指定したIDを持つアイテムを所持しているかを返す関数
    /// </summary>
    /// <param name="id">アイテムID</param>
    /// <param name="num">必要な個数</param>
    /// <returns></returns>
    public bool isHasItem(int id, int num)
    {
        foreach (var item in _ownedConsumeItems)
        {
            if (item.Id == id && item.Count >= num)//対象のIDのアイテムであり、必要個数持っていたら
            {
                return true;
            }
            else
                continue;
        }
        return false;
    }

    /// <summary>
    /// 指定されたIDのアイテムを所持リストに追加し、UI更新を行う
    /// </summary>
    public void AddItem(int id, ItemType type, int num = 1)
    {
        var targetCollection = (type == ItemType.Permanent) ? _ownedItems : _ownedConsumeItems;

        bool exists = false;
        foreach (var item in targetCollection)
        {
            if (item.Id == id)
            {
                exists = true;
                if(type == ItemType.Consume || type == ItemType.CraneItem) item.Count+=num;//消費・クレーンアイテムの場合、アイテムの所持数を増加
                _gameUIManager.AddPopupQueue(true,item.master);
                break;
            }
        }

        if (!exists)
        {
            ItemData original = GetItemDataById(id);
            if (original != null)
            {
                ItemInstance instance = new ItemInstance();
                instance.master = original;
                instance.Count = num;
                targetCollection.Add(instance);
                _gameUIManager.AddPopupQueue(true,instance.master);
            }
            else
            {
                Debug.LogError("指定されたIDは存在しません");
                return;
            }
        }

        OnItemObtained?.Invoke(id);
        _getItemCount += num;
        UpdateUI();
    }

    /// <summary>
    /// 指定されたIDのアイテムを所持リストから削除し、UI更新を行う
    /// </summary>
    public void RemoveItem(int id, ItemType type, int num = 1)
    {
        var targetCollection = (type == ItemType.Permanent) ? _ownedItems : _ownedConsumeItems;

        foreach (var item in targetCollection)
        {
            if (item != null && item.Id == id)
            {
                if(type == ItemType.Consume || type == ItemType.CraneItem) item.Count = Mathf.Max(item.Count - num, 0);//消費・クレーンアイテムの場合、アイテムの所持数を減少
                UpdateUI();
                _gameUIManager.AddPopupQueue(false,item.master);
                break;
            }
        }
    }
}
