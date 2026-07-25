using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

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
        MainMenu,
        Inquiry,
        LaunderConfirm,
        Processing,
        Success
    }

    /// <summary>
    /// ATMの全体的な挙動を制御するコントローラー。
    /// モニター上に配置された 3D TextMeshPro の文字列を書き換えることでATM表示を制御します。
    /// 表示テキストは、文字化けを防止し世界観を高めるため、すべて等幅フォントに対応した英語表記となっています。
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
        }

        private void Start()
        {
            // 初期状態はOFFにする
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

            // Escキー押下で元の視点へ戻る
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                TriggerExit();
                return;
            }

            // 3D物理ボタンの直接マウスレイキャストクリック検知
            Handle3DButtonClicks();

            // 物理テンキーやキー入力連動でボタンを沈ませる
            HandlePhysicalKeyboardInput();
        }

        /// <summary>
        /// プレハブ内で完結しているアセットの参照をバリデーションします。
        /// </summary>
        private void ValidateReferences()
        {
            if (cameraTargetTransform == null)
                Debug.LogError("[ATMController] cameraTargetTransform がアサインされていません。カメラの遷移先位置が必要です。", this);

            if (hoverOutline == null)
                Debug.LogError("[ATMController] hoverOutline がアサインされていません。クリック検出に必要です。", this);

            if (atmScreenText == null)
                Debug.LogError("[ATMController] atmScreenText (3D TextMeshPro) がアサインされていません。モニターにテキストを投影するために必須です。", this);

            if (keyButtons.Count == 0)
                Debug.LogWarning("[ATMController] 物理ボタン (keyButtons) が登録されていません。物理クリックインタラクションは動作しません。", this);
        }

        /// <summary>
        /// 別シーンから読み込まれるアセット（カメラ、プレイヤー）をランタイムで動的解決します。
        /// </summary>
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

            // クリックされたタイミングで別シーンの参照を最新の状態で解決する
            ResolveCrossSceneReferences();

            StartCoroutine(TransitionToATM());
        }

        private IEnumerator TransitionToATM()
        {
            _currentState = ATMState.TransitioningToATM;
            IsInteracting = true;

            if (hoverOutline != null) hoverOutline.enabled = false;

            // プレイヤーの移動と視点移動を無効化
            if (_fpController != null)
            {
                _fpController.enabled = false;
                var animator = _fpController.GetComponentInChildren<Animator>();
                if (animator != null) animator.SetFloat("Speed", 0f);
            }

            // 動的に他シーンから取得して無効化
            var pickupController = FindAnyObjectByType<CupPickupController>();
            if (pickupController != null) pickupController.enabled = false;

            // カメラの元の姿勢を保存
            if (playerCamera != null)
            {
                _originalPlayerCamPos = playerCamera.transform.position;
                _originalPlayerCamRot = playerCamera.transform.rotation;
            }

            // Slerpで専用位置にメインカメラを遷移
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

            // 起動！
            if (startupSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(startupSound);
            }

            SetATMState(true);
            _currentState = ATMState.Active;

            // 画面をWelcome状態にする
            ChangeSubState(ATMSubState.Welcome);

            // 3Dボタン直接クリックを行うため、マウスクロックを解除してカーソルを表示
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

            // シャットダウン演出
            if (shutdownSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(shutdownSound);
            }

            SetATMState(false);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // カメラを元のプレイヤー位置に逆遷移
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

            // プレイヤー移動・インタラクトを復旧
            if (_fpController != null) _fpController.enabled = true;
            
            var pickupController = FindAnyObjectByType<CupPickupController>();
            if (pickupController != null) pickupController.enabled = true;

            if (hoverOutline != null) hoverOutline.enabled = true;

            _currentState = ATMState.Off;
            IsInteracting = false;

            // 脱出完了時のカーソルロックと非表示を念押し
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void SetATMState(bool active)
        {
            if (atmScreenText != null) atmScreenText.gameObject.SetActive(active);

            if (atmLights != null)
            {
                foreach (var lightObj in atmLights)
                {
                    if (lightObj != null) lightObj.SetActive(active);
                }
            }
        }

        // --- 画面表示更新とサブ状態遷移 ---

        private void ChangeSubState(ATMSubState nextState)
        {
            _currentSubState = nextState;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (atmScreenText == null) return;

            string displayText = "";

            switch (_currentSubState)
            {
                case ATMSubState.Welcome:
                    displayText = 
                        "<color=#33FF66>=== FEVER CAPITAL ATM ===</color>\n" +
                        "SECURITY LEVEL: EXTREME\n\n" +
                        "INSERT DEBIT CARD OR\n" +
                        "CLICK ANY KEYPAD BUTTON\n" +
                        "TO START TRANSACTION";
                    break;

                case ATMSubState.MainMenu:
                    displayText =
                        "<color=#33FF66>=== MAIN MENU ===</color>\n" +
                        "SELECT TRANSACTION TYPE:\n\n" +
                        "[1] INQUIRE BALANCE (BAL)\n" +
                        "[2] LAUNDER CASH (LAUND)\n" +
                        "[3] EXIT TERMINAL (EXIT)\n\n" +
                        "<color=#558855>CLICK 3D KEYPAD TO ENTER CHOICE</color>";
                    break;

                case ATMSubState.Inquiry:
                    var wallet = PlayerWallet.Local;
                    float clean = wallet != null ? wallet.WashedAmount : 0f;
                    float dirty = wallet != null ? wallet.UnwashedAmount : 0f;
                    int gold = wallet != null ? wallet.GoldCoins : 0;
                    int silver = wallet != null ? wallet.SilverCoins : 0;
                    int bronze = wallet != null ? wallet.BronzeCoins : 0;
                    int diamond = wallet != null ? wallet.BlackDiamonds : 0;

                    displayText =
                        "<color=#33FF66>=== BALANCE INQUIRY ===</color>\n" +
                        $"CLEAN BALANCE:  ¥{clean:N0}\n" +
                        $"<color=#FF5533>UNWASHED DEBT:  ¥{dirty:N0}</color>\n\n" +
                        $"COINS RETRIEVED:\n" +
                        $"GOLD:{gold}  SILVER:{silver}  BRONZE:{bronze}\n" +
                        $"BLACK DIAMOND:{diamond}\n\n" +
                        "[0] BACK TO MAIN MENU";
                    break;

                case ATMSubState.LaunderConfirm:
                    var w = PlayerWallet.Local;
                    float unwashed = w != null ? w.UnwashedAmount : 0f;
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

        private float successAmountTextValue = 0f;

        private void OnATMKeyPressed(KeyRole role)
        {
            if (_currentState != ATMState.Active) return;

            // 処理中は操作を受け付けない
            if (_currentSubState == ATMSubState.Processing) return;

            switch (_currentSubState)
            {
                case ATMSubState.Welcome:
                    ChangeSubState(ATMSubState.MainMenu);
                    break;

                case ATMSubState.MainMenu:
                    if (role == KeyRole.Num1)
                    {
                        ChangeSubState(ATMSubState.Inquiry);
                    }
                    else if (role == KeyRole.Num2)
                    {
                        ChangeSubState(ATMSubState.LaunderConfirm);
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

                // テキストの進捗バーを構築
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

                // 物理ボタンを定期的にカタカタと沈ませる (処理中の機械演出)
                if (Time.frameCount % 25 == 0)
                {
                    AnimateRandomButton();
                }

                yield return null;
            }

            // 洗浄処理を実行
            var wallet = PlayerWallet.Local;
            float washedAmount = 0f;
            if (wallet != null)
            {
                float fee = amountToWash * launderingFeeRate;
                washedAmount = amountToWash - fee;

                wallet.UnwashedAmount = 0f;
                wallet.AddWashed(washedAmount);
            }

            successAmountTextValue = washedAmount;

            // 成功音の再生
            if (washSuccessSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(washSuccessSound);
            }

            ChangeSubState(ATMSubState.Success);
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

        /// <summary>
        /// 指定された役割のボタンのアニメーションを再生します。
        /// </summary>
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

        /// <summary>
        /// 3D空間上の物理ボタンに対するマウスクリックを検知します。
        /// </summary>
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

        /// <summary>
        /// 物理キーボードからの入力を、3Dボタンの役割に紐づけて統合処理します。
        /// </summary>
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
    }
}
