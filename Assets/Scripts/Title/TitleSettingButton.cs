using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class TitleSettingButton : MonoBehaviour
{
    [Header("ホバー (クリック判定に使用)")]
    [Tooltip("クリック判定に使う MouseHoverOutline。null なら自身に付いているものを自動取得")]
    public MouseHoverOutline hoverOutline;
    [SerializeField] private SettingUIManager _settingUIManager;

    private void Awake()
    {
        if (hoverOutline == null) hoverOutline = GetComponent<MouseHoverOutline>();
        if (hoverOutline == null)
        {
            Debug.LogWarning("[TitlePlayButton] MouseHoverOutline が未取得。Play クリックが反応しません。同一 GameObject にアタッチするか Inspector で指定してください", this);
        }
    }

    private void OnEnable()
    {
        if (hoverOutline != null) hoverOutline.OnClicked += HandleSettingButtonClicked;
    }

    private void OnDisable()
    {
        if (hoverOutline != null) hoverOutline.OnClicked -= HandleSettingButtonClicked;
    }

    private void HandleSettingButtonClicked()
    {
        _settingUIManager.OpenSettingMenu();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
