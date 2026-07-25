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

        [Header("効果音")]
        [Tooltip("再生用 AudioSource")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("起動時の電子音/HDD動作音")]
        [SerializeField] private AudioClip startupSound;

        [Tooltip("終了（電源オフ）時の音")]
        [SerializeField] private AudioClip shutdownSound;

        [Tooltip("ボタンを押した時のクリック音")]
        [SerializeField] private AudioClip keyClickSound;

        [Tooltip("資金洗浄に成功した時の音 (SE/debtPay など)")]
        [SerializeField] private AudioClip washSuccessSound;

        [Header("物理ボタン (テンキー) 設定 (プレハブ内アセット)")]
        [Tooltip("3Dモデル内の各ボタンオブジェクト。インスペクターでの指定が必須です")]
        [SerializeField] private List<ATMPhysicalButton> keyButtons = new List<ATMPhysicalButton>();

        [Header("資金洗浄パラメータ")]
        [Tooltip("資金洗浄時の手数料率 (0.1 = 10%)")]
        [Range(0f, 0.9f)]
        [SerializeField] private float launderingFeeRate = 0.1f;

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

        // スピンボックスのUIテキストコンポーネント
        private TextMeshProUGUI _goldQtyText;
        private TextMeshProUGUI _silverQtyText;
        private TextMeshProUGUI _bronzeQtyText;

        // 高速カウントアップ表示用のキャッシュ金額
        private float _visualWashedAmount = 0f;

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

        private void OnDestroy()
        {
            if (hoverOutline != null)
            {
                hoverOutline.OnClicked -= OnATMClicked;
            }
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

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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
                StartCoroutine(TypeWelcomeText());
            }
            else
            {
                UpdateDisplay();
            }
        }

        private IEnumerator TypeWelcomeText()
        {
            _isTyping = true;
            string fullTitle = "WELCOME";
            
            if (atmScreenText != null)
            {
                atmScreenText.text = 
                    "<color=#33FF66>=== FEVER CAPITAL ATM ===</color>\n" +
                    "SECURITY LEVEL: EXTREME\n\n";
            }

            string currentText = "";
            for (int i = 0; i < fullTitle.Length; i++)
            {
                currentText += fullTitle[i];
                if (atmScreenText != null)
                {
                    atmScreenText.text = 
                        "<color=#33FF66>=== FEVER CAPITAL ATM ===</color>\n" +
                        "SECURITY LEVEL: EXTREME\n\n" +
                        $"<size=120%><color=#33FF66>{currentText}</color></size>";
                }
                
                // 1文字ずつの電子音
                if (keyClickSound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(keyClickSound, 0.5f);
                }
                yield return new WaitForSeconds(0.08f);
            }

            yield return new WaitForSeconds(0.2f);

            if (atmScreenText != null)
            {
                atmScreenText.text = 
                    "<color=#33FF66>=== FEVER CAPITAL ATM ===</color>\n" +
                    "SECURITY LEVEL: EXTREME\n\n" +
                    $"<size=120%><color=#33FF66>{fullTitle}</color></size>\n\n" +
                    "<color=#88FF88>PRESS ANY KEY TO LOGIN</color>";
            }

            _isTyping = false;
            _canProceedFromWelcome = true;
        }

        private void UpdateDisplay()
        {
            if (atmScreenText == null || _isTyping || _currentSubState == ATMSubState.Welcome) return;

            string displayText = "";
            var wallet = PlayerWallet.Local;

            switch (_currentSubState)
            {
                case ATMSubState.PasscodeInput:
                    string asterisks = new string('*', _inputPasscode.Length);
                    // 4桁に満たない場合はアンダーバーで入力位置を表現
                    string displayPass = asterisks.PadRight(4, '_');
                    
                    displayText =
                        "<color=#33FF66>=== SECURITY LOGIN ===</color>\n" +
                        "PLEASE ENTER 4-DIGIT PIN CODE\n\n" +
                        "INPUT YOUR PASSCODE:\n" +
                        $"<size=140%><color=#33FF99>[ {displayPass} ]</color></size>\n\n" +
                        "<color=#558855>[ENTER] CONFIRM  /  [CANCEL] CLEAR</color>";
                    break;

                case ATMSubState.MainMenu:
                    displayText =
                        "<color=#33FF66>=== MAIN MENU ===</color>\n" +
                        "SELECT TRANSACTION TYPE:\n\n" +
                        "[1] INQUIRE BALANCE (BAL)\n" +
                        "[2] COIN EXCHANGE (EXCH)\n" +
                        "[3] EXIT TERMINAL (EXIT)\n\n" +
                        "<color=#558855>CLICK 3D KEYPAD TO ENTER CHOICE</color>";
                    break;

                case ATMSubState.Inquiry:
                    float clean = wallet != null ? wallet.WashedAmount : 0f;
                    float dirty = wallet != null ? wallet.UnwashedAmount : 0f;
                    int gold = wallet != null ? wallet.GoldCoins : 0;
                    int silver = wallet != null ? wallet.SilverCoins : 0;
                    int bronze = wallet != null ? wallet.BronzeCoins : 0;
                    int diamond = wallet != null ? wallet.BlackDiamonds : 0;

                    // 高速カウントアップ演出以外の時は実際の金額をキャッシュ
                    if (!_isCountingUp)
                    {
                        _visualWashedAmount = clean;
                    }

                    displayText =
                        "<color=#33FF66>=== BALANCE INQUIRY ===</color>\n" +
                        $"CLEAN BALANCE:  ¥{_visualWashedAmount:N0}\n" +
                        $"<color=#FF5533>UNWASHED DEBT:  ¥{dirty:N0}</color>\n\n" +
                        $"COINS RETRIEVED:\n" +
                        $"GOLD:{gold}  SILVER:{silver}  BRONZE:{bronze}\n" +
                        $"BLACK DIAMOND:{diamond}\n\n" +
                        "[0] BACK TO MAIN MENU";
                    break;

                case ATMSubState.LaunderConfirm:
                    float unwashed = wallet != null ? wallet.UnwashedAmount : 0f;
                    float fee = unwashed * launderingFeeRate;
                    float washed = unwashed - fee;

                    displayText =
                        "<color=#33FF66>=== LAUNDER DEBT ===</color>\n" +
                        $"DIRTY CASH: ¥{unwashed:N0}\n" +
                        $"LAUNDERING FEE ({(launderingFeeRate * 100f):F0}%): -¥{fee:N0}\n" +
                        $"<color=#33FF99>CREDITED NET AMOUNT: ¥{washed:N0}</color>\n\n" +
                        "EXECUTE LAUNDERING?\n" +
                        "[ENTER] CONFIRM TRANSACTION\n" +
                        "[CANCEL] EXIT TO MENU";
                    break;

                case ATMSubState.CoinExchange:
                    float cleanCash = wallet != null ? wallet.WashedAmount : 0f;
                    int goldOwned = wallet != null ? wallet.GoldCoins : 0;
                    int silverOwned = wallet != null ? wallet.SilverCoins : 0;
                    int bronzeOwned = wallet != null ? wallet.BronzeCoins : 0;

                    if (!_isCountingUp)
                    {
                        _visualWashedAmount = cleanCash;
                    }

                    // テキストのレイアウトに、スピンボックスと売却ボタンが綺麗に重なるように空白スペースを設けます。
                    displayText =
                        "<color=#33FF66>=== COIN EXCHANGE ===</color>\n" +
                        $"CLEAN BALANCE: ¥{_visualWashedAmount:N0}\n\n" +
                        $"GOLD  : {goldOwned} owned (¥{_goldPrice:N0})\n" +
                        "        Qty: [     ]       [        ]\n\n" +
                        $"SILVER: {silverOwned} owned (¥{_silverPrice:N0})\n" +
                        "        Qty: [     ]       [        ]\n\n" +
                        $"BRONZE: {bronzeOwned} owned (¥{_bronzePrice:N0})\n" +
                        "        Qty: [     ]       [        ]\n\n" +
                        "[0] BACK TO MAIN MENU";

                    // スピンボックスの数値テキストを同期更新
                    if (_goldQtyText != null) _goldQtyText.text = _goldSellQty.ToString();
                    if (_silverQtyText != null) _silverQtyText.text = _silverSellQty.ToString();
                    if (_bronzeQtyText != null) _bronzeQtyText.text = _bronzeSellQty.ToString();
                    break;

                case ATMSubState.Processing:
                    displayText =
                        "<color=#FFCC33>=== PROCESSING ===</color>\n" +
                        "LAUNDERING TRANSACTION IN PROGRESS...\n\n" +
                        "[□□□□□□□□□□] 0%\n\n" +
                        "<color=#FF3333>WARNING: DO NOT POWER OFF TERMINAL</color>";
                    break;

                case ATMSubState.Success:
                    displayText =
                        "<color=#33FF66>=== TRANSACTION SUCCESS ===</color>\n" +
                        "LAUNDERING COMPLETED SUCCESSFULLY.\n\n" +
                        $"CREDITED AMOUNT: ¥{successAmountTextValue:N0}\n\n" +
                        "[ENTER] RETURN TO MENU";
                    break;
            }

            atmScreenText.text = displayText;
        }

        private void OnATMKeyPressed(KeyRole role)
        {
            if (_currentState != ATMState.Active) return;

            // 処理中・カウントアップ演出中はキー入力をブロック
            if (_currentSubState == ATMSubState.Processing || _isLaunderProcessing || _isCountingUp) return;

            if (_currentSubState == ATMSubState.Welcome)
            {
                if (_canProceedFromWelcome)
                {
                    _inputPasscode = "";
                    ChangeSubState(ATMSubState.PasscodeInput);
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
                    if (role == KeyRole.Num0 || role == KeyRole.Cancel)
                    {
                        ChangeSubState(ATMSubState.MainMenu);
                    }
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

                if (atmScreenText != null)
                {
                    atmScreenText.text =
                        "<color=#FFCC33>=== PROCESSING ===</color>\n" +
                        "LAUNDERING TRANSACTION IN PROGRESS...\n\n" +
                        $"[{barText}] {Mathf.FloorToInt(progress * 100f)}%\n\n" +
                        "<color=#FF3333>WARNING: DO NOT POWER OFF TERMINAL</color>";
                }

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
                wallet.AddWashed(washedAmount);
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
                wallet.AddWashed(gained);
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
                wallet.AddWashed(gained);
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
                wallet.AddWashed(gained);
                float endCash = wallet.WashedAmount;

                _bronzeSellQty = 0;
                StartCoroutine(AnimateCashCountUp(startCash, endCash));
            }
        }

        /// <summary>
        /// 所持金が高速で流れ込むように上昇するカウントアップ演出。
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
            _isCountingUp = false;
            UpdateDisplay();
        }

        public void PlayKeyFeedback()
        {
            if (keyClickSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(keyClickSound);
            }
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
                    btn.Press(audioSource);
                    return;
                }
            }

            AnimateRandomButton();
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
                    btn.Press(audioSource);
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

            // 1. GOLD コイン行 (Y = 40f付近)
            CreateSpinboxAndSellButton(_coinExchangePanelGo.transform, 40f, 
                () => ChangeGoldQty(-1), 
                () => ChangeGoldQty(1), 
                () => SellGold(), 
                out _goldQtyText);

            // 2. SILVER コイン行 (Y = -50f付近)
            CreateSpinboxAndSellButton(_coinExchangePanelGo.transform, -50f, 
                () => ChangeSilverQty(-1), 
                () => ChangeSilverQty(1), 
                () => SellSilver(), 
                out _silverQtyText);

            // 3. BRONZE コイン行 (Y = -140f付近)
            CreateSpinboxAndSellButton(_coinExchangePanelGo.transform, -140f, 
                () => ChangeBronzeQty(-1), 
                () => ChangeBronzeQty(1), 
                () => SellBronze(), 
                out _bronzeQtyText);

            // 初期状態は非アクティブ
            _coinExchangePanelGo.SetActive(false);
            _uiCanvasGo.SetActive(false);
        }

        /// <summary>
        /// 指定されたY位置に、スピンボックス（マイナス、数量、プラス）と売却ボタンを組み立てます。
        /// </summary>
        private void CreateSpinboxAndSellButton(Transform parent, float yPos, 
            UnityEngine.Events.UnityAction onMinus, 
            UnityEngine.Events.UnityAction onPlus, 
            UnityEngine.Events.UnityAction onSell,
            out TextMeshProUGUI qtyTextComp)
        {
            // マイナスボタン
            GameObject minusBtn = CreateUIButton(parent, "MinusBtn", "-", new Vector2(-195f, yPos), new Vector2(30f, 30f), new Color(0.1f, 0.25f, 0.15f, 0.95f));
            minusBtn.GetComponent<Button>().onClick.AddListener(onMinus);

            // 数量表示テキスト
            GameObject qtyTextGo = new GameObject("QtyText", typeof(RectTransform), typeof(TextMeshProUGUI));
            qtyTextGo.transform.SetParent(parent, false);
            qtyTextComp = qtyTextGo.GetComponent<TextMeshProUGUI>();
            qtyTextComp.text = "0";
            qtyTextComp.fontSize = 22;
            qtyTextComp.color = Color.white;
            qtyTextComp.alignment = TextAlignmentOptions.Center;
            RectTransform qtyRt = qtyTextGo.GetComponent<RectTransform>();
            qtyRt.anchoredPosition = new Vector2(-160f, yPos);
            qtyRt.sizeDelta = new Vector2(40f, 30f);

            // プラスボタン
            GameObject plusBtn = CreateUIButton(parent, "PlusBtn", "+", new Vector2(-125f, yPos), new Vector2(30f, 30f), new Color(0.1f, 0.25f, 0.15f, 0.95f));
            plusBtn.GetComponent<Button>().onClick.AddListener(onPlus);

            // 売却実行ボタン
            GameObject sellBtn = CreateUIButton(parent, "SellBtn", "SELL", new Vector2(100f, yPos), new Vector2(90f, 30f), new Color(0.2f, 0.7f, 0.3f, 0.95f));
            sellBtn.GetComponent<Button>().onClick.AddListener(onSell);
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
