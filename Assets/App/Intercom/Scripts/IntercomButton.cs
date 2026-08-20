using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// インターホンの物理ボタンをマウスでクリックした際のイベントを管理するクラス。
/// オブジェクトに Collider が付いている必要があります。
/// </summary>
public class IntercomButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private UnityEvent onClick;
    //[SerializeField] private VisitorSystem _visitorSystem;
    [SerializeField] private GameObject _visitorUI;
    [SerializeField] private GameObject _intercomeTelope;
    [SerializeField] private TMP_Text _contentText;
    [SerializeField] private string _context;

    // マウスクリック時に UnityEvent を発火
    public void OnClick()
    {
        onClick?.Invoke();
    }

    // シンプルな動作確認用に OnMouseDown もサポート (Collider必須)
    private void OnMouseDown()
    {
        OnClick();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if(_contentText == null) return;
        if(_intercomeTelope == null) return;
        if(_visitorUI == null) return;

        _visitorUI.SetActive(true);
        _intercomeTelope.SetActive(true);
        _contentText.text = _context;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if(_contentText == null) return;
        if(_intercomeTelope == null) return;
        if(_visitorUI == null) return;


        _intercomeTelope.SetActive(false);
        _contentText.text = "";
    }
}

