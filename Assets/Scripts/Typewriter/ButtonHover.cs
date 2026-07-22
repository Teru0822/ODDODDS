using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private RewardSelectionUI _rewardSelectionUI;
    private int _rewardIndex = -1;
    public RewardSelectionUI RewardSelectionUI { set { _rewardSelectionUI = value; } }
    public int RewardIndex { set { _rewardIndex = value; } }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_rewardSelectionUI == null || _rewardIndex == -1) return;
        var options = _rewardSelectionUI.CurrentOptions;
        if (options == null || _rewardIndex >= options.Count) return;
        var data = options[_rewardIndex];

        // ホバー画像の切り替え（前のボタンのリセットも含む）
        _rewardSelectionUI.OnSkillButtonHover(_rewardIndex);

        // 説明文・プレビューを更新
        _rewardSelectionUI.SetExplainText(data.skillDescription);
        _rewardSelectionUI.ShowPreview(data);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 他のスキルをホバーするまでアクティブ状態を維持するため何もしない
    }
}
