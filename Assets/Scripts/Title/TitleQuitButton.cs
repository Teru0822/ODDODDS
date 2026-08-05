using UnityEngine;

[DisallowMultipleComponent]
public class TitleQuitButton : MonoBehaviour
{
    [Header("ホバー (クリック判定に使用)")]
    [Tooltip("クリック判定に使う MouseHoverOutline。null なら自身に付いているものを自動取得")]
    public MouseHoverOutline hoverOutline;

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
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;//シーン再生終了
        #else
            Application.Quit();//ゲームプレイ終了
        #endif
    }

}
