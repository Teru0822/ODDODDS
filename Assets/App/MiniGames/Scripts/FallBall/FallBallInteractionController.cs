using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

namespace MiniGames.FallBall
{
    [RequireComponent(typeof(Collider))]
    public class FallBallInteractionController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FallBallGameManager fallBallManager;
        [SerializeField] private Camera fallBallCamera;
        
        [Header("Player Tracking")]
        [Tooltip("プレイヤーのカメラ（通常時はこちらがON、プレイ中はOFFになります）")]
        [SerializeField] private Camera playerCamera;
        [Tooltip("プレイヤーの操作を止めるためのコンポーネントがあればここに設定")]
        [SerializeField] private MonoBehaviour playerController;

        private bool _isPlayerNear = false;
        private bool _showPrompt = false;
        private bool _isPaymentScreen = false;

        // UI Components
        private Canvas _dynamicCanvas;
        private TextMeshProUGUI _promptText;
        private GameObject _paymentPanel;
        private TextMeshProUGUI _paymentStatusText;
        private Button _payButton;
        private TextMeshProUGUI _payButtonText;
        
        private GameObject _timerPanel;
        private TextMeshProUGUI _timerText;

        private void Start()
        {
            // Trigger設定を強制
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            if (fallBallCamera != null)
            {
                fallBallCamera.enabled = false;
            }

            CreateDynamicUI();

            if (fallBallManager != null)
            {
                fallBallManager.OnGameCompleted += HandleGameCompleted;
            }
        }

        private void OnDestroy()
        {
            if (fallBallManager != null)
            {
                fallBallManager.OnGameCompleted -= HandleGameCompleted;
            }
            if (_dynamicCanvas != null)
            {
                Destroy(_dynamicCanvas.gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Playerタグを持つオブジェクトが近づいたか判定
            if (other.CompareTag("Player"))
            {
                _isPlayerNear = true;
                if (fallBallManager != null && !fallBallManager.IsPlaying)
                {
                    _showPrompt = true;
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                _isPlayerNear = false;
                _showPrompt = false;
                _isPaymentScreen = false;
            }
        }

        private void Update()
        {
            if (fallBallManager == null) return;

            // 接近中のキー入力処理
            if (_isPlayerNear && !fallBallManager.IsPlaying)
            {
                // [F]キーで支払い画面を開く／閉じる
                if (Keyboard.current.fKey.wasPressedThisFrame)
                {
                    if (!_isPaymentScreen)
                    {
                        _isPaymentScreen = true;
                        _showPrompt = false;
                    }
                    else
                    {
                        // キャンセルして戻る
                        _isPaymentScreen = false;
                        _showPrompt = true;
                    }
                }

                // 支払い画面中に[Space]キーで全額ベットして開始
                if (_isPaymentScreen && Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    TryStartPlay();
                }
            }

            // プレイ中に終了したい場合 (Fキーで強制終了など)
            if (fallBallManager.IsPlaying)
            {
                if (Keyboard.current.fKey.wasPressedThisFrame)
                {
                    Debug.Log("FallBall: プレイヤーにより強制終了されました");
                    fallBallManager.GameOver(false);
                }
            }

            UpdateDynamicUI();
        }

        private void TryStartPlay()
        {
            if (MoneyManager.Instance == null)
            {
                Debug.LogWarning("MoneyManagerが見つかりません。");
                return;
            }

            float currentMoney = MoneyManager.Instance.CurrentMoney;
            if (currentMoney <= 0)
            {
                Debug.Log("所持金がありません！");
                return;
            }

            // 全額ベット！
            MoneyManager.Instance.ReduceMoney(currentMoney);
            
            _isPaymentScreen = false;
            
            // カメラとプレイヤーの操作切り替え
            SwitchToFallBallCamera();

            // ゲーム開始
            fallBallManager.Initialize(currentMoney);
            fallBallManager.StartGame();
        }

        private void HandleGameCompleted(bool isSuccess, float multiplier)
        {
            // プレイ終了時のカメラと操作の復帰
            SwitchToPlayerCamera();
            
            if (_isPlayerNear)
            {
                _showPrompt = true;
            }
        }

        private void SwitchToFallBallCamera()
        {
            if (playerCamera != null) playerCamera.enabled = false;
            if (playerController != null) playerController.enabled = false;

            if (fallBallCamera != null) fallBallCamera.enabled = true;

            // マウスカーソルを再度ロック（必要に応じて）
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void SwitchToPlayerCamera()
        {
            if (fallBallCamera != null) fallBallCamera.enabled = false;

            if (playerCamera != null) playerCamera.enabled = true;
            if (playerController != null) playerController.enabled = true;
        }

        private void CreateDynamicUI()
        {
            GameObject canvasGo = new GameObject("FallBallDynamicUICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _dynamicCanvas = canvasGo.GetComponent<Canvas>();
            _dynamicCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _dynamicCanvas.sortingOrder = 31000;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // 1. Prompt Text
            GameObject promptGo = new GameObject("PromptText", typeof(RectTransform), typeof(TextMeshProUGUI));
            promptGo.transform.SetParent(canvasGo.transform, false);
            _promptText = promptGo.GetComponent<TextMeshProUGUI>();
            _promptText.fontSize = 28;
            _promptText.alignment = TextAlignmentOptions.Center;
            _promptText.color = Color.white;
            _promptText.text = "[F] Play FallBall (ALL-IN)";
            
            RectTransform promptRt = promptGo.GetComponent<RectTransform>();
            promptRt.anchorMin = new Vector2(0.5f, 0f);
            promptRt.anchorMax = new Vector2(0.5f, 0f);
            promptRt.pivot = new Vector2(0.5f, 0f);
            promptRt.anchoredPosition = new Vector2(0f, 120f);
            promptRt.sizeDelta = new Vector2(600f, 60f);

            var shadow1 = promptGo.AddComponent<Shadow>();
            shadow1.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow1.effectDistance = new Vector2(2f, -2f);

            // 2. Payment Panel
            _paymentPanel = new GameObject("PaymentPanel", typeof(RectTransform), typeof(Image));
            _paymentPanel.transform.SetParent(canvasGo.transform, false);
            Image panelImg = _paymentPanel.GetComponent<Image>();
            panelImg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            RectTransform panelRt = _paymentPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(500f, 300f);

            // Title Text
            GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(_paymentPanel.transform, false);
            TextMeshProUGUI titleText = titleGo.GetComponent<TextMeshProUGUI>();
            titleText.fontSize = 26;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(1f, 0.3f, 0.3f);
            titleText.text = "FALLBALL: ALL-IN BET";
            RectTransform titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -30f);
            titleRt.sizeDelta = new Vector2(460f, 40f);

            // Status Text
            GameObject statusGo = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
            statusGo.transform.SetParent(_paymentPanel.transform, false);
            _paymentStatusText = statusGo.GetComponent<TextMeshProUGUI>();
            _paymentStatusText.fontSize = 20;
            _paymentStatusText.alignment = TextAlignmentOptions.Center;
            _paymentStatusText.color = Color.white;
            RectTransform statusRt = statusGo.GetComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0.5f, 0.5f);
            statusRt.anchorMax = new Vector2(0.5f, 0.5f);
            statusRt.pivot = new Vector2(0.5f, 0.5f);
            statusRt.anchoredPosition = new Vector2(0f, 10f);
            statusRt.sizeDelta = new Vector2(460f, 100f);

            // 3. Timer Panel (Play UI)
            _timerPanel = new GameObject("TimerPanel", typeof(RectTransform));
            _timerPanel.transform.SetParent(canvasGo.transform, false);
            RectTransform timerPanelRt = _timerPanel.GetComponent<RectTransform>();
            timerPanelRt.anchorMin = new Vector2(0.5f, 1f);
            timerPanelRt.anchorMax = new Vector2(0.5f, 1f);
            timerPanelRt.pivot = new Vector2(0.5f, 1f);
            timerPanelRt.anchoredPosition = new Vector2(0f, -50f);
            timerPanelRt.sizeDelta = new Vector2(400f, 100f);

            GameObject timerBgGo = new GameObject("TimerBG", typeof(RectTransform), typeof(Image));
            timerBgGo.transform.SetParent(_timerPanel.transform, false);
            Image timerBgImg = timerBgGo.GetComponent<Image>();
            timerBgImg.color = new Color(0f, 0f, 0f, 0.6f);
            RectTransform tbgRt = timerBgGo.GetComponent<RectTransform>();
            tbgRt.anchorMin = Vector2.zero;
            tbgRt.anchorMax = Vector2.one;
            tbgRt.sizeDelta = Vector2.zero;

            GameObject timerTextGo = new GameObject("TimerText", typeof(RectTransform), typeof(TextMeshProUGUI));
            timerTextGo.transform.SetParent(_timerPanel.transform, false);
            _timerText = timerTextGo.GetComponent<TextMeshProUGUI>();
            _timerText.fontSize = 24;
            _timerText.alignment = TextAlignmentOptions.Center;
            _timerText.color = Color.white;
            RectTransform timerTextRt = timerTextGo.GetComponent<RectTransform>();
            timerTextRt.anchorMin = Vector2.zero;
            timerTextRt.anchorMax = Vector2.one;
            timerTextRt.sizeDelta = Vector2.zero;

            // 初期化
            _promptText.gameObject.SetActive(false);
            _paymentPanel.SetActive(false);
            _timerPanel.SetActive(false);
        }

        private void UpdateDynamicUI()
        {
            if (_dynamicCanvas == null || fallBallManager == null) return;

            if (fallBallManager.IsPlaying)
            {
                _promptText.gameObject.SetActive(false);
                _paymentPanel.SetActive(false);
                _timerPanel.SetActive(true);

                float time = Mathf.Max(0f, fallBallManager.PlayTimer);
                int used = fallBallManager.UsedBallsCount;
                int max = fallBallManager.MaxPlayCount;
                _timerText.text = $"Time Left: {time:F1}s\nBalls: {used} / {max}\nBet: ¥{fallBallManager.CurrentBet:N0}";
            }
            else if (_isPaymentScreen)
            {
                _promptText.gameObject.SetActive(false);
                _timerPanel.SetActive(false);
                _paymentPanel.SetActive(true);

                float money = MoneyManager.Instance != null ? MoneyManager.Instance.CurrentMoney : 0f;
                if (money > 0)
                {
                    _paymentStatusText.text = $"Current Money: ¥{money:N0}\n\nWARNING: You will bet EVERYTHING.\nIf you fail, you lose it all.\n\nPress [Space] to ALL-IN and Start\nPress [F] to Cancel";
                }
                else
                {
                    _paymentStatusText.text = "You have NO MONEY.\nCome back later.\n\nPress [F] to Cancel";
                }
            }
            else if (_showPrompt)
            {
                _promptText.gameObject.SetActive(true);
                _paymentPanel.SetActive(false);
                _timerPanel.SetActive(false);
            }
            else
            {
                _promptText.gameObject.SetActive(false);
                _paymentPanel.SetActive(false);
                _timerPanel.SetActive(false);
            }
        }
    }
}
