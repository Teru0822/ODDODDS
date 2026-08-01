using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    private RewardSelectionUI _rewardSelectionUI;
    private int _rewardIndex = -1;
    private Vector3 _originalScale;

    [Tooltip("スケールアニメの対象Transform。ScrollView内でクリップされる場合はボタンの内部コンテンツTransformを指定してください。未設定の場合はこのTransformを使用します。")]
    [SerializeField] private Transform _scaleTarget;

    public RewardSelectionUI RewardSelectionUI { set { _rewardSelectionUI = value; } }
    public int RewardIndex { set { _rewardIndex = value; } }

    private void Awake()
    {
        if (_scaleTarget == null) _scaleTarget = transform;
        _originalScale = _scaleTarget.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_rewardSelectionUI == null || _rewardIndex == -1) return;
        var options = _rewardSelectionUI.CurrentOptions;
        if (options == null || _rewardIndex >= options.Count) return;
        var data = options[_rewardIndex];

        _rewardSelectionUI.OnSkillButtonHover(_rewardIndex);
        _rewardSelectionUI.SetExplainText(data.skillDescription);
        _rewardSelectionUI.ShowPreview(data);

        _scaleTarget.DOKill();
        _scaleTarget.DOScale(_originalScale * 1.05f, 0.1f).SetEase(Ease.OutCubic);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _rewardSelectionUI?.OnSkillButtonExit(_rewardIndex);

        _scaleTarget.DOKill();
        _scaleTarget.DOScale(_originalScale, 0.08f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _scaleTarget.DOKill();
        _scaleTarget.DOPunchScale(Vector3.one * -0.08f, 0.12f, 4, 0.5f);

        _rewardSelectionUI?.OnSkillButtonPress(_rewardIndex);
    }
}
