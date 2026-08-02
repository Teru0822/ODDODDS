using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;


/// <summary>
/// 報酬選択 UI。Show(options, onSelected) でオーバーレイを表示してカーソルを出す。
/// Prefab（uiRoot + SerializeField）ベースで使用する。
/// uiRoot が null の場合のみ AutoCreateUI() でシンプルなスクロール UI を自動生成する。
/// </summary>
[DisallowMultipleComponent]
public class RewardSelectionUI : MonoBehaviour
{
    [Header("UI (手動指定する場合)")]
    [Tooltip("Canvas ルート。null なら実行時に簡易 Canvas を自動生成")]
    public GameObject uiRoot;

    [Tooltip("どの Display に表示するか (0=Display1, 3=Display4)")]
    [Range(0, 7)]
    public int targetDisplay = 3;

    [Tooltip("ScreenSpaceCamera モードで使うカメラ。null なら targetDisplay 一致のカメラを自動検索")]
    public Camera worldCamera;

    [Tooltip("Canvas の plane distance (ScreenSpaceCamera 時)")]
    public float planeDistance = 1f;

    [Tooltip("選択肢ボタンの参照（手動指定時に使用）")]
    public Button[] optionButtons;

    [Tooltip("各ボタン上に表示するテキスト（手動指定時に使用）")]
    public Text[] optionTexts;

    [Tooltip("タイトル/見出しテキスト (任意)")]
    public Text titleText;

    [Tooltip("スキルの説明用テキスト（レガシー Text。AutoCreateUI / 旧Prefab用）")]
    public Text explainText;

    [Tooltip("スキルの説明用テキスト（TMP_Text。新Prefabで TextMeshProUGUI を使う場合はこちらを設定）")]
    [SerializeField] private TMP_Text _explainTextTMP;

    [Tooltip("見出し文字列")]
    public string titleString = "Select a reward";

    [Header("タブ")]
    [SerializeField] private Button _roguelikeTabButton;
    [SerializeField] private Button _itemTabButton;

    [Header("コンテンツ領域")]
    [Tooltip("ROGUELIKE タブのコンテンツ（ScrollView ルートなど）")]
    [SerializeField] private GameObject _roguelikeTabContent;
    [Tooltip("ITEM タブのスタブパネル（準備中表示）")]
    [SerializeField] private GameObject _itemTabStub;
    [Tooltip("動的ボタン生成先 Content RectTransform（Prefab 設定時のみ）")]
    [SerializeField] private RectTransform _scrollContentPrefab;

    [Header("プレビュー")]
    [SerializeField] private RawImage _previewRawImage;
    [SerializeField] private Image _previewFallbackImage;
    [SerializeField] private RoguelikePreviewRegistry _previewRegistry;

    [Header("タブボタン画像")]
    [Tooltip("選択中タブに使うスプライト")]
    [SerializeField] private Sprite _tabActiveSprite;
    [Tooltip("未選択タブに使うスプライト")]
    [SerializeField] private Sprite _tabInactiveSprite;

    [Header("タブボタン テキストカラー")]
    [Tooltip("選択中タブのテキスト色")]
    [SerializeField] private Color _tabActiveTextColor = new Color(0.1f, 0.07f, 0.02f, 1f);
    [Tooltip("未選択タブのテキスト色")]
    [SerializeField] private Color _tabInactiveTextColor = Color.white;

    [Header("タブボタン TMPマテリアル")]
    [Tooltip("選択中タブのテキストマテリアル（枠線・テクスチャなど）。null なら色のみ切り替え")]
    [SerializeField] private Material _tabActiveMaterial;
    [Tooltip("未選択タブのテキストマテリアル（枠線・テクスチャなど）。null なら色のみ切り替え")]
    [SerializeField] private Material _tabInactiveMaterial;

    [Header("スキル選択ボタン画像")]
    [Tooltip("ホバーしていない通常状態のスプライト")]
    [SerializeField] private Sprite _skillButtonNormalSprite;
    [Tooltip("ホバー中のスプライト")]
    [SerializeField] private Sprite _skillButtonHoverSprite;

    [Header("スキルボタン テキストカラー")]
    [Tooltip("通常時のテキスト色")]
    [SerializeField] private Color _skillButtonNormalTextColor = Color.white;
    [Tooltip("ホバー中のテキスト色")]
    [SerializeField] private Color _skillButtonHoverTextColor = new Color(0.1f, 0.07f, 0.02f, 1f);

    [Header("スキルボタン サイズ・レイアウト")]
    [Tooltip("ボタン1個の高さ（px）")]
    [SerializeField] private float _skillButtonHeight = 72f;
    [Tooltip("ボタン間のスペース（px）")]
    [SerializeField] private float _skillButtonSpacing = 8f;
    [Tooltip("リスト上下の余白（px）")]
    [SerializeField] private int _skillButtonPaddingVertical = 8;
    [Tooltip("リスト左右の余白（px）")]
    [SerializeField] private int _skillButtonPaddingHorizontal = 8;
    [Tooltip("ボタンラベルのフォントサイズ")]
    [SerializeField] private int _skillButtonFontSize = 28;
    [Tooltip("スキルボタンのラベルに使うフォント（null = デフォルト）")]
    [SerializeField] private Font _skillButtonFont;

    [Header("スキルボタン TMPマテリアル")]
    [Tooltip("TMPフォントアセット（設定すると縁・テクスチャ等が使用可能になる）")]
    [SerializeField] private TMP_FontAsset _skillButtonTMPFont;
    [Tooltip("通常状態のTMPマテリアル（縁・テクスチャ等を設定したもの。null = フォントのデフォルト）")]
    [SerializeField] private Material _skillButtonNormalMaterial;
    [Tooltip("ホバー状態のTMPマテリアル（縁・テクスチャ等を設定したもの。null = 通常と同じ）")]
    [SerializeField] private Material _skillButtonHoverMaterial;

    [Header("サウンド")]
    [Tooltip("タブ切り替え・スキルボタンホバー時の効果音 AudioSource（未設定時は自動生成）")]
    [SerializeField] private AudioSource _uiAudioSource;
    [Tooltip("UI 操作音のクリップ群。複数設定するとランダムに選ばれる")]
    [SerializeField] private AudioClip[] _uiKeySoundClips;
    [Tooltip("UI 操作音の音量")]
    [Range(0f, 2f)]
    [SerializeField] private float _uiKeySoundVolume = 1f;
    [Tooltip("スキルボタン押下瞬間の効果音（ホバー音とは別の、より鋭いキー音）")]
    [SerializeField] private AudioClip _skillPressClip;
    [Range(0f, 2f)]
    [SerializeField] private float _skillPressVolume = 1f;
    [Tooltip("スキル確定時の効果音（タイプライターのカーリッジリターンベル音）")]
    [SerializeField] private AudioClip _skillConfirmClip;
    [Range(0f, 2f)]
    [SerializeField] private float _skillConfirmVolume = 1f;

    [Header("確定エフェクト - シェイク")]
    [Tooltip("確定時のUIシェイク強度（px単位。Screen Space Cameraでは20〜30程度が目安）")]
    [SerializeField] private float _confirmShakeStrength = 25f;
    [Tooltip("確定時のUIシェイク時間（秒）。この後 Hide が呼ばれる）")]
    [SerializeField] private float _confirmShakeDuration = 0.35f;

    [Header("確定エフェクト - フラッシュ")]
    [SerializeField] private Color _flashColor = new Color(1f, 0.97f, 0.85f, 1f);
    [SerializeField, Range(0.3f, 1.5f)] private float _flashStartScale = 0.9f;
    [SerializeField, Range(1f, 5f)] private float _flashEndScale = 1.55f;
    [SerializeField, Range(0.05f, 1f)] private float _flashDuration = 0.22f;

    [Header("確定エフェクト - グロー")]
    [SerializeField] private Color _burstColor = new Color(1f, 0.82f, 0.15f, 1f);
    [SerializeField, Range(0.3f, 1.5f)] private float _burstStartScale = 0.82f;
    [SerializeField, Range(1f, 5f)] private float _burstEndScale = 1.9f;
    [SerializeField, Range(0.05f, 2f)] private float _burstDuration = 0.50f;

    [Header("確定エフェクト - リング")]
    [SerializeField] private Color _ringColor = new Color(1f, 0.88f, 0.35f, 1f);
    [SerializeField, Range(0.3f, 1.5f)] private float _ringStartScale = 0.88f;
    [SerializeField, Range(1f, 6f)] private float _ringEndScale = 2.1f;
    [SerializeField, Range(0.05f, 2f)] private float _ringDuration = 0.55f;

    [Header("確定エフェクト - スパーク")]
    [SerializeField] private Color _sparkColor = new Color(1f, 0.95f, 0.5f, 1f);
    [SerializeField, Range(4f, 64f)] private float _sparkSize = 22f;
    [Tooltip("スパーク開始位置：ボタン半サイズに乗じる係数（0＝中心、1＝コーナー端）")]
    [SerializeField, Range(0f, 1f)] private float _sparkStartRadius = 0.35f;
    [Tooltip("スパーク終了位置：ボタン半サイズに乗じる係数（1.75＝コーナーより外側）")]
    [SerializeField, Range(1f, 4f)] private float _sparkEndRadius = 1.75f;
    [SerializeField, Range(0.05f, 1f)] private float _sparkDuration = 0.30f;

    [Header("確定エフェクト - テクスチャ")]
    [Tooltip("角丸の半径比率（テクスチャ高さに対する割合）。大きいほど丸くなる")]
    [SerializeField, Range(0.05f, 0.5f)] private float _glowCornerRatio = 0.28f;

    private static Sprite _glowSprite;

    /// <summary>タイプライター UI の表示状態が変わったときに発火する静的イベント (true=表示, false=非表示)</summary>
    public static event Action<bool> OnTypewriterUIChanged;

    /// <summary>ESC キーでUIがキャンセルされたときに発火する静的イベント</summary>
    public static event Action OnTypewriterUICancelled;

    /// <summary>タイプライター報酬選択UI が現在表示中かどうか</summary>
    public static bool IsTypewriterUIShowing { get; private set; }

    private Action<RoguelikeData> _onSelected;
    private List<RoguelikeData> _currentOptions;
    private CursorLockMode _prevLockState;
    private bool _prevCursorVisible;

    // 自動生成 UI 用
    private bool _isAutoCreated;
    private RectTransform _scrollContent;
    private readonly List<Button> _dynButtons = new List<Button>();
    private readonly List<TMP_Text> _dynTexts = new List<TMP_Text>();

    // ホバー状態管理（画像用・音用で別々に管理）
    private int _lastHoveredIndex = -1;
    private int _lastSoundedIndex = -1;

    // プレビュー用
    private VideoPlayer _videoPlayer;
    private RenderTexture _renderTexture;
    private VideoPlayer.EventHandler _onVideoPrepared;

    public List<RoguelikeData> CurrentOptions => _currentOptions;
    public bool IsActive => uiRoot != null && uiRoot.activeSelf;
    public Sprite SkillButtonNormalSprite => _skillButtonNormalSprite;
    public Sprite SkillButtonHoverSprite  => _skillButtonHoverSprite;

    /// <summary>TMP版と旧Text版どちらが設定されていても説明テキストを更新する</summary>
    public void SetExplainText(string text)
    {
        if (_explainTextTMP != null) _explainTextTMP.text = text;
        else if (explainText != null) explainText.text = text;
    }

    private void Awake()
    {
        if (_uiAudioSource == null)
        {
            _uiAudioSource = GetComponent<AudioSource>();
            if (_uiAudioSource == null)
            {
                _uiAudioSource = gameObject.AddComponent<AudioSource>();
                _uiAudioSource.playOnAwake = false;
                _uiAudioSource.spatialBlend = 0f;
            }
        }

        if (uiRoot == null) AutoCreateUI();
        if (uiRoot != null) uiRoot.SetActive(false);
        WireButtons();
        if (titleText != null) titleText.text = titleString;
        EnsureEventSystem();

        if (_roguelikeTabButton != null)
            _roguelikeTabButton.onClick.AddListener(() => { OnTabSelected(0); PlayKeySound(); });
        if (_itemTabButton != null)
            _itemTabButton.onClick.AddListener(() => { OnTabSelected(1); PlayKeySound(); });
    }

    private void OnDestroy()
    {
        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }
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

        if (_scrollContentPrefab != null || _isAutoCreated)
        {
            RebuildDynamicButtons(options.Count);
            for (int i = 0; i < _dynTexts.Count; i++)
                if (_dynTexts[i] != null) _dynTexts[i].text = options[i].skillName;
        }
        else
        {
            if (optionButtons == null || optionTexts == null)
            {
                Debug.LogWarning("[RewardSelectionUI] Show: _scrollContentPrefab も optionButtons も未設定です。Inspector で設定してください");
                _currentOptions = null;
                _onSelected = null;
                return;
            }
            for (int i = 0; i < optionButtons.Length; i++)
            {
                bool has = i < options.Count;
                if (optionButtons[i] != null) optionButtons[i].gameObject.SetActive(has);
                if (has && optionTexts[i] != null) optionTexts[i].text = options[i].skillName;
            }
        }

        _lastHoveredIndex = -1;
        _lastSoundedIndex = -1;
        OnTabSelected(0);
        StopPreview();
        SetExplainText("");
        if (titleText != null) titleText.text = titleString;
        if (worldCamera != null) worldCamera.enabled = true;
        ApplyTargetDisplay();
        uiRoot.SetActive(true);
        IsTypewriterUIShowing = true;
        OnTypewriterUIChanged?.Invoke(true);

        _prevLockState = Cursor.lockState;
        _prevCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log($"[RewardSelectionUI] Show: options=[{string.Join(" | ", options)}]", this);
    }

    /// <summary>タブ切り替え（0=ROGUELIKE, 1=ITEM）</summary>
    public void OnTabSelected(int tabIndex)
    {
        bool isRoguelike = tabIndex == 0;

        // タブボタンの画像切り替え
        UpdateTabSprite(_roguelikeTabButton, isRoguelike);
        UpdateTabSprite(_itemTabButton, !isRoguelike);

        if (_roguelikeTabContent != null) _roguelikeTabContent.SetActive(isRoguelike);
        if (_itemTabStub != null) _itemTabStub.SetActive(!isRoguelike);
        if (!isRoguelike)
        {
            StopPreview();
            SetExplainText("");
        }
    }

    private void UpdateTabSprite(Button tab, bool isActive)
    {
        if (tab == null) return;
        var sprite = isActive ? _tabActiveSprite : _tabInactiveSprite;
        if (sprite != null)
        {
            var img = tab.GetComponent<Image>();
            if (img != null) img.sprite = sprite;
        }
        var tmp = tab.GetComponentInChildren<TMP_Text>();
        if (tmp != null)
        {
            tmp.color = isActive ? _tabActiveTextColor : _tabInactiveTextColor;
            var mat = isActive ? _tabActiveMaterial : _tabInactiveMaterial;
            if (mat != null) tmp.fontMaterial = mat;
        }
        else
        {
            var leg = tab.GetComponentInChildren<Text>();
            if (leg != null) leg.color = isActive ? _tabActiveTextColor : _tabInactiveTextColor;
        }
    }

    /// <summary>
    /// スキルボタンのホバー状態を管理する（ButtonHover から呼ばれる）。
    /// 前回ホバーしたボタンをノーマルに戻し、新しいボタンをホバー画像に切り替える。
    /// </summary>
    public void OnSkillButtonHover(int index)
    {
        // 音：前回と異なるボタン or カーソルが一度離れた後の再ホバー
        if (_lastSoundedIndex != index)
        {
            PlayKeySound();
            _lastSoundedIndex = index;
        }

        // 前のボタンをノーマルに戻す（別ボタンに移動した時のみ）
        if (_lastHoveredIndex >= 0 && _lastHoveredIndex != index
            && _lastHoveredIndex < _dynButtons.Count)
        {
            var prevBtn = _dynButtons[_lastHoveredIndex];
            if (prevBtn != null)
            {
                if (_skillButtonNormalSprite != null)
                { var prevImg = prevBtn.GetComponent<Image>(); if (prevImg != null) prevImg.sprite = _skillButtonNormalSprite; }
                SetButtonTextColor(prevBtn, _skillButtonNormalTextColor);
                SetButtonMaterial(prevBtn, _skillButtonNormalMaterial);
            }
        }

        _lastHoveredIndex = index;

        // 新しいボタンをホバー画像に
        if (index >= 0 && index < _dynButtons.Count)
        {
            var btn = _dynButtons[index];
            if (btn != null)
            {
                if (_skillButtonHoverSprite != null)
                { var img = btn.GetComponent<Image>(); if (img != null) img.sprite = _skillButtonHoverSprite; }
                SetButtonTextColor(btn, _skillButtonHoverTextColor);
                SetButtonMaterial(btn, _skillButtonHoverMaterial != null ? _skillButtonHoverMaterial : _skillButtonNormalMaterial);
            }
        }
    }

    private void SetButtonMaterial(Button btn, Material mat)
    {
        if (mat == null) return;
        var tmp = btn.GetComponentInChildren<TMP_Text>();
        if (tmp != null) tmp.fontMaterial = mat;
    }

    private void SetButtonTextColor(Button btn, Color color)
    {
        var tmp = btn.GetComponentInChildren<TMP_Text>();
        if (tmp != null) { tmp.color = color; return; }
        var leg = btn.GetComponentInChildren<Text>();
        if (leg != null) leg.color = color;
    }

    /// <summary>カーソルがボタン領域を外れた時に ButtonHover から呼ばれる。音状態をリセットして再ホバー時に音が鳴るようにする。</summary>
    public void OnSkillButtonExit(int index)
    {
        if (_lastSoundedIndex == index) _lastSoundedIndex = -1;
    }

    /// <summary>ボタン押下瞬間（ButtonHover.OnPointerDown から呼ばれる）</summary>
    public void OnSkillButtonPress(int index)
    {
        if (_skillPressClip == null || _uiAudioSource == null) return;
        _uiAudioSource.PlayOneShot(_skillPressClip, _skillPressVolume);
    }

    private void PlayKeySound()
    {
        if (_uiAudioSource == null || _uiKeySoundClips == null || _uiKeySoundClips.Length == 0) return;
        var clip = _uiKeySoundClips[UnityEngine.Random.Range(0, _uiKeySoundClips.Length)];
        if (clip == null) return;
        _uiAudioSource.PlayOneShot(clip, _uiKeySoundVolume);
    }

    /// <summary>ホバー中スキルのプレビューを表示する（ButtonHover から呼ばれる）。null で停止。</summary>
    public void ShowPreview(RoguelikeData data)
    {
        if (data == null) { StopPreview(); return; }

        var entry = _previewRegistry != null ? _previewRegistry.Get(data.id) : null;

        if (entry != null && entry.previewClip != null)
        {
            InitVideoPlayer();

            // 準備完了まで透明にして白フラッシュを防ぐ（SetActive(false)にするとVideoPlayerも止まるため透明度で制御）
            if (_previewRawImage != null)
            {
                _previewRawImage.gameObject.SetActive(true);
                _previewRawImage.color = new Color(1f, 1f, 1f, 0f);
            }
            if (_previewFallbackImage != null) _previewFallbackImage.gameObject.SetActive(false);

            // 前回の未完了ハンドラを除去
            if (_onVideoPrepared != null)
            {
                _videoPlayer.prepareCompleted -= _onVideoPrepared;
                _onVideoPrepared = null;
            }

            _videoPlayer.Stop();
            _videoPlayer.clip = entry.previewClip;

            _onVideoPrepared = (vp) =>
            {
                _videoPlayer.prepareCompleted -= _onVideoPrepared;
                _onVideoPrepared = null;
                vp.Play();
                if (_previewRawImage != null) _previewRawImage.color = Color.white;
            };
            _videoPlayer.prepareCompleted += _onVideoPrepared;
            _videoPlayer.Prepare();
        }
        else if (entry != null && entry.fallbackSprite != null)
        {
            if (_videoPlayer != null) _videoPlayer.Stop();
            if (_previewRawImage != null) _previewRawImage.gameObject.SetActive(false);
            if (_previewFallbackImage != null)
            {
                _previewFallbackImage.sprite = entry.fallbackSprite;
                _previewFallbackImage.gameObject.SetActive(true);
            }
        }
        else
        {
            StopPreview();
        }
    }

    private void StopPreview()
    {
        if (_onVideoPrepared != null && _videoPlayer != null)
        {
            _videoPlayer.prepareCompleted -= _onVideoPrepared;
            _onVideoPrepared = null;
        }
        if (_videoPlayer != null) _videoPlayer.Stop();
        if (_previewRawImage != null)
        {
            _previewRawImage.color = Color.white;
            _previewRawImage.gameObject.SetActive(false);
        }
        if (_previewFallbackImage != null) _previewFallbackImage.gameObject.SetActive(false);
    }

    private void InitVideoPlayer()
    {
        if (_videoPlayer != null) return;
        if (_previewRawImage == null) return;
        _renderTexture = new RenderTexture(640, 360, 0);
        // 黒で初期化して未描画フレームの白を防ぐ
        var prevRT = RenderTexture.active;
        RenderTexture.active = _renderTexture;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = prevRT;

        _videoPlayer = _previewRawImage.gameObject.AddComponent<VideoPlayer>();
        _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _videoPlayer.targetTexture = _renderTexture;
        _videoPlayer.isLooping = true;
        _videoPlayer.playOnAwake = false;
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        _previewRawImage.texture = _renderTexture;
        // prepareCompleted は ShowPreview() 側で都度登録する
    }

    private void RebuildDynamicButtons(int count)
    {
        var target = _scrollContentPrefab != null ? _scrollContentPrefab : _scrollContent;
        if (target == null) return;

        // Content に VerticalLayoutGroup がなければ自動追加
        var vlg = target.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            vlg = target.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
        }
        // Inspector で変更した値を毎回反映する
        vlg.spacing = _skillButtonSpacing;
        vlg.padding = new RectOffset(_skillButtonPaddingHorizontal, _skillButtonPaddingHorizontal,
                                     _skillButtonPaddingVertical,   _skillButtonPaddingVertical);
        // ContentSizeFitter がなければ自動追加（縦スクロール対応）
        var csf = target.GetComponent<ContentSizeFitter>();
        if (csf == null)
        {
            csf = target.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        // Content のアンカーを上端固定・下方向に伸びる設定に
        target.anchorMin = new Vector2(0f, 1f);
        target.anchorMax = new Vector2(1f, 1f);
        target.pivot     = new Vector2(0.5f, 1f);

        foreach (var btn in _dynButtons)
            if (btn != null) Destroy(btn.gameObject);
        _dynButtons.Clear();
        _dynTexts.Clear();

        for (int i = 0; i < count; i++)
        {
            var btnGo = new GameObject($"DynOption{i}", typeof(Image), typeof(Button));
            btnGo.transform.SetParent(target, false);

            var btnRect = (RectTransform)btnGo.transform;
            btnRect.sizeDelta = new Vector2(0, _skillButtonHeight);

            var le = btnGo.AddComponent<LayoutElement>();
            le.preferredHeight = _skillButtonHeight;
            le.flexibleHeight = 0f;

            var img = btnGo.GetComponent<Image>();
            var btn = btnGo.GetComponent<Button>();
            btn.targetGraphic = img;

            if (_skillButtonNormalSprite != null)
            {
                // スプライット使用時：白で表示、色変化はButtonHoverで管理
                img.sprite = _skillButtonNormalSprite;
                img.color = Color.white;
                btn.transition = Selectable.Transition.None;
            }
            else
            {
                // スプライット未設定時：デフォルトカラー
                img.color = new Color(0.25f, 0.28f, 0.45f, 1f);
                var colors = btn.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.6f, 0.8f, 1f, 1f);
                colors.pressedColor = new Color(0.35f, 0.55f, 1f, 1f);
                btn.colors = colors;
            }

            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(btnGo.transform, false);
            var txtRect = (RectTransform)txtGo.transform;
            txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = new Vector2(24, 8); txtRect.offsetMax = new Vector2(-24, -8);
            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            if (_skillButtonTMPFont != null) txt.font = _skillButtonTMPFont;
            if (_skillButtonNormalMaterial != null) txt.fontMaterial = _skillButtonNormalMaterial;
            txt.fontSize = _skillButtonFontSize;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = _skillButtonNormalTextColor;
            txt.enableWordWrapping = false;
            txt.overflowMode = TextOverflowModes.Ellipsis;

            int idx = i;
            btn.onClick.AddListener(() => OnOptionClicked(idx));

            var hover = btnGo.AddComponent<ButtonHover>();
            hover.RewardSelectionUI = this;
            hover.RewardIndex = i;

            _dynButtons.Add(btn);
            _dynTexts.Add(txt as TMP_Text);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(target);
    }

    private void ApplyTargetDisplay()
    {
        if (uiRoot == null) return;
        var canvas = uiRoot.GetComponent<Canvas>();
        if (canvas == null) canvas = uiRoot.GetComponentInChildren<Canvas>(true);
        if (canvas == null) return;

        // worldCamera が明示指定されており有効な場合のみ使用する。無効なら自動検索→Camera.main と順にフォールバック。
        Camera cam = (worldCamera != null && worldCamera.isActiveAndEnabled) ? worldCamera : null;
        if (cam == null) cam = FindCameraForDisplay(targetDisplay);
        if (cam == null) cam = Camera.main;

        if (cam != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = planeDistance;
            Debug.Log($"[RewardSelectionUI] ScreenSpaceCamera mode, camera={cam.name}", this);
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = targetDisplay;
            Debug.LogWarning($"[RewardSelectionUI] targetDisplay={targetDisplay} に一致するカメラが見つかりません", this);
        }
    }

    private static Camera FindCameraForDisplay(int display)
    {
        foreach (var c in Camera.allCameras)
        {
            if (c == null || !c.enabled) continue;
            if (c.targetDisplay == display) return c;
        }
        return null;
    }

    private void Update()
    {
        if (IsActive && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            OnTypewriterUICancelled?.Invoke();
            Hide();
        }
    }

    public void Hide()
    {
        if (uiRoot != null) uiRoot.SetActive(false);
        if (worldCamera != null && worldCamera != Camera.main) worldCamera.enabled = false;
        Cursor.lockState = _prevLockState;
        Cursor.visible = _prevCursorVisible;
        _onSelected = null;
        _currentOptions = null;
        _lastHoveredIndex = -1;
        _lastSoundedIndex = -1;
        StopPreview();
        IsTypewriterUIShowing = false;
        OnTypewriterUIChanged?.Invoke(false);
    }

    private void OnOptionClicked(int index)
    {
        if (_currentOptions == null || index < 0 || index >= _currentOptions.Count)
        {
            Debug.LogWarning($"[RewardSelectionUI] OnOptionClicked index={index} だがコンテキスト無効", this);
            return;
        }
        SetExplainText("");
        RoguelikeData picked = _currentOptions[index];
        Debug.Log($"[RewardSelectionUI] OnOptionClicked: index={index} text=\"{picked}\"", this);

        // ① 確定音（即再生）
        if (_skillConfirmClip != null && _uiAudioSource != null)
            _uiAudioSource.PlayOneShot(_skillConfirmClip, _skillConfirmVolume);

        // ② 選択ボタンの押し込み演出 ＋ 確定エフェクト再生
        var root = uiRoot != null ? uiRoot.transform : transform;
        if (index < _dynButtons.Count)
        {
            var btn = _dynButtons[index];
            btn.transform.DOKill();
            // 外側に膨らむと ScrollView でクリップされるため、内側への押し込みのみ
            btn.transform.DOPunchScale(Vector3.one * -0.06f, 0.25f, 4, 0.5f);

            var canvas = root.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var canvasRT = (RectTransform)canvas.transform;
                var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, btn.transform.position);
                var btnRT = btn.GetComponent<RectTransform>();
                var btnSize = btnRT != null ? new Vector2(btnRT.rect.width, btnRT.rect.height) : new Vector2(300f, 72f);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenPos, cam, out Vector2 localPos))
                    SpawnConfirmEffect(localPos, canvas.transform, btnSize);
            }
        }

        // ③ UI全体シェイク → 完了後に Hide + callback（二重呼び出し防止のため先にクリア）
        var cb = _onSelected;
        _onSelected = null;
        root.DOShakePosition(_confirmShakeDuration, _confirmShakeStrength, 12, 90f, false, true)
            .OnComplete(() => { Hide(); cb?.Invoke(picked); });
    }

    private void SpawnConfirmEffect(Vector2 canvasLocalPos, Transform canvasTransform, Vector2 buttonSize)
    {
        if (_glowSprite == null) _glowSprite = BuildCircleSprite(64);

        // ボタンの縦横比に合わせた角丸矩形スプライトを動的生成
        int th = 64;
        int tw = Mathf.Max(4, Mathf.RoundToInt(th * buttonSize.x / buttonSize.y));
        Texture2D glowTex = BuildRoundedRectGlowTex(tw, th, _glowCornerRatio);
        Texture2D ringTex = BuildRoundedRectRingTex(tw, th, _glowCornerRatio);
        Sprite glowRectSpr = Sprite.Create(glowTex, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f));
        Sprite ringRectSpr = Sprite.Create(ringTex, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f));

        // ルートをボタンサイズで配置
        var root = new GameObject("SkillConfirmEffect", typeof(RectTransform));
        root.transform.SetParent(canvasTransform, false);
        var rootRT = (RectTransform)root.transform;
        rootRT.anchoredPosition = canvasLocalPos;
        rootRT.sizeDelta = buttonSize;
        rootRT.SetAsLastSibling();

        // 矩形レイヤー：ルートを満たして広がる
        Image MakeRectLayer(string name, Sprite spr, Color color, float startScale)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.localScale = Vector3.one * startScale;
            var img = go.AddComponent<Image>();
            img.sprite = spr;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        // スパーク：固定円サイズ、ボタン四隅から飛ぶ
        Image MakeSpark(Vector2 startPos)
        {
            var go = new GameObject("Spark", typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(_sparkSize, _sparkSize);
            rt.anchoredPosition = startPos;
            var img = go.AddComponent<Image>();
            img.sprite = _glowSprite;
            img.color = _sparkColor;
            img.raycastTarget = false;
            return img;
        }

        var seq = DOTween.Sequence();

        // 瞬間フラッシュ：ボタン形状で白く光る
        var flash = MakeRectLayer("Flash", glowRectSpr, _flashColor, _flashStartScale);
        seq.Join(flash.rectTransform.DOScale(_flashEndScale, _flashDuration).SetEase(Ease.OutCubic));
        seq.Join(flash.DOFade(0f, _flashDuration).SetEase(Ease.OutCubic));

        // 金色グロー：ボタン形状のまま外側へ広がる
        var burst = MakeRectLayer("Burst", glowRectSpr, _burstColor, _burstStartScale);
        seq.Insert(0.02f, burst.rectTransform.DOScale(_burstEndScale, _burstDuration).SetEase(Ease.OutCubic));
        seq.Insert(0.02f, burst.DOFade(0f, _burstDuration).SetEase(Ease.Linear));

        // 角丸矩形リング：ボタン輪郭から外側に拡散
        var ring = MakeRectLayer("Ring", ringRectSpr, _ringColor, _ringStartScale);
        seq.Insert(0.03f, ring.rectTransform.DOScale(_ringEndScale, _ringDuration).SetEase(Ease.OutCubic));
        seq.Insert(0.03f, ring.DOFade(0f, _ringDuration).SetEase(Ease.Linear));

        // 四隅スパーク：ボタンコーナーから斜め外側へ飛ぶ
        float hw = buttonSize.x * 0.45f;
        float hh = buttonSize.y * 0.45f;
        Vector2[] corners = {
            new Vector2(-hw, -hh), new Vector2(hw, -hh),
            new Vector2(-hw,  hh), new Vector2(hw,  hh)
        };
        foreach (var corner in corners)
        {
            var spark = MakeSpark(corner * _sparkStartRadius);
            seq.Insert(0.03f, spark.rectTransform.DOAnchorPos(corner * _sparkEndRadius, _sparkDuration).SetEase(Ease.OutCubic));
            seq.Insert(0.03f, spark.DOFade(0f, _sparkDuration).SetEase(Ease.InCubic));
        }

        seq.OnComplete(() =>
        {
            if (root != null) Destroy(root);
            Destroy(glowTex); Destroy(ringTex);
            Destroy(glowRectSpr); Destroy(ringRectSpr);
        });
    }

    private static Sprite BuildCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var pix = new Color32[size * size];
        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                a = a * a;
                pix[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(pix);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static float SdRoundedRect(float px, float py, float bx, float by, float r)
    {
        float qx = Mathf.Abs(px) - bx + r;
        float qy = Mathf.Abs(py) - by + r;
        float outer = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
        return outer + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    private static Texture2D BuildRoundedRectGlowTex(int tw, int th, float cornerRatio)
    {
        var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var pix = new Color32[tw * th];
        float cx = tw * 0.5f, cy = th * 0.5f;
        float r  = th * cornerRatio;
        float bx = tw * 0.5f - r, by = th * 0.5f - r;
        float glowRange = th * 0.28f;
        for (int y = 0; y < th; y++)
            for (int x = 0; x < tw; x++)
            {
                float sdf = SdRoundedRect(x - cx, y - cy, bx, by, r);
                float a   = Mathf.Clamp01(1f - sdf / glowRange);
                a = Mathf.Sqrt(a);
                pix[y * tw + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(pix);
        tex.Apply();
        return tex;
    }

    private static Texture2D BuildRoundedRectRingTex(int tw, int th, float cornerRatio)
    {
        var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var pix = new Color32[tw * th];
        float cx = tw * 0.5f, cy = th * 0.5f;
        float r  = th * cornerRatio;
        float bx = tw * 0.5f - r, by = th * 0.5f - r;
        float ringWidth = th * 0.12f;
        for (int y = 0; y < th; y++)
            for (int x = 0; x < tw; x++)
            {
                float sdf = SdRoundedRect(x - cx, y - cy, bx, by, r);
                float a   = 1f - Mathf.Abs(sdf) / ringWidth;
                a = Mathf.Clamp01(a);
                a = Mathf.Sqrt(a);
                pix[y * tw + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(pix);
        tex.Apply();
        return tex;
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

        var scrollBgGo = new GameObject("ScrollBg", typeof(Image));
        scrollBgGo.transform.SetParent(canvasGo.transform, false);
        var scrollBgRect = (RectTransform)scrollBgGo.transform;
        scrollBgRect.anchorMin = new Vector2(0.2f, 0.22f);
        scrollBgRect.anchorMax = new Vector2(0.8f, 0.75f);
        scrollBgRect.offsetMin = Vector2.zero;
        scrollBgRect.offsetMax = Vector2.zero;
        scrollBgGo.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.1f, 0.95f);

        var scrollGo = new GameObject("ScrollView", typeof(ScrollRect));
        scrollGo.transform.SetParent(canvasGo.transform, false);
        var scrollRect = (RectTransform)scrollGo.transform;
        scrollRect.anchorMin = new Vector2(0.2f, 0.22f);
        scrollRect.anchorMax = new Vector2(0.8f, 0.75f);
        scrollRect.offsetMin = Vector2.zero;
        scrollRect.offsetMax = Vector2.zero;

        var viewportGo = new GameObject("Viewport", typeof(RectMask2D));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        var viewportRect = (RectTransform)viewportGo.transform;
        viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero; viewportRect.offsetMax = Vector2.zero;

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
        vlg.childControlHeight = true;

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
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule))
            .transform.SetParent(transform, false);
    }
}
