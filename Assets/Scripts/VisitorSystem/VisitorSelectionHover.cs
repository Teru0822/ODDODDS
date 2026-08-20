using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class VisitorSelectionHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,ILanguage
{
    private ScriptableObject _masterData;
    private TMP_Text _contentText;
    private TMP_Text _nameText;
    private Language _language;
    public void SettingLanguage(Language language)
    {
        _language = language;
    }

    public void Init(ScriptableObject data, TMP_Text contentText)
    {
        _masterData = data;
        _contentText = contentText;
        _nameText = GetComponentInChildren<TMP_Text>();
        if(_masterData != null)
        {
            if (_masterData is ItemData itemData)
            {
                _nameText.text = itemData.itemName[(int)_language];
            }
            else if (_masterData is EffectData effectData)
            {
                _nameText.text = effectData.effectName[(int)_language];
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if(_masterData != null)
        {
            if (_masterData is ItemData itemData)
            {
                _contentText.text = itemData.description[(int)_language];
            }
            else if (_masterData is EffectData effectData)
            {
                _contentText.text = effectData.description[(int)_language];
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _contentText.text = "どちらを選ぼうか...？";
    }
}
