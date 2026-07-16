using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 報酬選択 UI。Show(options, onSelected) で全画面オーバーレイを表示してカーソルを出す。
/// ユーザーが選択肢をクリックすると Hide() してコールバックに選んだ文字列を渡す。
/// uiRoot 未指定なら実行時に Screen Space Overlay Canvas を自動生成する。
/// 自動生成時はスクロールビュー + 動的ボタンで任意件数に対応する。
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

    [Tooltip("選択肢ボタンの参照 (要素数 = 同時に出す選択肢数、通常 2)。手動指定時に使用")]
    public Button[] optionButtons;

    [Tooltip("各ボタン上に表示するテキスト。手動指定時に使用")]
    public Text[] optionTexts;

    [Tooltip("タイトル/見出しテキスト (任意)")]
    public Text titleText;

    [Tooltip("スキルの説明用テキスト")]
    public Text explainText;

    [Tooltip("見出し文字列")]
    public string titleString = "Select a reward";

    private Action<RoguelikeData> _onSelected;
    private List<RoguelikeData> _currentOptions;
    private CursorLockMode _prevLockState;
    private bool _prevCursorVisible;

    // 自動生成UI用
    private bool _isAutoCreated;
    private RectTransform _scrollContent;
    private readonly List<Button> _dynButtons = new List<Button>();
    private readonly List<Text> _dynTexts = new List<Text>();

    public List<RoguelikeData> CurrentOptions { get { return _currentOptions;} }
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

            var hover = optionButtons[i].gameObject.AddComponent<ButtonHover>();
            hover.RewardSelectionUI = this;
            hover.RewardIndex = i;
        }
    }

    public void Show(List<RoguelikeData> options, Action<RoguelikeData> onSelected)
    {
        if (options == null || options.Count == 0)
        {
            Debug.LogWarning("[RewardSelectionUI] Show: options が空");
            return;
        }
        if (uiRoot == null)
        {
            Debug.LogWarning("[RewardSelectionUI] Show: UI が未初期化");
            return;
        }
        _currentOptions = options;
        _onSelected = onSelected;

        if (_isAutoCreated)
        {
            RebuildDynamicButtons(options.Count);
            for (int i = 0; i < _dynTexts.Count; i++)
                if (_dynTexts[i] != null) _dynTexts[i].text = options[i].skillName;
        }
        else
        {
            if (optionButtons == null || optionTexts == null)
            {
                Debug.LogWarning("[RewardSelectionUI] Show: UI が未初期化");
                return;
            }
            for (int i = 0; i < optionButtons.Length; i++)
            {
                bool has = i < options.Count;
                if (optionButtons[i] != null) optionButtons[i].gameObject.SetActive(has);
                if (has && optionTexts[i] != null) optionTexts[i].text = options[i].skillName;
            }
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

    private void RebuildDynamicButtons(int count)
    {
        foreach (var btn in _dynButtons)
            if (btn != null) Destroy(btn.gameObject);
        _dynButtons.Clear();
        _dynTexts.Clear();

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        for (int i = 0; i < count; i++)
        {
            var btnGo = new GameObject($"DynOption{i}", typeof(Image), typeof(Button));
            btnGo.transform.SetParent(_scrollContent, false);

            var btnRect = (RectTransform)btnGo.transform;
            btnRect.sizeDelta = new Vector2(0, 72);

            // childControlHeight=true のとき VLG は LayoutElement.preferredHeight を参照する
            var le = btnGo.AddComponent<LayoutElement>();
            le.preferredHeight = 72f;
            le.flexibleHeight = 0f;

            var img = btnGo.GetComponent<Image>();
            img.color = new Color(0.25f, 0.28f, 0.45f, 1f);
            var btn = btnGo.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.6f, 0.8f, 1f, 1f);
            colors.pressedColor = new Color(0.35f, 0.55f, 1f, 1f);
            btn.colors = colors;
            btn.targetGraphic = img;

            var txtRect = new GameObject("Text", typeof(Text)).GetComponent<RectTransform>();
            txtRect.SetParent(btnGo.transform, false);
            txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = new Vector2(24, 8); txtRect.offsetMax = new Vector2(-24, -8);
            var txt = txtRect.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = 28;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;

            int idx = i;
            btn.onClick.AddListener(() => OnOptionClicked(idx));

            var hover = btnGo.AddComponent<ButtonHover>();
            hover.RewardSelectionUI = this;
            hover.RewardIndex = i;

            _dynButtons.Add(btn);
            _dynTexts.Add(txt);
        }

        // ContentSizeFitter の計算を即時実行（次フレーム待ちだと高さ 0 のまま表示される）
        LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent);
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
        if (_currentOptions == null || index < 0 || index >= _currentOptions.Count)
        {
            Debug.LogWarning($"[RewardSelectionUI] OnOptionClicked index={index} だがコンテキスト無効", this);
            return;
        }
        if (explainText != null) explainText.text = "";
        RoguelikeData picked = _currentOptions[index];
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

        // スクロールビュー背景パネル
        var scrollBgGo = new GameObject("ScrollBg", typeof(Image));
        scrollBgGo.transform.SetParent(canvasGo.transform, false);
        var scrollBgRect = (RectTransform)scrollBgGo.transform;
        scrollBgRect.anchorMin = new Vector2(0.2f, 0.22f);
        scrollBgRect.anchorMax = new Vector2(0.8f, 0.75f);
        scrollBgRect.offsetMin = Vector2.zero;
        scrollBgRect.offsetMax = Vector2.zero;
        scrollBgGo.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.1f, 0.95f);

        // スクロールビュー（ボタン一覧エリア）
        var scrollGo = new GameObject("ScrollView", typeof(ScrollRect));
        scrollGo.transform.SetParent(canvasGo.transform, false);
        var scrollRect = (RectTransform)scrollGo.transform;
        scrollRect.anchorMin = new Vector2(0.2f, 0.22f);
        scrollRect.anchorMax = new Vector2(0.8f, 0.75f);
        scrollRect.offsetMin = Vector2.zero;
        scrollRect.offsetMax = Vector2.zero;

        // Viewport（RectMask2D — Image+Mask よりも確実にクリッピングされる）
        var viewportGo = new GameObject("Viewport", typeof(RectMask2D));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        var viewportRect = (RectTransform)viewportGo.transform;
        viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero; viewportRect.offsetMax = Vector2.zero;

        // Content（VerticalLayoutGroup で高さ自動拡張）
        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        _scrollContent = (RectTransform)contentGo.transform;
        _scrollContent.anchorMin = new Vector2(0f, 1f);
        _scrollContent.anchorMax = new Vector2(1f, 1f);
        _scrollContent.pivot = new Vector2(0.5f, 1f);
        _scrollContent.offsetMin = Vector2.zero;
        _scrollContent.offsetMax = Vector2.zero;
        _scrollContent.sizeDelta = new Vector2(0f, 0f);

        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true; // LayoutElement.preferredHeight を使用するために true

        var csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var sr = scrollGo.GetComponent<ScrollRect>();
        sr.content = _scrollContent;
        sr.viewport = viewportRect;
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 30f;
        sr.movementType = ScrollRect.MovementType.Clamped;

        // 説明欄パネル
        var explainPanelGo = new GameObject("ExplainPanel", typeof(Image));
        explainPanelGo.transform.SetParent(canvasGo.transform, false);
        var panelRect = explainPanelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.15f);
        panelRect.anchorMax = new Vector2(0.5f, 0.15f);
        panelRect.sizeDelta = new Vector2(1200, 120);
        panelRect.anchoredPosition = Vector2.zero;
        explainPanelGo.GetComponent<Image>().color = new Color(0.10f, 0.10f, 0.15f, 0.95f);

        var explainTextGo = new GameObject("ExplainText", typeof(Text));
        explainTextGo.transform.SetParent(explainPanelGo.transform, false);
        var textRect = explainTextGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(32, 16); textRect.offsetMax = new Vector2(-32, -16);
        explainText = explainTextGo.GetComponent<Text>();
        explainText.font = font;
        explainText.fontSize = 28;
        explainText.alignment = TextAnchor.UpperCenter;
        explainText.color = Color.white;
        explainText.text = "";

        uiRoot = canvasGo;
        _isAutoCreated = true;
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
