using System.Collections;
using MiniGames.Transitions;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// チュートリアル用の練習Devilキャッチャー(Practice_Cranegame)のセッション制御。
/// 実機の UFOCameraController / ItemSpawner / DevilItemGoal とは完全に独立しており、
/// ローグライクスキル・所持金・アイテム所持数など実機の永続データには一切影響しない。
///
/// LeverController / ButtonController の controlSourceOverride にこのコンポーネントを設定することで、
/// 実機のUFOCameraController(static Instance)を経由せず、このチュートリアルセッションだけを制御できる。
/// </summary>
public class TutorialCraneController : MonoBehaviour, ICraneControlSource, ISubCameraSource
{
    [Header("カメラ")]
    [Tooltip("プレイヤーの実カメラ（Main Camera）。未設定ならFirstPersonControllerから自動検出します")]
    [SerializeField] private Camera playerCamera;

    [Tooltip("チュートリアル視点の位置・向きを示すマーカー（Transformのみ）")]
    [SerializeField] private Transform tutorialCameraPos;

    [Header("プレイヤー制御")]
    [Tooltip("未設定なら自動検出します")]
    [SerializeField] private App.Player.FirstPersonController fpController;

    [Header("セッション設定")]
    [Tooltip("チュートリアルの制限時間（秒）")]
    [SerializeField] private float tutorialDuration = 30f;

    private bool _hasPlayedLowTimeWarning = false;

    [Header("チュートリアル開始時のローディング演出")]
    [Tooltip("開始時はすらーぷせず、SceneTransitionManager のローディング画面の裏で瞬間移動します。ローディング画面の最低表示時間（秒）")]
    [SerializeField] private float loadingMinimumDuration = 1.0f;

    [Header("テレビの移動")]
    [Tooltip("実機側の television オブジェクトについている TelevisionAnimator。チュートリアル専用の TV を別途用意せず、" +
             "これを移動して使い回す。移動先の座標は TelevisionAnimator 側の『チュートリアル座標』に設定する")]
    [SerializeField] private TelevisionAnimator televisionAnimator;

    [Tooltip("実機側の television に付いている TelevisionStaticController。未設定なら自動検出します")]
    [SerializeField] private TelevisionStaticController televisionStaticController;

    [Header("UI連携（実機のセッション開始と同じUI切り替え）")]
    [Tooltip("未設定なら GameUIManager.Instance を自動使用します")]
    [SerializeField] private GameUIManager gameUIManager;

    [Tooltip("未設定なら自動検出します")]
    [SerializeField] private DevilCatcherUIManager devilCatcherUIManager;

    [Header("アーム（任意・Start Descent/Toggle Claw完了検知用）")]
    [Tooltip("必ず練習機側のDevilControllerを手動で設定すること。実機と練習機の両方にUFOArmControllerが" +
             "存在し、名前も同じ「DevilController」のため、自動検出（FindAnyObjectByType）ではどちらが" +
             "見つかるか不定になり、実機側を誤って掴んでしまうことがある")]
    [SerializeField] private UFOArmController armController;

    private bool _armEventsSubscribed;

    private void EnsureArmController()
    {
        // 実機・練習機の両方にUFOArmControllerが存在し名前も同一のため、自動検出はしない
        // （手動でarmControllerに練習機側を設定しておく必要がある）
        if (armController != null && !_armEventsSubscribed)
        {
            Debug.Log($"[TutorialDebug] EnsureArmController: subscribing to {armController.gameObject.name} (id={armController.GetInstanceID()}), this TutorialCraneController id={GetInstanceID()}");
            armController.OnDescentCycleCompleted += () => OnDescentCycleCompleted?.Invoke();
            armController.OnClawToggleCompleted += () => OnClawToggleCompleted?.Invoke();
            _armEventsSubscribed = true;
        }
    }

    /// <summary>Start Descentの下降→掴み→上昇の一連動作が本当に完了した瞬間に発火する
    /// （UFOArmController.OnDescentCycleCompletedの転送）</summary>
    public event System.Action OnDescentCycleCompleted;

    /// <summary>Toggle Clawの手動開閉が完了した瞬間に発火する（UFOArmController.OnClawToggleCompletedの転送）</summary>
    public event System.Action OnClawToggleCompleted;

    /// <summary>Start Descentによる下降→掴み→上昇の一連動作中かどうか</summary>
    public bool IsArmDescentCycleBusy
    {
        get
        {
            EnsureArmController();
            return armController != null && armController.IsBusy;
        }
    }

    /// <summary>Toggle Clawによる爪の手動開閉動作中かどうか</summary>
    public bool IsArmClawToggling
    {
        get
        {
            EnsureArmController();
            return armController != null && armController.IsInputLocked && !armController.IsBusy;
        }
    }

    /// <summary>
    /// 連打等でアームの内部状態（手動開閉コルーチン・下降シーケンスの_state）がIdle以外のまま
    /// 固まってしまっている場合に備えて、強制的にリセットする。「完了検知で進む」ステップに入る
    /// 直前など、確実にクリーンな状態から操作させたい場面で呼ぶ。
    /// </summary>
    public void ForceReleaseArmLock()
    {
        EnsureArmController();
        armController?.ForceResetToIdle();
    }

    [Header("落とし口（任意・アイテム獲得演出/完了検知用）")]
    [Tooltip("未設定なら自動検出します")]
    [SerializeField] private TutorialItemGoal itemGoal;

    /// <summary>チュートリアルの落とし口にアイテムが入った瞬間に発火する</summary>
    public event System.Action<UFOItemType> OnItemGoalDropped;

    private void HandleItemGoalDropped(UFOItemType type) => OnItemGoalDropped?.Invoke(type);

    private bool _itemGoalEventsSubscribed;

    /// <summary>itemGoalがまだ見つかっていなければ再検索し、見つかり次第イベント購読する（armController同様、
    /// Awake()の実行順序に依存しないようにするため）。Inspectorで手動設定済みの場合でも購読漏れが
    /// 起きないよう、購読済みかどうかは別フラグで管理する</summary>
    private void EnsureItemGoal()
    {
        if (itemGoal == null)
        {
            itemGoal = FindAnyObjectByType<TutorialItemGoal>(FindObjectsInactive.Include);
        }

        if (itemGoal != null && !_itemGoalEventsSubscribed)
        {
            itemGoal.OnItemDropped += HandleItemGoalDropped;
            _itemGoalEventsSubscribed = true;
        }
    }

    [Header("コイン投入 Play Animation（実機の UFOCameraController.AnimateCoinInsertion 相当）")]
    [Tooltip("コイン投入演出で出現させるコインのPrefab（実機と同じPrefabを使い回してよい）")]
    [SerializeField] private GameObject coinPrefab;

    [Tooltip("コインの親にする Practice_Cranegame 側の Transform。実機は名前検索(DEVILCATCHER)だが、" +
             "実機と同名のオブジェクトが複製されていて誤って実機側を掴む恐れがあるため、こちらは直接参照する")]
    [SerializeField] private Transform coinParent;

    [Tooltip("コイン出現時のローカル座標")]
    [SerializeField] private Vector3 coinStartLocalPos;

    [Tooltip("コイン移動終了時のローカル座標（ここから物理落下させます）")]
    [SerializeField] private Vector3 coinEndLocalPos;

    [Tooltip("生成するコインのスケール")]
    [SerializeField] private Vector3 coinAnimationScale = Vector3.one;

    [Tooltip("コインの移動時間（秒）")]
    [SerializeField] private float coinAnimationDuration = 1.0f;

    [Tooltip("生成するコインの初期ローカル角度（オイラー角）")]
    [SerializeField] private Vector3 coinAnimationRotation = new Vector3(0f, -90f, 0f);

    [Tooltip("コイン投入演出のリピート回数（実機と同じく複数枚のコインを演出する）")]
    [SerializeField] private int coinAnimationRepeatCount = 3;

    [Tooltip("1枚のコインの移動が終わってから、次のコインが出現するまでの追加の間（秒）。" +
             "coinAnimationDurationちょうどだと、1枚目が物理落下し始めた瞬間に2枚目が出てきてしまい" +
             "詰まって見えるため、少し余白を持たせる")]
    [SerializeField, Min(0f)] private float coinInsertionGap = 0.5f;

    private int _triggeredCoinCountTutorial;

    [Header("Q/E サブカメラ切り替え（チュートリアル用・Practice_Cranegame側のカメラ）")]
    [Tooltip("Practice_Cranegame 側の Left カメラ。実機の leftCamera と同じ役割で、TV画面(canvasLeft)に表示される")]
    [SerializeField] private Camera leftCamera;
    [Tooltip("Practice_Cranegame 側の Right カメラ")]
    [SerializeField] private Camera rightCamera;
    [Tooltip("Practice_Cranegame 側の Back カメラ")]
    [SerializeField] private Camera backCamera;

    /// <summary>チュートリアルが終わって操作が返ってきた時に呼ばれる（Quit等、途中で切り上げた場合）</summary>
    public event System.Action OnTutorialFinished;

    /// <summary>チュートリアルのステップ演出を最後まで完了して終わった時に呼ばれる（Quitとは区別する）</summary>
    public event System.Action OnTutorialCompleted;

    /// <summary>ローディング（+ 瞬間移動）が完了し、練習機側での操作が始まった時に呼ばれる</summary>
    public event System.Action OnTutorialEntered;

    /// <summary>チュートリアルプレイ中かどうか</summary>
    public bool IsPlayingTutorial { get; private set; }

    /// <summary>いずれかのチュートリアルセッションがプレイ中かどうか（静的）。
    /// 実機のUFOCameraController側が「今はチュートリアルがQ/E等を処理しているので自分は何もしない」と
    /// 判定するために使う（実機とチュートリアルはインスタンス参照を持たず完全に独立させたいため、
    /// 静的フラグのみで疎結合に連携する）</summary>
    public static bool IsAnyTutorialPlaying { get; private set; }

    // ICraneControlSource
    public bool IsPlayingCrane => IsPlayingTutorial;

    /// <summary>指定した種別のボタンが実際に押された瞬間に発火（TutorialStepControllerが連打防止ロックに使う）</summary>
    public event System.Action<ButtonController.ButtonType> OnButtonPressed;

    public void NotifyButtonPressed(ButtonController.ButtonType buttonType)
    {
        OnButtonPressed?.Invoke(buttonType);
    }

    /// <summary>Play2_tutorialのPlayが押され、BeginTutorialPlay()で操作が解禁されたかどうか
    /// （DevilChaseLightControllerが、練習機のチェイス演出を開始してよいかの判定に使う）</summary>
    public bool AreControlsUnlocked => _controlsUnlocked;

    /// <summary>入力種別に関わらず共通の基礎条件（プレイ中・操作解禁済み・タイマー残あり）。
    /// レバー/ボタン/カメラ切り替えはこれに加えて、それぞれ独立したallowedフラグを見る</summary>
    private bool BaseControlActive => IsPlayingTutorial && _controlsUnlocked && _playTimer > 0f;

    // 実機のUFOCameraController.IsControlActiveと同じく、_timerStartedは条件に含めない。
    // _timerStartedはNotifyControlInputUsed（操作された瞬間）でしか true にならないため、
    // ここに含めると「操作できるまで操作を受け付けない」循環参照になり、一切操作できなくなる。
    public bool IsControlActive => BaseControlActive && _leverInputAllowed;

    /// <summary>Q/Eによるサブカメラ切り替えを受け付けてよい状態かどうか</summary>
    public bool IsCameraSwitchActive => BaseControlActive && _cameraSwitchAllowed;

    private bool _leverInputAllowed = true;
    private bool _cameraSwitchAllowed = true;
    private bool _startDescentAllowed = true;
    private bool _toggleClawAllowed = true;
    private bool _feverTimeButtonAllowed = true;

    public bool IsButtonTypeActive(ButtonController.ButtonType buttonType)
    {
        if (!BaseControlActive)
        {
            Debug.Log($"[TutorialDebug] BaseControlActive=false: IsPlayingTutorial={IsPlayingTutorial}, " +
                      $"_controlsUnlocked={_controlsUnlocked}, _playTimer={_playTimer}, _timerPaused={_timerPaused}");
            return false;
        }
        switch (buttonType)
        {
            case ButtonController.ButtonType.StartDescent: return _startDescentAllowed;
            case ButtonController.ButtonType.ToggleClaw: return _toggleClawAllowed;
            case ButtonController.ButtonType.FeverTime: return _feverTimeButtonAllowed;
            default: return true;
        }
    }

    /// <summary>指定した種別のボタン操作を一時的に禁止/許可する</summary>
    public void SetButtonTypeAllowed(ButtonController.ButtonType buttonType, bool allowed)
    {
        switch (buttonType)
        {
            case ButtonController.ButtonType.StartDescent: _startDescentAllowed = allowed; break;
            case ButtonController.ButtonType.ToggleClaw: _toggleClawAllowed = allowed; break;
            case ButtonController.ButtonType.FeverTime: _feverTimeButtonAllowed = allowed; break;
        }
    }

    /// <summary>全種別のボタン操作をまとめて禁止/許可する</summary>
    public void SetButtonInputAllowed(bool allowed)
    {
        _startDescentAllowed = allowed;
        _toggleClawAllowed = allowed;
        _feverTimeButtonAllowed = allowed;
    }

    /// <summary>特定のチュートリアルステップ中、レバー操作を一時的に禁止/許可する。
    /// ボタン・カメラ切り替えとは完全に独立しており、これをOFFにしても他の入力には影響しない</summary>
    public void SetLeverInputAllowed(bool allowed)
    {
        _leverInputAllowed = allowed;
    }

    /// <summary>特定のチュートリアルステップ中、Q/Eによるカメラ切り替えを一時的に禁止/許可する</summary>
    public void SetCameraSwitchAllowed(bool allowed)
    {
        _cameraSwitchAllowed = allowed;
    }

    /// <summary>チュートリアル中のキー「3」（television収納/復元トグル）の入力を一時的に禁止/許可する</summary>
    public void SetKey3InputAllowed(bool allowed)
    {
        if (televisionAnimator != null)
        {
            televisionAnimator.SetTutorialKey3InputAllowed(allowed);
        }
    }

    private Vector3 _originalPlayerCamPos;
    private Quaternion _originalPlayerCamRot;
    private CameraClearFlags _originalPlayerCamClearFlags;
    private Color _originalPlayerCamBackgroundColor;
    private Vector3 _originalTelevisionPos;
    private Quaternion _originalTelevisionRot;
    private bool _controlsUnlocked;
    private bool _timerStarted;
    private float _playTimer;
    private bool _timerPaused;
    private bool _isTransitioning;
    private UFOCameraController.UfoSubCameraState _currentSubCameraState = UFOCameraController.UfoSubCameraState.Back;

    /// <summary>現在のモニターのサブカメラ状態（Back/Left/Right）。チュートリアルステップ側の完了検知に使う</summary>
    public UFOCameraController.UfoSubCameraState CurrentSubCameraState => _currentSubCameraState;

    private void Awake()
    {
        if (fpController == null)
        {
            fpController = FindAnyObjectByType<App.Player.FirstPersonController>();
        }
        if (playerCamera == null && fpController != null)
        {
            playerCamera = fpController.GetComponentInChildren<Camera>(true);
        }
        if (televisionStaticController == null)
        {
            televisionStaticController = FindAnyObjectByType<TelevisionStaticController>();
        }
        if (gameUIManager == null)
        {
            gameUIManager = GameUIManager.Instance != null ? GameUIManager.Instance : FindAnyObjectByType<GameUIManager>();
        }
        if (devilCatcherUIManager == null)
        {
            devilCatcherUIManager = FindAnyObjectByType<DevilCatcherUIManager>();
        }
        // armController/itemGoalは、他スクリプトのAwake()実行順序に依存しないよう、
        // ここで一度試すだけでなく実際に使う時（Ensure〜()）にも再試行する
        EnsureArmController();
        EnsureItemGoal();
    }

    private void Update()
    {
        if (!IsPlayingTutorial || _isTransitioning) return;

        // itemGoalはArmController等と違い毎フレーム参照されるプロパティが無いため、
        // ここで明示的に再試行して順序依存を無くす（見つかり次第イベント購読される）。
        // armControllerも同様。以前はTutorialStepController側のポーリング（プロパティアクセス）が
        // 毎フレームEnsureArmController()を間接的に呼んでいたため、Awake()時点で解決できなくても
        // 後から自動的に再試行されていたが、ポーリングをイベント購読方式に置き換えた際に
        // その再試行機会も失われていた。購読はここで確実に行う。
        EnsureItemGoal();
        EnsureArmController();

        if (_controlsUnlocked && _cameraSwitchAllowed)
        {
            HandleSubCameraInput();
        }

        if (_controlsUnlocked && _timerStarted && _playTimer > 0f && !_timerPaused)
        {
            _playTimer -= Time.deltaTime;
            if (_playTimer <= 0f)
            {
                _playTimer = 0f;
                // 以前はここでExitTutorial()を呼んでいたが、タイマー切れでチュートリアルを
                // 終了させるのはやめた。終了はチュートリアルステップが最後まで完了した時
                // （CompleteTutorial）にのみ行う。タイマー切れ自体はイベントとして通知するだけ。
                DevilBGMManager.Instance?.StopPracticeBgm();
                OnTimerExpired?.Invoke();
            }
        }

        UpdateWarningSound();
    }

    /// <summary>
    /// 残り時間が10秒を切ったら警告音をループ再生する（実機のUFOCameraController.UpdateWarningSoundと
    /// 同じロジック）。パトランプは複数箇所に配置されるため、音はここに一元化して1回だけ鳴らす。
    /// </summary>
    private void UpdateWarningSound()
    {
        bool shouldPlay = IsPlayingTutorial && _playTimer > 0f && _playTimer <= 10f;

        if (shouldPlay)
        {
            if (!_hasPlayedLowTimeWarning)
            {
                _hasPlayedLowTimeWarning = true;
                DevilSEManager.Instance?.StartLowTimeWarning(isPractice: true);
            }
        }
        else
        {
            bool isActuallyPlayingWarning = DevilSEManager.Instance != null && DevilSEManager.Instance.IsLowTimeWarningActive(isPractice: true);
            if (_hasPlayedLowTimeWarning || isActuallyPlayingWarning)
            {
                _hasPlayedLowTimeWarning = false;
                DevilSEManager.Instance?.StopLowTimeWarning(isPractice: true);
            }
        }

        // BGMのテンポ切り替えは、演出中でも警告音を止めるような例外(前述コメント参照)に
        // 引きずられないよう、shouldPlay(残り時間だけの判定)に直接連動させる
        DevilBGMManager.Instance?.SetPracticeLowTime(shouldPlay);
    }

    /// <summary>タイマーが0になった瞬間に発火する。チュートリアルステップ側が
    /// 「制限時間切れで次のステップへ進む」演出に使う</summary>
    public event System.Action OnTimerExpired;

    /// <summary>
    /// タイマーを指定秒数にリセットし、_timerStartedもfalseに戻す（次にレバー/ボタンを操作した瞬間から
    /// 再びカウントダウンが始まる、通常の開始と同じ挙動）。チュートリアルの後半でもう一度
    /// 制限時間ありのプレイを体験させたいステップ用。
    /// </summary>
    public void ResetTimer(float seconds)
    {
        _playTimer = seconds;
        _timerStarted = false;
        _timerPaused = false;
        _hasPlayedLowTimeWarning = false;
        DevilBGMManager.Instance?.SetPracticeLowTime(false);
    }

    /// <summary>タイマーの残り秒数。チュートリアルステップ側から閾値監視に使う</summary>
    public float RemainingTime => _playTimer;

    /// <summary>タイマーが動き出しているか（レバー/ボタンを一度でも操作したか）</summary>
    public bool IsTimerStarted => _timerStarted;

    /// <summary>
    /// タイマーを指定秒数に固定して一時停止する。チュートリアルステップ演出側が、
    /// ある残り秒数に達した瞬間に説明を挟みたい場合などに使う。
    /// </summary>
    public void PauseTimerAt(float seconds)
    {
        _timerPaused = true;
        _playTimer = seconds;
    }

    /// <summary>PauseTimerAtで止めたタイマーを再開する</summary>
    public void ResumeTimer()
    {
        _timerPaused = false;
    }

    /// <summary>
    /// 練習機側のDevilCatcherUIManager（price_table等）の表示だけを一時的に切り替える。
    /// TutorialStepControllerのfullDarkBackgroundステップから呼ばれる。
    /// </summary>
    public void SetPracticeUIVisible(bool visible)
    {
        if (devilCatcherUIManager != null)
        {
            devilCatcherUIManager.SetUIVisibleExternal(visible);
        }
    }

    /// <summary>
    /// 残り時間を延長する（実機のUFOCameraController.AddPlayTime相当）。
    /// TutorialItemGoalが時計獲得時に呼び出す。
    /// </summary>
    public void AddPlayTime(float seconds)
    {
        if (!IsPlayingTutorial)
        {
            Debug.LogWarning("[TutorialCraneController] AddPlayTime: チュートリアルがアクティブではないため、時間延長をスキップします。");
            return;
        }
        _playTimer += seconds;
        Debug.Log($"[TutorialCraneController] 残り時間を {seconds}秒延長しました。現在の残り時間: {_playTimer:F1}秒");
    }

    /// <summary>
    /// 実機の UFOCameraController.HandleUfoInput の Q/E 部分と同じロジック。
    /// Practice_Cranegame 側の Left/Right/Back カメラを切り替え、共有の TelevisionStaticController へ通知する。
    /// </summary>
    private void HandleSubCameraInput()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            switch (_currentSubCameraState)
            {
                case UFOCameraController.UfoSubCameraState.Back:
                    SetSubCameraState(UFOCameraController.UfoSubCameraState.Left);
                    break;
                case UFOCameraController.UfoSubCameraState.Right:
                    SetSubCameraState(UFOCameraController.UfoSubCameraState.Back);
                    break;
            }
        }
        else if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            switch (_currentSubCameraState)
            {
                case UFOCameraController.UfoSubCameraState.Back:
                    SetSubCameraState(UFOCameraController.UfoSubCameraState.Right);
                    break;
                case UFOCameraController.UfoSubCameraState.Left:
                    SetSubCameraState(UFOCameraController.UfoSubCameraState.Back);
                    break;
            }
        }
    }

    private void SetSubCameraState(UFOCameraController.UfoSubCameraState state)
    {
        _currentSubCameraState = state;
        if (backCamera != null) backCamera.enabled = (state == UFOCameraController.UfoSubCameraState.Back);
        if (leftCamera != null) leftCamera.enabled = (state == UFOCameraController.UfoSubCameraState.Left);
        if (rightCamera != null) rightCamera.enabled = (state == UFOCameraController.UfoSubCameraState.Right);

        if (televisionStaticController != null)
        {
            televisionStaticController.HandleSubCameraChanged(state);
        }
    }

    /// <summary>ISubCameraSource実装。TelevisionStaticControllerがQ/E切り替え時にこちらを参照する（実機のUFOCameraController.GetSubCameraの代わり）</summary>
    public Camera GetSubCamera(UFOCameraController.UfoSubCameraState state)
    {
        switch (state)
        {
            case UFOCameraController.UfoSubCameraState.Left: return leftCamera;
            case UFOCameraController.UfoSubCameraState.Right: return rightCamera;
            case UFOCameraController.UfoSubCameraState.Back:
            default: return backCamera;
        }
    }

    /// <summary>チュートリアル開始（TVのYesボタン等から呼ぶ）</summary>
    public void EnterTutorial()
    {
        Debug.Log($"[TutorialDebug] EnterTutorial called: IsPlayingTutorial={IsPlayingTutorial}, _isTransitioning={_isTransitioning}");
        if (IsPlayingTutorial || _isTransitioning) return;
        StartCoroutine(EnterTutorialRoutine());
    }

    private IEnumerator EnterTutorialRoutine()
    {
        _isTransitioning = true;
        _controlsUnlocked = false;
        _timerStarted = false;
        _timerPaused = false;
        _leverInputAllowed = true;
        _cameraSwitchAllowed = true;
        _startDescentAllowed = true;
        _toggleClawAllowed = true;
        _feverTimeButtonAllowed = true;
        _playTimer = tutorialDuration;
        _hasPlayedLowTimeWarning = false;

        if (fpController != null)
        {
            fpController.enabled = false;
            fpController.SetAvatarVisibility(false);
        }

        if (playerCamera != null)
        {
            _originalPlayerCamPos = playerCamera.transform.position;
            _originalPlayerCamRot = playerCamera.transform.rotation;

            // チュートリアル中は周りを真っ白な空間に見せるため、プレイヤーカメラの背景を白い単色に上書きする
            // （実機プレイ時の見た目に影響しないよう、元の設定はExitTutorialRoutineで必ず復元する）
            _originalPlayerCamClearFlags = playerCamera.clearFlags;
            _originalPlayerCamBackgroundColor = playerCamera.backgroundColor;
            playerCamera.clearFlags = CameraClearFlags.SolidColor;
            playerCamera.backgroundColor = Color.black;
        }

        if (televisionAnimator != null)
        {
            televisionAnimator.GetCurrentTransform(out _originalTelevisionPos, out _originalTelevisionRot);
            televisionAnimator.SetTutorialModeActive(true);
        }

        // Q/E のサブカメラ切り替えを、実機の UFOCameraController の代わりにこちら（練習機側）へ差し替える
        // （切り替え自体はまだ有効化しない。BeginTutorialPlay() で実機と同様に解禁する）
        if (televisionStaticController != null)
        {
            televisionStaticController.SetSubCameraSourceOverride(this);
        }
        _currentSubCameraState = UFOCameraController.UfoSubCameraState.Back;
        if (backCamera != null) backCamera.enabled = false;
        if (leftCamera != null) leftCamera.enabled = false;
        if (rightCamera != null) rightCamera.enabled = false;

        // すらーぷはせず、ローディング画面の裏で瞬間移動する
        if (SceneTransitionManager.Instance != null)
        {
            bool loadingDone = false;
            SceneTransitionManager.Instance.ShowTurnTransition(
                loadingMinimumDuration,
                onDuringLoading: WarpToTutorialPositions,
                onComplete: () => loadingDone = true);

            yield return new WaitUntil(() => loadingDone);
        }
        else
        {
            WarpToTutorialPositions();
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        IsPlayingTutorial = true;
        IsAnyTutorialPlaying = true;

        // 前回のプレイで獲得演出（時計・ブラックダイヤ等）の最中に中断された場合、
        // IsFlashingがtrueのまま固まっている可能性があるため、新しいセッション開始時に強制リセットする
        EnsureItemGoal();
        itemGoal?.ResetStuckFlashState();

        // ここではまだレバー/ボタン操作を解禁しない。Play_tutorial → Play2_tutorial のメニューを
        // 一通り見せてから、Play2_tutorial の Play が押された瞬間に BeginTutorialPlay() で解禁する
        // （実機が StartPlaySessionFromTelevision 直後は _controlsUnlocked = false で、
        // コイン投入演出が終わってから操作を解禁するのと同じ考え方）。
        _controlsUnlocked = false;
        Debug.Log("[TutorialDebug] _controlsUnlocked set to FALSE (EnterTutorialRoutine end)");
        _isTransitioning = false;
        OnTutorialEntered?.Invoke();
    }

    private void WarpToTutorialPositions()
    {
        if (playerCamera != null && tutorialCameraPos != null)
        {
            playerCamera.transform.SetPositionAndRotation(tutorialCameraPos.position, tutorialCameraPos.rotation);
        }

        if (televisionAnimator != null)
        {
            televisionAnimator.SetToTutorialTransform();
        }
    }

    /// <summary>
    /// Play2_tutorial の Play が押された瞬間に呼ばれる。実機と同じ操作方法にするため、
    /// コイン投入演出（物理オブジェクト）を再生し、television をスタート座標(チュートリアル)から
    /// ゴール座標(チュートリアル)へアニメーション移動させ、Q/E のサブカメラ切り替えを解禁する。
    /// レバー/ボタン操作もここで解禁する（実機がコイン投入演出の完了後に操作を解禁するのと同じ考え方）。
    /// タイマー自体は、解禁後に実際にレバー/ボタンが操作された瞬間（NotifyControlInputUsed）から進み始める。
    /// </summary>
    public void BeginTutorialPlay()
    {
        Debug.Log($"[TutorialDebug] BeginTutorialPlay called: IsPlayingTutorial={IsPlayingTutorial}");
        if (!IsPlayingTutorial) return;
        _controlsUnlocked = true;
        Debug.Log("[TutorialDebug] _controlsUnlocked set to TRUE");

        // 実機の StartPlaySessionFromTelevision 直後（IsPlaySessionActive = true）と同じUI切り替えを行う。
        // 実機の UFOCameraController.IsPlaySessionActive 自体には触れない（別コンポーネント扱いのため）。
        if (gameUIManager != null) gameUIManager.ApplySessionActiveUIState(true);
        if (devilCatcherUIManager != null) devilCatcherUIManager.ApplySessionActiveUIState(true);

        _currentSubCameraState = UFOCameraController.UfoSubCameraState.Back;
        if (backCamera != null) backCamera.enabled = true;
        if (leftCamera != null) leftCamera.enabled = false;
        if (rightCamera != null) rightCamera.enabled = false;

        if (televisionStaticController != null)
        {
            televisionStaticController.SetCameraSwitchingEnabled(true);
        }

        if (televisionAnimator != null)
        {
            televisionAnimator.PlayCoinAnimationTutorial(() =>
            {
                if (televisionStaticController != null)
                {
                    // HandleSubCameraChangedは内部でSyncCanvasWorldCameras（チュートリアル側backCameraへの
                    // 割り当て。これをやらないとQ/Eを押すまで実機のbackCameraが映り続けてしまう）→
                    // 砂嵐演出→UpdateCanvasVisibleの順に行うため、直接呼ぶより実機と同じ「砂嵐を挟んだ
                    // 切り替え」になる（以前はSyncCanvasWorldCameras/UpdateCanvasVisibilityを直接呼んで
                    // いたため、この最初の切り替えだけ砂嵐が出なかった）
                    televisionStaticController.HandleSubCameraChanged(UFOCameraController.UfoSubCameraState.Back);
                }
            });
        }

        _triggeredCoinCountTutorial = 0;
        StartCoroutine(TriggerCoinInsertionRepeatedly());
    }

    /// <summary>
    /// 実機の TriggerSoundPlayer によるコイン投入アニメーションの連続トリガーを、
    /// アニメーションイベントの代わりに自動的に行う。1枚分の移動時間（coinAnimationDuration）が
    /// 終わってから、さらにcoinInsertionGap秒待ってから次のコインを投入することで、
    /// 1枚目が物理落下し始めた瞬間に2枚目が出てきて詰まって見えるのを防ぐ
    /// </summary>
    private IEnumerator TriggerCoinInsertionRepeatedly()
    {
        for (int i = 0; i < coinAnimationRepeatCount; i++)
        {
            TriggerCoinInsertionAnimationTutorial();
            yield return new WaitForSeconds(coinAnimationDuration + coinInsertionGap);
        }
    }

    /// <summary>
    /// 実機の UFOCameraController.TriggerCoinInsertionAnimation 相当。
    /// 設定された上限回数（coinAnimationRepeatCount）に達するまで、1回ずつコインを投入する。
    /// </summary>
    public void TriggerCoinInsertionAnimationTutorial()
    {
        if (_triggeredCoinCountTutorial < coinAnimationRepeatCount)
        {
            StartCoroutine(AnimateCoinInsertionTutorial());
            _triggeredCoinCountTutorial++;
        }
    }

    /// <summary>
    /// 実機の UFOCameraController.AnimateCoinInsertion 相当。
    /// コインを出現させ、Practice_Cranegame 側の coinParent 配下でローカル座標間を移動させたのち、物理落下させる。
    /// 実機は GameObject.Find(devilCatcherName) で親を探すが、同名オブジェクトが複製されて誤爆する恐れがあるため、
    /// こちらは coinParent を直接参照する。
    /// </summary>
    private IEnumerator AnimateCoinInsertionTutorial()
    {
        if (coinPrefab == null)
        {
            Debug.LogWarning("[TutorialCraneController] コイン投入演出用の coinPrefab がアタッチされていないため演出をスキップします。");
            yield break;
        }

        if (coinParent == null)
        {
            // 親未設定のまま生成すると、coinStartLocalPos/coinEndLocalPos（実機からコピーしたローカル座標）が
            // そのままワールド座標として扱われ、実機側の位置にコインが出現してしまう。
            // それを防ぐため、親が未設定の間は演出自体をスキップする。
            Debug.LogWarning("[TutorialCraneController] コイン投入演出用の coinParent が未設定のため演出をスキップします（未設定のまま生成すると実機側の位置に出現してしまいます）。");
            yield break;
        }

        GameObject coin = Instantiate(coinPrefab);
        coin.transform.SetParent(coinParent, false);

        coin.transform.localPosition = coinStartLocalPos;
        coin.transform.localScale = coinAnimationScale;
        coin.transform.localEulerAngles = coinAnimationRotation;

        Rigidbody rb = coin.GetComponent<Rigidbody>();
        bool originalKinematic = true;
        if (rb != null)
        {
            originalKinematic = rb.isKinematic;
            rb.isKinematic = true;
        }

        float elapsed = 0f;
        while (elapsed < coinAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / coinAnimationDuration);
            coin.transform.localPosition = Vector3.Lerp(coinStartLocalPos, coinEndLocalPos, t);
            yield return null;
        }

        if (rb != null)
        {
            rb.isKinematic = originalKinematic;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// レバー/ボタンが実際に操作された瞬間に呼ばれる。最初の操作でタイマーのカウントダウンを開始する
    /// （実機のUFOCameraController.NotifyControlInputUsedと同じ考え方）。
    /// 最初の操作の瞬間にBGMの再生も開始する（実機のOnControlInputUsed→DevilBGMManagerと同じ考え方）。
    /// </summary>
    public void NotifyControlInputUsed()
    {
        if (_timerStarted) return;
        _timerStarted = true;
        DevilBGMManager.Instance?.StartPracticeBgm();
    }

    public Camera GetActiveCamera() => playerCamera != null ? playerCamera : Camera.main;

    /// <summary>
    /// チュートリアル終了。実機のような報酬演出（引き出し・カウント加算）は一切行わない。
    /// ここではプレイヤーへ操作を返さず、実機の television を見ている状態（Devil_Camera_Front_Pos）へ戻すだけにする。
    /// FirstPersonController は無効のまま、カーソルも表示状態のまま維持し、Tutorial_Canvas の Yes/No 操作に備える。
    /// Play_tutorial の Quit から呼ばれる（チュートリアルを最後まで見ずに途中で切り上げるケース）。
    /// </summary>
    public void ExitTutorial()
    {
        if (!IsPlayingTutorial || _isTransitioning) return;
        StartCoroutine(ExitTutorialRoutine(completedViaSteps: false));
    }

    /// <summary>
    /// チュートリアルのステップ演出が最後まで完了した時に呼ぶ。ExitTutorial()と違い、終了後は
    /// Tutorial_Canvas（Yes/No）を出さず、いつも通り実機のPlay_Canvasへ直接戻す。
    /// </summary>
    public void CompleteTutorial()
    {
        if (!IsPlayingTutorial || _isTransitioning) return;
        StartCoroutine(ExitTutorialRoutine(completedViaSteps: true));
    }

    private IEnumerator ExitTutorialRoutine(bool completedViaSteps)
    {
        _isTransitioning = true;
        IsPlayingTutorial = false;
        IsAnyTutorialPlaying = false;
        _controlsUnlocked = false;

        // タイマー満了より前に途中で終了（Quit等）した場合の保険として、ここでもBGMを止めておく
        // （タイマー満了時は既にOnTimerExpired発火箇所で止めているため、こちらは二重呼び出しでも無害）
        DevilBGMManager.Instance?.StopPracticeBgm();

        // UIも実機のセッション終了時と同じ状態（閲覧用フォーカス表示 / price_table非表示）に戻す
        if (gameUIManager != null) gameUIManager.ApplySessionActiveUIState(false);
        if (devilCatcherUIManager != null) devilCatcherUIManager.ApplySessionActiveUIState(false);

        // Q/E 切り替え・サブカメラ参照先を実機側に返し、チュートリアル用の状態をリセットする
        if (televisionStaticController != null)
        {
            televisionStaticController.SetCameraSwitchingEnabled(false);
            televisionStaticController.SetSubCameraSourceOverride(null);
        }
        if (televisionAnimator != null)
        {
            televisionAnimator.SetTutorialModeActive(false);
        }
        _currentSubCameraState = UFOCameraController.UfoSubCameraState.Back;
        if (backCamera != null) backCamera.enabled = false;
        if (leftCamera != null) leftCamera.enabled = false;
        if (rightCamera != null) rightCamera.enabled = false;

        // すらーぷはせず、ローディング画面の裏で瞬間移動する（Enter時と同じ仕組み）
        if (SceneTransitionManager.Instance != null)
        {
            bool loadingDone = false;
            SceneTransitionManager.Instance.ShowTurnTransition(
                loadingMinimumDuration,
                onDuringLoading: WarpToOriginalPositions,
                onComplete: () => loadingDone = true);

            yield return new WaitUntil(() => loadingDone);
        }
        else
        {
            WarpToOriginalPositions();
        }

        _isTransitioning = false;
        if (completedViaSteps)
        {
            OnTutorialCompleted?.Invoke();
        }
        else
        {
            OnTutorialFinished?.Invoke();
        }
    }

    private void WarpToOriginalPositions()
    {
        if (playerCamera != null)
        {
            playerCamera.transform.SetPositionAndRotation(_originalPlayerCamPos, _originalPlayerCamRot);
            playerCamera.clearFlags = _originalPlayerCamClearFlags;
            playerCamera.backgroundColor = _originalPlayerCamBackgroundColor;
        }

        if (televisionAnimator != null)
        {
            televisionAnimator.ApplyTransform(_originalTelevisionPos, _originalTelevisionRot);
        }
    }
}
