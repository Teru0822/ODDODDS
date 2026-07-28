using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class VisitorSelectionHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private ScriptableObject _masterData;
    private TMP_Text _contentText;
    private TMP_Text _nameText;

    public void Init(ScriptableObject data, TMP_Text contentText)
    {
        _masterData = data;
        _contentText = contentText;
        _nameText = GetComponentInChildren<TMP_Text>();
        if(_masterData != null)
        {
            if (_masterData is ItemData itemData)
            {
                _nameText.text = itemData.itemName;
            }
            else if (_masterData is EffectData effectData)
            {
                _nameText.text = effectData.effectName;
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if(_masterData != null)
        {
            if (_masterData is ItemData itemData)
            {
                _contentText.text = itemData.description;
            }
            else if (_masterData is EffectData effectData)
            {
                _contentText.text = effectData.description;
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _contentText.text = "どちらを選ぼうか...？";
    }
}
