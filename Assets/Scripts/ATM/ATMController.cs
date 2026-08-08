using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;

namespace App.ATM
{
    public enum ATMState
    {
        Off,
        TransitioningToATM,
        Active,
        TransitioningToPlayer
    }

    /// <summary>
    /// ATMの操作画面のサブ状態（画面の状態）。
    /// </summary>
    public enum ATMSubState
    {
        Welcome,
        PasscodeInput,
        MainMenu,
        Inquiry,
        LaunderConfirm,
        CoinExchange,
        Processing,
        Success
    }

    /// <summary>
    /// ATMの全体的な挙動を制御するコントローラー。
    /// 3D TMPへの英語テキスト流し込み、タイピング演出、パスコード伏字、買取価格のランダム変動、
    /// スピンボックス・売却ボタン(WorldSpace)、及び所持金の高速カウントアップ演出を統合管理します。
    /// </summary>
    [DisallowMultipleComponent]
    public class ATMController : MonoBehaviour
    {
        public static ATMController Instance { get; private set; }
        public static bool IsInteracting { get; private set; } = false;

        [Header("カメラ・遷移設定 (別シーンアセット)")]
        [Tooltip("プレイヤーのメインカメラ。ランタイムで自動取得するためアサイン不要です")]
        [SerializeField] private Camera playerCamera;

        [Header("カメラ・遷移設定 (プレハブ内アセット)")]
        [Tooltip("ATM正面のカメラ配置位置（空のTransform等）。インスペクターでの指定が必須です")]
        [SerializeField] private Transform cameraTargetTransform;

        [Tooltip("カメラ遷移の所要時間（秒）")]
        [SerializeField] private float transitionDuration = 1.0f;

        [Tooltip("遷移のイージング曲線")]
        [SerializeField] private AnimationCurve transitionEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("画面表示 (3D TextMeshPro アサイン必須)")]
        [Tooltip("モニター位置に手動配置した 3D TextMeshPro コンポーネント")]
        [SerializeField] private TextMeshPro atmScreenText;

        [Header("インタラクション検出 (プレハブ内アセット)")]
        [Tooltip("ATMにアタッチした MouseHoverOutline。インスペクターでの指定が必須です")]
        [SerializeField] private MouseHoverOutline hoverOutline;

        [Header("演出用オブジェクト (プレハブ内アセット)")]
        [Tooltip("起動時に有効化するライトオブジェクト群")]
        [SerializeField] private GameObject[] atmLights;

        [Header("効果音 - 共通")]
        [Tooltip("再生用 AudioSource")]
        [SerializeField] private AudioSource audioSource;

        [Header("効果音 - ATM本体")]
        [Tooltip("起動音。ATMに寄って電源が入る時に鳴る（起動電子音・HDD動作音など）")]
        [SerializeField] private AudioClip startupSound;

        [Tooltip("電源オフ音。ATMを閉じて元の視点へ戻る時に鳴る")]
        [SerializeField] private AudioClip shutdownSound;

        [Tooltip("資金洗浄・コイン売却が成立した時の音 (SE/debtPay など)")]
        [SerializeField] private AudioClip washSuccessSound;

        [Header("効果音 - キー操作")]
        [Tooltip("物理キークリック音。キーパッドを押した時の「カチッ」という機械音。" +
                 "ATMPhysicalButton 側に個別の Click Sound があればそちらが優先され、無い場合にこれが鳴る")]
        [SerializeField] private AudioClip keyClickSound;

        [Tooltip("選択音。↑↓で項目（金/銀/銅の行）を移動した時の電子音。物理キークリック音に重ねて鳴る")]
        [SerializeField] private AudioClip selectSound;

        [Tooltip("決定音。決定(Confirm)キーを押した時の電子音。物理キークリック音に重ねて鳴る")]
        [SerializeField] private AudioClip confirmSound;

        [Tooltip("キャンセル音。取消(Cancel)キーを押した時の電子音。物理キークリック音に重ねて鳴る")]
        [SerializeField] private AudioClip cancelSound;

        [Tooltip("選択音・決定音・キャンセル音の音量。物理キークリック音との音量バランス調整用")]
        [Range(0f, 1f)]
        [SerializeField] private float electronicSoundVolume = 1f;

        [Header("物理ボタン (テンキー) 設定 (プレハブ内アセット)")]
        [Tooltip("3Dモデル内の各ボタンオブジェクト。インスペクターでの指定が必須です")]
        [SerializeField] private List<ATMPhysicalButton> keyButtons = new List<ATMPhysicalButton>();

        [Header("資金洗浄パラメータ")]
        [Tooltip("資金洗浄時の手数料率 (0.1 = 10%)")]
        [Range(0f, 0.9f)]
        [SerializeField] private float launderingFeeRate = 0.1f;

        [Header("起動時に点灯させるマテリアルのEmission")]
        [Tooltip("Emissionを制御する対象マテリアル。ATM起動中のみ点灯し、閉じると0(消灯)になります")]
        [SerializeField] private Material emissionMaterial;

        [Tooltip("ATM起動中のEmission色(HDR)。ここで設定した値で点灯します")]
        [ColorUsage(false, true)]
        [SerializeField] private Color emissionColor = Color.black;

        [Header("現金払い出し演出 (プレハブ内アセット)")]
        [Tooltip("開閉させる紙幣の排出口。未指定なら子階層から \"atm_door\" を名前で自動検索します")]
        [SerializeField] private Transform atmDoor;

        [Tooltip("排出口が自身のローカルY軸の正方向へ開く距離")]
        [SerializeField] private float doorOpenDistance = 0.1f;

        [Tooltip("排出口の開閉にかける時間(秒)")]
        [SerializeField] private float doorMoveDuration = 0.35f;

        [Tooltip("紙幣プレハブを出す位置。未指定なら排出口(atm_door)の位置に出します")]
        [SerializeField] private Transform cashSpawnPoint;

        [Tooltip("払い出し金額が大きい時に出す紙幣プレハブ (money_big)")]
        [SerializeField] private GameObject moneyBigPrefab;

        [Tooltip("払い出し金額が中くらいの時に出す紙幣プレハブ (money_middle)")]
        [SerializeField] private GameObject moneyMiddlePrefab;

        [Tooltip("払い出し金額が小さい時に出す紙幣プレハブ (money_small)")]
        [SerializeField] private GameObject moneySmallPrefab;

        [Tooltip("この金額以上なら money_big を出す")]
        [SerializeField] private float moneyBigThreshold = 10000f;

        [Tooltip("この金額以上なら money_middle を出す (下回れば money_small)")]
        [SerializeField] private float moneyMiddleThreshold = 1000f;

        [Tooltip("紙幣を物理で落とすか。OFF(既定)なら指定位置に固定表示する。" +
                 "money_* はピンボール用にRigidbody/Colliderを持つため、OFFの間はそれらを無効化します")]
        [SerializeField] private bool moneyPropUsePhysics = false;

        // Emission制御用のシェーダプロパティID
        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

        private ATMState _currentState = ATMState.Off;
        private ATMSubState _currentSubState = ATMSubState.Welcome;
        private App.Player.FirstPersonController _fpController;
        private Vector3 _originalPlayerCamPos;
        private Quaternion _originalPlayerCamRot;

        // コイン売却パラメータ
        private float _goldPrice;
        private float _silverPrice;
        private float _bronzePrice;

        private int _goldSellQty = 0;
        private int _silverSellQty = 0;
        private int _bronzeSellQty = 0;

        // パスコード入力用
        private string _inputPasscode = "";
        private const string TargetPasscode = "1234"; 

        // タイピング演出および入力アニメーション排他制御フラグ
        private bool _isTyping = false;
        private bool _canProceedFromWelcome = false;
        private bool _isLaunderProcessing = false;
        private bool _isCountingUp = false;

        // 動的生成するスピンボックス/売却ボタン用の WorldSpace Canvas オブジェクト
        private GameObject _uiCanvasGo;
        private GameObject _coinExchangePanelGo;

        // コイン両替：縦スピンボックス(0=金,1=銀,2=銅)。text は "▲/数量/▼"、bg は緑ハイライト用
        private readonly TextMeshProUGUI[] _spinTexts = new TextMeshProUGUI[3];
        private readonly UnityEngine.UI.Image[] _spinBgs = new UnityEngine.UI.Image[3];

        // コイン両替の選択/編集状態
        private int _selectedCoinRow = 0;        // 0=金,1=銀,2=銅（初期選択は金）
        private bool _isEditingQty = false;      // false=行選択モード, true=個数編集モード
        private bool _qtyFreshInput = true;      // 編集開始後、最初の数字入力で置き換えるためのフラグ

        [Header("コイン両替スピンボックス配置")]
        [Tooltip("【推奨】金/銀/銅スピンボックスの位置アンカー。ATMの子に空オブジェクトを3個作り、Sceneビューで各コイン行の右にドラッグ配置してここに割当てるだけ。実行中もドラッグで即追従します。未割当ての行は下のVector2(Canvasローカル座標)で配置。")]
        [SerializeField] private Transform[] _spinboxAnchors = new Transform[3];

        [Tooltip("アンカー未割当て時のフォールバック配置(Canvasローカル座標)。金/銀/銅")]
        [SerializeField] private Vector2 _spinboxGoldPos = new Vector2(250f, 40f);
        [SerializeField] private Vector2 _spinboxSilverPos = new Vector2(250f, -50f);
        [SerializeField] private Vector2 _spinboxBronzePos = new Vector2(250f, -140f);

        [Tooltip("スピンボックスの大きさ(Canvasローカル px)。実行中に変更しても毎フレーム反映される")]
        [SerializeField] private Vector2 _spinboxSize = DefaultSpinboxSize;

        [Tooltip("スピンボックス内の「▲/数量/▼」の文字サイズ。実行中に変更しても毎フレーム反映される")]
        [SerializeField] private float _spinboxFontSize = DefaultSpinboxFontSize;

        // 既定値。ContextMenu の「既定値にリセット」からも参照する
        private static readonly Vector2 DefaultSpinboxSize = new Vector2(34f, 52f);
        private const float DefaultSpinboxFontSize = 11f;
        private static readonly Color SpinboxDimColor = new Color(0.10f, 0.16f, 0.10f, 0.90f);
        private static readonly Color SpinboxActiveColor = new Color(0.20f, 0.85f, 0.30f, 0.95f);

        // 高速カウントアップ表示用のキャッシュ金額
        private float _visualWashedAmount = 0f;
        private float successAmountTextValue = 0f;

        // 現金払い出し演出用。閉位置はAwakeで一度だけ控え、開位置はそこからの相対で毎回計算する
        private Vector3 _doorClosedLocalPos;
        private bool _doorPosCached = false;
        private GameObject _spawnedMoney;

        // 画面テキストの外部データ(YAML)レンダラと画像オーバーレイ用コンテナ
        private ATMScreenRenderer _screenRenderer;
        private Transform _imageContainer;

        // アニメーション演出でレンダラへ渡す可変トークンのバッキング値
        private string _typedTitle = "";
        private string _loginPrompt = "";
        private string _progressBar = "";
        private int _progressPercent = 0;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // AudioSource 自動補正
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

            // SEの自動ロード (Resourcesフォルダの debtPay をデフォルト成功音に)
            if (washSuccessSound == null)
            {
                washSuccessSound = Resources.Load<AudioClip>("Sound/SE/debtPay");
            }

            // 紙幣の排出口を解決し、閉状態のローカル座標を控える
            if (atmDoor == null) atmDoor = FindChildByName(transform, "atm_door");
            if (atmDoor != null)
            {
                _doorClosedLocalPos = atmDoor.localPosition;
                _doorPosCached = true;
            }

            // 必須アサインの確認と警告
            ValidateReferences();

            // ハイブリッドボタン用の極小 WorldSpace Canvas をバックグラウンドで事前生成
            CreateWorldSpaceUICanvas();
        }

        private void Start()
        {
            SetATMState(false);

            if (hoverOutline != null)
            {
                hoverOutline.OnClicked += OnATMClicked;
            }
        }

        private void OnDisable()
        {
            // コルーチンが途中で止まっても、紙幣と開いたままの排出口を残さない
            DespawnMoneyProp();
            if (atmDoor != null && _doorPosCached) atmDoor.localPosition = _doorClosedLocalPos;
        }

        private void OnDestroy()
        {
            if (hoverOutline != null)
            {
                hoverOutline.OnClicked -= OnATMClicked;
            }
            // 対象マテリアルは共有アセットのため、破棄/プレイ停止時にEmissionを確実に消灯へ戻す
            SetEmission(false);
            // フォーカス中に破棄されても Escape が握られたままにならないようにする
            App.Input.GameInputGate.ReleaseEscape(this);
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_currentState != ATMState.Active) return;

            // Escキー押下で元の視点へ戻る (アニメーションや洗浄処理中以外)
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (!_isLaunderProcessing && !_isCountingUp)
                {
                    TriggerExit();
                    return;
                }
            }

            // UIボタンの上にマウスカーソルがない場合のみ、3Dキーパッドの直接クリックを判定する (ハイブリッドクリックの両立)
            if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
            {
                Handle3DButtonClicks();
            }

            // 物理キーボードからの入力判定
            HandlePhysicalKeyboardInput();

            // コイン両替中はスピンボックスをアンカーへ毎フレーム追従（実行中のドラッグ調整に即反応）
            if (_currentSubState == ATMSubState.CoinExchange)
            {
                PositionSpinboxes();
            }
        }

        private void ValidateReferences()
        {
            if (cameraTargetTransform == null)
                Debug.LogError("[ATMController] cameraTargetTransform がアサインされていません。", this);

            if (hoverOutline == null)
                Debug.LogError("[ATMController] hoverOutline がアサインされていません。", this);

            if (atmScreenText == null)
                Debug.LogError("[ATMController] atmScreenText (3D TextMeshPro) がアサインされていません。", this);

            if (keyButtons.Count == 0)
                Debug.LogWarning("[ATMController] 物理ボタン (keyButtons) が登録されていません。", this);
        }

        private void ResolveCrossSceneReferences()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            if (_fpController == null)
            {
                _fpController = FindAnyObjectByType<App.Player.FirstPersonController>();
                if (_fpController != null && playerCamera == null)
                {
                    playerCamera = _fpController.GetComponentInChildren<Camera>(true);
                }
            }
        }

        private void OnATMClicked()
        {
            if (_currentState != ATMState.Off) return;

            ResolveCrossSceneReferences();

            // コインの現在買取価格をターン変動としてランダム設定
            _goldPrice = Mathf.Round(Random.Range(8000f, 15000f));
            _silverPrice = Mathf.Round(Random.Range(1500f, 3500f));
            _bronzePrice = Mathf.Round(Random.Range(150f, 450f));

            // 売却数量のリセット
            _goldSellQty = 0;
            _silverSellQty = 0;
            _bronzeSellQty = 0;

            StartCoroutine(TransitionToATM());
        }

        private IEnumerator TransitionToATM()
        {
            _currentState = ATMState.TransitioningToATM;
            IsInteracting = true;
            // フォーカス中の Escape は ATM 退出に使うため、設定画面側に渡さない
            App.Input.GameInputGate.CaptureEscape(this);

            if (hoverOutline != null) hoverOutline.enabled = false;

            if (_fpController != null)
            {
                _fpController.enabled = false;
                var animator = _fpController.GetComponentInChildren<Animator>();
                if (animator != null) animator.SetFloat("Speed", 0f);
            }

            var pickupController = FindAnyObjectByType<CupPickupController>();
            if (pickupController != null) pickupController.enabled = false;

            if (playerCamera != null)
            {
                _originalPlayerCamPos = playerCamera.transform.position;
                _originalPlayerCamRot = playerCamera.transform.rotation;
            }

            if (playerCamera != null && cameraTargetTransform != null)
            {
                Vector3 startPos = playerCamera.transform.position;
                Quaternion startRot = playerCamera.transform.rotation;
                Vector3 targetPos = cameraTargetTransform.position;
                Quaternion targetRot = cameraTargetTransform.rotation;

                float elapsed = 0f;
                while (elapsed < transitionDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / transitionDuration);
                    float easeValue = transitionEase.Evaluate(t);
                    playerCamera.transform.position = Vector3.Lerp(startPos, targetPos, easeValue);
                    playerCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, easeValue);
                    yield return null;
                }

                playerCamera.transform.position = targetPos;
                playerCamera.transform.rotation = targetRot;
            }

            if (startupSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(startupSound);
            }

            SetATMState(true);
            _currentState = ATMState.Active;

            // Welcome画面でタイピング演出を開始
            ChangeSubState(ATMSubState.Welcome);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void TriggerExit()
        {
            if (_currentState != ATMState.Active) return;
            StartCoroutine(TransitionToPlayer());
        }

        private IEnumerator TransitionToPlayer()
        {
            _currentState = ATMState.TransitioningToPlayer;

            if (shutdownSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(shutdownSound);
            }

            SetATMState(false);
            _screenRenderer?.ClearImages();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (playerCamera != null)
            {
                Vector3 startPos = playerCamera.transform.position;
                Quaternion startRot = playerCamera.transform.rotation;

                float elapsed = 0f;
                while (elapsed < transitionDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / transitionDuration);
                    float easeValue = transitionEase.Evaluate(t);
                    playerCamera.transform.position = Vector3.Lerp(startPos, _originalPlayerCamPos, easeValue);
                    playerCamera.transform.rotation = Quaternion.Slerp(startRot, _originalPlayerCamRot, easeValue);
                    yield return null;
                }

                playerCamera.transform.position = _originalPlayerCamPos;
                playerCamera.transform.rotation = _originalPlayerCamRot;
            }

            if (_fpController != null) _fpController.enabled = true;
            
            var pickupController = FindAnyObjectByType<CupPickupController>();
            if (pickupController != null) pickupController.enabled = true;

            if (hoverOutline != null) hoverOutline.enabled = true;

            _currentState = ATMState.Off;
            IsInteracting = false;
            // プレイヤー視点へ戻り切ってから解放する。これより早く解放すると、
            // 退出のために押した Escape が同じフレームで設定画面を開いてしまう
            App.Input.GameInputGate.ReleaseEscape(this);

            yield return RestorePlayerCursor();
        }

        /// <summary>
        /// プレイヤー操作用のカーソル状態（ロック＋非表示）へ戻す。
        /// Unity は Escape が押されると CursorLockMode.Locked を自動解除するため、
        /// 1フレームだけの代入では退出直後に解除が後追いで効いてカーソルが残ることがある。
        /// そのため数フレームにわたって再適用する。
        /// </summary>
        private IEnumerator RestorePlayerCursor()
        {
            const int reapplyFrames = 5;

            for (int i = 0; i < reapplyFrames; i++)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                yield return null;
            }
        }

        private void SetATMState(bool active)
        {
            if (atmScreenText != null) atmScreenText.gameObject.SetActive(active);

            if (_uiCanvasGo != null) _uiCanvasGo.SetActive(active);

            if (atmLights != null)
            {
                foreach (var lightObj in atmLights)
                {
                    if (lightObj != null) lightObj.SetActive(active);
                }
            }

            if (!active)
            {
                if (_coinExchangePanelGo != null) _coinExchangePanelGo.SetActive(false);
            }

            // 起動中はインスペクター設定のEmission色、閉じると0(消灯)
            SetEmission(active);
        }

        /// <summary>
        /// 対象マテリアルのEmissionを制御する。active=true でインスペクター設定色、false で0(黒=消灯)。
        /// </summary>
        private void SetEmission(bool active)
        {
            if (emissionMaterial == null) return;

            Color target = active ? emissionColor : Color.black;
            emissionMaterial.SetColor(EmissionColorID, target);

            // 点灯時はEmissionキーワード/GIフラグを有効化（URP Lit / Standard 双方で反映させるため）
            if (active)
            {
                emissionMaterial.EnableKeyword("_EMISSION");
                emissionMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
        }

        // --- 画面表示更新とサブ状態遷移 ---

        private void ChangeSubState(ATMSubState nextState)
        {
            _currentSubState = nextState;

            // コイン売却画面の時だけ、WorldSpaceのスピンボックス/売却ボタンUIをアクティブにする
            if (_coinExchangePanelGo != null)
            {
                _coinExchangePanelGo.SetActive(nextState == ATMSubState.CoinExchange);
            }

            if (nextState == ATMSubState.Welcome)
            {
                _canProceedFromWelcome = false;
                _typedTitle = "";
                _inputPasscode = ""; // 起動画面＝ログイン画面。パスコードを初期化
                _screenRenderer?.SetScreen("welcome", BuildTokens());
                StartCoroutine(TypeWelcomeText());
            }
            else
            {
                // コイン両替に入るときは行選択を金(先頭)・編集モード解除にリセット
                if (nextState == ATMSubState.CoinExchange)
                {
                    _selectedCoinRow = 0;
                    _isEditingQty = false;
                    _qtyFreshInput = true;
                }

                // 画面切替時に画像オーバーレイを再構築しつつテキストも更新
                _screenRenderer?.SetScreen(ScreenIdFor(nextState), BuildTokens());
                if (nextState == ATMSubState.CoinExchange) UpdateSpinboxes();
            }
        }

        /// <summary>ATMSubState を YAML の画面 id に対応付ける。</summary>
        private static string ScreenIdFor(ATMSubState s)
        {
            switch (s)
            {
                case ATMSubState.Welcome: return "welcome";
                case ATMSubState.PasscodeInput: return "passcode";
                case ATMSubState.MainMenu: return "mainMenu";
                case ATMSubState.Inquiry: return "inquiry";
                case ATMSubState.LaunderConfirm: return "launderConfirm";
                case ATMSubState.CoinExchange: return "coinExchange";
                case ATMSubState.Processing: return "processing";
                case ATMSubState.Success: return "success";
                default: return "welcome";
            }
        }

        /// <summary>現在のウォレット/ATM状態から、画面テキストに差し込むトークン辞書を構築する。</summary>
        private Dictionary<string, string> BuildTokens()
        {
            var wallet = PlayerWallet.Local;
            float clean = wallet != null ? wallet.WashedAmount : 0f;
            float dirty = wallet != null ? wallet.UnwashedAmount : 0f;
            int gold = wallet != null ? wallet.GoldCoins : 0;
            int silver = wallet != null ? wallet.SilverCoins : 0;
            int bronze = wallet != null ? wallet.BronzeCoins : 0;
            int diamond = wallet != null ? wallet.BlackDiamonds : 0;

            // カウントアップ演出中以外は実残高を表示用キャッシュへ同期
            if (!_isCountingUp) _visualWashedAmount = clean;

            float fee = dirty * launderingFeeRate;
            float net = dirty - fee;
            string masked = new string('*', _inputPasscode.Length).PadRight(4, '_');

            // コイン両替：選択中の行だけ緑色にするための color タグ差し込み用トークン
            const string selOpen = "<color=#33FF66>";
            const string selClose = "</color>";

            return new Dictionary<string, string>(32)
            {
                { "selGoldOpen",   _selectedCoinRow == 0 ? selOpen : "" },
                { "selGoldClose",  _selectedCoinRow == 0 ? selClose : "" },
                { "selSilverOpen", _selectedCoinRow == 1 ? selOpen : "" },
                { "selSilverClose",_selectedCoinRow == 1 ? selClose : "" },
                { "selBronzeOpen", _selectedCoinRow == 2 ? selOpen : "" },
                { "selBronzeClose",_selectedCoinRow == 2 ? selClose : "" },
                { "cleanBalance", _visualWashedAmount.ToString("N0") },
                { "unwashedDebt", dirty.ToString("N0") },
                { "gold", gold.ToString() },
                { "silver", silver.ToString() },
                { "bronze", bronze.ToString() },
                { "diamond", diamond.ToString() },
                { "goldPrice", _goldPrice.ToString("N0") },
                { "silverPrice", _silverPrice.ToString("N0") },
                { "bronzePrice", _bronzePrice.ToString("N0") },
                { "goldOwned", gold.ToString() },
                { "silverOwned", silver.ToString() },
                { "bronzeOwned", bronze.ToString() },
                { "feeRatePercent", (launderingFeeRate * 100f).ToString("0") },
                { "launderFee", fee.ToString("N0") },
                { "launderNet", net.ToString("N0") },
                { "passcodeMasked", masked },
                { "creditedAmount", successAmountTextValue.ToString("N0") },
                { "typedTitle", _typedTitle },
                { "loginPrompt", _loginPrompt },
                { "progressBar", _progressBar },
                { "progressPercent", _progressPercent.ToString() },
            };
        }

        /// <summary>縦スピンボックス3個の数量表示と、編集中行の緑ハイライトを更新する。</summary>
        private void UpdateSpinboxes()
        {
            for (int i = 0; i < 3; i++)
            {
                if (_spinTexts[i] == null) continue;
                int qty = i == 0 ? _goldSellQty : (i == 1 ? _silverSellQty : _bronzeSellQty);
                _spinTexts[i].text = $"▲\n{qty}\n▼";

                bool active = _isEditingQty && _selectedCoinRow == i;
                if (_spinBgs[i] != null) _spinBgs[i].color = active ? SpinboxActiveColor : SpinboxDimColor;
                _spinTexts[i].color = active ? Color.white : new Color(0.7f, 0.8f, 0.7f, 1f);
            }
        }

        /// <summary>
        /// スピンボックスの位置を更新する。アンカーTransformが割当ててあればそのワールド座標に追従
        /// （Sceneビューでドラッグ配置でき、実行中も即追従）。無ければCanvasローカルVector2で配置。
        /// </summary>
        private void PositionSpinboxes()
        {
            for (int i = 0; i < 3; i++)
            {
                if (_spinBgs[i] == null) continue;
                RectTransform rt = _spinBgs[i].rectTransform;
                Transform anchor = (_spinboxAnchors != null && i < _spinboxAnchors.Length) ? _spinboxAnchors[i] : null;
                if (anchor != null)
                    rt.position = anchor.position; // ワールド座標に追従（Sceneで空オブジェクトをドラッグ）
                else
                    rt.anchoredPosition = i == 0 ? _spinboxGoldPos : (i == 1 ? _spinboxSilverPos : _spinboxBronzePos);
                rt.sizeDelta = _spinboxSize;

                // 文字サイズも追従させ、Inspector で大きさを詰めた時に文字がはみ出さないようにする
                // (TMP の fontSize は同値なら再構築しないので毎フレーム代入して問題ない)
                if (_spinTexts[i] != null) _spinTexts[i].fontSize = _spinboxFontSize;
            }
        }

        /// <summary>
        /// スピンボックスの大きさ・文字サイズをコード上の既定値へ戻す。
        /// シーン/プレハブに古い大きな値が保存済みの場合、コード側の既定値では上書きされないため、
        /// Inspector の ATMController のコンテキストメニュー（コンポーネント右上の「⋮」）から実行する。
        /// </summary>
        [ContextMenu("スピンボックスの大きさを既定値にリセット")]
        private void ResetSpinboxSize()
        {
            _spinboxSize = DefaultSpinboxSize;
            _spinboxFontSize = DefaultSpinboxFontSize;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            if (!Application.isPlaying && gameObject.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }

        private IEnumerator TypeWelcomeText()
        {
            _isTyping = true;
            string fullTitle = "WELCOME";

            // タイトルを1文字ずつ表示（レイアウト・色は YAML の welcome 画面が担う）
            _typedTitle = "";
            _screenRenderer?.UpdateText("welcome", BuildTokens());

            string currentText = "";
            for (int i = 0; i < fullTitle.Length; i++)
            {
                currentText += fullTitle[i];
                _typedTitle = currentText;
                _screenRenderer?.UpdateText("welcome", BuildTokens());

                // 1文字ずつの電子音
                if (keyClickSound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(keyClickSound, 0.5f);
                }
                yield return new WaitForSeconds(0.08f);
            }

            yield return new WaitForSeconds(0.2f);

            _typedTitle = fullTitle;
            _screenRenderer?.UpdateText("welcome", BuildTokens());

            // タイピング完了。以降このwelcome画面上でパスコード入力を受け付ける
            _isTyping = false;
            _canProceedFromWelcome = true;
        }

        private void UpdateDisplay()
        {
            if (atmScreenText == null || _isTyping || _currentSubState == ATMSubState.Welcome) return;

            _screenRenderer?.UpdateText(ScreenIdFor(_currentSubState), BuildTokens());

            // コイン両替画面ではスピンボックスの数値表示も同期
            if (_currentSubState == ATMSubState.CoinExchange) UpdateSpinboxes();
        }

        private void OnATMKeyPressed(KeyRole role)
        {
            if (_currentState != ATMState.Active) return;

            // 処理中・カウントアップ演出中はキー入力をブロック
            if (_currentSubState == ATMSubState.Processing || _isLaunderProcessing || _isCountingUp) return;

            // 起動画面のタイピング演出が終わるまでは入力不可
            if (_currentSubState == ATMSubState.Welcome && !_canProceedFromWelcome) return;

            // 入力が受理される時だけ、役割に応じた電子音を物理キークリック音に重ねて鳴らす。
            // ここが全入力経路（3Dキーパッドのクリック／物理キーボード）の合流点。
            PlayRoleSound(role);

            // 起動画面＝ログイン画面に統合。タイピング完了後、この画面上で直接パスコードを入力する。
            if (_currentSubState == ATMSubState.Welcome)
            {
                if (role >= KeyRole.Num0 && role <= KeyRole.Num9)
                {
                    if (_inputPasscode.Length < 4)
                    {
                        _inputPasscode += ((int)role).ToString();
                        _screenRenderer?.UpdateText("welcome", BuildTokens());
                    }
                }
                else if (role == KeyRole.Cancel)
                {
                    if (_inputPasscode.Length > 0)
                    {
                        _inputPasscode = _inputPasscode.Substring(0, _inputPasscode.Length - 1);
                        _screenRenderer?.UpdateText("welcome", BuildTokens());
                    }
                }
                else if (role == KeyRole.Confirm)
                {
                    if (_inputPasscode.Length == 4)
                    {
                        // ログイン成功→メインメニューは廃止し、コイン両替画面へ直行
                        ChangeSubState(ATMSubState.CoinExchange);
                    }
                    else if (keyClickSound != null && audioSource != null)
                    {
                        audioSource.PlayOneShot(keyClickSound, 1.2f); // 桁数不足エラー音
                    }
                }
                return;
            }

            switch (_currentSubState)
            {
                case ATMSubState.PasscodeInput:
                    if (role >= KeyRole.Num0 && role <= KeyRole.Num9)
                    {
                        if (_inputPasscode.Length < 4)
                        {
                            _inputPasscode += ((int)role).ToString();
                            UpdateDisplay();
                        }
                    }
                    else if (role == KeyRole.Cancel)
                    {
                        if (_inputPasscode.Length > 0)
                        {
                            _inputPasscode = _inputPasscode.Substring(0, _inputPasscode.Length - 1);
                            UpdateDisplay();
                        }
                    }
                    else if (role == KeyRole.Confirm)
                    {
                        // パスコードが入力されていれば何でも通す仕様（または "1234"）
                        if (_inputPasscode.Length == 4)
                        {
                            ChangeSubState(ATMSubState.MainMenu);
                        }
                        else
                        {
                            // 桁数不足エラーのフィードバック音
                            if (keyClickSound != null && audioSource != null)
                            {
                                audioSource.PlayOneShot(keyClickSound, 1.2f);
                            }
                        }
                    }
                    break;

                case ATMSubState.MainMenu:
                    if (role == KeyRole.Num1)
                    {
                        ChangeSubState(ATMSubState.Inquiry);
                    }
                    else if (role == KeyRole.Num2)
                    {
                        ChangeSubState(ATMSubState.CoinExchange);
                    }
                    else if (role == KeyRole.Num3 || role == KeyRole.Cancel)
                    {
                        TriggerExit();
                    }
                    break;

                case ATMSubState.Inquiry:
                    if (role == KeyRole.Num0 || role == KeyRole.Cancel)
                    {
                        ChangeSubState(ATMSubState.MainMenu);
                    }
                    break;

                case ATMSubState.CoinExchange:
                    HandleCoinExchangeKey(role);
                    break;

                case ATMSubState.LaunderConfirm:
                    if (role == KeyRole.Confirm)
                    {
                        var wallet = PlayerWallet.Local;
                        float unwashed = wallet != null ? wallet.UnwashedAmount : 0f;

                        if (unwashed <= 0f)
                        {
                            Debug.Log("[ATMController] 洗浄する資金がありません。");
                            if (keyClickSound != null && audioSource != null)
                            {
                                audioSource.PlayOneShot(keyClickSound);
                            }
                            return;
                        }
                        StartCoroutine(ProcessLaundering(unwashed));
                    }
                    else if (role == KeyRole.Cancel)
                    {
                        ChangeSubState(ATMSubState.MainMenu);
                    }
                    break;

                case ATMSubState.Success:
                    if (role == KeyRole.Confirm || role == KeyRole.Cancel)
                    {
                        ChangeSubState(ATMSubState.MainMenu);
                    }
                    break;
            }
        }

        private IEnumerator ProcessLaundering(float amountToWash)
        {
            _isLaunderProcessing = true;
            ChangeSubState(ATMSubState.Processing);

            float duration = 2.0f; 
            float elapsed = 0f;

            if (keyClickSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(keyClickSound);
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                int bars = Mathf.FloorToInt(progress * 10f);
                string barText = "";
                for (int i = 0; i < 10; i++)
                {
                    barText += (i < bars) ? "■" : "□";
                }

                // プログレスバーと％をトークンとして processing 画面(YAML)へ差し込む
                _progressBar = barText;
                _progressPercent = Mathf.FloorToInt(progress * 100f);
                _screenRenderer?.UpdateText("processing", BuildTokens());

                if (Time.frameCount % 25 == 0)
                {
                    AnimateRandomButton();
                }

                yield return null;
            }

            var wallet = PlayerWallet.Local;
            float washedAmount = 0f;
            if (wallet != null)
            {
                float fee = amountToWash * launderingFeeRate;
                washedAmount = amountToWash - fee;

                wallet.UnwashedAmount = 0f;
                // ここでは即時追加ではなく、後ほどカウントアップ演出で加算するためキャッシュだけする
                _visualWashedAmount = wallet.WashedAmount;
                MoneyManager.Instance.AddMoney(washedAmount);
            }

            successAmountTextValue = washedAmount;

            if (washSuccessSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(washSuccessSound);
            }

            _isLaunderProcessing = false;
            ChangeSubState(ATMSubState.Success);
        }

        // --- コイン売却スピンボックス処理・高速カウントアップ ---

        public void ChangeGoldQty(int amount)
        {
            if (_isCountingUp) return;
            var wallet = PlayerWallet.Local;
            int max = wallet != null ? wallet.GoldCoins : 0;
            _goldSellQty = Mathf.Clamp(_goldSellQty + amount, 0, max);
            PlayKeyFeedback();
            UpdateDisplay();
        }

        public void ChangeSilverQty(int amount)
        {
            if (_isCountingUp) return;
            var wallet = PlayerWallet.Local;
            int max = wallet != null ? wallet.SilverCoins : 0;
            _silverSellQty = Mathf.Clamp(_silverSellQty + amount, 0, max);
            PlayKeyFeedback();
            UpdateDisplay();
        }

        public void ChangeBronzeQty(int amount)
        {
            if (_isCountingUp) return;
            var wallet = PlayerWallet.Local;
            int max = wallet != null ? wallet.BronzeCoins : 0;
            _bronzeSellQty = Mathf.Clamp(_bronzeSellQty + amount, 0, max);
            PlayKeyFeedback();
            UpdateDisplay();
        }

        public void SellGold()
        {
            if (_isCountingUp || _goldSellQty <= 0) return;
            var wallet = PlayerWallet.Local;
            if (wallet != null && wallet.GoldCoins >= _goldSellQty)
            {
                float gained = _goldSellQty * _goldPrice;
                wallet.GoldCoins -= _goldSellQty;
                
                float startCash = wallet.WashedAmount;
                MoneyManager.Instance.AddMoney(gained);
                float endCash = wallet.WashedAmount;

                _goldSellQty = 0;
                StartCoroutine(AnimateCashCountUp(startCash, endCash));
            }
        }

        public void SellSilver()
        {
            if (_isCountingUp || _silverSellQty <= 0) return;
            var wallet = PlayerWallet.Local;
            if (wallet != null && wallet.SilverCoins >= _silverSellQty)
            {
                float gained = _silverSellQty * _silverPrice;
                wallet.SilverCoins -= _silverSellQty;
                
                float startCash = wallet.WashedAmount;
                MoneyManager.Instance.AddMoney(gained);
                float endCash = wallet.WashedAmount;

                _silverSellQty = 0;
                StartCoroutine(AnimateCashCountUp(startCash, endCash));
            }
        }

        public void SellBronze()
        {
            if (_isCountingUp || _bronzeSellQty <= 0) return;
            var wallet = PlayerWallet.Local;
            if (wallet != null && wallet.BronzeCoins >= _bronzeSellQty)
            {
                float gained = _bronzeSellQty * _bronzePrice;
                wallet.BronzeCoins -= _bronzeSellQty;
                
                float startCash = wallet.WashedAmount;
                MoneyManager.Instance.AddMoney(gained);
                float endCash = wallet.WashedAmount;

                _bronzeSellQty = 0;
                StartCoroutine(AnimateCashCountUp(startCash, endCash));
            }
        }

        // --- コイン両替：行選択 / 個数編集 の状態機械 ---

        /// <summary>
        /// コイン両替画面でのキー入力処理。選択モードと編集モードで↑↓等の役割が変わる。
        /// 効果音はここでは鳴らさない。物理キークリック音は入力経路（Handle3DButtonClicks /
        /// AnimateButtonByRole）が、選択/決定/キャンセルの電子音は OnATMKeyPressed が担当する。
        /// </summary>
        private void HandleCoinExchangeKey(KeyRole role)
        {
            if (!_isEditingQty)
            {
                // 行選択モード：↑↓で金/銀/銅を選択、Enterで編集開始、CancelでATM退出
                if (role == KeyRole.Up)
                {
                    _selectedCoinRow = Mathf.Max(0, _selectedCoinRow - 1);
                    UpdateDisplay();
                }
                else if (role == KeyRole.Down)
                {
                    _selectedCoinRow = Mathf.Min(2, _selectedCoinRow + 1);
                    UpdateDisplay();
                }
                else if (role == KeyRole.Confirm)
                {
                    _isEditingQty = true;      // スピンボックスが緑に点灯し、個数を編集できる
                    _qtyFreshInput = true;
                    UpdateDisplay();
                }
                else if (role == KeyRole.Cancel)
                {
                    TriggerExit();             // メニューが無いため選択モードのCancelはATMを閉じる
                }
            }
            else
            {
                // 個数編集モード：数字キーで入力、↑↓で±1、Enterで売却確定、Cancelで中止
                if (role >= KeyRole.Num0 && role <= KeyRole.Num9)
                {
                    int digit = (int)role;
                    int next = _qtyFreshInput ? digit : GetSelectedQty() * 10 + digit;
                    _qtyFreshInput = false;
                    SetSelectedQty(next);
                    UpdateDisplay();
                }
                else if (role == KeyRole.Up)
                {
                    _qtyFreshInput = false;
                    SetSelectedQty(GetSelectedQty() + 1);
                    UpdateDisplay();
                }
                else if (role == KeyRole.Down)
                {
                    _qtyFreshInput = false;
                    SetSelectedQty(GetSelectedQty() - 1);
                    UpdateDisplay();
                }
                else if (role == KeyRole.Confirm)
                {
                    SellSelectedRow();         // 個数確定→売却(現金化)＆カウントアップ演出
                    _isEditingQty = false;
                    UpdateDisplay();
                }
                else if (role == KeyRole.Cancel)
                {
                    SetSelectedQty(0);         // 中止：個数を0に戻す
                    _isEditingQty = false;
                    UpdateDisplay();
                }
            }
        }

        private int GetSelectedQty()
        {
            return _selectedCoinRow == 0 ? _goldSellQty : (_selectedCoinRow == 1 ? _silverSellQty : _bronzeSellQty);
        }

        private int GetOwned(int row)
        {
            var w = PlayerWallet.Local;
            if (w == null) return 0;
            return row == 0 ? w.GoldCoins : (row == 1 ? w.SilverCoins : w.BronzeCoins);
        }

        /// <summary>選択行の売却個数を所持数の範囲にクランプして設定する。</summary>
        private void SetSelectedQty(int value)
        {
            int clamped = Mathf.Clamp(value, 0, GetOwned(_selectedCoinRow));
            if (_selectedCoinRow == 0) _goldSellQty = clamped;
            else if (_selectedCoinRow == 1) _silverSellQty = clamped;
            else _bronzeSellQty = clamped;
        }

        private void SellSelectedRow()
        {
            switch (_selectedCoinRow)
            {
                case 0: SellGold(); break;
                case 1: SellSilver(); break;
                default: SellBronze(); break;
            }
        }

        /// <summary>
        /// 所持金が高速で流れ込むように上昇するカウントアップ演出。
        /// 併せて排出口(atm_door)を開き、払い出し金額に応じた紙幣プレハブを出す。
        /// カウントアップが終わったら紙幣を消し、排出口を閉じる。
        /// </summary>
        private IEnumerator AnimateCashCountUp(float startAmount, float targetAmount)
        {
            _isCountingUp = true;

            float duration = 1.0f; // 1秒かけてカウントアップ
            float elapsed = 0f;

            // 成功・取引時の音
            if (washSuccessSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(washSuccessSound, 0.8f);
            }

            // 排出口を開けてから、払い出し金額に応じた紙幣を出す
            yield return MoveDoor(true);
            SpawnMoneyProp(targetAmount - startAmount);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // イージングをかけて最後になだらかに流し込む
                float rate = 1f - Mathf.Pow(1f - t, 3); // EaseOutCubic
                
                _visualWashedAmount = Mathf.Round(Mathf.Lerp(startAmount, targetAmount, rate));
                UpdateDisplay();

                // カウントアップ中のダダダダッという連続電子音
                if (Time.frameCount % 4 == 0)
                {
                    if (keyClickSound != null && audioSource != null)
                    {
                        audioSource.PlayOneShot(keyClickSound, 0.3f);
                    }
                }

                yield return null;
            }

            _visualWashedAmount = targetAmount;
            UpdateDisplay();

            // 払い出し完了：紙幣を消して排出口を閉じる
            DespawnMoneyProp();
            yield return MoveDoor(false);

            _isCountingUp = false;
            UpdateDisplay();
        }

        /// <summary>
        /// 排出口を自身のローカルY軸の正方向へ doorOpenDistance だけ Slerp で開閉する。
        /// open=false のときは Awake で控えた閉位置へ戻す。未アサインなら何もしない。
        /// </summary>
        private IEnumerator MoveDoor(bool open)
        {
            if (atmDoor == null || !_doorPosCached) yield break;

            Vector3 from = atmDoor.localPosition;
            Vector3 to = open ? DoorOpenLocalPos() : _doorClosedLocalPos;

            if (doorMoveDuration <= 0f)
            {
                atmDoor.localPosition = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < doorMoveDuration)
            {
                elapsed += Time.deltaTime;
                atmDoor.localPosition = Vector3.Slerp(from, to, Mathf.Clamp01(elapsed / doorMoveDuration));
                yield return null;
            }
            atmDoor.localPosition = to;
        }

        /// <summary>
        /// 閉位置から扉自身のローカルY軸の正方向へ doorOpenDistance だけ進めた開位置を返す。
        /// localRotation を掛けることで、親の軸ではなく扉自身の軸方向になる。
        /// </summary>
        private Vector3 DoorOpenLocalPos()
        {
            if (atmDoor == null) return _doorClosedLocalPos;
            return _doorClosedLocalPos + (atmDoor.localRotation * Vector3.up) * doorOpenDistance;
        }

        /// <summary>払い出し金額に応じた紙幣プレハブを排出位置に出す。プレハブ未設定なら何もしない。</summary>
        private void SpawnMoneyProp(float payoutAmount)
        {
            DespawnMoneyProp(); // 二重生成の保険

            GameObject prefab = payoutAmount >= moneyBigThreshold
                ? moneyBigPrefab
                : (payoutAmount >= moneyMiddleThreshold ? moneyMiddlePrefab : moneySmallPrefab);
            if (prefab == null) return;

            Transform point = cashSpawnPoint != null ? cashSpawnPoint : atmDoor;
            if (point == null) return;

            // 固定表示なら排出位置の子にしてATMに追従させる。物理で落とす場合は親から切り離す
            _spawnedMoney = moneyPropUsePhysics
                ? Instantiate(prefab, point.position, point.rotation)
                : Instantiate(prefab, point.position, point.rotation, point);

            if (!moneyPropUsePhysics)
            {
                // ピンボール用のRigidbody/Colliderが付いているので、落下や押し出しが起きないよう止める
                foreach (var rb in _spawnedMoney.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
                foreach (var col in _spawnedMoney.GetComponentsInChildren<Collider>()) col.enabled = false;
            }
        }

        /// <summary>出している紙幣プレハブを片付ける。</summary>
        private void DespawnMoneyProp()
        {
            if (_spawnedMoney != null)
            {
                Destroy(_spawnedMoney);
                _spawnedMoney = null;
            }
        }

        /// <summary>子階層を再帰的に辿って指定名のTransformを探す。</summary>
        private static Transform FindChildByName(Transform root, string targetName)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == targetName) return child;

                Transform found = FindChildByName(child, targetName);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>SEを鳴らす小さなヘルパー。クリップ／AudioSource 未設定時は何もしない。</summary>
        private void PlaySe(AudioClip clip, float volume = 1f)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }

        /// <summary>
        /// キーの役割に対応する電子音を鳴らす。物理キーの「カチッ」というクリック音とは独立しており、
        /// 両方が重なって鳴る（クリック音は入力経路側の ATMPhysicalButton.Press が担当）。
        /// 数字キーには専用の電子音を割り当てない（クリック音のみ）。
        /// </summary>
        private void PlayRoleSound(KeyRole role)
        {
            AudioClip clip;
            switch (role)
            {
                case KeyRole.Up:
                case KeyRole.Down:
                    clip = selectSound;
                    break;
                case KeyRole.Confirm:
                    clip = confirmSound;
                    break;
                case KeyRole.Cancel:
                    clip = cancelSound;
                    break;
                default:
                    return;
            }
            PlaySe(clip, electronicSoundVolume);
        }

        public void PlayKeyFeedback()
        {
            PlaySe(keyClickSound);
            AnimateRandomButton();
        }

        public void AnimateRandomButton()
        {
            if (keyButtons.Count == 0) return;
            int randIndex = Random.Range(0, keyButtons.Count);
            if (keyButtons[randIndex] != null)
            {
                keyButtons[randIndex].Press(null);
            }
        }

        public void AnimateButtonByRole(KeyRole role)
        {
            foreach (var btn in keyButtons)
            {
                if (btn != null && btn.Role == role)
                {
                    btn.Press(audioSource, keyClickSound);
                    return;
                }
            }

            // 対応する3Dボタンが無い役割（↑↓を実機モデルに持たない等）でもクリック音は鳴らす
            AnimateRandomButton();
            PlaySe(keyClickSound);
        }

        private void Handle3DButtonClicks()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            if (playerCamera == null) return;

            Ray ray = playerCamera.ScreenPointToRay(mousePos);
            if (Physics.Raycast(ray, out RaycastHit hit, 5f))
            {
                ATMPhysicalButton btn = hit.collider.GetComponent<ATMPhysicalButton>();
                if (btn == null) btn = hit.collider.GetComponentInParent<ATMPhysicalButton>();

                if (btn != null)
                {
                    btn.Press(audioSource, keyClickSound);
                    OnATMKeyPressed(btn.Role);
                }
            }
        }

        private void HandlePhysicalKeyboardInput()
        {
            if (Keyboard.current == null) return;

            bool keyPressed = false;
            KeyRole inputRole = KeyRole.Other;

            if (Keyboard.current.digit0Key.wasPressedThisFrame || Keyboard.current.numpad0Key.wasPressedThisFrame) { inputRole = KeyRole.Num0; keyPressed = true; }
            else if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) { inputRole = KeyRole.Num1; keyPressed = true; }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) { inputRole = KeyRole.Num2; keyPressed = true; }
            else if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) { inputRole = KeyRole.Num3; keyPressed = true; }
            else if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame) { inputRole = KeyRole.Num4; keyPressed = true; }
            else if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame) { inputRole = KeyRole.Num5; keyPressed = true; }
            else if (Keyboard.current.digit6Key.wasPressedThisFrame || Keyboard.current.numpad6Key.wasPressedThisFrame) { inputRole = KeyRole.Num6; keyPressed = true; }
            else if (Keyboard.current.digit7Key.wasPressedThisFrame || Keyboard.current.numpad7Key.wasPressedThisFrame) { inputRole = KeyRole.Num7; keyPressed = true; }
            else if (Keyboard.current.digit8Key.wasPressedThisFrame || Keyboard.current.numpad8Key.wasPressedThisFrame) { inputRole = KeyRole.Num8; keyPressed = true; }
            else if (Keyboard.current.digit9Key.wasPressedThisFrame || Keyboard.current.numpad9Key.wasPressedThisFrame) { inputRole = KeyRole.Num9; keyPressed = true; }
            else if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame) { inputRole = KeyRole.Confirm; keyPressed = true; }
            else if (Keyboard.current.backspaceKey.wasPressedThisFrame) { inputRole = KeyRole.Cancel; keyPressed = true; }
            else if (Keyboard.current.upArrowKey.wasPressedThisFrame) { inputRole = KeyRole.Up; keyPressed = true; }
            else if (Keyboard.current.downArrowKey.wasPressedThisFrame) { inputRole = KeyRole.Down; keyPressed = true; }

            if (keyPressed)
            {
                AnimateButtonByRole(inputRole);
                OnATMKeyPressed(inputRole);
            }
        }

        // --- スピンボックスと売却ボタンのみを重ねる WorldSpace Canvas の動的構築 ---

        private void CreateWorldSpaceUICanvas()
        {
            _uiCanvasGo = new GameObject("ATMHibrydCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            
            // スケール歪みを防ぐため、ATMのルート直下に配置し、ワールド同期します
            _uiCanvasGo.transform.SetParent(transform, false);

            Canvas canvas = _uiCanvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 30100; // 3D TMPより前面

            CanvasScaler scaler = _uiCanvasGo.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            RectTransform canvasRt = _uiCanvasGo.GetComponent<RectTransform>();
            
            // 手動配置された atmScreenText にワールド座標と角度を合わせます。
            if (atmScreenText != null)
            {
                // チラつき（Zファイティング）を防ぐため、3D TMPの正面方向に 2mm (0.002f) 浮かせます
                Vector3 offsetPos = atmScreenText.transform.position + atmScreenText.transform.forward * 0.002f;
                _uiCanvasGo.transform.position = offsetPos;
                _uiCanvasGo.transform.rotation = atmScreenText.transform.rotation;
                
                // 3D TMPのスケール（アスペクト比）と同一にします
                canvasRt.localScale = atmScreenText.transform.localScale;
            }
            else
            {
                _uiCanvasGo.transform.localPosition = new Vector3(0f, 1.48f, 0.172f);
                _uiCanvasGo.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                canvasRt.localScale = Vector3.one * 0.00045f;
            }

            canvasRt.sizeDelta = new Vector2(800f, 600f);

            // コイン交換用のオーバーレイパネル
            _coinExchangePanelGo = new GameObject("CoinExchangePanel", typeof(RectTransform));
            _coinExchangePanelGo.transform.SetParent(_uiCanvasGo.transform, false);
            RectTransform panelRt = _coinExchangePanelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.sizeDelta = Vector2.zero;

            // 金/銀/銅 の縦スピンボックスを生成（キーボード操作。編集中は緑に点灯）
            CreateVerticalSpinbox(0);
            CreateVerticalSpinbox(1);
            CreateVerticalSpinbox(2);
            PositionSpinboxes();

            // 画面テキスト由来の画像オーバーレイを載せるコンテナ（キャンバス全面）
            var imageContainerGo = new GameObject("ScreenImages", typeof(RectTransform));
            imageContainerGo.transform.SetParent(_uiCanvasGo.transform, false);
            RectTransform imgContRt = imageContainerGo.GetComponent<RectTransform>();
            imgContRt.anchorMin = Vector2.zero;
            imgContRt.anchorMax = Vector2.one;
            imgContRt.sizeDelta = Vector2.zero;
            _imageContainer = imageContainerGo.transform;

            // YAML から画面テキスト/画像を読み込むレンダラを生成
            _screenRenderer = new ATMScreenRenderer(atmScreenText, _imageContainer);
            if (!_screenRenderer.IsLoaded)
            {
                Debug.LogWarning("[ATMController] ATMScreens.yaml を読み込めませんでした。StreamingAssets/ATM/ATMScreens.yaml を確認してください。", this);
            }

            // 初期状態は非アクティブ
            _coinExchangePanelGo.SetActive(false);
            _uiCanvasGo.SetActive(false);
        }

        /// <summary>
        /// 指定行(0=金,1=銀,2=銅)の縦スピンボックスを生成する。
        /// 背景Image(緑ハイライト用)＋ "▲/数量/▼" の3行テキストで構成。操作はキーボード。
        /// </summary>
        private void CreateVerticalSpinbox(int row)
        {
            // 背景（点灯時に緑になる）。位置は PositionSpinboxes() が設定する。
            GameObject bgGo = new GameObject($"Spinbox{row}", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(_coinExchangePanelGo.transform, false);
            Image bg = bgGo.GetComponent<Image>();
            bg.color = SpinboxDimColor;
            bg.raycastTarget = false;
            RectTransform bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.sizeDelta = _spinboxSize;
            _spinBgs[row] = bg;

            // "▲ / 数量 / ▼" の縦並びテキスト
            GameObject txtGo = new GameObject("Qty", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(bgGo.transform, false);
            TextMeshProUGUI tmp = txtGo.GetComponent<TextMeshProUGUI>();
            tmp.text = "▲\n0\n▼";
            tmp.fontSize = _spinboxFontSize;
            tmp.color = new Color(0.7f, 0.8f, 0.7f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            RectTransform txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;
            _spinTexts[row] = tmp;
        }

        private GameObject CreateUIButton(Transform parent, string name, string text, Vector2 anchoredPos, Vector2 size, Color baseColor)
        {
            GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);

            btnGo.GetComponent<Image>().color = baseColor;
            Button btn = btnGo.GetComponent<Button>();
            
            ColorBlock cb = btn.colors;
            cb.normalColor = baseColor;
            cb.highlightedColor = baseColor * 1.3f;
            cb.pressedColor = baseColor * 0.7f;
            cb.selectedColor = baseColor;
            btn.colors = cb;

            RectTransform rt = btnGo.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            GameObject txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(btnGo.transform, false);
            TextMeshProUGUI tmp = txtGo.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 18;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            RectTransform txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;

            // ボタンクリック時のSE
            btn.onClick.AddListener(() => {
                if (keyClickSound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(keyClickSound, 0.8f);
                }
            });

            return btnGo;
        }
    }
}
