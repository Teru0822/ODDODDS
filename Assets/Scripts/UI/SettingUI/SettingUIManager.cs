using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using static UnityEngine.InputSystem.InputActionRebindingExtensions;

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
    [Header("キーバインド")]
    [SerializeField] private InputActionReference _openMenuReference;//escapeキーを押したらメニュー表示
    bool _isOpenMenu = false;

    [Header("UIのオブジェクト(Common)")]
    [SerializeField] private List<Button> _settingButtons = new List<Button>();
    [SerializeField] private Button _closeButton;
    [SerializeField] private List<GameObject> _settingContents = new List<GameObject>();
    [SerializeField] private GameObject _settingPanel;
    private int _settingIndex = 0;

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

    [Header("UIのオブジェクト(Sound)")]
    [SerializeField] private Slider _bgmSlider;
    [SerializeField] private Slider _seSlider;
    [SerializeField] private Slider _voiceSlider; 
    [SerializeField] private AudioSource _audio;
    [SerializeField] private AudioClip _tryBGM;
    [SerializeField] private AudioClip _trySe;
    [SerializeField] private AudioClip _tryVoice;

    [Header("UIのオブジェクト(KeyBind)")]
    [SerializeField] private List<KeyBindItem> _keybindItems;
    [SerializeField] private bool isDoLoad = true;
    private bool isNowRebinding = false;
    private PlayerInput _myPlayerInput;
    private InputActionRebindingExtensions.RebindingOperation _rebindingOperation;

    private void Start()
    {
        _settingButtons[0].Select();
        PushSettingButton(0);

        _openMenuReference.action.Enable();
        _isOpenMenu = _settingPanel.activeSelf;

        CreateRefreshRateOptions();

        _volume = FindFirstObjectByType<Volume>();
        _volume.profile.TryGet(out _colorAdjustments);
        _mainCamera = Camera.main;
        _cameraData = _mainCamera.GetUniversalAdditionalCameraData();

        _myPlayerInput = FindFirstObjectByType<PlayerInput>();//TODO:ネットワーク対応
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
    }

    public void OnEnable()
    {
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

        //キーバインド項目のリスナー追加

        foreach (var keybindItem in _keybindItems)
        {
            keybindItem.messageText.gameObject.SetActive(false);
            keybindItem.button.onClick.AddListener(() =>
            {
                keybindItem.button.GetComponent<Image>().color = Color.red;
                keybindItem.bindText.text = "";
                keybindItem.messageText.gameObject.SetActive(true);
                Debug.Log(keybindItem.inputActionReference.action.enabled);
                SetRebinding(keybindItem);
            });
        }
    }

    private void OnDisable()
    {
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

    private void OnApplicationQuit()
    {
        _colorAdjustments.postExposure.value = 0;
    }

    private void Update()
    {
        //escapeキーを押したときにメニュー切り替え
        if (_openMenuReference.action.WasPressedThisFrame() && !isNowRebinding)
        {
            _isOpenMenu = !_isOpenMenu;
            _settingPanel.SetActive(_isOpenMenu);

            if (_isOpenMenu)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                SettingButtonAnimation(_settingIndex, -1);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                SettingButtonAnimation(-1, _settingIndex);
                _settingIndex = 0;
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
    /// 設定用のUIを閉じる
    /// </summary>
    public void CloseSettingMenu()
    {
        _settingPanel.SetActive(false);
        _isOpenMenu = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SettingButtonAnimation(-1, _settingIndex);
        _settingIndex = 0;
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
            _settingButtons[nextIndex].targetGraphic.color = Color.white;
            _settingButtons[nextIndex].transform.GetChild(0).gameObject.GetComponent<TMP_Text>().color = Color.black;
        }

        //以前表示していたbuttonの処理
        if (prevIndex != nextIndex && prevIndex != -1)
        {
            _settingButtons[prevIndex].targetGraphic.color = new Color(53f / 255f, 53f / 255f, 53f / 255f);
            _settingButtons[prevIndex].transform.GetChild(0).gameObject.GetComponent<TMP_Text>().color = Color.white;
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

    /// <summary>
    /// 適応ボタン押したときにセーブ&適応
    /// </summary>
    private void SaveChanged()
    {
        //TODO:実装してね
    }


    /*--- ここからはグラフィック用の設定項目に関する関数 ---------*/
    /// <summary>
    /// ゲームの解像度を設定する関数
    /// </summary>
    /// <param name="value"></param>
    public void ChangeScreenSize(int value)
    {
        switch ((ScreenSize)value)
        {
            case ScreenSize.FHD:
                Screen.SetResolution(1920, 1080, false);
                break;
            case ScreenSize.WQHD:
                Screen.SetResolution(2560, 1440, false);
                break ;
            case ScreenSize.FourK:
                Screen.SetResolution(3840, 2160, false);
                break ;
            case ScreenSize.Ultra_XGA:
                Screen.SetResolution(1600, 1200, false);
                break ;
            default:
                Debug.LogError("そのような基底のウィンドウサイズは存在しません。");
                break;
        }

        Debug.Log("ScreenSize:" + value);
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
        _colorAdjustments.postExposure.value = -1 + 2 * value;
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
    public void ChangeBgmVolume(float value)
    {
        //TODO:処理を実装する
    }

    public void ChangeSeVolume(float value)
    {
        //TODO:処理を実装する
    }

    public void ChangeVoiceVolume(float value)
    {
        //TODO:処理を実装する
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
                        item.button.GetComponent<Image>().color = Color.white;
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
        item.button.GetComponent<Image>().color = Color.white;
        item.messageText.gameObject.SetActive(false);
        _closeButton.gameObject.SetActive(true);
        isNowRebinding = false;

        //リバインディング時は空のアクションマップだったので通常のアクションマップに切り替え
        _myPlayerInput.SwitchCurrentActionMap("Player");

        //リバインディングしたキーを保存(シーン開始時に読み込むため)
        PlayerPrefs.SetString("RebindingSettings", _myPlayerInput.actions.SaveBindingOverridesAsJson());
    }
}
