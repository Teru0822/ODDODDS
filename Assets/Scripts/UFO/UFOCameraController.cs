using System.Collections;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Class to manage UFO Catcher camera switching and keyboard control modes.
/// </summary>
public class UFOCameraController : MonoBehaviour, IsaveDataProvider
{
    public static UFOCameraController Instance { get; private set; }
    public static bool IsPlayingUfo { get; private set; } = false;
    public static event System.Action<bool> OnUfoModeChanged;
    public static event System.Action OnCoinInserted;
    public static event System.Action OnAllCoinsInserted;
    public static event System.Action<UfoSubCameraState> OnSubCameraChanged;
    public static bool IsPlaySessionActive { get; private set; } = false;
    public static bool IsControlActive => IsPlaySessionActive && IsPlaySpotlightActive && Instance != null && Instance._controlsUnlocked && Instance._playTimer > 0f;

    [Header("Camera Settings")]
    [Tooltip("Player's first-person camera")]
    [SerializeField] private Camera playerCamera;

    [Tooltip("UFO Catcher front view camera (autodetected if null)")]
    [SerializeField] private Camera frontCamera;

    [Tooltip("Left view camera")]
    [SerializeField] private Camera leftCamera;

    [Tooltip("Right view camera")]
    [SerializeField] private Camera rightCamera;

    [Tooltip("Back view camera (renamed from Top)")]
    [UnityEngine.Serialization.FormerlySerializedAs("topCamera")]
    [SerializeField] private Camera backCamera;

    public enum UfoSubCameraState
    {
        Back,
        Left,
        Right
    }

    [Header("UI Settings")]
    [Tooltip("UFO Catcher UI Canvas/Object to hide when not playing")]
    [SerializeField] private GameObject ufoUiCanvas;



    [Header("Cost / Timer / Spotlight Settings")]
    [Tooltip("Control time limit per play (seconds)")]
    [SerializeField] private float playDuration = 30f;

    [Tooltip("Max play count per session（今後複数回プレイに対応予定。現在は1）")]
    [SerializeField] private int maxPlayCount = 1;

    [Tooltip("Spotlight GameObject enabled only during active play session")]
    [SerializeField] private GameObject playSpotlight;

    [Tooltip("タイマー終了時、ルーレットのコイン降雨(ItemSpawner.IsSpawning)が発生中の場合、それが終わってから消灯・終了処理をするまでの追加待機秒数")]
    [SerializeField] private float postRouletteRainExitDelay = 2f;

    [Header("デバッグ設定")]
    [Tooltip("ON: 実際にPlayを開始した後（有料セッション中）でも F / Escape キーで強制的に中断・終了できるようにします（テスト用）。\n" +
             "OFF (本番用): Playを開始した後は通常通り中断できません（Play2_Canvasの選択画面に戻っているだけの間はこの設定に関わらずいつでもキャンセルできます）。")]
    [SerializeField] private bool debugAllowInterruptDuringPlay = false;

    [Header("Audio Settings")]
    [Tooltip("再生用のAudioSource。未設定の場合は自動でGetComponentします")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("コイン投入時（ゲーム開始時）の効果音")]
    [SerializeField] private AudioClip coinInsertSound;
    [Tooltip("残り10秒を切ったとき（警告時）の効果音")]
    [SerializeField] private AudioClip lowTimeWarningSound;
    [Tooltip("効果音の音量調整 (1.0より大きい値で音量増幅可能)")]
    [Range(0f, 10f)]
    [SerializeField] private float soundVolume = 1.0f;

    [Header("Interaction Settings")]
    [Tooltip("Interaction distance to start playing")]
    [SerializeField] private float interactionDistance = 3.5f;

    [Tooltip("Reference Transform for UFO center (autodetected if null)")]
    [SerializeField] private Transform ufoCenterTransform;

    [Header("Camera Transition Settings")]
    [Tooltip("UFOキャッチャー台にアタッチした MouseHoverOutline。ホバー判定とクリック開始に使う")]
    [SerializeField] private MouseHoverOutline machineHover;

    [Min(0.01f)]
    [Tooltip("カメラ移動の所要時間（秒）")]
    [SerializeField] private float transitionDuration = 1.0f;

    [Tooltip("移動の緩急（0→1）")]
    [SerializeField] private AnimationCurve transitionEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Coin Play Animation Settings")]
    [Tooltip("コイン投入演出で出現させるコインのPrefab")]
    [SerializeField] private GameObject coinPrefab;

    [Tooltip("コインの親とするDEVILCATCHERオブジェクト名")]
    [SerializeField] private string devilCatcherName = "DEVILCATCHER";

    [Tooltip("コイン出現時のローカル座標")]
    [SerializeField] private Vector3 coinStartLocalPos = new Vector3(10.5422926f, 10.5422945f, 10.5422926f);

    [Tooltip("コイン移動終了時のローカル座標（ここから放します）")]
    [SerializeField] private Vector3 coinEndLocalPos = new Vector3(10.5422926f, 10.5422945f, 10.5422926f);

    [Tooltip("生成するコインのスケール")]
    [SerializeField] private Vector3 coinAnimationScale = new Vector3(10.5422926f, 10.5422945f, 10.5422926f);

    [Tooltip("コインの移動時間（秒）")]
    [SerializeField] private float coinAnimationDuration = 1.0f;

    [Tooltip("生成するコインの初期ローカル角度（オイラー角）")]
    [SerializeField] private Vector3 coinAnimationRotation = new Vector3(0f, -90f, 0f);

    [Tooltip("TriggerSoundPlayer反応時のコイン投入演出リピート回数")]
    [SerializeField] private int coinAnimationRepeatCount = 3;

    [Tooltip("制御対象とするUFOキャッチャーのライトオブジェクト群（ヒエラルキーからアタッチします）")]
    [SerializeField] private GameObject[] targetLights;

    [Header("Coin Insertion Light Settings")]
    [Tooltip("コイン投入中のみ点灯する追加のライトオブジェクト群")]
    [SerializeField] private GameObject[] coinInsertionLights;

    [Header("Light Audio Settings")]
    [Tooltip("ライト点滅時のチカッという音")]
    [SerializeField] private AudioClip lightFlickerSound;
    [Tooltip("点滅（起動）音の音量")]
    [SerializeField] [Range(0f, 10f)] private float lightFlickerVolume = 0.5f;
    [Tooltip("ライト消灯時のカチッという音")]
    [SerializeField] private AudioClip lightOffSound;
    [Tooltip("消灯音の音量")]
    [SerializeField] [Range(0f, 10f)] private float lightOffVolume = 0.5f;

    /// <summary>
    /// 指定された効果音を筐体メインのaudioSourceの位置から、末尾を指定秒数カットした状態で2D再生します。
    /// </summary>
    private void PlayLightSoundWithTailCut(AudioClip clip, float volume, float cutDuration)
    {
        if (clip == null) return;

        // メインスピーカーのGameObjectを親にして、一時的な再生オブジェクトを作成（一箇所再生を維持）
        GameObject tempAudioObj = new GameObject("TempLightAudioSource");
        if (audioSource != null)
        {
            tempAudioObj.transform.SetParent(audioSource.transform, false);
        }
        else
        {
            tempAudioObj.transform.SetParent(transform, false);
        }

        AudioSource tempSource = tempAudioObj.AddComponent<AudioSource>();
        tempSource.clip = clip;
        tempSource.volume = volume;
        tempSource.spatialBlend = 0.0f; // 2D再生
        tempSource.playOnAwake = false;
        tempSource.loop = false;

        tempSource.Play();

        // (効果音の長さ - カット秒数) 秒だけ再生して停止・破棄する
        float playDuration = Mathf.Max(0f, clip.length - cutDuration);
        StartCoroutine(StopAndDestroyAudioCoroutine(tempAudioObj, tempSource, playDuration));
    }

    private System.Collections.IEnumerator StopAndDestroyAudioCoroutine(GameObject audioObj, AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (source != null)
        {
            source.Stop();
        }
        if (audioObj != null)
        {
            Destroy(audioObj);
        }
    }

    /// <summary>
    /// 現在スポットライトおよび連動ライトが有効（点灯）になっているかどうか
    /// </summary>
    public static bool IsPlaySpotlightActive { get; private set; } = false;

    private App.Player.FirstPersonController _fpController;
    private UFOArmController _ufoController;
    private Camera _activeCamera;
    private Texture2D _bgTexture;



    // 元のライト強度を保存する辞書
    private System.Collections.Generic.Dictionary<Light, float> _originalLightIntensities = new System.Collections.Generic.Dictionary<Light, float>();
    // 元のライト範囲を保存する辞書
    private System.Collections.Generic.Dictionary<Light, float> _originalLightRanges = new System.Collections.Generic.Dictionary<Light, float>();

    /// <summary>
    /// 制御対象となるすべてのライトの元の強度と照射範囲をキャッシュします。
    /// </summary>
    private void CacheOriginalLightIntensities()
    {
        _originalLightIntensities.Clear();
        _originalLightRanges.Clear();

        if (playSpotlight != null)
        {
            foreach (var light in playSpotlight.GetComponentsInChildren<Light>(true))
            {
                if (light != null && !_originalLightIntensities.ContainsKey(light))
                {
                    _originalLightIntensities[light] = light.intensity;
                    _originalLightRanges[light] = light.range;
                }
            }
        }

        if (targetLights != null)
        {
            foreach (var obj in targetLights)
            {
                if (obj != null)
                {
                    foreach (var light in obj.GetComponentsInChildren<Light>(true))
                    {
                        if (light != null && !_originalLightIntensities.ContainsKey(light))
                        {
                            _originalLightIntensities[light] = light.intensity;
                            _originalLightRanges[light] = light.range;
                        }
                    }
                }
            }
        }
    }

    private int _paymentCount = 0;
    // MoneyManager のターン（ラウンド）が進むまで再プレイを禁止するためのフラグ。
    // 旧 RoundManager.CanPlayUfo() の代替。ターンが進むたびに false へリセットされる。
    private bool _hasPlayedThisRound = false;
    private float _playTimer = 0f;
    private int _triggeredCoinCount = 0;
    private int _completedCoinAnimationCount = 0;
    private int _destroyedCoinCount = 0;
    private float _feverTimer = 0f; // フィーバータイム残り時間

    /// <summary>
    /// フィーバータイム（制限時間ストップ）が現在有効かどうか
    /// </summary>
    public static bool IsFeverTimeActive => Instance != null && Instance._feverTimer > 0f;

    private bool _isTransitioning = false;
    private bool _hasPlayedLowTimeWarning = false;
    private Vector3 _originalPlayerCamPos;
    private Quaternion _originalPlayerCamRot;

    /// <summary>
    /// television 経由でのプレイセッション開始時、コイン投入演出＋television 移動アニメーションが
    /// 完了するまで true。この間はレバー/ボタン操作を受け付けない。
    /// </summary>
    private bool _controlsUnlocked = false;

    /// <summary>
    /// プレイヤーが実際にレバー/ボタンを操作した瞬間に true になり、以後 _playTimer のカウントダウンを開始する。
    /// </summary>
    private bool _timerStarted = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("[UFOCameraController] Multiple instances of UFOCameraController detected in the scene.");
        }
    }

    private void OnEnable()
    {
        TelevisionAnimator.OnCoinAnimationComplete += HandleTelevisionAnimationComplete;
    }

    private void OnDisable()
    {
        TelevisionAnimator.OnCoinAnimationComplete -= HandleTelevisionAnimationComplete;
    }

    /// <summary>
    /// television がゴール座標への移動を完了した時に呼ばれる。これ以降レバー/ボタン操作を解禁する。
    /// </summary>
    private void HandleTelevisionAnimationComplete()
    {
        _controlsUnlocked = true;
        Debug.Log("[UFOCameraController] television のアニメーションが完了しました。レバー/ボタン操作を解禁します。");
    }

    /// <summary>
    /// レバー/ボタンの操作を検知した際に LeverController / ButtonController から呼び出される。
    /// 最初の操作の瞬間にタイマーのカウントダウンを開始する。
    /// </summary>
    public void NotifyControlInputUsed()
    {
        if (_timerStarted) return;
        _timerStarted = true;
        Debug.Log("[UFOCameraController] レバー/ボタン操作を検知しました。タイマーのカウントダウンを開始します。");
    }

    /// <summary>
    /// Returns the currently active camera.
    /// </summary>
    public Camera GetActiveCamera()
    {
        if (IsPlayingUfo && _activeCamera != null)
        {
            return _activeCamera;
        }
        return playerCamera != null ? playerCamera : Camera.main;
    }

    /// <summary>
    /// 正面カメラ (frontCamera) を返します。
    /// </summary>
    public Camera FrontCamera => frontCamera != null ? frontCamera : (playerCamera != null ? playerCamera : Camera.main);

    private void Start()
    {
        // プレイヤーとUFOキャッチャーのアームコントローラーを自動検出
        _fpController = FindAnyObjectByType<App.Player.FirstPersonController>();
        _ufoController = GetComponent<UFOArmController>();
        if (_ufoController == null)
        {
            _ufoController = GetComponentInChildren<UFOArmController>(true);
        }
        if (_ufoController == null)
        {
            _ufoController = GetComponentInParent<UFOArmController>(true);
        }
        if (_ufoController == null)
        {
            _ufoController = FindAnyObjectByType<UFOArmController>();
        }

        if (ufoCenterTransform == null)
        {
            ufoCenterTransform = transform;
        }

        if (_fpController != null && playerCamera == null)
        {
            playerCamera = _fpController.GetComponentInChildren<Camera>(true);
        }

        // ===== デバッグログ =====
        Debug.Log($"[UFOCameraController] Start: fpController={(_fpController != null ? _fpController.name : "NULL")}");
        Debug.Log($"[UFOCameraController] Start: playerCamera={( playerCamera != null ? playerCamera.name : "NULL")}");
        Debug.Log($"[UFOCameraController] Start: ufoCenterTransform={ufoCenterTransform.name}");
        Debug.Log($"[UFOCameraController] Start: interactionDistance={interactionDistance}");
        // =======================

        SetupDynamicCameras();

        // AudioSourceの自動取得
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        // 開始時はUFOプレイモードではない状態にする（実際のセッション終了ではないのでイベント通知はしない）
        SetUfoMode(false, notifyListeners: false);

        // 開始時はライトをオフにしておく
        SetPlaySpotlight(false, false);
        SetCoinInsertionLightsActive(false);

        if (machineHover != null)
        {
            machineHover.OnClicked += OnMachineClicked;
        }

        // ラウンド（MoneyManager のターン）が進むたびに、UFOキャッチャーを再びプレイ可能にする。
        // MoneyManager は加法ロードされる別サブシーン側にいるため、MultiSceneLoader の非同期ロードが
        // 終わるまで Instance が null の可能性がある。Start() で直接 null チェックすると購読自体が
        // スキップされてしまうため、EffectManager と同様に Instance が現れるまで毎フレーム待ってから購読する。
        Observable.EveryUpdate()
            .Select(_ => MoneyManager.Instance)
            .Where(mm => mm != null)
            .First()
            .Subscribe(mm =>
            {
                mm.OnCurrentTurnChange
                    .Skip(1)
                    .Subscribe(_ => _hasPlayedThisRound = false)
                    .AddTo(this);
            })
            .AddTo(this);
    }

    private void SetupDynamicCameras()
    {
        if (frontCamera == null)
        {
            frontCamera = transform.Find("Ufo_camera")?.GetComponent<Camera>();
            if (frontCamera == null)
            {
                // 子オブジェクトからカメラを検索
                frontCamera = GetComponentInChildren<Camera>(true);
            }
        }

        if (frontCamera == null)
        {
            Debug.LogWarning("[UFOCameraController] 正面カメラ (frontCamera) が見つかりません。自動生成用カメラをベースにできません。");
            return;
        }

        // オーディオマネージャー等の警告対策として、UFO側のカメラのAudioListenerは無効化しておく
        var listener = frontCamera.GetComponent<AudioListener>();
        if (listener != null) listener.enabled = false;

        // 左側カメラの自動生成
        if (leftCamera == null)
        {
            leftCamera = CreateCopyCamera("Ufo_camera_left");
            leftCamera.transform.SetParent(ufoCenterTransform, false);
            // 正面 (9.98, 6.86, -3.465) から見て左側 (Z負方向) に配置
            leftCamera.transform.localPosition = new Vector3(0f, 6.86f, -13.465f);
            leftCamera.transform.localEulerAngles = new Vector3(25f, 0f, 0f);
        }

        // 右側カメラの自動生成
        if (rightCamera == null)
        {
            rightCamera = CreateCopyCamera("Ufo_camera_right");
            rightCamera.transform.SetParent(ufoCenterTransform, false);
            // 正面から見て右側 (Z正方向) に配置
            rightCamera.transform.localPosition = new Vector3(0f, 6.86f, 6.535f);
            rightCamera.transform.localEulerAngles = new Vector3(25f, 180f, 0f);
        }

        // 背面カメラの自動生成（インスペクター未設定時）
        if (backCamera == null)
        {
            backCamera = CreateCopyCamera("Ufo_camera_back");
            backCamera.transform.SetParent(ufoCenterTransform, false);
            backCamera.transform.localPosition = new Vector3(0f, 6.86f, -3.465f);
            backCamera.transform.localEulerAngles = new Vector3(25f, 90f, 0f);
        }
    }

    private Camera CreateCopyCamera(string name)
    {
        GameObject go = new GameObject(name);
        Camera newCam = go.AddComponent<Camera>();

        // 正面カメラの設定を複製
        newCam.fieldOfView = frontCamera.fieldOfView;
        newCam.nearClipPlane = frontCamera.nearClipPlane;
        newCam.farClipPlane = frontCamera.farClipPlane;
        newCam.cullingMask = frontCamera.cullingMask;
        newCam.clearFlags = frontCamera.clearFlags;
        newCam.backgroundColor = frontCamera.backgroundColor;
        newCam.depth = frontCamera.depth - 1; // プレイヤーカメラの下にするため低めに設定
        newCam.targetDisplay = frontCamera.targetDisplay; // Display2など使用中のDisplayに合わせる
        newCam.enabled = false;
        go.tag = frontCamera.tag; // タグ（MainCamera等）を引き継ぐ

        // URP対応：UniversalAdditionalCameraData のコピー
        var srcData = frontCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (srcData != null)
        {
            var destData = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            destData.renderShadows = srcData.renderShadows;
            destData.requiresDepthOption = srcData.requiresDepthOption;
            destData.requiresColorOption = srcData.requiresColorOption;
            destData.renderPostProcessing = srcData.renderPostProcessing;
            destData.antialiasing = srcData.antialiasing;
        }

        // AudioListenerがあれば無効化
        var listener = go.GetComponent<AudioListener>();
        if (listener != null) listener.enabled = false;

        return newCam;
    }

    private void Update()
    {
        if (_fpController == null)
        {
            _fpController = FindAnyObjectByType<App.Player.FirstPersonController>();
            if (_fpController == null) return;
        }

        // プレイヤーの当たり判定（CharacterControllerのカプセル体）の表面とUFOキャッチャー中心との最短距離を計算
        float distance = GetDistanceToPlayer(_fpController, ufoCenterTransform.position);
        bool isClose = distance <= interactionDistance;

        // ===== デバッグログ（毎秒1回だけ表示）=====
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"[UFOCameraController] 距離: {distance:F2} / 判定距離: {interactionDistance} / 近い: {isClose} / プレイ中: {IsPlayingUfo} / タイマー: {_playTimer:F1}");
        }
        // ===========================================

        if (!IsPlayingUfo)
        {
            bool canPlayUfo = !_hasPlayedThisRound;

            // プレイ可能かつ遷移中でない場合のみ、ホバーアウトラインを有効化する
            if (machineHover != null)
            {
                machineHover.enabled = canPlayUfo && !_isTransitioning;
            }
        }
        else
        {
            // フィーバータイムのカウントダウン
            if (_feverTimer > 0f)
            {
                _feverTimer -= Time.deltaTime;
                if (_feverTimer <= 0f)
                {
                    _feverTimer = 0f;
                    Debug.Log("[UFOCameraController] Fever Time Ended.");
                }
            }

            // プレイセッションがアクティブ、かつライトが点灯している場合のみカウントダウンを起動
            if (IsPlaySessionActive && IsPlaySpotlightActive)
            {
                // レバー/ボタンを実際に操作するまではタイマーを減少させない。
                // フィーバータイム中およびルーレットスピン中も同様に減少させない。
                if (_timerStarted && _feverTimer <= 0f && !IsRouletteTimePaused)
                {
                    _playTimer -= Time.deltaTime;
                }

                if (_playTimer <= 0f)
                {
                    bool isBusy = _ufoController != null && _ufoController.IsBusy;
                    string stateStr = _ufoController != null ? _ufoController.CurrentState.ToString() : "NULL";
                    string nameStr = _ufoController != null ? _ufoController.name : "NULL";
                    
                    if (Time.frameCount % 30 == 0)
                    {
                        Debug.Log($"[UFOCameraController] Timer: {_playTimer:F2}, Controller: {nameStr}, IsBusy: {isBusy}, State: {stateStr}");
                    }

                    // Only end the session if the claw arm is not busy, or as a fail-safe if the timer is way negative (e.g. -10s)
                    if (_ufoController == null || !_ufoController.IsBusy || _playTimer <= -10f)
                    {
                        _playTimer = 0f;
                        IsPlaySessionActive = false;
                        // 遷移中（UFOプレイモード中）はライトをつけたままにするため、ここでの自動消灯は行いません
                        _destroyedCoinCount = 0; // 反応数をリセット
                        Debug.Log("[UFOCameraController] UFO Catcher play session expired. Coin count reset.");

                        // もう追加でプレイできる回数が残っていなければ、自動でプレイヤー視点へ戻る
                        // （これにより OnUfoModeChanged(false) が発火し、獲得コイン・ダイヤの加算も確定する）
                        if (_paymentCount >= maxPlayCount)
                        {
                            Debug.Log("[UFOCameraController] 残りプレイ回数が0のため、自動的にUFOキャッチャーを終了します。");
                            StartCoroutine(ExitAfterRouletteRainRoutine());
                        }
                    }
                }
            }

            HandleUfoInput();
        }

        // 警告音（残り時間10秒以下）の再生・停止管理
        UpdateWarningSound();
    }

    public int   PaymentCount  => _paymentCount;
    public float RemainingTime => _playTimer;
    public int   MaxPlayCount  => maxPlayCount;
    public float PlayDuration  => playDuration;
    public bool  IsRouletteTimePaused { get; set; } = false;

    public void ResetPaymentCount()
    {
        _paymentCount = 0;
    }

    /// <summary>
    /// フィーバータイムを開始し、制限時間を指定秒数間ストップさせます。
    /// </summary>
    public void StartFeverTime(float duration)
    {
        _feverTimer = duration;
        Debug.Log($"[UFOCameraController] Fever Time Started for {duration} seconds!");
    }

    /// <summary>
    /// UFOキャッチャーの残り時間を seconds 秒延長する。
    /// スキルアイテム（時計）取得時などから呼び出す。
    /// </summary>
    public void AddPlayTime(float seconds)
    {
        if (!IsPlaySessionActive)
        {
            Debug.LogWarning("[UFOCameraController] AddPlayTime: ゲームセッションがアクティブではないため、時間延長をスキップします。");
            return;
        }
        _playTimer += seconds;
        Debug.Log($"[UFOCameraController] 残り時間を {seconds}秒延長しました。現在の残り時間: {_playTimer:F1}秒");
    }

    /// <summary>
    /// television の Play_Canvas2 側 "Play" から呼び出される、UFOキャッチャーのプレイセッション開始処理。
    /// costDevilCoins 分の所持金（MoneyManager 経由）を消費してから、選択された秒数をプレイ時間として使用する。
    /// 旧 UFODynamicUICanvas の Pay to Play と同じ一連の流れ（コイン投入演出 → 完了時に television がゴール座標へ移動 →
    /// カメラ切り替え解禁 → ライト点灯）を引き継ぐ。
    /// </summary>
    /// <returns>プレイセッションを開始できた場合 true。プレイ中・上限回数到達・所持金不足の場合は false。</returns>
    public bool StartPlaySessionFromTelevision(float durationSeconds, float costDevilCoins)
    {
        if (IsPlaySessionActive) return false;
        if (_paymentCount >= maxPlayCount) return false;

        if (MoneyManager.Instance != null)
        {
            if (MoneyManager.Instance.CurrentMoney < costDevilCoins) return false;
            MoneyManager.Instance.ReduceMoney(costDevilCoins);
        }
        else
        {
            Debug.LogWarning("[UFOCameraController] MoneyManager が見つからないため、決済をスキップしてプレイを開始します。");
        }

        playDuration = durationSeconds;

        _paymentCount++;
        _hasPlayedThisRound = true;
        _playTimer = playDuration;
        IsPlaySessionActive = true;
        _hasPlayedLowTimeWarning = false; // 新規セッション開始時に警告音再生フラグをリセット

        // アニメーション（コイン投入 → television 移動）完了までレバー/ボタン操作を禁止し、
        // 実際に操作されるまではタイマーも進めない
        _controlsUnlocked = false;
        _timerStarted = false;

        // コイン投入中のみ点灯する追加ライトをON
        SetCoinInsertionLightsActive(true);

        // コイン投入演出の開始（カウンターをリセットして1枚目を投入。完了時に television がゴール座標へ移動する）
        _triggeredCoinCount = 0;
        _completedCoinAnimationCount = 0;
        TriggerCoinInsertionAnimation();

        // コイン投入音の再生
        PlaySound(coinInsertSound);

        // カメラ切り替え（Q/E）を解禁したうえで、Back状態への切り替えを発火させ、
        // 砂嵐を挟んで Play_Canvas2 から TelevisionB_Canvas（Backカメラ映像）へ表示を切り替える
        var tvController = FindAnyObjectByType<TelevisionStaticController>();
        if (tvController != null)
        {
            tvController.EnableCameraSwitching();
        }
        SetSubCameraState(UfoSubCameraState.Back);

        Debug.Log($"[UFOCameraController] television 経由でプレイセッションを開始しました。Duration: {playDuration}s, Cost: {costDevilCoins} Devil Coins, Total plays: {_paymentCount}");
        return true;
    }

    /// <summary>
    /// コイン支払い時に、コインを出現させてDEVILCATCHERの中を指定座標間で移動し、物理落下させる演出コルーチン。
    /// </summary>
    private System.Collections.IEnumerator AnimateCoinInsertion()
    {
        GameObject activeCoinPrefab = coinPrefab;
        if (activeCoinPrefab == null)
        {
            // 自動フォールバック: ItemSpawner からゴールドコインのPrefabを探す
            if (ItemSpawner.Instance != null && ItemSpawner.Instance.goldCoinPrefab != null)
            {
                activeCoinPrefab = ItemSpawner.Instance.goldCoinPrefab;
            }
        }

        if (activeCoinPrefab == null)
        {
            Debug.LogWarning("[UFOCameraController] コイン投入演出用の coinPrefab がアタッチされておらず、自動取得も失敗したため演出をスキップします。");
            yield break;
        }

        // 親とする DEVILCATCHER オブジェクトをシーンから検索
        GameObject devilCatcher = GameObject.Find(devilCatcherName);
        if (devilCatcher == null)
        {
            // 表記揺れ対策
            devilCatcher = GameObject.Find("new_ufocatcher 1");
        }

        Transform parentTransform = devilCatcher != null ? devilCatcher.transform : null;

        // コインをインスタンス化
        GameObject coin = Instantiate(activeCoinPrefab);
        if (parentTransform != null)
        {
            // ローカル座標で動かすため親を設定
            coin.transform.SetParent(parentTransform, false);
        }

        // 初期座標・スケール・角度を設定
        coin.transform.localPosition = coinStartLocalPos;
        coin.transform.localScale = coinAnimationScale;
        coin.transform.localEulerAngles = coinAnimationRotation;

        // Rigidbodyを取得して一時的に物理挙動を無効化（Lerp移動させるため）
        Rigidbody rb = coin.GetComponent<Rigidbody>();
        bool originalKinematic = true;
        if (rb != null)
        {
            originalKinematic = rb.isKinematic;
            rb.isKinematic = true;
        }

        // 1秒間かけて Lerp 移動
        float elapsed = 0f;
        while (elapsed < coinAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / coinAnimationDuration);
            coin.transform.localPosition = Vector3.Lerp(coinStartLocalPos, coinEndLocalPos, t);
            yield return null;
        }

        // 移動完了後、放す（isKinematicを元に戻して物理落下させる）
        if (rb != null)
        {
            rb.isKinematic = originalKinematic;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // コイン投入演出の完了カウント
        _completedCoinAnimationCount++;
        if (_completedCoinAnimationCount >= coinAnimationRepeatCount)
        {
            Debug.Log($"[UFOCameraController] すべてのコイン({coinAnimationRepeatCount}枚)の投入アニメーションが完了しました。OnAllCoinsInserted を発火します。");
            OnAllCoinsInserted?.Invoke();
            OnCoinInserted?.Invoke();
        }
    }

    /// <summary>
    /// 外部（TriggerSoundPlayerなど）からコイン投入演出を連動してトリガーする公開メソッド。
    /// 設定された上限回数（coinAnimationRepeatCount）に達するまで、1回ずつコインを投入します。
    /// </summary>
    public void TriggerCoinInsertionAnimation()
    {
        if (_triggeredCoinCount < coinAnimationRepeatCount)
        {
            StartCoroutine(AnimateCoinInsertion());
            _triggeredCoinCount++;
            Debug.Log($"[UFOCameraController] 連動コイン投入を実行しました。回数: {_triggeredCoinCount} / {coinAnimationRepeatCount}");
        }
        else
        {
            Debug.Log($"[UFOCameraController] 連動コイン投入は上限回数（{coinAnimationRepeatCount}回）に達しているため実行しません。");
        }
    }

    /// <summary>
    /// CoinDestroyerZoneでコインが破棄された際に呼び出されます。
    /// 3回反応するとライトを点灯します。
    /// </summary>
    public void NotifyCoinDestroyed()
    {
        _destroyedCoinCount++;
        Debug.Log($"[UFOCameraController] CoinDestroyerZone反応数: {_destroyedCoinCount} / 3");

        if (_destroyedCoinCount >= 3)
        {
            SetCoinInsertionLightsActive(false);
        }
    }

    /// <summary>
    /// スポットライトおよびアタッチされたライト群、さらにUFOItemGoalのライト・マテリアルの有効/無効を一括設定します。
    /// 点灯（ON）時は徐々に速くなる点滅演出を行ってから完全点灯します。
    /// </summary>
    public void SetPlaySpotlight(bool active, bool playSound = true)
    {
        if (active)
        {
            // すでに点灯中・点灯演出中の二重起動を防ぐ
            if (IsPlaySpotlightActive) return;
            CacheOriginalLightIntensities(); // 元の強度をキャッシュ

            // ライト点滅開始の最初のタイミングで、起動音を末尾0.4秒カットして一度だけ流す
            if (playSound && lightFlickerSound != null)
            {
                PlayLightSoundWithTailCut(lightFlickerSound, lightFlickerVolume, 0.4f);
            }

            StartCoroutine(PlayLightOnFlickerCoroutine());
        }
        else
        {
            IsPlaySpotlightActive = false;
            ApplyRawLightsState(false);
            
            // 消灯音の再生（カットなしで最後まで流す）
            if (playSound && lightOffSound != null)
            {
                PlayLightSoundWithTailCut(lightOffSound, lightOffVolume, 0f);
            }
            Debug.Log("[UFOCameraController] ライトを消灯しました。");
        }
    }

    /// <summary>
    /// ライトオブジェクト群およびUFOItemGoalマテリアル・ライトの状態を明るさ倍率を適用して適用します。
    /// </summary>
    private void ApplyRawLightsState(bool active, float brightnessMultiplier = 1.0f)
    {
        if (playSpotlight != null)
        {
            playSpotlight.SetActive(active);
            if (active)
            {
                foreach (var light in playSpotlight.GetComponentsInChildren<Light>(true))
                {
                    if (light != null && _originalLightIntensities.TryGetValue(light, out float origInt))
                    {
                        light.intensity = origInt * brightnessMultiplier;
                        if (_originalLightRanges.TryGetValue(light, out float origRange))
                        {
                            light.range = origRange * brightnessMultiplier;
                        }
                    }
                }
            }
        }

        if (targetLights != null)
        {
            foreach (var lightObj in targetLights)
            {
                if (lightObj != null)
                {
                    lightObj.SetActive(active);
                    if (active)
                    {
                        foreach (var light in lightObj.GetComponentsInChildren<Light>(true))
                        {
                            if (light != null && _originalLightIntensities.TryGetValue(light, out float origInt))
                            {
                                light.intensity = origInt * brightnessMultiplier;
                                if (_originalLightRanges.TryGetValue(light, out float origRange))
                                {
                                    light.range = origRange * brightnessMultiplier;
                                }
                            }
                        }
                    }
                }
            }
        }

        var ufoGoal = FindAnyObjectByType<UFOItemGoal>();
        if (ufoGoal != null)
        {
            ufoGoal.SetInsertableItemsLights(active, brightnessMultiplier);
        }
    }

    /// <summary>
    /// 徐々に点滅が速くなりながら最終的に完全点灯するフリッカーコルーチン。
    /// 完全点灯した瞬間にタイマーのカウントダウンを開始します。
    /// </summary>
    private System.Collections.IEnumerator PlayLightOnFlickerCoroutine()
    {
        float duration = 1.8f;      // 演出時間を1.8秒に延長（+0.5秒）
        float elapsed = 0f;
        bool state = false;
        float minInterval = 0.03f;  // 後半の高速点滅の間隔
        float maxInterval = 0.22f;  // 前半の点滅間隔を0.35秒から0.22秒に逆補正（スピードアップ）

        while (elapsed < duration)
        {
            state = !state;

            // 経過時間割合(0〜1)に応じて明るさ倍率(0.15〜1.0)を算出
            float progress = elapsed / duration;
            float multiplier = Mathf.Lerp(0.15f, 1.0f, progress);

            // 点滅オン・オフに合わせて明るさ倍率を反映
            ApplyRawLightsState(state, multiplier);

            // 2乗のイージングをかけて、後半に向けて一気に加速させます
            float currentInterval = Mathf.Lerp(maxInterval, minInterval, progress * progress);

            yield return new WaitForSeconds(currentInterval);
            elapsed += currentInterval;
        }

        // 最終的に完全点灯状態にし、タイマーを起動
        ApplyRawLightsState(true, 1.0f);
        IsPlaySpotlightActive = true;
        Debug.Log("[UFOCameraController] ライト点滅演出が完了しました。常時点灯を開始しタイマーを起動します。");
    }

    private void OnMachineClicked()
    {
        if (IsPlayingUfo || _isTransitioning) return;

        if (_hasPlayedThisRound) return;

        // クリックした瞬間にライトを点灯
        SetPlaySpotlight(true);

        StartCoroutine(TransitionToUfoCamera());
    }

    private System.Collections.IEnumerator TransitionToUfoCamera()
    {
        _isTransitioning = true;

        if (machineHover != null)
        {
            machineHover.enabled = false;
        }

        if (_fpController != null)
        {
            _fpController.enabled = false;
            _fpController.SetAvatarVisibility(false);
            var animator = _fpController.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
            }
        }

        if (playerCamera != null)
        {
            _originalPlayerCamPos = playerCamera.transform.position;
            _originalPlayerCamRot = playerCamera.transform.rotation;
        }

        if (playerCamera != null && frontCamera != null)
        {
            Vector3 startPos = playerCamera.transform.position;
            Quaternion startRot = playerCamera.transform.rotation;
            Vector3 targetPos = frontCamera.transform.position;
            Quaternion targetRot = frontCamera.transform.rotation;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / transitionDuration;
                float e = transitionEase.Evaluate(Mathf.Clamp01(t));
                playerCamera.transform.position = Vector3.Lerp(startPos, targetPos, e);
                playerCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, e);
                yield return null;
            }

            playerCamera.transform.position = targetPos;
            playerCamera.transform.rotation = targetRot;
        }

        _isTransitioning = false;
        EnterUfoMode();
    }

    private System.Collections.IEnumerator TransitionBackToPlayerCamera()
    {
        _isTransitioning = true;

        IsPlayingUfo = false;
        OnUfoModeChanged?.Invoke(false); // UFO終了イベントを通知（ここで引き出しの演出が開始される）

        // 引き出しの演出（開く→見せる→閉じる）が終わるまで、プレイヤー視点への切り替えを待つ
        if (UFODrawerRewardDisplay.Instance != null)
        {
            yield return new WaitUntil(() => !UFODrawerRewardDisplay.Instance.IsShowing);
        }

        IsPlaySessionActive = false;
        SetPlaySpotlight(false);
        SetCoinInsertionLightsActive(false);

        // 途中でやめた場合・プレイが終了した場合ともに、television の画面を初期状態（Play_Canvas）へ戻す
        var tvControllerOnExit = FindAnyObjectByType<TelevisionStaticController>();
        if (tvControllerOnExit != null)
        {
            tvControllerOnExit.DisableCameraSwitching(); // カメラ切り替えを無効化しつつ Play_Canvas を表示する
        }

        if (ufoUiCanvas != null)
        {
            ufoUiCanvas.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerCamera != null)
        {
            Camera activeCam = _activeCamera != null ? _activeCamera : frontCamera;
            if (activeCam != null)
            {
                playerCamera.transform.position = activeCam.transform.position;
                playerCamera.transform.rotation = activeCam.transform.rotation;
            }
            playerCamera.enabled = true;
        }

        if (frontCamera != null) frontCamera.enabled = false;
        if (leftCamera != null) leftCamera.enabled = false;
        if (rightCamera != null) rightCamera.enabled = false;
        if (backCamera != null) backCamera.enabled = false;

        if (_fpController != null)
        {
            _fpController.SetAvatarVisibility(true);
        }

        if (playerCamera != null)
        {
            Vector3 startPos = playerCamera.transform.position;
            Quaternion startRot = playerCamera.transform.rotation;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / transitionDuration;
                float e = transitionEase.Evaluate(Mathf.Clamp01(t));
                playerCamera.transform.position = Vector3.Lerp(startPos, _originalPlayerCamPos, e);
                playerCamera.transform.rotation = Quaternion.Slerp(startRot, _originalPlayerCamRot, e);
                yield return null;
            }

            playerCamera.transform.position = _originalPlayerCamPos;
            playerCamera.transform.rotation = _originalPlayerCamRot;
        }

        if (_fpController != null)
        {
            _fpController.enabled = true;
        }

        var pickupController = FindAnyObjectByType<CupPickupController>();
        if (pickupController != null)
        {
            pickupController.enabled = true;
        }

        bool canPlayUfo = !_hasPlayedThisRound;
        if (machineHover != null)
        {
            machineHover.enabled = canPlayUfo;
        }

        _isTransitioning = false;
        Debug.Log("[UFOCameraController] Exit UFO mode completed.");
    }

    private void EnterUfoMode()
    {
        _paymentCount = 0;
        _playTimer = 0f;
        IsPlaySessionActive = false;
        _controlsUnlocked = false;
        _timerStarted = false;

        SetUfoMode(true);
        Debug.Log($"[UFOCameraController] EnterUfoMode: frontCamera={( frontCamera != null ? frontCamera.name + " display=" + frontCamera.targetDisplay : "NULL")}");
        Debug.Log($"[UFOCameraController] EnterUfoMode: leftCamera={(  leftCamera  != null ? leftCamera.name  : "NULL")}");
        Debug.Log($"[UFOCameraController] EnterUfoMode: rightCamera={( rightCamera != null ? rightCamera.name : "NULL")}");
        Debug.Log($"[UFOCameraController] EnterUfoMode: backCamera={(  backCamera  != null ? backCamera.name  : "NULL")}");
        
        if (frontCamera != null)
        {
            if (!frontCamera.gameObject.activeInHierarchy) frontCamera.gameObject.SetActive(true);
            frontCamera.enabled = true;
            _activeCamera = frontCamera;
        }
        
        // 開始時はBackカメラをアクティブに設定
        SetSubCameraState(UfoSubCameraState.Back);
    }

    private void ExitUfoMode()
    {
        if (_isTransitioning) return;
        StartCoroutine(TransitionBackToPlayerCamera());
    }

    /// <summary>
    /// タイマー終了による自動終了時、ルーレットのコイン降雨中（ItemSpawner.IsSpawning）であれば
    /// それが終わるまで待ち、さらに postRouletteRainExitDelay 秒待ってから ExitUfoMode() を呼ぶ。
    /// 操作自体はこの待機中も IsPlaySessionActive = false により既にロックされている。
    /// </summary>
    private System.Collections.IEnumerator ExitAfterRouletteRainRoutine()
    {
        while (ItemSpawner.Instance != null && ItemSpawner.IsSpawning)
        {
            yield return null;
        }

        yield return new WaitForSeconds(postRouletteRainExitDelay);

        Debug.Log("[UFOCameraController] コイン降雨終了（または無し）+ 待機完了。UFOキャッチャーを終了します。");
        ExitUfoMode();
    }

    /// <summary>
    /// television の Quit ボタン（TouchPanelOutlineController）等、外部から UFO キャッチャーモードの終了を
    /// リクエストするための公開メソッド。F / Escape キーでの終了と同じ処理を行う。
    /// </summary>
    public void RequestExitUfoMode()
    {
        ExitUfoMode();
    }

    private void SetUfoMode(bool active, bool notifyListeners = true)
    {
        IsPlayingUfo = active;

        // イベントを通じて外部UIやシステムに状態変更を通知（Observerパターン）
        // ※起動時の初期化呼び出し（Start内）では、実際にセッションが終了したわけではないため
        //   notifyListeners=false にして、引き出し演出等が誤って走らないようにする
        if (notifyListeners)
        {
            OnUfoModeChanged?.Invoke(active);
        }

        if (_fpController != null)
        {
            if (active)
            {
                // キャラクターの移動・回転アニメーションをリセットしてからスクリプトを停止する
                var animator = _fpController.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.SetFloat("Speed", 0f);
                }
            }
            _fpController.enabled = !active;
            _fpController.SetAvatarVisibility(!active); // UFOプレイ中はキャラクターモデルを非表示にする
        }

        // UFOプレイ中以外は他インタラクション（カップ回収等）を無効化するため、CupPickupController も無効化
        var pickupController = FindAnyObjectByType<CupPickupController>();
        if (pickupController != null)
        {
            pickupController.enabled = !active;
        }

        // プレイヤーカメラとUFO UIの有効化/無効化
        if (playerCamera != null)
        {
            playerCamera.enabled = !active;
        }

        if (ufoUiCanvas != null)
        {
            ufoUiCanvas.SetActive(active);
        }

        // カーソルの表示状態を切り替え
        if (active)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // 全てのUFOカメラを無効にする（描画負荷削減）
            if (frontCamera != null) frontCamera.enabled = false;
            if (leftCamera  != null) leftCamera.enabled  = false;
            if (rightCamera != null) rightCamera.enabled = false;
            if (backCamera  != null) backCamera.enabled  = false;

            // スポットライトもオフにする
            if (playSpotlight != null) playSpotlight.SetActive(false);
        }
    }

    private UfoSubCameraState _currentSubCameraState = UfoSubCameraState.Back;

    /// <summary>
    /// モニター用サブカメラ（Display 8 / Render Texture）の状態を切り替えます。
    /// 描画負荷を最小限に抑えるため、アクティブな1台のカメラのみ Camera.enabled = true に設定します。
    /// </summary>
    public void SetSubCameraState(UfoSubCameraState state)
    {
        _currentSubCameraState = state;

        if (backCamera  != null) backCamera.enabled  = (state == UfoSubCameraState.Back);
        if (leftCamera  != null) leftCamera.enabled  = (state == UfoSubCameraState.Left);
        if (rightCamera != null) rightCamera.enabled = (state == UfoSubCameraState.Right);

        Camera activeSubCam = GetSubCamera(state);
        if (activeSubCam != null && !activeSubCam.gameObject.activeInHierarchy)
        {
            activeSubCam.gameObject.SetActive(true);
        }

        OnSubCameraChanged?.Invoke(state);

        Debug.Log($"[UFOCameraController] サブカメラ切り替え → {state} (Camera: {activeSubCam?.name})");
    }

    public Camera GetSubCamera(UfoSubCameraState state)
    {
        switch (state)
        {
            case UfoSubCameraState.Left:  return leftCamera;
            case UfoSubCameraState.Right: return rightCamera;
            case UfoSubCameraState.Back:
            default:                      return backCamera;
        }
    }

    private void HandleUfoInput()
    {
        if (Keyboard.current == null) return;

        var tvController = FindAnyObjectByType<TelevisionStaticController>();

        // モード終了（F, Escape）※Qキーはカメラ操作に使用
        if (Keyboard.current.fKey.wasPressedThisFrame ||
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // Play_Canvas2（television の選択画面）を表示中は、UFO自体を終了せず砂嵐を挟んで Play_Canvas へ戻る
            // （まだ Play を押して支払いを開始していないため、いつでもキャンセル可能）
            if (tvController != null && tvController.IsPlayCanvas2Active)
            {
                var touchPanel = FindAnyObjectByType<TouchPanelOutlineController>();
                if (touchPanel != null)
                {
                    touchPanel.ReturnToPlay1();
                }
                else
                {
                    tvController.PlayStaticThenShowCanvas(tvController.PlayCanvas);
                }
            }
            else if (IsPlaySessionActive && !debugAllowInterruptDuringPlay)
            {
                // 実際に Play を押して有料セッションが開始された後は、通常は途中で中断できない
                // （デバッグ用チェックボックスがONの場合のみ強制終了を許可する）
            }
            else
            {
                ExitUfoMode();
            }
            return;
        }

        // Q / E キーでのサブカメラ切り替え（状態遷移）
        // TelevisionStaticController でカメラ切り替えが無効な場合はスキップ
        if (tvController != null && !tvController.IsCameraSwitchingEnabled) return;

        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            switch (_currentSubCameraState)
            {
                case UfoSubCameraState.Back:
                    SetSubCameraState(UfoSubCameraState.Left);
                    break;
                case UfoSubCameraState.Right:
                    SetSubCameraState(UfoSubCameraState.Back);
                    break;
                case UfoSubCameraState.Left:
                    // LeftのときQは反応しない
                    break;
            }
        }
        else if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            switch (_currentSubCameraState)
            {
                case UfoSubCameraState.Back:
                    SetSubCameraState(UfoSubCameraState.Right);
                    break;
                case UfoSubCameraState.Left:
                    SetSubCameraState(UfoSubCameraState.Back);
                    break;
                case UfoSubCameraState.Right:
                    // RightのときEは反応しない
                    break;
            }
        }
    }

    private void UpdateWarningSound()
    {
        // UFOItemGoal.IsFlashing (アイテム演出中) は警告音を鳴らさない
        bool shouldPlay = IsPlaySessionActive && _playTimer > 0f && _playTimer <= 10f && !UFOItemGoal.IsFlashing;

        if (shouldPlay)
        {
            if (!_hasPlayedLowTimeWarning)
            {
                _hasPlayedLowTimeWarning = true;
                if (lowTimeWarningSound != null)
                {
                    if (audioSource == null)
                    {
                        audioSource = GetComponent<AudioSource>();
                        if (audioSource == null)
                        {
                            audioSource = gameObject.AddComponent<AudioSource>();
                            audioSource.playOnAwake = false;
                            audioSource.spatialBlend = 0f;
                        }
                    }
                    if (audioSource != null)
                    {
                        audioSource.clip = lowTimeWarningSound;
                        audioSource.loop = true;
                        audioSource.volume = soundVolume;
                        audioSource.Play();
                    }
                }
            }
        }
        else
        {
            // フラグがONであるか、または実際に警告音が再生中の場合は確実に停止処理を実行
            bool isActuallyPlayingWarning = audioSource != null && audioSource.isPlaying && audioSource.clip == lowTimeWarningSound;
            if (_hasPlayedLowTimeWarning || isActuallyPlayingWarning)
            {
                _hasPlayedLowTimeWarning = false;
                if (audioSource != null && audioSource.clip == lowTimeWarningSound)
                {
                    audioSource.Stop();
                    audioSource.clip = null;
                    audioSource.loop = false;
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (_bgTexture != null)
        {
            Destroy(_bgTexture);
        }

        if (machineHover != null)
        {
            machineHover.OnClicked -= OnMachineClicked;
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null)
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0f; // 2D音響にして距離減衰を無視する
                }
            }
            audioSource.PlayOneShot(clip, soundVolume);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform center = ufoCenterTransform != null ? ufoCenterTransform : transform;
        
        // シーンビューで可視化するための黄色のワイヤー球を描画
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center.position, interactionDistance);

        // 中心点に小さな赤い球を描画して位置をわかりやすくする
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(center.position, 0.1f);
    }

    /// <summary>
    /// プレイヤーのCharacterController（カプセル型コライダー）の表面から、UFOキャッチャーの中心までの最短距離を計算します。
    /// </summary>
    private float GetDistanceToPlayer(App.Player.FirstPersonController player, Vector3 ufoCenter)
    {
        if (player == null) return float.MaxValue;

        var cc = player.GetComponent<CharacterController>();
        if (cc == null)
        {
            // CharacterControllerが見つからない場合は、プレイヤーの原点座標からの距離を返します
            return Vector3.Distance(player.transform.position, ufoCenter);
        }

        // プレイヤーのCharacterControllerのワールド空間における中心座標を計算
        Vector3 playerPos = player.transform.position;
        Vector3 capsuleCenter = playerPos + cc.center;
        
        // カプセルの円筒部分（上下の半球の中心を結ぶ線分）の半分の長さを計算
        float halfSegLength = Mathf.Max(0f, (cc.height * 0.5f) - cc.radius);

        // カプセルの上部球体中心と下部球体中心のワールド座標
        Vector3 pBottom = capsuleCenter + Vector3.down * halfSegLength;
        Vector3 pTop = capsuleCenter + Vector3.up * halfSegLength;

        // カプセルの中心線分（pBottom -> pTop）上で、ufoCenterに最も近い点を求める
        Vector3 segmentDirection = pTop - pBottom;
        float segmentLength = segmentDirection.magnitude;
        if (segmentLength < 0.0001f)
        {
            float dist = Vector3.Distance(pBottom, ufoCenter) - cc.radius;
            return Mathf.Max(0f, dist);
        }

        Vector3 segmentDirNormal = segmentDirection / segmentLength;
        float t = Vector3.Dot(ufoCenter - pBottom, segmentDirNormal);
        t = Mathf.Clamp(t, 0f, segmentLength);
        Vector3 closestPointOnSegment = pBottom + segmentDirNormal * t;

        // 最寄りの点からufoCenterまでの距離を求め、コライダー半径を引くことでカプセル表面からの最短距離を算出
        float distanceToSurface = Vector3.Distance(closestPointOnSegment, ufoCenter) - cc.radius;
        return Mathf.Max(0f, distanceToSurface);
    }

    private void SetCoinInsertionLightsActive(bool active)
    {
        if (coinInsertionLights != null)
        {
            foreach (var lightObj in coinInsertionLights)
            {
                if (lightObj != null)
                {
                    lightObj.SetActive(active);
                }
            }
        }
    }

    public void WriteSaveData(RoguelikeSaveData saveData)
    {
        saveData.isFinishUfoCatcherInRound = _hasPlayedThisRound;
    }
    public void ReadSaveData(RoguelikeSaveData saveData)
    {
        _hasPlayedThisRound = saveData.isFinishUfoCatcherInRound;
    }
}
