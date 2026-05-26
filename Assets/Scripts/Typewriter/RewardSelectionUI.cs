using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 報酬選択 UI。Show(options, onSelected) で全画面オーバーレイを表示してカーソルを出す。
/// ユーザーが選択肢をクリックすると Hide() してコールバックに選んだ文字列を渡す。
/// uiRoot 未指定なら実行時に Screen Space Overlay Canvas を自動生成する。
/// </summary>
[DisallowMultipleComponent]
public class RewardSelectionUI : MonoBehaviour
{
    [Header("UI (手動指定する場合)")]
    [Tooltip("Canvas ルート。null なら実行時に簡易 Canvas を自動生成")]
    public GameObject uiRoot;

    [Tooltip("どの Display に表示するか (0=Display1, 3=Display4)。Show 時に Canvas.targetDisplay / 対応カメラへ反映")]
    [Range(0, 7)]
    public int targetDisplay = 3;

    [Tooltip("ScreenSpaceCamera モードで使うカメラ。null なら targetDisplay 一致のカメラを自動検索 (Input System Package のマルチ Display 互換のため必須)")]
    public Camera worldCamera;

    [Tooltip("Canvas の plane distance (ScreenSpaceCamera 時)")]
    public float planeDistance = 1f;

    [Tooltip("選択肢ボタンの参照 (要素数 = 同時に出す選択肢数、通常 2)")]
    public Button[] optionButtons;

    [Tooltip("各ボタン上に表示するテキスト")]
    public Text[] optionTexts;

    [Tooltip("タイトル/見出しテキスト (任意)")]
    public Text titleText;

    [Tooltip("見出し文字列")]
    public string titleString = "Select a reward";

    private Action<string> _onSelected;
    private string[] _currentOptions;
    private CursorLockMode _prevLockState;
    private bool _prevCursorVisible;

    public bool IsActive => uiRoot != null && uiRoot.activeSelf;

    private void Awake()
    {
        if (uiRoot == null) AutoCreateUI();
        if (uiRoot != null) uiRoot.SetActive(false);
        WireButtons();
        if (titleText != null) titleText.text = titleString;
        EnsureEventSystem();
    }

    private void WireButtons()
    {
        if (optionButtons == null) return;
        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (optionButtons[i] == null) continue;
            int idx = i;
            optionButtons[i].onClick.RemoveAllListeners();
            optionButtons[i].onClick.AddListener(() => OnOptionClicked(idx));
        }
    }

    public void Show(string[] options, Action<string> onSelected)
    {
        if (options == null || options.Length == 0)
        {
            Debug.LogWarning("[RewardSelectionUI] Show: options が空");
            return;
        }
        if (uiRoot == null || optionButtons == null || optionTexts == null)
        {
            Debug.LogWarning("[RewardSelectionUI] Show: UI が未初期化");
            return;
        }
        _currentOptions = options;
        _onSelected = onSelected;
        for (int i = 0; i < optionButtons.Length; i++)
        {
            bool has = i < options.Length;
            if (optionButtons[i] != null) optionButtons[i].gameObject.SetActive(has);
            if (has && optionTexts[i] != null) optionTexts[i].text = options[i];
        }
        if (titleText != null) titleText.text = titleString;
        ApplyTargetDisplay();
        uiRoot.SetActive(true);

        _prevLockState = Cursor.lockState;
        _prevCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log($"[RewardSelectionUI] Show: options=[{string.Join(" | ", options)}]", this);
    }

    private void ApplyTargetDisplay()
    {
        if (uiRoot == null) return;
        var canvas = uiRoot.GetComponent<Canvas>();
        if (canvas == null) canvas = uiRoot.GetComponentInChildren<Canvas>(true);
        if (canvas == null) return;

        // Input System Package + ScreenSpaceOverlay は targetDisplay != 0 だとボタンクリックを受けない既知の問題があるため、
        // 対応 Display を持つカメラがあれば ScreenSpaceCamera モードに切り替える。
        if (worldCamera == null) worldCamera = FindCameraForDisplay(targetDisplay);

        if (worldCamera != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = worldCamera;
            canvas.planeDistance = planeDistance;
            Debug.Log($"[RewardSelectionUI] ScreenSpaceCamera mode, camera={worldCamera.name} (targetDisplay={worldCamera.targetDisplay})", this);
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = targetDisplay;
            Debug.LogWarning($"[RewardSelectionUI] targetDisplay={targetDisplay} に一致するカメラが見つからずフォールバックで Overlay。Display 4 ではボタンクリックを受けない可能性あり", this);
        }
    }

    private static Camera FindCameraForDisplay(int display)
    {
        var cams = Camera.allCameras;
        foreach (var c in cams)
        {
            if (c == null || !c.enabled) continue;
            if (c.targetDisplay == display) return c;
        }
        return null;
    }

    public void Hide()
    {
        if (uiRoot != null) uiRoot.SetActive(false);
        Cursor.lockState = _prevLockState;
        Cursor.visible = _prevCursorVisible;
        _onSelected = null;
        _currentOptions = null;
    }

    private void OnOptionClicked(int index)
    {
        if (_currentOptions == null || index < 0 || index >= _currentOptions.Length)
        {
            Debug.LogWarning($"[RewardSelectionUI] OnOptionClicked index={index} だがコンテキスト無効", this);
            return;
        }
        string picked = _currentOptions[index];
        Debug.Log($"[RewardSelectionUI] OnOptionClicked: index={index} text=\"{picked}\"", this);
        var cb = _onSelected;
        Hide();
        cb?.Invoke(picked);
    }

    private void AutoCreateUI()
    {
        var canvasGo = new GameObject("RewardSelectionCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        // ApplyTargetDisplay() で Show 時に ScreenSpaceCamera に切り替える
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var dimmer = new GameObject("Dimmer", typeof(Image)).GetComponent<RectTransform>();
        dimmer.SetParent(canvasGo.transform, false);
        dimmer.anchorMin = Vector2.zero; dimmer.anchorMax = Vector2.one;
        dimmer.offsetMin = Vector2.zero; dimmer.offsetMax = Vector2.zero;
        dimmer.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var titleRect = new GameObject("Title", typeof(Text)).GetComponent<RectTransform>();
        titleRect.SetParent(canvasGo.transform, false);
        titleRect.anchorMin = new Vector2(0.5f, 0.78f);
        titleRect.anchorMax = new Vector2(0.5f, 0.78f);
        titleRect.sizeDelta = new Vector2(1200, 100);
        titleRect.anchoredPosition = Vector2.zero;
        titleText = titleRect.GetComponent<Text>();
        titleText.font = font;
        titleText.fontSize = 44;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleText.text = titleString;

        optionButtons = new Button[2];
        optionTexts = new Text[2];
        for (int i = 0; i < 2; i++)
        {
            var btnGo = new GameObject($"Option{i}", typeof(Image), typeof(Button));
            btnGo.transform.SetParent(canvasGo.transform, false);
            var btnRect = (RectTransform)btnGo.transform;
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.sizeDelta = new Vector2(1200, 140);
            btnRect.anchoredPosition = new Vector2(0f, 110f - i * 200f);
            var img = btnGo.GetComponent<Image>();
            img.color = new Color(0.10f, 0.10f, 0.15f, 0.95f);
            var btn = btnGo.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.75f, 0.85f, 1f, 1f);
            colors.pressedColor = new Color(0.5f, 0.7f, 1f, 1f);
            btn.colors = colors;
            btn.targetGraphic = img;
            optionButtons[i] = btn;

            var txtRect = new GameObject("Text", typeof(Text)).GetComponent<RectTransform>();
            txtRect.SetParent(btnGo.transform, false);
            txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = new Vector2(32, 16);
            txtRect.offsetMax = new Vector2(-32, -16);
            var txt = txtRect.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = 32;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            optionTexts[i] = txt;
        }
        uiRoot = canvasGo;
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var es = FindAnyObjectByType<EventSystem>();
        if (es != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        go.transform.SetParent(transform, false);
    }
}
