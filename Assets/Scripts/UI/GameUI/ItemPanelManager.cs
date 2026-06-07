using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// アイテムパネルの表示およびアイテム所持状況の永続化を管理するクラス
/// </summary>
public class ItemPanelManager : MonoBehaviour, IsaveDataProvider
{
    [Header("アイテム表示用オブジェクト")]
    [SerializeField] private ItemDataBase _itemDataBase;
    [SerializeField] private List<Button> _itemButtons = new List<Button>();

    [Header("説明用オブジェクト")]
    [SerializeField] private TMP_Text _detailDescriptionText;
    [SerializeField] private Image _detailIconImage;

    ReactiveCollection<int> _ownedItemIds = new ReactiveCollection<int>();

    private void Awake()
    {
        _ownedItemIds
            .ObserveAdd()
            .Subscribe(index =>
            {
                Debug.LogWarning("ItemID[" + index + "]のアイテムをゲットしました。");
                UpdateUI();
            }).AddTo(this);

        _ownedItemIds
            .ObserveRemove()
            .Subscribe(index =>
            {
                Debug.LogWarning("ItemID[" + index + "]のアイテムが無くなりました。");
                UpdateUI();
            }).AddTo(this);
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Keyboard.current.iKey.wasPressedThisFrame)
        {
            foreach (var item in _ownedItemIds)
            {
                Debug.LogWarning(item);
            }
            UpdateUI();
            Debug.Log("試しにイベント機能を使います。");
        }
#endif
    }

    public void WriteSaveData(RoguelikeSaveData saveData)
    {
        saveData.ownedItems = _ownedItemIds.ToList();
    }

    public void ReadSaveData(RoguelikeSaveData saveData)
    {
        foreach (var item in saveData.ownedItems)
        {
            _ownedItemIds.Add(item);
        }
    }


    /// <summary>
    /// 所持アイテムのデータに合わせてScroll Viewのボタンを更新する
    /// </summary>
    public void UpdateUI()
    {
        List<ItemData> ownedItems = new List<ItemData>();
        foreach (int id in _ownedItemIds)
        {
            ItemData item = GetItemDataById(id);
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
                SetButtonText(_itemButtons[i].gameObject, item.itemName);

                _itemButtons[i].onClick.RemoveAllListeners();
                _itemButtons[i].onClick.AddListener(() => OnSelectItem(item));
            }
            else
            {
                _itemButtons[i].gameObject.SetActive(false);
            }
        }

        //説明用のオブジェクトは一旦非表示
        _detailDescriptionText.gameObject.SetActive(false);
        _detailIconImage.gameObject.SetActive(false);
    }

    /// <summary>
    /// ボタンオブジェクト配下からTMP_Textを探してテキストを設定する
    /// </summary>
    private void SetButtonText(GameObject btnObj, string text)
    {
        TMP_Text tmpText = btnObj.GetComponentInChildren<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = text;
            return;
        }
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
    private void OnSelectItem(ItemData item)
    {
        if (item == null) return;

        if (_detailDescriptionText != null)
        {
            _detailDescriptionText.text = item.description;
            _detailDescriptionText.gameObject.SetActive(true);
        }

        if (_detailIconImage != null)
        {
            _detailIconImage.sprite = item.iconImage;
            _detailIconImage.gameObject.SetActive(item.iconImage != null);
        }
    }

    /// <summary>
    /// 指定されたIDのアイテムを所持リストに追加し、UI更新を行う
    /// </summary>
    public void AddItem(int id)
    {
        if (!_ownedItemIds.Contains(id))
        {
            _ownedItemIds.Add(id);
            
            UpdateUI();
        }
    }

    /// <summary>
    /// 指定されたIDのアイテムを所持リストから削除し、UI更新を行う
    /// </summary>
    public void RemoveItem(int id)
    {
        if (_ownedItemIds.Contains(id))
        {
            _ownedItemIds.Remove(id);
            UpdateUI();
        }
    }
}
