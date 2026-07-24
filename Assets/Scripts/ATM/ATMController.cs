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
    /// ホバーアウトライン、カメラ遷移、起動・停止演出、画面UI、資金洗浄ロジックなどを一括管理する。
    /// インスペクターで未設定の場合は、コライダー設定やUIキャンバスを自動生成する。
    /// </summary>
    [DisallowMultipleComponent]
    public class ATMController : MonoBehaviour
    {
        public static ATMController Instance { get; private set; }
        public static bool IsInteracting { get; private set; } = false;

        [Header("カメラ・遷移設定")]
        [Tooltip("プレイヤーのメインカメラ。未指定なら Camera.main")]
        [SerializeField] private Camera playerCamera;

        [Tooltip("ATM正面のカメラ配置位置（空のTransform等）。未指定ならPrefab内の 'cameraPos' を自動検索")]
        [SerializeField] private Transform cameraTargetTransform;

        [Tooltip("カメラ遷移の所要時間（秒）")]
        [SerializeField] private float transitionDuration = 1.0f;

        [Tooltip("遷移のイージング曲線")]
        [SerializeField] private AnimationCurve transitionEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("インタラクション検出")]
        [Tooltip("ATMにアタッチした MouseHoverOutline。未指定なら同一オブジェクトから取得")]
        [SerializeField] private MouseHoverOutline hoverOutline;

        [Header("演出用オブジェクト")]
        [Tooltip("起動時に有効化するライトオブジェクト群")]
        [SerializeField] private GameObject[] atmLights;

        [Tooltip("ATMの操作用 Canvas (WorldSpace / ScreenSpace)。起動時に有効化されます")]
        [SerializeField] private GameObject atmUiCanvas;

        [Header("効果音")]
        [Tooltip("再生用 AudioSource。未指定なら自動取得")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("起動時の電子音/HDD動作音")]
        [SerializeField] private AudioClip startupSound;

        [Tooltip("終了（電源オフ）時の音")]
        [SerializeField] private AudioClip shutdownSound;

        [Tooltip("ボタンを押した時のクリック音")]
        [SerializeField] private AudioClip keyClickSound;

        [Tooltip("資金洗浄に成功した時の音 (SE/debtPay など)")]
        [SerializeField] private AudioClip washSuccessSound;

        [Header("物理ボタン (テンキー) 設定")]
        [Tooltip("3Dモデル内の各ボタンオブジェクト。未指定なら子から自動検索し自動アタッチ")]
        [SerializeField] private List<ATMPhysicalButton> keyButtons = new List<ATMPhysicalButton>();

        [Header("資金洗浄パラメータ")]
        [Tooltip("資金洗浄時の手数料率 (0.1 = 10%)")]
        [Range(0f, 0.9f)]
        [SerializeField] private float launderingFeeRate = 0.1f;

        [Header("画面UIパネル参照 (空なら動的生成されます)")]
        [SerializeField] private GameObject welcomePanel;
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject inquiryPanel;
        [SerializeField] private GameObject launderPanel;
        [SerializeField] private GameObject processingPanel;
        [SerializeField] private GameObject successPanel;

        [Header("画面UIテキスト (空なら動的生成されます)")]
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

        // 動的生成UIを保持するオブジェクト
        private GameObject _dynamicCanvasGo;

        /// <summary>
        /// ゲームロード時にシーン内のATMオブジェクトを自動検出し、ATMControllerをアタッチして自動初期化します。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitATMInScene()
        {
            GameObject atm = GameObject.Find("ATM");
            if (atm == null) atm = GameObject.Find("atm");
            if (atm == null)
            {
                // 名前に "ATM" が含まれるGameObjectを検索
                foreach (var go in FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (go.name.Contains("ATM", System.StringComparison.OrdinalIgnoreCase))
                    {
                        atm = go;
                        break;
                    }
                }
            }

            if (atm != null)
            {
                if (atm.GetComponent<ATMController>() == null)
                {
                    atm.AddComponent<ATMController>();
                    Debug.Log($"[ATMController] シーン内の '{atm.name}' に ATMController を自動アタッチしました。");
                }
            }
        }

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

            // AudioSource 自動取得
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

            // SEの自動ロード (Resourcesフォルダの debtPay をデフォルト成功音に)
            if (washSuccessSound == null)
            {
                washSuccessSound = Resources.Load<AudioClip>("Sound/SE/debtPay");
            }

            // 1. コライダーの自動設定 (atm_main)
            SetupAtmCollider();

            // 2. MouseHoverOutline 自動アタッチと設定
            SetupHoverOutline();

            // 3. cameraPos の自動スキャン
            if (cameraTargetTransform == null) cameraTargetTransform = transform.Find("cameraPos");
            if (cameraTargetTransform == null)
            {
                // 無ければATMの前に動的生成する
                GameObject camPosGo = new GameObject("cameraPos");
                camPosGo.transform.SetParent(transform, false);
                camPosGo.transform.localPosition = new Vector3(0f, 1.4f, -1.1f);
                camPosGo.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
                cameraTargetTransform = camPosGo.transform;
            }

            // 4. 物理ボタンの自動スキャン & ATMPhysicalButton アタッチ
            SetupPhysicalButtons();

            // 5. ライトの自動検出
            if (atmLights == null || atmLights.Length == 0)
            {
                var lights = new List<GameObject>();
                foreach (var lightComp in GetComponentsInChildren<Light>(true))
                {
                    lights.Add(lightComp.gameObject);
                }
                atmLights = lights.ToArray();
            }

            // 6. UIキャンバスの自動生成 (アサインされていなければ)
            if (atmUiCanvas == null)
            {
                CreateDynamicUICanvas();
            }
        }

        private void Start()
        {
            _fpController = FindAnyObjectByType<App.Player.FirstPersonController>();
            if (_fpController != null && playerCamera == null)
            {
                playerCamera = _fpController.GetComponentInChildren<Camera>(true);
            }
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            // 初期状態はOFF
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

            // 物理テンキーやキー入力連動でボタンを沈ませる
            HandlePhysicalKeyboardInput();
        }

        private void SetupAtmCollider()
        {
            Transform mainMesh = transform.Find("atm_main");
            if (mainMesh == null)
            {
                foreach (Transform child in transform.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name.Contains("atm_main") || child.name.Contains("main"))
                    {
                        mainMesh = child;
                        break;
                    }
                }
            }

            if (mainMesh != null)
            {
                MeshCollider mc = mainMesh.GetComponent<MeshCollider>();
                if (mc == null)
                {
                    mc = mainMesh.gameObject.AddComponent<MeshCollider>();
                    mc.convex = false; 
                }
            }
            else
            {
                if (GetComponent<Collider>() == null)
                {
                    BoxCollider bc = gameObject.AddComponent<BoxCollider>();
                    bc.center = new Vector3(0f, 1f, 0f);
                    bc.size = new Vector3(1f, 2f, 1f);
                }
            }
        }

        private void SetupHoverOutline()
        {
            if (hoverOutline == null) hoverOutline = GetComponent<MouseHoverOutline>();
            if (hoverOutline == null) hoverOutline = GetComponentInChildren<MouseHoverOutline>();
            
            if (hoverOutline == null)
            {
                hoverOutline = gameObject.AddComponent<MouseHoverOutline>();
                hoverOutline.outlineColor = new Color(0.2f, 1.0f, 0.5f, 1f); 
                hoverOutline.outlineWidth = 0.006f;
            }
        }

        private void SetupPhysicalButtons()
        {
            var existingButtons = new List<ATMPhysicalButton>(GetComponentsInChildren<ATMPhysicalButton>(true));
            keyButtons = existingButtons;

            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                string lowerName = child.name.ToLower();
                if (lowerName.Contains("key") || lowerName.Contains("btn") || lowerName.Contains("button"))
                {
                    ATMPhysicalButton btn = child.GetComponent<ATMPhysicalButton>();
                    if (btn == null)
                    {
                        btn = child.gameObject.AddComponent<ATMPhysicalButton>();
                    }

                    if (!keyButtons.Contains(btn))
                    {
                        keyButtons.Add(btn);
                    }
                }
            }
        }

        private void OnATMClicked()
        {
            if (_currentState != ATMState.Off) return;

            float dist = Vector3.Distance(playerCamera.transform.position, transform.position);
            if (dist > 4.5f)
            {
                Debug.Log("[ATMController] ATMが遠すぎます。");
                return;
            }

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

            ShowPanel(welcomePanel);

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

        private void ShowPanel(GameObject targetPanel)
        {
            if (welcomePanel != null) welcomePanel.SetActive(welcomePanel == targetPanel);
            if (mainMenuPanel != null) mainMenuPanel.SetActive(mainMenuPanel == targetPanel);
            if (inquiryPanel != null) inquiryPanel.SetActive(inquiryPanel == targetPanel);
            if (launderPanel != null) launderPanel.SetActive(launderPanel == targetPanel);
            if (processingPanel != null) processingPanel.SetActive(processingPanel == targetPanel);
            if (successPanel != null) successPanel.SetActive(successPanel == targetPanel);

            PlayKeyFeedback();
        }

        public void OnTouchWelcome()
        {
            ShowPanel(mainMenuPanel);
        }

        public void OnClickInquiry()
        {
            UpdateInquiryUI();
            ShowPanel(inquiryPanel);
        }

        public void OnClickLaunderMenu()
        {
            var wallet = PlayerWallet.Local;
            float unwashed = wallet != null ? wallet.UnwashedAmount : 0f;

            if (launderConfirmText != null)
            {
                launderConfirmText.text = $"未洗浄資金:\n¥{unwashed:N0}\n\n手数料 ({(launderingFeeRate * 100f):F0}%):\n-¥{(unwashed * launderingFeeRate):N0}\n\n口座送金額:\n¥{(unwashed * (1f - launderingFeeRate)):N0}";
            }
            ShowPanel(launderPanel);
        }

        public void OnClickExitATM()
        {
            TriggerExit();
        }

        public void OnClickBackToMenu()
        {
            ShowPanel(mainMenuPanel);
        }

        public void OnClickExecuteLaunder()
        {
            var wallet = PlayerWallet.Local;
            float unwashed = wallet != null ? wallet.UnwashedAmount : 0f;

            if (unwashed <= 0f)
            {
                Debug.Log("[ATMController] 洗浄する資金がありません。");
                PlayKeyFeedback();
                return;
            }

            StartCoroutine(ProcessLaundering(unwashed));
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

                if (Time.frameCount % 25 == 0)
                {
                    AnimateRandomButton();
                }

                yield return null;
            }

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

        public void AnimateButtonAtIndex(int index)
        {
            if (keyButtons.Count == 0) return;
            int targetIndex = Mathf.Clamp(index, 0, keyButtons.Count - 1);
            if (keyButtons[targetIndex] != null)
            {
                keyButtons[targetIndex].Press(audioSource);
            }
        }

        private void HandlePhysicalKeyboardInput()
        {
            if (Keyboard.current == null) return;

            bool keyPressed = false;
            int buttonIndex = -1;

            if (Keyboard.current.digit0Key.wasPressedThisFrame || Keyboard.current.numpad0Key.wasPressedThisFrame) { buttonIndex = 0; keyPressed = true; }
            else if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) { buttonIndex = 1; keyPressed = true; }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) { buttonIndex = 2; keyPressed = true; }
            else if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) { buttonIndex = 3; keyPressed = true; }
            else if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame) { buttonIndex = 4; keyPressed = true; }
            else if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame) { buttonIndex = 5; keyPressed = true; }
            else if (Keyboard.current.digit6Key.wasPressedThisFrame || Keyboard.current.numpad6Key.wasPressedThisFrame) { buttonIndex = 6; keyPressed = true; }
            else if (Keyboard.current.digit7Key.wasPressedThisFrame || Keyboard.current.numpad7Key.wasPressedThisFrame) { buttonIndex = 7; keyPressed = true; }
            else if (Keyboard.current.digit8Key.wasPressedThisFrame || Keyboard.current.numpad8Key.wasPressedThisFrame) { buttonIndex = 8; keyPressed = true; }
            else if (Keyboard.current.digit9Key.wasPressedThisFrame || Keyboard.current.numpad9Key.wasPressedThisFrame) { buttonIndex = 9; keyPressed = true; }
            else if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame) { buttonIndex = 10; keyPressed = true; }
            else if (Keyboard.current.backspaceKey.wasPressedThisFrame) { buttonIndex = 11; keyPressed = true; }

            if (keyPressed)
            {
                if (keyButtons.Count > 0)
                {
                    int actualIndex = buttonIndex < keyButtons.Count ? buttonIndex : Random.Range(0, keyButtons.Count);
                    AnimateButtonAtIndex(actualIndex);
                }
                else
                {
                    if (keyClickSound != null && audioSource != null)
                    {
                        audioSource.PlayOneShot(keyClickSound);
                    }
                }
            }
        }

        private void CreateDynamicUICanvas()
        {
            _dynamicCanvasGo = new GameObject("ATMDynamicCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _dynamicCanvasGo.transform.SetParent(transform, false);

            Canvas canvas = _dynamicCanvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;

            CanvasScaler scaler = _dynamicCanvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject monitorGo = new GameObject("MonitorFrame", typeof(RectTransform), typeof(Image));
            monitorGo.transform.SetParent(_dynamicCanvasGo.transform, false);
            
            Image monitorImg = monitorGo.GetComponent<Image>();
            monitorImg.color = new Color(0.04f, 0.08f, 0.05f, 0.97f); 

            RectTransform monitorRt = monitorGo.GetComponent<RectTransform>();
            monitorRt.anchorMin = new Vector2(0.5f, 0.5f);
            monitorRt.anchorMax = new Vector2(0.5f, 0.5f);
            monitorRt.pivot = new Vector2(0.5f, 0.5f);
            monitorRt.anchoredPosition = Vector2.zero;
            monitorRt.sizeDelta = new Vector2(850f, 650f);

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
            CreateText(welcomePanel.transform, "TitleText", "FEVER CAPITAL ATM", 50, new Vector2(0f, 150f), new Color(0.2f, 1.0f, 0.4f));
            CreateText(welcomePanel.transform, "SubtitleText", "SECURITY LEVEL: EXTREME", 22, new Vector2(0f, 90f), new Color(0.5f, 0.8f, 0.5f));
            
            GameObject insertCardBtn = CreateButton(welcomePanel.transform, "TouchScreenButton", "画面をタッチしてください", new Vector2(0f, -80f), new Vector2(400f, 80f));
            insertCardBtn.GetComponent<Button>().onClick.AddListener(OnTouchWelcome);

            // 2. Main Menu Panel
            mainMenuPanel = CreatePanel(monitorGo.transform, "MainMenuPanel");
            CreateText(mainMenuPanel.transform, "MenuTitleText", "MAIN MENU - 資金洗浄・口座取引", 34, new Vector2(0f, 200f), new Color(0.2f, 1.0f, 0.4f));

            GameObject inquiryBtn = CreateButton(mainMenuPanel.transform, "InquiryButton", "残高照会 (BALANCE)", new Vector2(0f, 70f), new Vector2(450f, 70f));
            inquiryBtn.GetComponent<Button>().onClick.AddListener(OnClickInquiry);

            GameObject launderBtn = CreateButton(mainMenuPanel.transform, "LaunderButton", "資金洗浄 (LAUNDER CASH)", new Vector2(0f, -20f), new Vector2(450f, 70f));
            launderBtn.GetComponent<Button>().onClick.AddListener(OnClickLaunderMenu);

            GameObject exitBtn = CreateButton(mainMenuPanel.transform, "ExitButton", "カード返却・終了 (EXIT)", new Vector2(0f, -110f), new Vector2(450f, 70f), new Color(0.9f, 0.3f, 0.2f));
            exitBtn.GetComponent<Button>().onClick.AddListener(OnClickExitATM);

            // 3. Inquiry Panel
            inquiryPanel = CreatePanel(monitorGo.transform, "InquiryPanel");
            CreateText(inquiryPanel.transform, "InqTitleText", "残高照会 - BALANCE INQUIRY", 34, new Vector2(0f, 200f), new Color(0.2f, 1.0f, 0.4f));
            
            cleanCashText = CreateText(inquiryPanel.transform, "CleanCash", "Clean: ¥0", 28, new Vector2(0f, 100f), Color.white);
            dirtyCashText = CreateText(inquiryPanel.transform, "DirtyCash", "Dirty: ¥0", 28, new Vector2(0f, 40f), new Color(0.9f, 0.4f, 0.3f));
            coinsText = CreateText(inquiryPanel.transform, "Coins", "金貨: 0  銀貨: 0  銅貨: 0", 20, new Vector2(0f, -30f), new Color(0.8f, 0.8f, 0.8f));

            GameObject inqBackBtn = CreateButton(inquiryPanel.transform, "InqBackButton", "戻る (BACK)", new Vector2(0f, -150f), new Vector2(300f, 60f));
            inqBackBtn.GetComponent<Button>().onClick.AddListener(OnClickBackToMenu);

            // 4. Launder Panel 
            launderPanel = CreatePanel(monitorGo.transform, "LaunderPanel");
            CreateText(launderPanel.transform, "LaunderTitle", "裏金資金洗浄処理 (LAUNDERING)", 34, new Vector2(0f, 200f), new Color(0.2f, 1.0f, 0.4f));
            
            launderConfirmText = CreateText(launderPanel.transform, "LaunderConfirmText", "洗浄手数料: 10%\n口座への送金額: ¥0", 24, new Vector2(0f, 40f), Color.white);

            GameObject executeLaunderBtn = CreateButton(launderPanel.transform, "ExecLaunderBtn", "洗浄を実行する (CONFIRM)", new Vector2(-160f, -140f), new Vector2(300f, 70f), new Color(0.2f, 0.9f, 0.4f));
            executeLaunderBtn.GetComponent<Button>().onClick.AddListener(OnClickExecuteLaunder);

            GameObject cancelLaunderBtn = CreateButton(launderPanel.transform, "CancelLaunderBtn", "キャンセル (CANCEL)", new Vector2(160f, -140f), new Vector2(300f, 70f), new Color(0.6f, 0.6f, 0.6f));
            cancelLaunderBtn.GetComponent<Button>().onClick.AddListener(OnClickBackToMenu);

            // 5. Processing Panel
            processingPanel = CreatePanel(monitorGo.transform, "ProcessingPanel");
            CreateText(processingPanel.transform, "ProcTitle", "資金洗浄中...", 36, new Vector2(0f, 120f), new Color(0.2f, 1.0f, 0.4f));
            CreateText(processingPanel.transform, "ProcSub", "DON'T TURN OFF THE POWER", 18, new Vector2(0f, 70f), new Color(0.9f, 0.4f, 0.3f));

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
            CreateText(successPanel.transform, "SuccTitle", "資金洗浄完了", 36, new Vector2(0f, 150f), new Color(0.2f, 1.0f, 0.4f));
            CreateText(successPanel.transform, "SuccSub", "口座に以下の金額を送金しました:", 20, new Vector2(0f, 80f), Color.white);
            
            successAmountText = CreateText(successPanel.transform, "SuccessAmount", "¥0", 42, new Vector2(0f, 0f), new Color(0.2f, 1.0f, 0.5f));

            GameObject succOkBtn = CreateButton(successPanel.transform, "SuccOkBtn", "確認 (OK)", new Vector2(0f, -120f), new Vector2(300f, 65f));
            succOkBtn.GetComponent<Button>().onClick.AddListener(OnClickBackToMenu);

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
            rt.sizeDelta = new Vector2(700f, fontSize + 20f);

            return tmp;
        }

        private GameObject CreateButton(Transform parent, string name, string text, Vector2 anchoredPos, Vector2 size, Color? normalColor = null)
        {
            GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);

            Color color = normalColor ?? new Color(0.1f, 0.3f, 0.15f, 0.9f);
            btnGo.GetComponent<Image>().color = color;

            Button btn = btnGo.GetComponent<Button>();
            
            ColorBlock cb = btn.colors;
            cb.normalColor = color;
            cb.highlightedColor = color * 1.3f;
            cb.pressedColor = color * 0.7f;
            cb.selectedColor = color;
            btn.colors = cb;

            RectTransform rt = btnGo.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            GameObject txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(btnGo.transform, false);
            TextMeshProUGUI tmp = txtGo.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 20;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            RectTransform txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;

            btn.onClick.AddListener(PlayKeyFeedback);

            return btnGo;
        }
    }
}
