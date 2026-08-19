using System.Collections;
using System.Collections.Generic;
using System.Linq;
using App.Intro;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UniRx;
using UnityEngine.UI;


public class GameUIManager : MonoBehaviour,ILanguage
{
    public static GameUIManager Instance { get; private set; }

    /// <summary>通常のMoneyCount表示（HUD側）。チュートリアルのハイライト対象などInspectorで直接ドラッグ
    /// できない場面はこちら経由で取得する</summary>
    public RectTransform MoneyTextRect => _moneyText != null ? _moneyText.rectTransform : null;

    /// <summary>playerInfoPanel内の「差し引き後MoneyCount」表示。実行時（playerInfoPanel表示中）にのみ実体が確定するため、
    /// チュートリアルのハイライト対象などInspectorで直接ドラッグできない場面はこちら経由で取得する</summary>
    public RectTransform MoneyTextInfoRect => _moneyText_info != null ? _moneyText_info.rectTransform : null;

    [Header("表示制御")]
    [Tooltip("GameUI全体の表示切り替えに使用するCanvasGroup。未設定なら自動取得")]
    [SerializeField] private CanvasGroup _canvasGroup;
    private bool _isGameUIVisible = true;
    public bool IsGameUIVisible => _isGameUIVisible;
    private void Awake()
    {
        if (Instance == null) Instance = this;
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
    }

    [Header("ゲーム内のオブジェクト")]
    [SerializeField] private TMP_Text _turnText;
    [SerializeField] private TMP_Text _nextDebtTurnText;
    [SerializeField] private TMP_Text _moneyText;
    [SerializeField] private TMP_Text _unwashedMoneyText;

    [Header("playerInfoPanel内のオブジェクト")]
    [SerializeField] private GameObject _playerInfoPanel;
    [SerializeField] private TMP_Text _moneyText_info;
    [SerializeField] private TMP_Text _unwashedMoneyText_info;
    [SerializeField] private TMP_Text _bronzeCoinText_info;
    [SerializeField] private TMP_Text _silverCoinText_info;
    [SerializeField] private TMP_Text _goldCoinText_info;
    [SerializeField] private TMP_Text _blackDiamondText_info;
    [SerializeField] private TMP_Text _virtuePointText;
    [SerializeField] private TMP_Text _playerNameText;
    [SerializeField] private TMP_Text _leftDebtMoneyText;
    [SerializeField] private TMP_Text _nextQuotaText;

    [Header("itemPanel内のオブジェクト")]
    [SerializeField] private GameObject _itemPanel;

    [Header("roguelikePanel内のオブジェクト")]
    [SerializeField] private GameObject _roguelikePanel;

    [Header("UnwashCoinの個別UI")]
    [SerializeField] private TMP_Text _bronzeCoinText;
    [SerializeField] private TMP_Text _silverCoinText;
    [SerializeField] private TMP_Text _goldCoinText;

    [Tooltip("UnwashCoin内の黒ダイヤ行(磨き段階0:呪われた〜3:ゴッドの順)。磨き段階に応じて1つだけ表示し、他は非表示にします")]
    [SerializeField] private GameObject[] _blackDiamondStageObjects;

    [Header("報酬フォーカス演出（Drawer Camera Viewpoint用）")]
    [Tooltip("フォーカス中に隠したいUI（お金のテキストなど、UnwashCoin以外の常時表示UI）")]
    [SerializeField] private List<GameObject> _hudElementsToHideDuringRewardFocus = new List<GameObject>();
    [Tooltip("UnwashCoinの4つのテキストをまとめている親（レイアウトグループ等）のRectTransform")]
    [SerializeField] private RectTransform _unwashCoinGroupContainer;
    [Tooltip("フォーカス中、UnwashCoinグループ全体を何倍の大きさにするか")]
    [SerializeField] private float _rewardFocusScale = 2.5f;
    [Tooltip("フォーカス中、UnwashCoinグループ全体を移動させる先の座標（anchoredPosition基準。画面中央など）")]
    [SerializeField] private Vector2 _unwashCoinGroupFocusPosition;

    private Vector3 _unwashCoinGroupOriginalScale;
    private Vector2 _unwashCoinGroupOriginalPosition;
    private bool _isRewardFocusActive = false;

    [Header("tutorial_canvas / Play_canvas / Play2_canvas 閲覧中のフォーカス表示")]
    [Tooltip("報酬フォーカスと同じく _hudElementsToHideDuringRewardFocus を隠すが、拡大・移動はせず等倍のまま表示する。" +
             "そのうえで、この閲覧中だけ改めて表示しておきたい要素（Moneyの表示など）")]
    [SerializeField] private List<GameObject> _hudElementsToShowWhileBrowsing = new List<GameObject>();


    [Header("メニュー用のSettings")]
    [SerializeField] private Language _language;
    [SerializeField] private InputActionReference _openMenuReference;//Tabキーを押したらメニュー表示
    [SerializeField] private SerializeDictionary<int, GameObject> _menuTitle = new SerializeDictionary<int, GameObject>();
    private MouseHoverOutline[] _mouseHoverOutlines;
    private bool _isOpenMenu = false;

    /// <summary>Tabメニューが開いているか。ホバー判定などの抑止条件として参照される。</summary>
    public static bool IsMenuOpen => Instance != null && Instance._isOpenMenu;
    private int _index = 0;

    [Header("アイテム取得・消失用UI")]
    [SerializeField] private GameObject _getOrLostPanel;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private List<GameObject> _popUpListIndex;

    private  Queue<ScriptableObject> _getQueue = new();
    private  Queue<ScriptableObject> _lostQueue = new();

    private Coroutine _getOrLostAnimationCoroutine;

    //その他プライベート変数
    private float _previousMoneyValue = 0;
    private float _previousUnwashedMoneyValue = 0;
    private int _previousVirtuePointValue = 0;
    private int _previousBronzeCoinValue = 0;
    private int _previousSilverCoinValue = 0;
    private int _previousGoldCoinValue = 0;
    private int _previousBlackDiamondValue = 0;

    /// <summary>
    /// 初期化を行う処理
    /// </summary>
    /// <param name="wallet"></param>
    private void Init(PlayerWallet wallet)
    {
        _previousMoneyValue = wallet.WashedAmount;
        _previousUnwashedMoneyValue = wallet.UnwashedAmount;
        _previousVirtuePointValue = wallet.VirtuePoints;
        _previousBronzeCoinValue = wallet.BronzeCoins;
        _previousSilverCoinValue = wallet.SilverCoins;
        _previousGoldCoinValue = wallet.GoldCoins;
        _previousBlackDiamondValue = wallet.BlackDiamonds;


        //以前の数値を記録する変数の初期化
        wallet.OnWashedMoneyAmountChange
            .Subscribe(x =>
            {
                //お金の更新処理
                DOTween.To(() => _previousMoneyValue,//ターゲットとなる変数
                        num => _previousMoneyValue = num,    //値の更新を行う
                        x,                                     //最終的な値
                        1.0f                                   //時間
                        ).OnUpdate(() =>
                        {
                            _moneyText.text = _previousMoneyValue.ToString("N0");
                            _moneyText_info.text = _previousMoneyValue.ToString("N0");
                        }); 
            }).AddTo(this);

        wallet.OnUnwashedMoneyAmountChange
            .Subscribe(x =>
            {
                //未洗浄のお金の更新処理
                DOTween.To(() => _previousUnwashedMoneyValue,//ターゲットとなる変数
                        num => _previousUnwashedMoneyValue = num,    //値の更新を行う
                        x,                                     //最終的な値
                        1.0f                                   //時間
                        ).OnUpdate(() =>
                        {
                            //_unwashedMoneyText.text = _previousUnwashedMoneyValue.ToString("N0");
                            //_unwashedMoneyText_info.text = _previousUnwashedMoneyValue.ToString("N0");
                        });
            }).AddTo(this);

        wallet.OnvirtuePointAmountChange
            .Subscribe(x =>
            {
                //恒常ポイントの更新処理
                DOTween.To(() => _previousVirtuePointValue,//ターゲットとなる変数
                        num => _previousVirtuePointValue = num,    //値の更新を行う
                        x,                                     //最終的な値
                        1.0f                                   //時間
                        ).OnUpdate(() =>
                        {
                            _virtuePointText.text = _previousVirtuePointValue.ToString("N0");
                        });
            }).AddTo(this);

        bool isBronzeInitialized = false;
        wallet.OnBronzeCoinsChange
            .Subscribe(x =>
            {
                if (!isBronzeInitialized)
                {
                    _previousBronzeCoinValue = x;
                    isBronzeInitialized = true;
                    if (_bronzeCoinText != null)
                    {
                        _bronzeCoinText.text = "x" + x.ToString("N0");
                        _bronzeCoinText_info.text = "x" + x.ToString("N0");
                    }
                    
                    return;
                }

                if (_bronzeCoinText != null)
                {
                    DOTween.To(() => _previousBronzeCoinValue,
                        num => _previousBronzeCoinValue = num,
                        x,
                        1.0f
                    ).OnUpdate(() =>
                    {
                        _bronzeCoinText.text = "x" + _previousBronzeCoinValue.ToString("N0");
                        _bronzeCoinText_info.text = "x" + _previousBronzeCoinValue.ToString("N0");
                    });
                }
            }).AddTo(this);

        bool isSilverInitialized = false;
        wallet.OnSilverCoinsChange
            .Subscribe(x =>
            {
                if (!isSilverInitialized)
                {
                    _previousSilverCoinValue = x;
                    isSilverInitialized = true;
                    if (_silverCoinText != null)
                    {
                        _silverCoinText.text = "x" + x.ToString("N0");
                        _silverCoinText_info.text = "x" + x.ToString("N0");
                    } 
                    return;
                }

                if (_silverCoinText != null)
                {
                    DOTween.To(() => _previousSilverCoinValue,
                        num => _previousSilverCoinValue = num,
                        x,
                        1.0f
                    ).OnUpdate(() =>
                    {
                        _silverCoinText.text = "x" + _previousSilverCoinValue.ToString("N0");
                        _silverCoinText_info.text = "x" + _previousSilverCoinValue.ToString("N0");
                    });
                }
            }).AddTo(this);

        bool isGoldInitialized = false;
        wallet.OnGoldCoinsChange
            .Subscribe(x =>
            {
                if (!isGoldInitialized)
                {
                    _previousGoldCoinValue = x;
                    isGoldInitialized = true;
                    if (_goldCoinText != null)
                    {
                        _goldCoinText.text = "x" + x.ToString("N0");
                        _goldCoinText_info.text = "x" + x.ToString("N0");
                    } 
                    return;
                }

                if (_goldCoinText != null)
                {
                    DOTween.To(() => _previousGoldCoinValue,
                        num => _previousGoldCoinValue = num,
                        x,
                        1.0f
                    ).OnUpdate(() =>
                    {
                        _goldCoinText.text = "x" + _previousGoldCoinValue.ToString("N0");
                        _goldCoinText_info.text = "x" + _previousGoldCoinValue.ToString("N0");
                    });
                }
            }).AddTo(this);

        bool isBlackDiamondInitialized = false;
        wallet.OnBlackDiamondsChange
            .Subscribe(x =>
            {
                if (!isBlackDiamondInitialized)
                {
                    _previousBlackDiamondValue = x;
                    isBlackDiamondInitialized = true;
                    SetBlackDiamondCountText(x);
                    if (_blackDiamondText_info != null) _blackDiamondText_info.text = "x" + x.ToString("N0");
                    return;
                }

                DOTween.To(() => _previousBlackDiamondValue,
                    num => _previousBlackDiamondValue = num,
                    x,
                    1.0f
                ).OnUpdate(() =>
                {
                    SetBlackDiamondCountText(_previousBlackDiamondValue);
                    if (_blackDiamondText_info != null) _blackDiamondText_info.text = "x" + _previousBlackDiamondValue.ToString("N0");
                });
            }).AddTo(this);

        MoneyManager.Instance.OnCurrentTurnChange.Subscribe(turnNum => _turnText.text = turnNum.ToString("000")).AddTo(this);
        MoneyManager.Instance.OnNextDebtCollectionTurnChange.Subscribe(turnNum => _nextDebtTurnText.text = turnNum.ToString("00")).AddTo(this);
        MoneyManager.Instance.OnQuotaAmount.Subscribe(quotaNum => _nextQuotaText.text = quotaNum.ToString("00")).AddTo(this);
        MoneyManager.Instance.OnLeftDebtAmount.Subscribe(leftDebtNum => _leftDebtMoneyText.text = leftDebtNum.ToString("00")).AddTo(this);
        Debug.Log(this.name + "初期化完了");
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _openMenuReference.action.Enable();

        //初期化処理
        Observable.EveryUpdate()
            .Select(_ => PlayerWallet.Local)
            .Where(target => target != null)
            .First()
            .Subscribe(target =>
            {
                Init(target);
            })
            .AddTo(this);

        UFOCameraController.OnUfoModeChanged += HandleUfoModeChanged;
        UFOCameraController.OnPlaySessionActiveChanged += HandlePlaySessionActiveChanged;
        RewardSelectionUI.OnTypewriterUIChanged += HandleTypewriterUIChanged;
        RoguelikeManager.OnDiamondPolishStageChanged += SetBlackDiamondStage;

        // イベントは磨き段階が「変わった」時にしか飛ばないため、起動時点の段階をここで一度反映しておく
        var roguelikeManager = FindFirstObjectByType<RoguelikeManager>();
        if (roguelikeManager != null) SetBlackDiamondStage(roguelikeManager.GetDiamondPolishStage());

        // IntroTourDirectorが存在するならOnTourFinished後に表示、なければ即表示
        // IsRunningはWaitUntilSceneReady後に立つため、ここで確認しても常にfalseになる点に注意
        var tourDirector = FindFirstObjectByType<IntroTourDirector>();
        if (tourDirector != null)
        {
            SetGameUIVisible(false);
            tourDirector.OnTourFinished += () => SetGameUIVisible(true);
        }
        else
            SetGameUIVisible(true);
    }

    private void Update()
    {
        //Tabキーを押したときにメニュー切り替え
        //本の演出でTabを置き換えている間は開かない (BookOpenController)
        if (_openMenuReference.action.WasPressedThisFrame() && _canvasGroup.alpha == 1 && !BookOpenController.SuppressInfoMenu)
        {
            _isOpenMenu = !_isOpenMenu;
            if(_menuTitle.TryGetValue(_index, out var value))
                value.SetActive(_isOpenMenu);

            if (_isOpenMenu)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                _mouseHoverOutlines = FindObjectsByType<MouseHoverOutline>(FindObjectsSortMode.InstanceID);
                if(_mouseHoverOutlines.Length != 0)
                {
                    foreach(var item in _mouseHoverOutlines)
                        item.IsOpenUI = true;
                }
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _index = 0;

                if(_mouseHoverOutlines.Length != 0)
                {
                    foreach(var item in _mouseHoverOutlines)
                        item.IsOpenUI = false;
                }
            }
        }
    }

    public void SettingLanguage(Language language)
    {
        _language = language;

    }

    /// <summary>
    /// メニュー内のページを切り替えるボタンを押したときの処理
    /// </summary>
    public void ChangePageButton(string type)
    {
        if (type == "Previous")
            _index -= 1;
        else if (type == "Next")
            _index += 1;

        switch (_index)
        { 
            case 0://プレイヤー情報ページ
                _playerInfoPanel.SetActive(true);
                _itemPanel.SetActive(false);
                _roguelikePanel.SetActive(false);
                break;
            case 1://恒常アイテムの説明ページ
                _playerInfoPanel.SetActive(false);
                _itemPanel.SetActive(true);
                _roguelikePanel.SetActive(false);
                break;
            case 2://これ以降は各ミニゲームのローグライク要素を記述
                _playerInfoPanel.SetActive(false);
                _itemPanel.SetActive(false);
                _roguelikePanel.SetActive(true);
                break;
            default:
                Debug.LogError("想定外の数値");
                break;
        }
    }

    private void OnDestroy()
    {
        UFOCameraController.OnUfoModeChanged -= HandleUfoModeChanged;
        UFOCameraController.OnPlaySessionActiveChanged -= HandlePlaySessionActiveChanged;
        RewardSelectionUI.OnTypewriterUIChanged -= HandleTypewriterUIChanged;
        RoguelikeManager.OnDiamondPolishStageChanged -= SetBlackDiamondStage;
    }

    /// <summary>
    /// UnwashCoin内の黒ダイヤ行を、磨き段階に対応する1つだけ表示し、他は非表示にする
    /// </summary>
    private void SetBlackDiamondStage(int stage)
    {
        if (_blackDiamondStageObjects == null || _blackDiamondStageObjects.Length == 0) return;

        stage = Mathf.Clamp(stage, 0, _blackDiamondStageObjects.Length - 1);
        for (int i = 0; i < _blackDiamondStageObjects.Length; i++)
        {
            var go = _blackDiamondStageObjects[i];
            if (go == null) continue;

            bool isCurrent = (i == stage);
            go.SetActive(isCurrent);

            // 親のアクティブ状態の継承だけに頼らず、子(Image/Text)自身のアクティブ状態も
            // 明示的に揃える（子だけ個別のアクティブ状態を持っていて食い違うのを防ぐため）
            foreach (Transform child in go.transform)
                child.gameObject.SetActive(isCurrent);
        }

        // 表示切り替え直後も個数表示がズレないよう、今の所持数を反映しておく
        SetBlackDiamondCountText(Mathf.RoundToInt(_previousBlackDiamondValue));
    }

    /// <summary>
    /// UnwashCoin内の黒ダイヤ行(4つとも)の個数テキストを更新する。
    /// 4行それぞれが独立したTMP_Textを持つため、非表示中の行も含めて全て更新しておく
    /// （表示切り替えの前後関係に関わらず、表示された瞬間に必ず正しい数値になるようにするため）
    /// </summary>
    private void SetBlackDiamondCountText(int count)
    {
        if (_blackDiamondStageObjects == null) return;

        string text = "x" + count.ToString("N0");
        foreach (var go in _blackDiamondStageObjects)
        {
            if (go == null) continue;
            var t = go.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.text = text;
        }
    }

    private void HandleUfoModeChanged(bool isPlayingUfo)
    {
        if (isPlayingUfo)
        {
            // machine をクリックした直後（tutorial_canvas / Play_canvas / Play2_canvas を閲覧中）はまだ
            // 有料セッションが始まっていないため、GameUI自体は表示したまま、UnwashCoin以外を隠す
            // 閲覧用フォーカス表示にする（拡大・移動はせず等倍のまま）。
            // price_table 側のUIは実際にプレイが始まってから表示される。
            SetGameUIVisible(true);
            SetBrowsingFocusMode(true);
            return;
        }

        SetGameUIVisible(true);
        SetBrowsingFocusMode(false);

        // UFOキャッチャー終了時、GameUIが再表示された瞬間に通常状態が一瞬映ってしまわないよう、
        // 最初からフォーカスモード（UnwashCoin以外を隠した状態）にしておく。
        // 通常表示への復帰は、引き出しの演出が完全に終わった時点で SetRewardFocusMode(false) が呼ばれる。
        //
        // ただし、Play2_Canvasで一度もPlayを押さずに（＝有料セッションを一度も開始せずに）UFOモードを
        // 抜けた場合は、UFOItemGoal側でOpenDrawer()自体がスキップされ引き出しの演出が発生しないため、
        // SetRewardFocusMode(false) を呼ぶ機会が無い。その場合は上の SetBrowsingFocusMode(false) で
        // 既に元の表示へ戻っているため、ここでは何もしない。
        bool didStartPlaySession = UFOCameraController.Instance != null && UFOCameraController.Instance.PaymentCount > 0;
        if (didStartPlaySession) SetRewardFocusMode(true);
    }

    /// <summary>
    /// UFOCameraController.IsPlaySessionActive が変化した時に呼ばれる。
    /// Play_Canvas2 で実際に Play を押してセッションが始まった瞬間に GameUI を完全に隠し（price_table 側のUIに切り替え）、
    /// セッションが終わってもまだ UFOモード中（再び Play_Canvas を選べる状態）なら閲覧用フォーカス表示に戻す。
    /// </summary>
    private void HandlePlaySessionActiveChanged(bool isSessionActive)
    {
        ApplySessionActiveUIState(isSessionActive);
    }

    /// <summary>
    /// 実機のセッション開始/終了時と同じGameUI切り替えを行う。
    /// TutorialCraneController から、実機の UFOCameraController.IsPlaySessionActive には一切触れずに
    /// 同じ見た目のUI切り替え（Play2_tutorial の Play を押した時など）だけを行うために呼ばれる。
    /// </summary>
    public void ApplySessionActiveUIState(bool isSessionActive)
    {
        if (isSessionActive)
        {
            SetGameUIVisible(false);
            return;
        }

        if (UFOCameraController.IsPlayingUfo)
        {
            SetGameUIVisible(true);
            SetBrowsingFocusMode(true);
        }
        // UFOモードごと終了した場合は HandleUfoModeChanged 側で処理される
    }

    private void HandleTypewriterUIChanged(bool isShowing)
    {
        SetGameUIVisible(!isShowing);
    }

    private void SetGameUIVisible(bool visible)
    {
        if (_canvasGroup == null) return;
        _canvasGroup.alpha = visible ? 1f : 0f;
        _canvasGroup.interactable = visible;
        _canvasGroup.blocksRaycasts = visible;

        _isGameUIVisible = visible;
    }

    /// <summary>
    /// 外部（TutorialStepControllerのfullDarkBackgroundステップなど）からGameUI全体の表示を
    /// 切り替えるための公開API
    /// </summary>
    public void SetGameUIVisibleExternal(bool visible)
    {
        SetGameUIVisible(visible);
    }

    /// <summary>
    /// 報酬演出（Drawer Camera Viewpoint）用のフォーカスモード。
    /// true: UnwashCoin以外のHUDを隠し、UnwashCoinのテキストを拡大表示する。
    /// false: 元の表示状態に戻す。
    /// </summary>
    public void SetRewardFocusMode(bool enabled)
    {
        if (enabled == _isRewardFocusActive) return;
        _isRewardFocusActive = enabled;

        foreach (var element in _hudElementsToHideDuringRewardFocus)
        {
            if (element != null) element.SetActive(!enabled);
        }

        if (_unwashCoinGroupContainer == null) return;

        if (enabled)
        {
            _unwashCoinGroupOriginalScale = _unwashCoinGroupContainer.localScale;
            _unwashCoinGroupOriginalPosition = _unwashCoinGroupContainer.anchoredPosition;

            _unwashCoinGroupContainer.localScale = _unwashCoinGroupOriginalScale * _rewardFocusScale;
            _unwashCoinGroupContainer.anchoredPosition = _unwashCoinGroupFocusPosition;
        }
        else
        {
            _unwashCoinGroupContainer.localScale = _unwashCoinGroupOriginalScale;
            _unwashCoinGroupContainer.anchoredPosition = _unwashCoinGroupOriginalPosition;
        }
    }

    /// <summary>
    /// tutorial_canvas / Play_canvas / Play2_canvas 閲覧中（まだプレイ開始前）用のフォーカスモード。
    /// SetRewardFocusMode と同じ _hudElementsToHideDuringRewardFocus を隠すが、
    /// UnwashCoinグループの拡大・移動は行わず等倍のまま表示する。
    /// さらに _hudElementsToShowWhileBrowsing（Moneyの表示など）は、隠した後で改めて表示する。
    /// </summary>
    public void SetBrowsingFocusMode(bool enabled)
    {
        foreach (var element in _hudElementsToHideDuringRewardFocus)
        {
            if (element != null) element.SetActive(!enabled);
        }

        if (enabled)
        {
            foreach (var element in _hudElementsToShowWhileBrowsing)
            {
                if (element != null) element.SetActive(true);
            }
        }
    }

    /// <summary>
    /// Play_Canvas2 表示中、選択した秒数の消費 Devil Coins を反映して MoneyCount の表示を
    /// 「現在の所持数 → 差し引き後の所持数」に切り替える。TouchPanelOutlineController から、
    /// 30/60/90 選択時（Play_Canvas2 へ切り替わるタイミング）に呼ばれる。
    /// </summary>
    public void ShowMoneyCountPreview(float cost)
    {
        if (MoneyManager.Instance == null) return;

        float current = MoneyManager.Instance.CurrentMoney;
        float after = current - cost;
        string text = $"{current:N0} → {after:N0}";

        if (_moneyText != null) _moneyText.text = text;
        if (_moneyText_info != null) _moneyText_info.text = text;
    }

    /// <summary>
    /// Play_Canvas に戻った時、MoneyCount の表示を通常の所持数表示に戻す。
    /// </summary>
    public void ClearMoneyCountPreview()
    {
        if (MoneyManager.Instance == null) return;

        string text = MoneyManager.Instance.CurrentMoney.ToString("N0");
        if (_moneyText != null) _moneyText.text = text;
        if (_moneyText_info != null) _moneyText_info.text = text;
    }

    /// <summary>
    /// アイテム、エフェクト、お金（訪問者イベントのみ）を取得・消失した際にそれぞれキューに追加する。
    /// </summary>
    /// <param name="isGet"></param>
    /// <param name="masterData"></param>
    public void AddPopupQueue(bool isGet,ScriptableObject masterData)
    {
        if (isGet)
        {
            _getQueue.Enqueue(masterData);

            if (_getOrLostAnimationCoroutine == null)
                _getOrLostAnimationCoroutine = StartCoroutine(GetOrLostAnimation(true));
        }
        else
        {
            _lostQueue.Enqueue(masterData);

            if (_getOrLostAnimationCoroutine == null)
                _getOrLostAnimationCoroutine = StartCoroutine(GetOrLostAnimation(false));
        }
    }

    /// <summary>
    /// 取得・消失した時のUIアニメーションを再生させるコルーチン
    /// </summary>
    /// <param name="isGet"></param>
    /// <returns></returns>
    private IEnumerator GetOrLostAnimation(bool isGet)
    {
        //最初のセットアップ
        Queue<ScriptableObject> queue = isGet ? _getQueue : _lostQueue;
        _titleText.text = isGet ? "Get" : "Lost";
        _titleText.color = isGet ? Color.skyBlue : Color.red;
        _getOrLostPanel.SetActive(true);


        if (!_getOrLostPanel.TryGetComponent<CanvasGroup>(out var canvasGroup))
        {
            canvasGroup = _getOrLostPanel.AddComponent<CanvasGroup>();
        }

        //_getOrLostPanelが0.5秒かけてフェードアウト（出現）してくる
        canvasGroup.alpha = 0f;
        yield return canvasGroup.DOFade(1f, 0.5f).WaitForCompletion();

        //手に入れたアイテム・エフェクトを順にスケールアップ（0.5秒かけてスケールを1に変化）させる        
        int index = 0;
        while (true)
        {
            // キューが空なら終了(３秒だけ猶予は与える)
            if (queue.Count == 0)
            {
                // 現在表示中の内容を3秒見せる
                yield return new WaitForSeconds(3f);

                // その間に追加されなかったら一旦終了
                if (queue.Count == 0)
                {
                    Sequence disappear = DOTween.Sequence();
                    for (int i = 0; i < index; i++)
                    {
                        disappear.Insert(i * 0.1f, _popUpListIndex[i].transform.DOScale(Vector3.zero, 0.3f));
                    }
                    yield return disappear.WaitForCompletion();
                    break;
                }
            }

            // 最大件数を表示したら一旦待機して消す
            if(index == _popUpListIndex.Count)
            {
                Sequence disappear = DOTween.Sequence();
                for (int i = 0; i < _popUpListIndex.Count; i++)
                {
                    disappear.Insert(i * 0.1f, _popUpListIndex[i].transform.DOScale(Vector3.zero, 0.3f));
                }
                
                yield return disappear.WaitForCompletion();
                index = 0;
            }

            //データをもとにUI表示アニメーションを再生
            ScriptableObject data = queue.Dequeue();
            Image image = _popUpListIndex[index].transform.Find("Image").GetComponentInChildren<Image>();
            TMP_Text text = _popUpListIndex[index].GetComponentInChildren<TMP_Text>();

            if (data is ItemData itemData)
            {
                image.sprite = itemData.iconImage;
                text.text = itemData.itemName;
            }
            else if (data is EffectData effectData)
            {
                image.sprite = effectData.effectIcon;
                text.text = effectData.effectName;
            }
            else if(data is MoneyData moneyData)
            {
                image.sprite = moneyData.iconImage;
                text.text = moneyData.price.ToString("N0") + "DC";
            }

            _popUpListIndex[index].transform.localScale = Vector3.zero;
            yield return _popUpListIndex[index].transform.DOScale(Vector3.one, 0.5f).WaitForCompletion();

            index++;
        }

        //getOrLostPanelが0.5秒かけてフェードイン（消失）する
        yield return canvasGroup.DOFade(0f, 0.5f).WaitForCompletion();
        _getOrLostPanel.SetActive(false);

        //コルーチンの情報をnullにする.そしてもう片方のキューに情報があったら、そっちの情報を記載するコルーチンを発動
        if(isGet)
        {
            _getOrLostAnimationCoroutine = null;
            if(_lostQueue.Count != 0 && _getOrLostAnimationCoroutine == null)
                _getOrLostAnimationCoroutine = StartCoroutine(GetOrLostAnimation(false));
        }
        else
        {
            _getOrLostAnimationCoroutine = null;
            if(_getQueue.Count != 0 && _getOrLostAnimationCoroutine == null)
                _getOrLostAnimationCoroutine = StartCoroutine(GetOrLostAnimation(true));
        }
    }
}
