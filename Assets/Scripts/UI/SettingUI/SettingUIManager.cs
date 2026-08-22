using App.Player;
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UnityEngine.InputSystem.InputActionRebindingExtensions;
using UnityEngine.Localization.Settings;

public enum ScreenSize
{
    /// <summary>
    /// 1920×1080
    /// </summary>
    FHD,
    /// <summary>
    /// 2560×1440
    /// </summary>
    WQHD,
    /// <summary>
    /// 3940×2160
    /// </summary>
    FourK,
    /// <summary>
    /// 1600×1200
    /// </summary>
    Ultra_XGA,
}

[Serializable]
public class KeyBindItem
{
    public uint _bindingIndex;
    public Button button;//対応するbutton
    public TMP_Text bindText;//何のキーが設定されてるか見せるテキスト
    public TMP_Text messageText;//リバインド時にキー入力を誘導するテキスト
    public InputActionReference inputActionReference;//設定されるアクション
}

public class SettingUIManager : MonoBehaviour
{
    private enum CheckActionMode
    {
        None,
        Reset,
        BackTitle,
    }
    public static SettingUIManager Instance;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("キーバインド")]
    [SerializeField] private InputActionReference _openMenuReference;//escapeキーを押したらメニュー表示
    bool _isOpenMenu = false;

    /// <summary>設定メニューが開いているか。ホバー判定などの抑止条件として参照される。</summary>
    public static bool IsMenuOpen => Instance != null && Instance._isOpenMenu;

    [Header("UIのオブジェクト(Common)")]
    [SerializeField] private List<Button> _settingButtons = new List<Button>();
    [SerializeField] private Button _closeButton;
    [SerializeField] private List<GameObject> _settingContents = new List<GameObject>();
    [SerializeField] private GameObject _settingPanel;
    [SerializeField] private Button _resetButton;
    [SerializeField] private Button _backTitleButton;
    [SerializeField] private GameObject _checkActionPanel;
    [SerializeField] private TMP_Text _warningText;
    [SerializeField] private Button _yesButton;
    [SerializeField] private Button _noButton;
    private bool _isCantSettingUI = false;//SettingUIの表示・非表示ができない状態に設定するためのbool
    public bool IsCantSettingUI{private get{return _isCantSettingUI;} set{_isCantSettingUI = value;}}

    private int _settingIndex = 0;
    private int _checkActionIndex = -1;//-1:未選択, 0:はい, 1:いいえ

    [Header("UIのオブジェクト(Graphic)")]
    [SerializeField] private Toggle _windowModeToggle;
    [SerializeField] private Toggle _fullScreenModeToggle;
    [SerializeField] private TMP_Dropdown _screenSizeDropDown;
    [SerializeField] private TMP_Dropdown _frameRateDropDown;
    [SerializeField] private TMP_Dropdown _refreshRateDropDown;
    [SerializeField] private Slider _lightSlider;
    [SerializeField] private TMP_Dropdown _qualityPresetDropDown;
    [SerializeField] private TMP_Dropdown _textureDropDown;
    [SerializeField] private TMP_Dropdown _materialDropDown;
    [SerializeField] private TMP_Dropdown _antiAliasingDropDown;
    [SerializeField] private TMP_Dropdown _shadowDropDown;
    [SerializeField] private UniversalRenderPipelineAsset _urpAsset;

    private List<int> _availableRefreshRates = new() { 30,60,120,144,165,240};
    private Volume _volume;
    private ColorAdjustments _colorAdjustments;
    Camera _mainCamera;
    private UniversalAdditionalCameraData _cameraData;
    private Language _language;

    [Header("UIのオブジェクト(Sound)")]
    [SerializeField] private Slider _bgmSlider;
    [SerializeField] private Slider _seSlider;
    [SerializeField] private Slider _voiceSlider; 
    [SerializeField] private AudioSource _audio;
    [SerializeField] private AudioClip _tryBGM;
    [SerializeField] private AudioClip _trySe;
    [SerializeField] private AudioClip _tryVoice;
    [SerializeField] private Toggle _soundPitchOnToggle;
    [SerializeField] private Toggle _soundPitchOffToggle;
    [SerializeField] private List<AudioSource> _audioSources = new List<AudioSource>();

    [Header("UIのオブジェクト(操作)")]
    [Tooltip("マウス感度のスライダー。未設定なら感度設定は無効")]
    [SerializeField] private Slider _sensitivitySlider;

    [Tooltip("感度の数値入力欄。スライダーと相互に同期する。未設定でもスライダーだけで動く")]
    [SerializeField] private TMP_InputField _sensitivityInput;

    [Tooltip("表示上の感度の最小値・最大値。ここで入力できる範囲が決まる")]
    [SerializeField] private float _sensitivityDisplayMin = 1f;
    [SerializeField] private float _sensitivityDisplayMax = 100f;

    [Tooltip("表示値が最小・最大のときの実際のマウス感度 (deg/pixel)")]
    [SerializeField] private float _sensitivityValueMin = 0.02f;
    [SerializeField] private float _sensitivityValueMax = 0.40f;

    [SerializeField] private List<KeyBindItem> _keybindItems;
    [SerializeField] private bool isDoLoad = true;
    private bool isNowRebinding = false;
    private PlayerInput _myPlayerInput;
    private FirstPersonController _fpsController;
    private InputActionRebindingExtensions.RebindingOperation _rebindingOperation;

    [Header("UIのオブジェクト(Other)")]

    [SerializeField] private TMP_Dropdown _languageDropDown;

    // 設定値の保存キー。音量と明るさは PlayerPrefs に残して次回起動時に復元する
    private const string PrefBgmVolume   = "Setting_BgmVolume";
    private const string PrefSeVolume    = "Setting_SeVolume";
    private const string PrefVoiceVolume = "Setting_VoiceVolume";
    private const string PrefBrightness  = "Setting_Brightness";
    private const string PrefSensitivity = "Setting_Sensitivity";

    [Header("配色")]
    [Tooltip("タブ選択色・キーバインド色をここから取る。未設定なら従来のハードコード値を使う")]
    [SerializeField] private OddOdds.UI.Settings.SettingsTheme _theme;

    // テーマ未設定でも従来どおり動くよう、既定値は変更前の値と同じにしてある
    private Color TabSelectedFill      => _theme != null ? _theme.tabSelectedFill      : Color.white;
    private Color TabSelectedText      => _theme != null ? _theme.tabSelectedText      : Color.black;
    private Color TabDeselectedFill    => _theme != null ? _theme.tabDeselectedFill    : new Color(53f / 255f, 53f / 255f, 53f / 255f);
    private Color TabDeselectedText    => _theme != null ? _theme.tabDeselectedText    : Color.white;
    private Color KeybindIdleFill      => _theme != null ? _theme.keybindIdleFill      : Color.white;
    private Color KeybindRebindingFill => _theme != null ? _theme.keybindRebindingFill : Color.red;


    private MouseHoverOutline[] _mouseHoverOutlines;

    // 設定を開く直前の状態を覚えておき、閉じたときはそこへ戻す。
    // クレーンゲーム/チュートリアル/TV閲覧中は、それぞれ独自にfpsControllerを無効化・
    // カーソル状態を管理しているため、設定を閉じた際に無条件でfpsControllerを有効化・
    // カーソルをロックしてしまうと、それらのモードの最中でもプレイヤーのカメラに
    // 戻ってしまう（GameInputGateにはATMしか登録していないため、設定側はこれらのモード中か
    // どうかを知る手段が無く、この状態記憶で対応する）
    private bool _fpsControllerWasEnabledBeforeMenu;
    private CursorLockMode _cursorLockStateBeforeMenu;
    private bool _cursorVisibleBeforeMenu;

    void Awake()
    {
        //シングルトン設定
        if(Instance == null)
        {
            Instance = this;
            Init();
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    /// <summary>
    /// 初期化処理をおこなう
    /// </summary>
    public void Init()
    {
        _settingButtons[0].Select();
        PushSettingButton(0);

        _openMenuReference.action.Enable();
        _isOpenMenu = _settingPanel.activeSelf;

        CreateRefreshRateOptions();

        // ColorAdjustments を持つ Volume を選んで確保する（無ければ警告だけ出して続行）
        EnsureColorAdjustments();
        _mainCamera = Camera.main;
        _cameraData = _mainCamera.GetUniversalAdditionalCameraData();

        try
        {
            _myPlayerInput = FindFirstObjectByType<PlayerInput>();//TODO:ネットワーク対応
            _fpsController = FindFirstObjectByType<FirstPersonController>();
        }
        catch (System.Exception)
        {
            Debug.LogError("このシーンにはプレイヤーがいない");
        }

        Debug.Log(_myPlayerInput.gameObject.name);

        //すでにリバインディングしたことがある場合はシーン読み込み時に変更。
        string rebinds = PlayerPrefs.GetString("RebindingSettings");
        if (!string.IsNullOrEmpty(rebinds) && isDoLoad)
        {
            //リバインディング状態をロード
            _myPlayerInput.actions.LoadBindingOverridesFromJson(rebinds);

            //バインディング名を取得
            foreach (var item in _keybindItems)
            {
                //各アクションの今のパスを取得して、item.bindTextを更新したい
                item.bindText.text = InputControlPath.ToHumanReadableString(
                item.inputActionReference.action.bindings[(int)item._bindingIndex].effectivePath,
                InputControlPath.HumanReadableStringOptions.OmitDevice);
            }
        }

        //UIの言語を変更する
        var languages = InterfaceFinder.FindAllByInterface<ILanguage>();
        Debug.Log(languages.Count());
        foreach(var lan in languages)
        {
            lan.SettingLanguage(_language);
        }

        //音声関連の初期化を行う
        _audioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.InstanceID).ToList();
    }

    public void OnEnable()
    {
        //コモン
        _resetButton.onClick.AddListener(() =>
        {
            _warningText.text = "設定を初期化しますか？";
            StartCoroutine(CheckActionView(CheckActionMode.Reset));
        });
        _backTitleButton.onClick.AddListener(() =>
        {
            _warningText.text = "タイトルに戻りますか？";
            StartCoroutine(CheckActionView(CheckActionMode.BackTitle)); 
        });

        _yesButton.onClick.AddListener(() => _checkActionIndex = 0);
        _noButton.onClick.AddListener(() => _checkActionIndex = 1);
        _languageDropDown.onValueChanged.AddListener(value => LanguageSetting(value));

        //グラフィック項目のリスナー追加
        _windowModeToggle.onValueChanged.AddListener(value => { if (value) Screen.fullScreen = false; Debug.Log("_windowModeToggle:" + value); });
        _fullScreenModeToggle.onValueChanged.AddListener(value => { if (value) Screen.fullScreen = true; Debug.Log("_fullScreenModeToggle:" + value); });
        _screenSizeDropDown.onValueChanged.AddListener(value => ChangeScreenSize(value));
        _frameRateDropDown.onValueChanged.AddListener(value => ChangeFramerate(value));
        _refreshRateDropDown.onValueChanged.AddListener(value => ChangeRefreshRate(value));
        _lightSlider.onValueChanged.AddListener(value => ChangeLight(value));
        _qualityPresetDropDown.onValueChanged.AddListener(value => ChangeQualityPreset(value));
        _textureDropDown.onValueChanged.AddListener(value => ChangeTextureSetting(value));
        _materialDropDown.onValueChanged.AddListener(value => ChangeMaterialSetting(value));
        _antiAliasingDropDown.onValueChanged.AddListener(value => ChangeAntiAliasingSetting(value));
        _shadowDropDown.onValueChanged.AddListener(value => ChangeShadowSetting(value));

        //サウンド項目のリスナー追加
        _bgmSlider.onValueChanged.AddListener(value => ChangeBgmVolume(value));
        _seSlider.onValueChanged.AddListener(value => ChangeSeVolume(value));
        _voiceSlider.onValueChanged.AddListener(value => ChangeVoiceVolume(value));

        SetupSensitivity();
        SetupPreviewAudio();
        RestoreSavedSettings();

        //キーバインド項目のリスナー追加

        foreach (var keybindItem in _keybindItems)
        {
            keybindItem.messageText.gameObject.SetActive(false);
            keybindItem.button.onClick.AddListener(() =>
            {
                keybindItem.button.GetComponent<Image>().color = KeybindRebindingFill;
                keybindItem.bindText.text = "";
                keybindItem.messageText.gameObject.SetActive(true);
                Debug.Log(keybindItem.inputActionReference.action.enabled);
                SetRebinding(keybindItem);
            });
        }
    }

    private void OnDisable()
    {
        //コモンのリスナー削除
        _resetButton.onClick.RemoveAllListeners();
        _backTitleButton.onClick.RemoveAllListeners();
        _languageDropDown.onValueChanged.RemoveAllListeners();

        //グラフィック項目のリスナー削除
        _windowModeToggle.onValueChanged.RemoveAllListeners();
        _fullScreenModeToggle.onValueChanged.RemoveAllListeners();
        _screenSizeDropDown.onValueChanged.RemoveAllListeners();
        _frameRateDropDown.onValueChanged.RemoveAllListeners();
        _refreshRateDropDown.onValueChanged.RemoveAllListeners();
        _lightSlider.onValueChanged.RemoveAllListeners();
        _qualityPresetDropDown.onValueChanged.RemoveAllListeners();
        _textureDropDown.onValueChanged.RemoveAllListeners();
        _materialDropDown.onValueChanged.RemoveAllListeners();
        _antiAliasingDropDown.onValueChanged.RemoveAllListeners();
        _shadowDropDown.onValueChanged.RemoveAllListeners();

        //サウンド項目のリスナー削除
        _bgmSlider.onValueChanged.RemoveAllListeners();
        _seSlider.onValueChanged.RemoveAllListeners();
        _voiceSlider.onValueChanged.RemoveAllListeners();

        //キーバインド項目のリスナー削除
        foreach (var keybindItem in _keybindItems)
        {
            keybindItem.button.onClick.RemoveAllListeners();
        }
    }

    /// <summary>
    /// 言語設定を変更させる
    /// </summary>
    /// <param name="value"></param>
    private void LanguageSetting(int value)
    {
        _language = (Language)value;

        //設定された言語をもとに全てのUIの言語を反映させる
        var languages = InterfaceFinder.FindAllByInterface<ILanguage>();
        foreach(var lan in languages)
        {
            lan.SettingLanguage(_language);
        }

        var locales = LocalizationSettings.AvailableLocales.Locales;
        LocalizationSettings.SelectedLocale = locales[value];
    }

    private void OnApplicationQuit()
    {
        if (_colorAdjustments != null) _colorAdjustments.postExposure.value = 0;
    }

    private void Update()
    {
        // ATM等にカメラをフォーカス中は、Escapeがそのフォーカス解除に使われるので設定を開かない。
        // (メニューを開いている間は、閉じる操作を受け付けるため素通りさせる)
        if (!_isOpenMenu)
        {
            App.Input.GameInputGate.PurgeDestroyedEscapeOwners();
            if (App.Input.GameInputGate.IsEscapeCaptured) return;
        }

        //escapeキーを押したときにメニュー切り替え
        if (_openMenuReference.action.WasPressedThisFrame() && !isNowRebinding && !_isCantSettingUI && SceneManager.GetActiveScene().buildIndex != 0)
        {
            _isOpenMenu = !_isOpenMenu;
            _settingPanel.SetActive(_isOpenMenu);

            if (_isOpenMenu)
            {
                // クレーンゲーム/チュートリアル/TV閲覧中にEscapeで設定を開いた場合、閉じたときに
                // この直前の状態へ戻せるよう記憶しておく
                _fpsControllerWasEnabledBeforeMenu = _fpsController != null && _fpsController.enabled;
                _cursorLockStateBeforeMenu = Cursor.lockState;
                _cursorVisibleBeforeMenu = Cursor.visible;

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if(_fpsController != null) _fpsController.enabled = false;
                SettingButtonAnimation(_settingIndex, -1);

                _mouseHoverOutlines = FindObjectsByType<MouseHoverOutline>(FindObjectsSortMode.InstanceID);
                if(_mouseHoverOutlines.Length != 0)
                {
                    foreach(var item in _mouseHoverOutlines)
                        item.IsOpenUI = true;
                }
            }
            else
            {
                // クレーンゲーム/チュートリアル/TV閲覧中に開いていた場合は、無条件でプレイヤーの
                // カメラに戻さず、開く直前の状態（それらのモードのカメラのまま）に戻す
                Cursor.lockState = _cursorLockStateBeforeMenu;
                Cursor.visible = _cursorVisibleBeforeMenu;
                if(_fpsController != null && _fpsControllerWasEnabledBeforeMenu && DebtCollectionManager.Instance.IsStartDebtCollection == false) _fpsController.enabled = true;
                SettingButtonAnimation(-1, _settingIndex);
                _settingIndex = 0;

                if(_mouseHoverOutlines.Length != 0)
                {
                    foreach(var item in _mouseHoverOutlines)
                        item.IsOpenUI = false;
                }
            }

            if(SceneManager.GetActiveScene().buildIndex == 0)//タイトルシーンのみ
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    /// <summary>
    /// 設定上部のボタンを押したときに表示するコンテンツを変える
    /// </summary>
    /// <param name="index"></param>
    public void PushSettingButton(int index)
    {
        //SettingButtonAnimation(false, _settingIndex);
        SettingButtonAnimation(index, _settingIndex);
        _settingIndex = index;
    }

    /// <summary>
    /// 設定用のUIを開ける(ボタン専用)
    /// </summary>
    public void OpenSettingMenu()
    {
        if(_isCantSettingUI)
        {
            Debug.LogError("現在、SettingUIの表示・非表示を変更できません");
            return;
        } 

        _fpsControllerWasEnabledBeforeMenu = _fpsController != null && _fpsController.enabled;
        _cursorLockStateBeforeMenu = Cursor.lockState;
        _cursorVisibleBeforeMenu = Cursor.visible;

        _settingPanel.SetActive(true);
        _isOpenMenu = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if(_fpsController != null) _fpsController.enabled = false;
        SettingButtonAnimation(_settingIndex, -1);

        /*
        _mouseHoverOutlines = FindObjectsByType<MouseHoverOutline>(FindObjectsSortMode.InstanceID);
        if(_mouseHoverOutlines.Length != 0)
        {
            foreach(var item in _mouseHoverOutlines)
                item.IsOpenUI = true;
        }
        */
    }

    /// <summary>
    /// 設定用のUIを閉じる(ボタン専用)
    /// </summary>
    public void CloseSettingMenu()
    {
        if(_isCantSettingUI && SceneManager.GetActiveScene().buildIndex != 0)
        {
            Debug.LogError("現在、SettingUIの表示・非表示を変更できません");
            return;
        } 

        _settingPanel.SetActive(false);
        _isOpenMenu = false;
        if(_fpsController != null && _fpsControllerWasEnabledBeforeMenu && DebtCollectionManager.Instance.IsStartDebtCollection == false) _fpsController.enabled = true;//悪魔の取り立て中は体の自由は聞かないまま
        Cursor.lockState = _cursorLockStateBeforeMenu;
        Cursor.visible = _cursorVisibleBeforeMenu;
        SettingButtonAnimation(-1, _settingIndex);
        _settingIndex = 0;

        /*
        if(_mouseHoverOutlines.Length != 0)
        {
            foreach(var item in _mouseHoverOutlines)
                item.IsOpenUI = false;
        }
        */
        if(SceneManager.GetActiveScene().buildIndex == 0)//タイトルシーンのみ
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    /// <summary>
    /// 設定上部のボタンを押したときのアニメーションを実行
    /// </summary>
    /// <param name="index">表示するセクションの番号</param>
    private void SettingButtonAnimation(int nextIndex = -1, int prevIndex = -1)
    {
        //次に表示するbuttonの処理
        if (nextIndex != -1)
        {
            SetContent(nextIndex);
            _settingButtons[nextIndex].targetGraphic.color = TabSelectedFill;
            _settingButtons[nextIndex].transform.GetChild(0).gameObject.GetComponent<TMP_Text>().color = TabSelectedText;
        }

        //以前表示していたbuttonの処理
        if (prevIndex != nextIndex && prevIndex != -1)
        {
            _settingButtons[prevIndex].targetGraphic.color = TabDeselectedFill;
            _settingButtons[prevIndex].transform.GetChild(0).gameObject.GetComponent<TMP_Text>().color = TabDeselectedText;
        }

    }

    /// <summary>
    /// 表示するコンテンツ以外は非表示にする。
    /// </summary>
    /// <param name="index">番号</param>
    private void SetContent(int index)
    {
        for (int i = 0; i < _settingContents.Count; i++)
        {
            if (i != index)
                _settingContents[i].SetActive(false);
            else
            {
                _settingContents[i].SetActive(true);
            }
        }
    }

    /// <summary>
    /// 全ての設定をデバイスごとに合わせた適切な設定にする
    /// </summary>
    private void AutoSetting()
    { 
        //TODO:実装してね
    }

    private void ResetSetting()
    {
        AutoSetting();

        //デバイスに関係ない項目をもとに戻す
        _lightSlider.value = 0.5f;
        _bgmSlider.value = 0.5f;
        _seSlider.value = 0.5f;
        _voiceSlider.value = 0.5f;
        _soundPitchOnToggle.isOn = true;
        _soundPitchOffToggle.isOn = false;
    }

    private IEnumerator CheckActionView(CheckActionMode mode)
    {
        _isCantSettingUI = true;
        _checkActionPanel.SetActive(true);
        Sequence sequence = DOTween.Sequence();
        sequence.Append(_checkActionPanel.transform.DOLocalMoveY(-100,0.3f).SetEase(Ease.OutQuint))
        .Join(_checkActionPanel.GetComponent<CanvasGroup>().DOFade(endValue: 1f, duration: 0.1f));

        //最初にアニメーションを流す（ポップアップ出現）
        yield return sequence.WaitForCompletion();

        //プレイヤーが「はい」「いいえ」を選択するまで待機
        yield return new WaitUntil(() => _checkActionIndex != -1);

        //ポップアップを消す
        sequence = DOTween.Sequence();
        sequence.Append(_checkActionPanel.transform.DOLocalMoveY(-150,0.3f).SetEase(Ease.OutQuint))
        .Join(_checkActionPanel.GetComponent<CanvasGroup>().DOFade(endValue: 0f, duration: 0.1f));
        yield return sequence.WaitForCompletion();
        _checkActionPanel.SetActive(false);
        _isCantSettingUI = false;
        //選択に応じて処理を分岐
        if(mode == CheckActionMode.Reset && _checkActionIndex == 0)
        {
            ResetSetting();
        }
        else if(mode == CheckActionMode.BackTitle && _checkActionIndex == 0)
        {
            _checkActionIndex = -1;
            if(_canvasGroup == null)  TryGetComponent<CanvasGroup>(out _canvasGroup);

            //タイトル画面へ遷移する機能
            try
            {
                RoguelikeSaveManager.Save();
            }
            catch (System.Exception)
            {
                Debug.LogError("セーブできませんでした。");
            }

            if(SceneManager.GetActiveScene().buildIndex != 0)
            {
                //設定UIが透明になるまで待つ（セーブ処理を待つ時間を稼ぐ演出）
                yield return _canvasGroup.DOFade(0, 1.0f).OnComplete(() => 
                {
                    CloseSettingMenu();
                    _canvasGroup.alpha = 1;//透明になっていたのを戻す
                }).WaitForCompletion();
                MiniGames.Transitions.SceneTransitionManager.Instance.TransitionToScene("3D_Title_Sample" , () => 
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    //MiniGames.Transitions.SceneTransitionManager.Instance.TransitionCanvas.worldCamera = Camera.main;
                });
            }
            
        }


        _checkActionIndex = -1;
        yield return null;
    }


    /*--- ここからはグラフィック用の設定項目に関する関数 ---------*/
    /// <summary>
    /// ゲームの解像度を設定する関数
    /// </summary>
    /// <param name="value"></param>
    public void ChangeScreenSize(int value)
    {
        // 解像度だけ変えたいので、ウィンドウ／フルスクリーンの状態は今のまま保つ。
        // ここで false を渡すとフルスクリーン中に解像度を変えた時に
        // 勝手にウィンドウ化してしまう
        FullScreenMode mode = Screen.fullScreenMode;

        switch ((ScreenSize)value)
        {
            case ScreenSize.FHD:
                Screen.SetResolution(1920, 1080, mode);
                break;
            case ScreenSize.WQHD:
                Screen.SetResolution(2560, 1440, mode);
                break ;
            case ScreenSize.FourK:
                Screen.SetResolution(3840, 2160, mode);
                break ;
            case ScreenSize.Ultra_XGA:
                Screen.SetResolution(1600, 1200, mode);
                break ;
            default:
                Debug.LogError("そのような基底のウィンドウサイズは存在しません。");
                break;
        }

        Debug.Log("ScreenSize:" + value);

#if UNITY_EDITOR
        // エディタの Game ビューは Screen.SetResolution を無視する。
        // 効いていないように見えるのは仕様なので、ビルドで確認する必要がある
        Debug.Log("[SettingUIManager] エディタでは解像度変更は反映されません。ビルドで確認してください。");
#endif
    }

    public void ChangeFramerate(int value)
    {
        //ProjectSettingsのQuality->VSyncCountを変更
        switch (value)
        {
            case 0://30fpsのとき
                QualitySettings.vSyncCount = 0;//フレームレートの固定ができるように(VsyncをOFFにする)
                Application.targetFrameRate = 30;
                break;
            case 1://60fpsのとき
                QualitySettings.vSyncCount = 0;//フレームレートの固定ができるように(VsyncをOFFにする)
                Application.targetFrameRate = 60;
                break;
            case 2://120fpsのとき
                QualitySettings.vSyncCount = 0;//フレームレートの固定ができるように(VsyncをOFFにする)
                Application.targetFrameRate = 120;
                break;
            case 3://Unlimited
                QualitySettings.vSyncCount = 1; //ディスプレイのリフレッシュレートを目標値に
                break;
            default:
                Debug.LogError("そのようなFPSの選択肢は存在しません。");
                break;
        }

        Debug.Log("Frame:" + value);
    }

    public void ChangeRefreshRate(int value)
    {
        //SetResolutionに必要な情報を集めていく
        string selectedText = _refreshRateDropDown.options[_refreshRateDropDown.value].text;
        int selectedHz = int.Parse(selectedText.Replace(" Hz", ""));
        Resolution current = Screen.currentResolution;
        FullScreenMode mode = FullScreenMode.FullScreenWindow;
        if (_windowModeToggle.isOn && !_fullScreenModeToggle.isOn)
            mode = FullScreenMode.Windowed;
        else if(!_windowModeToggle.isOn && _fullScreenModeToggle.isOn)
            mode = FullScreenMode.FullScreenWindow;

        Screen.SetResolution(
            current.width,
            current.height,
            mode,
            new RefreshRate
            {
                numerator = (uint)selectedHz,
                denominator = 1
            });

        Debug.Log("RefreshRate:" + value);
    }

    private void CreateRefreshRateOptions()
    {
        // Dropdown初期化
        _refreshRateDropDown.ClearOptions();

        // 重複しないリフレッシュレートを取得
        HashSet<int> refreshRates = new();

        // モニターが対応している最大Hzを取得
        int maxRefreshRate = 0;
        foreach (Resolution resolution in Screen.resolutions)
        {
            int rate = Mathf.RoundToInt((float)resolution.refreshRateRatio.value);
            if (rate > maxRefreshRate)
            {
                maxRefreshRate = rate;
            }
        }

        //最大値を超えていない項目のみオプションに追加
        List<string> options = new();
        foreach (int rate in _availableRefreshRates)
        {
            if (rate <= maxRefreshRate)
            {
                options.Add($"{rate} Hz");
            }
        }
        _refreshRateDropDown.AddOptions(options);

        // 現在のリフレッシュレートを選択状態にする
        int currentIndex = options.Count - 1;//一番最後の要素が現在利用可能なリフレッシュレートの最大値
        if (currentIndex >= 0)
        {
            _refreshRateDropDown.value = currentIndex;
            _refreshRateDropDown.RefreshShownValue();
        }
    }

    public void ChangeLight(float value)
    {
        if (!EnsureColorAdjustments()) return;

        // URP の Volume は overrideState を立てないと値が無視される。
        // プロファイル側でチェックが入っていないと「設定しているのに変わらない」ので、
        // 触る時に必ず有効化しておく
        _colorAdjustments.postExposure.overrideState = true;
        _colorAdjustments.postExposure.value = -1f + 2f * value;

        PlayerPrefs.SetFloat(PrefBrightness, value);
    }

    /// <summary>
    /// 明るさを操作する ColorAdjustments を確保する。
    ///
    /// FindFirstObjectByType&lt;Volume&gt;() だと ColorAdjustments を持たない Volume を
    /// 拾ってしまうことがあるため、プロファイルに ColorAdjustments がある Volume を
    /// 優先度の高い順に探す。
    /// </summary>
    private bool EnsureColorAdjustments()
    {
        if (_colorAdjustments != null) return true;

        Volume best = null;
        ColorAdjustments bestAdjustments = null;

        var volumes = FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var volume in volumes)
        {
            if (volume == null || volume.profile == null) continue;
            if (!volume.profile.TryGet(out ColorAdjustments adjustments)) continue;

            // グローバルで優先度が高いものを選ぶ。ローカル Volume は範囲内でしか効かない
            bool better = best == null
                          || (volume.isGlobal && !best.isGlobal)
                          || (volume.isGlobal == best.isGlobal && volume.priority > best.priority);
            if (!better) continue;

            best = volume;
            bestAdjustments = adjustments;
        }

        if (bestAdjustments == null)
        {
            WarnOnceAboutBrightness();
            return false;
        }

        _volume = best;
        _colorAdjustments = bestAdjustments;
        return true;
    }

    private bool _warnedAboutBrightness;

    private void WarnOnceAboutBrightness()
    {
        if (_warnedAboutBrightness) return;
        _warnedAboutBrightness = true;
        Debug.LogWarning("[SettingUIManager] ColorAdjustments を持つ Volume が見つからないため、" +
                         "明るさ設定を反映できません。シーンの Global Volume の Profile に " +
                         "Color Adjustments を追加してください。", this);
    }

    public void ChangeQualityPreset(int value)
    {
        if(value == _qualityPresetDropDown.options.Count -1)//customのときは無視
            return;

        _textureDropDown.value = value;
        _materialDropDown.value = value;
        _shadowDropDown.value = value;
    }
    public void ChangeTextureSetting(int value)
    {

        if(value != _qualityPresetDropDown.value)
            _qualityPresetDropDown.value = _qualityPresetDropDown.options.Count - 1;//customに変更
        //TODO：処理を実装する
    }

    public void ChangeMaterialSetting(int value)
    {
        if (value != _qualityPresetDropDown.value)
            _qualityPresetDropDown.value = _qualityPresetDropDown.options.Count - 1;//customに変更
        //TODO：処理を実装する
    }

    public void ChangeAntiAliasingSetting(int value)
    {
        switch (value)
        {
            case 0://OFFのとき
                _mainCamera.allowMSAA = false;
                _urpAsset.msaaSampleCount = 1;//1で実質OFFになるらしい
                _cameraData.antialiasing = AntialiasingMode.None;
                break;
            case 1://FXAAのとき
                _cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                break;
            case 2://SMAAのとき
                _cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                break;
            case 3://TAAのとき
                _cameraData.antialiasing = AntialiasingMode.TemporalAntiAliasing;
                break;
            case 4://MSAA x2のとき
                _mainCamera.allowMSAA = true;
                _urpAsset.msaaSampleCount = 2;
                break;
            case 5://MSAA x4のとき
                _mainCamera.allowMSAA = true;
                _urpAsset.msaaSampleCount = 4;
                break;
            case 6://MSAA x8のとき
                _mainCamera.allowMSAA = true;
                _urpAsset.msaaSampleCount = 8;
                break;
            default:
                Debug.LogError("そのようなFPSの選択肢は存在しません。");
                break;
        }
    }

    public void ChangeShadowSetting(int value)
    {
        if (value != _qualityPresetDropDown.value)
            _qualityPresetDropDown.value = _qualityPresetDropDown.options.Count - 1;//customに変更
        //TODO：処理を実装する
        switch (value)
        {
            case 0://Lowのとき
                _urpAsset.shadowDistance = 10;
                break;
            case 1://standardのとき
                _urpAsset.shadowDistance = 20;
                break;
            case 2://Highのとき
                _urpAsset.shadowDistance = 30;
                break;
            case 3://Ultraのとき
                _urpAsset.shadowDistance = 40; 
                break;
            default:
                Debug.LogError("そのようなFPSの選択肢は存在しません。");
                break;
        }
    }


    /*--- ここからはサウンド用の設定項目に関する関数 ---------*/
    
    /// <summary>
    /// 音量つまみの実体。AudioVolumeController がシーン上の AudioSource へ倍率を掛ける。
    /// どの音がどの分類かは AudioCategoryTag か、AudioVolumeController の Inspector で決める。
    /// </summary>
    private App.Audio.AudioVolumeController Volume
    {
        get
        {
            var controller = App.Audio.AudioVolumeController.Instance;
            if (controller != null) return controller;

            // シーンに置き忘れていても音量設定が死なないよう、無ければ自分で用意する
            controller = FindFirstObjectByType<App.Audio.AudioVolumeController>();
            if (controller == null)
            {
                var go = new GameObject("AudioVolumeController");
                DontDestroyOnLoad(go);
                controller = go.AddComponent<App.Audio.AudioVolumeController>();
                Debug.Log("[SettingUIManager] AudioVolumeController が見つからないため自動生成しました。", controller);
            }
            return controller;
        }
    }

    /*--- ここからは操作（感度）用の設定項目に関する関数 ---------*/

    /// <summary>感度スライダーと数値入力欄をつなぐ。</summary>
    private void SetupSensitivity()
    {
        if (_sensitivitySlider != null)
        {
            _sensitivitySlider.onValueChanged.AddListener(_ => ApplySensitivityFromSlider(true));
        }

        if (_sensitivityInput != null)
        {
            _sensitivityInput.contentType = TMP_InputField.ContentType.DecimalNumber;
            // 入力途中の半端な値で感度が飛ばないよう、確定時だけ反映する
            _sensitivityInput.onEndEdit.AddListener(ApplySensitivityFromInput);
            _sensitivityInput.onSubmit.AddListener(ApplySensitivityFromInput);
        }
    }

    /// <summary>表示値(1〜100 など) を実際のマウス感度へ変換する。</summary>
    private float DisplayToSensitivity(float display)
    {
        float span = Mathf.Max(0.0001f, _sensitivityDisplayMax - _sensitivityDisplayMin);
        float t = Mathf.Clamp01((display - _sensitivityDisplayMin) / span);
        return Mathf.Lerp(_sensitivityValueMin, _sensitivityValueMax, t);
    }

    /// <summary>スライダーの位置から表示値を取り出す。スライダーの最小/最大がいくつでも動く。</summary>
    private float SliderToDisplay()
    {
        if (_sensitivitySlider == null) return _sensitivityDisplayMin;
        float t = Mathf.Clamp01(_sensitivitySlider.normalizedValue);
        return Mathf.Lerp(_sensitivityDisplayMin, _sensitivityDisplayMax, t);
    }

    private void ApplySensitivityFromSlider(bool syncInput)
    {
        float display = SliderToDisplay();
        ApplySensitivity(display);

        if (syncInput && _sensitivityInput != null)
        {
            _sensitivityInput.SetTextWithoutNotify(display.ToString("0.#"));
        }
    }

    private void ApplySensitivityFromInput(string text)
    {
        if (!float.TryParse(text, out float display))
        {
            // 数字として読めない入力は、今の値に戻して知らせる
            if (_sensitivityInput != null)
                _sensitivityInput.SetTextWithoutNotify(SliderToDisplay().ToString("0.#"));
            return;
        }

        display = Mathf.Clamp(display, _sensitivityDisplayMin, _sensitivityDisplayMax);

        if (_sensitivitySlider != null)
        {
            float span = Mathf.Max(0.0001f, _sensitivityDisplayMax - _sensitivityDisplayMin);
            _sensitivitySlider.SetValueWithoutNotify(
                Mathf.Lerp(_sensitivitySlider.minValue, _sensitivitySlider.maxValue,
                           (display - _sensitivityDisplayMin) / span));
        }

        if (_sensitivityInput != null)
            _sensitivityInput.SetTextWithoutNotify(display.ToString("0.#"));

        ApplySensitivity(display);
    }

    /// <summary>実際にプレイヤーへ感度を反映して保存する。</summary>
    private void ApplySensitivity(float display)
    {
        float sensitivity = DisplayToSensitivity(display);

        // シーンを跨ぐとプレイヤーが入れ替わるので、その都度探し直す
        if (_fpsController == null) _fpsController = FindFirstObjectByType<FirstPersonController>();
        if (_fpsController != null) _fpsController.LookSensitivity = sensitivity;

        PlayerPrefs.SetFloat(PrefSensitivity, display);
    }

    /// <summary>
    /// 試聴用の音は音量設定の倍率を掛けない。
    /// スライダーの値をそのまま鳴らして確認するためのものなので、
    /// さらに設定倍率が乗ると二重に小さくなってしまう。
    /// </summary>
    private void SetupPreviewAudio()
    {
        if (_audio == null) return;
        if (_audio.GetComponent<App.Audio.AudioCategoryTag>() != null) return;

        var tag = _audio.gameObject.AddComponent<App.Audio.AudioCategoryTag>();
        tag.category = App.Audio.AudioCategory.Unmanaged;
        tag.applyToChildren = false;
    }

    /// <summary>前回終了時の音量と明るさを復元する。スライダーへ入れると各 Change〜 が呼ばれて反映される。</summary>
    private void RestoreSavedSettings()
    {
        if (PlayerPrefs.HasKey(PrefBgmVolume))   _bgmSlider.value   = PlayerPrefs.GetFloat(PrefBgmVolume);
        if (PlayerPrefs.HasKey(PrefSeVolume))    _seSlider.value    = PlayerPrefs.GetFloat(PrefSeVolume);
        if (PlayerPrefs.HasKey(PrefVoiceVolume)) _voiceSlider.value = PlayerPrefs.GetFloat(PrefVoiceVolume);
        if (PlayerPrefs.HasKey(PrefBrightness))  _lightSlider.value = PlayerPrefs.GetFloat(PrefBrightness);

        // 感度はスライダーの範囲が任意なので、表示値から位置を逆算して入れる
        if (_sensitivitySlider != null)
        {
            float display = PlayerPrefs.HasKey(PrefSensitivity)
                ? PlayerPrefs.GetFloat(PrefSensitivity)
                : SliderToDisplay();
            ApplySensitivityFromInput(display.ToString("0.#"));
        }

        // スライダーの値が保存値と同じだと onValueChanged が飛ばないので、明示的に流し込む
        ChangeBgmVolume(_bgmSlider.value);
        ChangeSeVolume(_seSlider.value);
        ChangeVoiceVolume(_voiceSlider.value);
        ChangeLight(_lightSlider.value);
    }

    public void ChangeBgmVolume(float value)
    {
        Volume.Bgm = value;
        PlayerPrefs.SetFloat(PrefBgmVolume, value);
    }

    public void ChangeSeVolume(float value)
    {
        Volume.Se = value;
        PlayerPrefs.SetFloat(PrefSeVolume, value);
    }

    public void ChangeVoiceVolume(float value)
    {
        Volume.Voice = value;
        PlayerPrefs.SetFloat(PrefVoiceVolume, value);
    }

    public void PlayTrySound(int type)
    {
        switch (type)
        {
            case 0://BGM
                _audio.Stop();
                _audio.volume = _bgmSlider.value;
                _audio.PlayOneShot(_tryBGM);
                Debug.Log("BGM");
                break;
            case 1://SE
                _audio.Stop();
                _audio.volume = _seSlider.value;
                _audio.PlayOneShot(_trySe);
                Debug.Log("SE");
                break;
            case 2://Voice
                _audio.Stop();
                _audio.volume = _voiceSlider.value;
                _audio.PlayOneShot(_tryVoice);
                Debug.Log("Voice");
                break;
        }
    }

    /*--- ここからはキーバインド用の設定項目に関する関数 ---------*/
    public void SetRebinding(KeyBindItem item)
    {
        //ボタンの誤作動を防ぐため、何も無いアクションマップに切り替え
        _myPlayerInput.SwitchCurrentActionMap("Blank");

        //設定中はSettingを閉じることができない
        _closeButton.gameObject.SetActive(false);
        isNowRebinding = true;

        Debug.Log($"Current Map : {_myPlayerInput.currentActionMap.name}");
        Debug.Log($"Action Enabled : {item.inputActionReference.action.enabled}");

        item.inputActionReference.action.Disable();

        //リバインディング開始
        _rebindingOperation = item.inputActionReference.action.PerformInteractiveRebinding((int)item._bindingIndex)
                    //.WithTargetBinding(item.inputActionReference.action.GetBindingIndexForControl(item.inputActionReference.action.controls[index]))
                    .WithControlsExcluding("Mouse")
                    .OnMatchWaitForAnother(0.1f)
                    .OnComplete(operation => RebindComplete(item, (int)item._bindingIndex))
                    .OnCancel(op =>
                    {
                        //バインディングしたキーの名称を取得する
                        item.bindText.text = InputControlPath.ToHumanReadableString(
                            item.inputActionReference.action.bindings[(int)item._bindingIndex].effectivePath,
                            InputControlPath.HumanReadableStringOptions.OmitDevice);
                        //画面を通常に戻す
                        item.button.GetComponent<Image>().color = KeybindIdleFill;
                        item.messageText.gameObject.SetActive(false);
                        _closeButton.gameObject.SetActive(true);
                        isNowRebinding = false;

                        //リバインディング時は空のアクションマップだったので通常のアクションマップに切り替え
                        _myPlayerInput.SwitchCurrentActionMap("Player");
                    })
                    .Start();
    }

    public void RebindComplete(KeyBindItem item, int bindingIndex)
    {
        //バインディングしたキーの名称を取得する
        item.bindText.text = InputControlPath.ToHumanReadableString(
            item.inputActionReference.action.bindings[bindingIndex].effectivePath,
            InputControlPath.HumanReadableStringOptions.OmitDevice);

        _rebindingOperation.Dispose();

        //画面を通常に戻す
        item.button.GetComponent<Image>().color = KeybindIdleFill;
        item.messageText.gameObject.SetActive(false);
        _closeButton.gameObject.SetActive(true);
        isNowRebinding = false;

        //リバインディング時は空のアクションマップだったので通常のアクションマップに切り替え
        _myPlayerInput.SwitchCurrentActionMap("Player");

        //リバインディングしたキーを保存(シーン開始時に読み込むため)
        PlayerPrefs.SetString("RebindingSettings", _myPlayerInput.actions.SaveBindingOverridesAsJson());
    }
}
