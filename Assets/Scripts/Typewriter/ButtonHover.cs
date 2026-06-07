using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHover : MonoBehaviour, IPointerEnterHandler,IPointerExitHandler
{
    private RewardSelectionUI _rewardSelectionUI;
    private int _rewardIndex = -1;
    public RewardSelectionUI RewardSelectionUI { set { _rewardSelectionUI = value; } }
    public int RewardIndex { set { _rewardIndex = value; } }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_rewardSelectionUI != null && _rewardIndex != -1)
            _rewardSelectionUI.explainText.text = _rewardSelectionUI.CurrentOptions[_rewardIndex].skillDescription;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_rewardSelectionUI != null)
            _rewardSelectionUI.explainText.text = "";
    }
}
