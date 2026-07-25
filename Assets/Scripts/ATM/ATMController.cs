using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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
    /// ATMの全体的な挙動を制御するコントローラー。
    /// モニター部分（WorldSpace Canvas）に情報を映し出し、3D空間上の物理ボタンへのマウスレイキャストクリック、
    /// およびキーボード入力によってATM操作（残高照会・資金洗浄等）を処理します。
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

        [Header("モニター投影設定 (WorldSpace Canvas用)")]
        [Tooltip("ATMの画面モニター位置のメッシュまたはTransform。アサインされるとWorldSpace Canvasが自動フィット配置されます")]
        [SerializeField] private Transform screenTargetTransform;

        [Tooltip("モニター画面に投影する Canvas のスケール倍率。モニターの大きさに合わせて調整してください")]
        [SerializeField] private float uiScaleMultiplier = 0.00045f;

        [Header("インタラクション検出 (プレハブ内アセット)")]
        [Tooltip("ATMにアタッチした MouseHoverOutline。インスペクターでの指定が必須です")]
        [SerializeField] private MouseHoverOutline hoverOutline;

        [Header("演出用オブジェクト (プレハブ内アセット)")]
        [Tooltip("起動時に有効化するライトオブジェクト群")]
        [SerializeField] private GameObject[] atmLights;

        [Tooltip("ATMの操作用 Canvas。未設定（null）の場合は、起動時にWorldSpaceとして自動生成されます")]
        [SerializeField] private GameObject atmUiCanvas;

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

        [Header("画面UIパネル参照 (空の場合は自動生成されます)")]
        [SerializeField] private GameObject welcomePanel;
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject inquiryPanel;
        [SerializeField] private GameObject launderPanel;
        [SerializeField] private GameObject processingPanel;
        [SerializeField] private GameObject successPanel;

        [Header("画面UIテキスト/スライダー (空の場合は自動生成されます)")]
        [SerializeField] private TextMeshProUGUI cleanCashText;
        [SerializeField] private TextMeshProUGUI dirtyCashText;
        [SerializeField] private TextMeshProUGUI coinsText;

        [SerializeField] private TextMeshProUGUI launderConfirmText;
        [SerializeField] private Slider processingSlider;
        [SerializeField] private TextMeshProUGUI successAmountText;

        private ATMState _currentState = ATMState.Off;
        private App.Player.FirstPersonController _fpController;
        private Vector3 _originalPlayerCamPos;
        private Quaternion _originalPlayerCamRot;

        // 動的生成した Canvas を保持
        private GameObject _dynamicCanvasGo;

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

            // UI Canvas が未設定の場合、動的にWorldSpace UIを構築
            if (atmUiCanvas == null)
            {
                Debug.Log("[ATMController] atmUiCanvas が未設定のため、動的フォールバックUI (WorldSpace Canvas) を生成します。", this);
                CreateDynamicUICanvas();
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

            // Escキー押下で元の視点へ戻る (または取引終了キー)
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

            if (atmUiCanvas == null)
                Debug.LogError("[ATMController] atmUiCanvas が存在しません。", this);

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

            // プレイヤーの移動と視点移動を無効化 (動的に取得したコントローラーに命令)
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

            // UIをWelcome画面で開く
            ShowPanel(welcomePanel);

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
        }

        private void SetATMState(bool active)
        {
            if (atmUiCanvas != null) atmUiCanvas.SetActive(active);

            if (atmLights != null)
            {
                foreach (var lightObj in atmLights)
                {
                    if (lightObj != null) lightObj.SetActive(active);
                }
            }
        }

        // --- UI用パネル切り替え・制御メソッド ---

        private void ShowPanel(GameObject targetPanel)
        {
            if (targetPanel == null) return;

            if (welcomePanel != null) welcomePanel.SetActive(welcomePanel == targetPanel);
            if (mainMenuPanel != null) mainMenuPanel.SetActive(mainMenuPanel == targetPanel);
            if (inquiryPanel != null) inquiryPanel.SetActive(inquiryPanel == targetPanel);
            if (launderPanel != null) launderPanel.SetActive(launderPanel == targetPanel);
            if (processingPanel != null) processingPanel.SetActive(processingPanel == targetPanel);
            if (successPanel != null) successPanel.SetActive(successPanel == targetPanel);
        }

        private void OnATMKeyPressed(KeyRole role)
        {
            if (_currentState != ATMState.Active) return;

            // 画面ごとの入力処理とUI遷移ロジック
            if (welcomePanel != null && welcomePanel.activeSelf)
            {
                // 初期画面ではどれかキーを押せばメインメニューへ
                ShowPanel(mainMenuPanel);
            }
            else if (mainMenuPanel != null && mainMenuPanel.activeSelf)
            {
                // メインメニュー
                if (role == KeyRole.Num1)
                {
                    UpdateInquiryUI();
                    ShowPanel(inquiryPanel);
                }
                else if (role == KeyRole.Num2)
                {
                    var wallet = PlayerWallet.Local;
                    float unwashed = wallet != null ? wallet.UnwashedAmount : 0f;

                    if (launderConfirmText != null)
                    {
                        launderConfirmText.text = $"未洗浄資金:\n¥{unwashed:N0}\n\n手数料 ({(launderingFeeRate * 100f):F0}%):\n-¥{(unwashed * launderingFeeRate):N0}\n\n口座送金額:\n¥{(unwashed * (1f - launderingFeeRate)):N0}";
                    }
                    ShowPanel(launderPanel);
                }
                else if (role == KeyRole.Num3 || role == KeyRole.Cancel)
                {
                    TriggerExit();
                }
            }
            else if (inquiryPanel != null && inquiryPanel.activeSelf)
            {
                // 残高照会画面 (0またはキャンセルでメニューへ戻る)
                if (role == KeyRole.Num0 || role == KeyRole.Cancel)
                {
                    ShowPanel(mainMenuPanel);
                }
            }
            else if (launderPanel != null && launderPanel.activeSelf)
            {
                // 資金洗浄確認画面
                if (role == KeyRole.Confirm)
                {
                    var wallet = PlayerWallet.Local;
                    float unwashed = wallet != null ? wallet.UnwashedAmount : 0f;

                    if (unwashed <= 0f)
                    {
                        Debug.Log("[ATMController] 洗浄する資金がありません。");
                        // 警告フィードバック音
                        if (keyClickSound != null && audioSource != null) audioSource.PlayOneShot(keyClickSound);
                        return;
                    }
                    StartCoroutine(ProcessLaundering(unwashed));
                }
                else if (role == KeyRole.Cancel)
                {
                    ShowPanel(mainMenuPanel);
                }
            }
            else if (successPanel != null && successPanel.activeSelf)
            {
                // 洗浄成功画面 (EnterまたはCancelで戻る)
                if (role == KeyRole.Confirm || role == KeyRole.Cancel)
                {
                    ShowPanel(mainMenuPanel);
                }
            }
        }

        private IEnumerator ProcessLaundering(float amountToWash)
        {
            ShowPanel(processingPanel);

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
                if (processingSlider != null) processingSlider.value = progress;

                // 物理ボタンを定期的にカタカタと沈ませる (処理中の機械演出)
                if (Time.frameCount % 25 == 0)
                {
                    AnimateRandomButton();
                }

                yield return null;
            }

            // 洗浄処理を実行
            var wallet = PlayerWallet.Local;
            if (wallet != null)
            {
                float fee = amountToWash * launderingFeeRate;
                float washedAmount = amountToWash - fee;

                wallet.UnwashedAmount = 0f;
                wallet.AddWashed(washedAmount);

                if (successAmountText != null)
                {
                    successAmountText.text = $"¥{washedAmount:N0}";
                }
            }

            // 成功音の再生
            if (washSuccessSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(washSuccessSound);
            }

            ShowPanel(successPanel);
        }

        private void UpdateInquiryUI()
        {
            var wallet = PlayerWallet.Local;
            if (wallet != null)
            {
                if (cleanCashText != null) cleanCashText.text = $"¥{wallet.WashedAmount:N0}";
                if (dirtyCashText != null) dirtyCashText.text = $"¥{wallet.UnwashedAmount:N0}";
                if (coinsText != null)
                {
                    coinsText.text = $"金貨: {wallet.GoldCoins}枚  銀貨: {wallet.SilverCoins}枚  銅貨: {wallet.BronzeCoins}枚\nブラックダイヤ: {wallet.BlackDiamonds}個";
                }
            }
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

            // 見つからなければランダムに沈ませる
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
                    // 3Dボタンの沈み込みアニメーションを実行し、入力を送信
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
                // 対応する3Dボタンをアニメーションさせて処理
                AnimateButtonByRole(inputRole);
                OnATMKeyPressed(inputRole);
            }
        }

        // --- WorldSpace Canvas/UI 自動生成 (フォールバック) ---

        private void CreateDynamicUICanvas()
        {
            _dynamicCanvasGo = new GameObject("ATMDynamicCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            
            // 親スケール（FBXインポート時などの変則アスペクト比）の継承による画面歪みを防ぐため、
            // 動的CanvasはATMのルートオブジェクト直下にアタッチし、ワールド座標で位置・回転を同期させます。
            _dynamicCanvasGo.transform.SetParent(transform, false);

            Canvas canvas = _dynamicCanvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 30000;

            CanvasScaler scaler = _dynamicCanvasGo.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f; 

            RectTransform canvasRt = _dynamicCanvasGo.GetComponent<RectTransform>();
            
            // screenTargetTransform があればその位置・角度にワールド座標を同期
            if (screenTargetTransform != null)
            {
                // チラつき（Z-fighting）を防ぐため、画面メッシュの正面方向（forward）に 0.002f (2mm) だけオフセットして配置
                Vector3 offsetPos = screenTargetTransform.position + screenTargetTransform.forward * 0.002f;
                _dynamicCanvasGo.transform.position = offsetPos;
                _dynamicCanvasGo.transform.rotation = screenTargetTransform.rotation;
            }
            else
            {
                // モニター位置のフォールバック (ATM前面の上部付近)
                _dynamicCanvasGo.transform.localPosition = new Vector3(0f, 1.48f, 0.17f);
                _dynamicCanvasGo.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            }

            // 800x600 解像度
            canvasRt.sizeDelta = new Vector2(800f, 600f);
            // インスペクターで設定された倍率でスケールを設定 (非等方スケール無効化)
            canvasRt.localScale = Vector3.one * uiScaleMultiplier;

            // 全体の背景コンテナ (CRT風モニター)
            GameObject monitorGo = new GameObject("MonitorFrame", typeof(RectTransform), typeof(Image));
            monitorGo.transform.SetParent(_dynamicCanvasGo.transform, false);
            
            Image monitorImg = monitorGo.GetComponent<Image>();
            monitorImg.color = new Color(0.04f, 0.08f, 0.05f, 0.99f); 

            RectTransform monitorRt = monitorGo.GetComponent<RectTransform>();
            monitorRt.anchorMin = Vector2.zero;
            monitorRt.anchorMax = Vector2.one;
            monitorRt.sizeDelta = Vector2.zero;

            // 枠線
            GameObject borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(monitorGo.transform, false);
            Image borderImg = borderGo.GetComponent<Image>();
            borderImg.color = new Color(0.2f, 0.9f, 0.4f, 0.5f);
            RectTransform borderRt = borderGo.GetComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.sizeDelta = new Vector2(-20f, -20f);
            
            var outline = borderGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.9f, 0.4f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            // 1. Welcome Panel
            welcomePanel = CreatePanel(monitorGo.transform, "WelcomePanel");
            CreateText(welcomePanel.transform, "TitleText", "FEVER CAPITAL ATM", 45, new Vector2(0f, 120f), new Color(0.2f, 1.0f, 0.4f));
            CreateText(welcomePanel.transform, "SubtitleText", "SECURITY LEVEL: EXTREME", 20, new Vector2(0f, 60f), new Color(0.5f, 0.8f, 0.5f));
            
            // 物理ボタン操作を促すテキスト
            CreateText(welcomePanel.transform, "TouchScreenButton", "取引を開始するには\nいずれかのキーをクリックしてください", 24, new Vector2(0f, -80f), Color.white);

            // 2. Main Menu Panel
            mainMenuPanel = CreatePanel(monitorGo.transform, "MainMenuPanel");
            CreateText(mainMenuPanel.transform, "MenuTitleText", "MAIN MENU - 資金洗浄・口座取引", 32, new Vector2(0f, 180f), new Color(0.2f, 1.0f, 0.4f));

            CreateText(mainMenuPanel.transform, "InquiryText", "[1] 残高照会 (BALANCE)", 24, new Vector2(0f, 60f), Color.white);
            CreateText(mainMenuPanel.transform, "LaunderText", "[2] 資金洗浄 (LAUNDER CASH)", 24, new Vector2(0f, -10f), Color.white);
            CreateText(mainMenuPanel.transform, "ExitText", "[3] 取引終了 (EXIT)", 24, new Vector2(0f, -80f), new Color(0.9f, 0.3f, 0.2f));
            CreateText(mainMenuPanel.transform, "Instruction", "物理テンキーをクリックして選択してください", 18, new Vector2(0f, -160f), new Color(0.5f, 0.8f, 0.5f));

            // 3. Inquiry Panel
            inquiryPanel = CreatePanel(monitorGo.transform, "InquiryPanel");
            CreateText(inquiryPanel.transform, "InqTitleText", "残高照会 - BALANCE INQUIRY", 32, new Vector2(0f, 180f), new Color(0.2f, 1.0f, 0.4f));
            
            cleanCashText = CreateText(inquiryPanel.transform, "CleanCash", "Clean: ¥0", 26, new Vector2(0f, 90f), Color.white);
            dirtyCashText = CreateText(inquiryPanel.transform, "DirtyCash", "Dirty: ¥0", 26, new Vector2(0f, 30f), new Color(0.9f, 0.4f, 0.3f));
            coinsText = CreateText(inquiryPanel.transform, "Coins", "金貨: 0  銀貨: 0  銅貨: 0", 18, new Vector2(0f, -35f), new Color(0.8f, 0.8f, 0.8f));

            CreateText(inquiryPanel.transform, "InqBackButton", "[0] メニューに戻る", 24, new Vector2(0f, -130f), Color.white);

            // 4. Launder Panel 
            launderPanel = CreatePanel(monitorGo.transform, "LaunderPanel");
            CreateText(launderPanel.transform, "LaunderTitle", "裏金資金洗浄処理 (LAUNDERING)", 32, new Vector2(0f, 180f), new Color(0.2f, 1.0f, 0.4f));
            
            launderConfirmText = CreateText(launderPanel.transform, "LaunderConfirmText", "洗浄手数料: 10%\n口座への送金額: ¥0", 22, new Vector2(0f, 30f), Color.white);

            CreateText(launderPanel.transform, "ExecLaunderBtn", "[Enter] 洗浄を実行する (CONFIRM)", 24, new Vector2(0f, -80f), new Color(0.2f, 0.9f, 0.4f));
            CreateText(launderPanel.transform, "CancelLaunderBtn", "[Clear] キャンセル (CANCEL)", 24, new Vector2(0f, -130f), new Color(0.6f, 0.6f, 0.6f));

            // 5. Processing Panel
            processingPanel = CreatePanel(monitorGo.transform, "ProcessingPanel");
            CreateText(processingPanel.transform, "ProcTitle", "資金洗浄中...", 36, new Vector2(0f, 100f), new Color(0.2f, 1.0f, 0.4f));
            CreateText(processingPanel.transform, "ProcSub", "DON'T TURN OFF THE POWER", 18, new Vector2(0f, 50f), new Color(0.9f, 0.4f, 0.3f));

            GameObject sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(processingPanel.transform, false);
            processingSlider = sliderGo.GetComponent<Slider>();
            RectTransform sliderRt = sliderGo.GetComponent<RectTransform>();
            sliderRt.anchoredPosition = new Vector2(0f, -40f);
            sliderRt.sizeDelta = new Vector2(500f, 30f);

            GameObject sliderBg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            sliderBg.transform.SetParent(sliderGo.transform, false);
            sliderBg.GetComponent<Image>().color = new Color(0.1f, 0.2f, 0.1f, 1f);
            RectTransform bgRt = sliderBg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            RectTransform faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = Vector2.zero;
            faRt.anchorMax = Vector2.one;
            faRt.sizeDelta = new Vector2(-10f, 0f);

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            fill.GetComponent<Image>().color = new Color(0.2f, 1.0f, 0.4f, 1f);
            RectTransform fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.sizeDelta = Vector2.zero;

            processingSlider.fillRect = fillRt;
            processingSlider.targetGraphic = fill.GetComponent<Image>();
            processingSlider.minValue = 0f;
            processingSlider.maxValue = 1f;

            // 6. Success Panel
            successPanel = CreatePanel(monitorGo.transform, "SuccessPanel");
            CreateText(successPanel.transform, "SuccTitle", "資金洗浄完了", 36, new Vector2(0f, 140f), new Color(0.2f, 1.0f, 0.4f));
            CreateText(successPanel.transform, "SuccSub", "口座に以下の金額を送金しました:", 20, new Vector2(0f, 70f), Color.white);
            
            successAmountText = CreateText(successPanel.transform, "SuccessAmount", "¥0", 42, new Vector2(0f, -10f), new Color(0.2f, 1.0f, 0.5f));

            CreateText(successPanel.transform, "SuccOkBtn", "[Enter] メインメニューに戻る", 24, new Vector2(0f, -120f), Color.white);

            atmUiCanvas = _dynamicCanvasGo;
        }

        private GameObject CreatePanel(Transform parent, string name)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            RectTransform rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            panel.SetActive(false);
            return panel;
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, string text, int fontSize, Vector2 anchoredPos, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(700f, fontSize + 30f);

            return tmp;
        }
    }
}
