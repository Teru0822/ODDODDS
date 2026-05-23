using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages the turn-based game loop (Round system).
/// Phase 1: UFO Catcher (play 1-3 times).
/// Phase 2: Pinball (buy EXACTLY 1 ball, shoot it, convert unwashed money to normal cash).
/// Once cash-out is complete and all balls are cleared, advance to the next round.
/// Can be toggled on/off in the Inspector for easier debugging.
/// </summary>
public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }

    [Header("Turn System Config")]
    [Tooltip("Enable or disable the turn-based round system. Turn this off for free debugging.")]
    public bool isTurnSystemEnabled = true;

    [Header("Status")]
    [Tooltip("The current round number.")]
    public int currentRound = 1;

    public enum RoundPhase
    {
        UfoPhase,      // Phase 1: UFO Catcher (accumulating unwashed money)
        PinballPhase   // Phase 2: Pinball Shop, launching balls, cashing out at exchange
    }

    [Header("Current Phase")]
    public RoundPhase currentPhase = RoundPhase.UfoPhase;

    [Header("World Space Display")]
    [Tooltip("Optional: TextMeshPro 3D component in the world (e.g. above the exchange station) to display the round number.")]
    public TMP_Text worldRoundText;

    private Canvas _hudCanvas;
    private TextMeshProUGUI _hudRoundText;
    private int _purchasedBallCountThisRound = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceOnSceneLoad()
    {
        // Auto-create only if no instance exists in the scene
        if (FindAnyObjectByType<RoundManager>() == null)
        {
            var go = new GameObject("[RoundManager]");
            go.AddComponent<RoundManager>();
            Debug.Log("[RoundManager] Auto-created [RoundManager] GameObject at runtime.");
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        CreateHUD();
        SetupWorldText();
        SetupListeners();
        UpdateUI();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_hudCanvas == null)
        {
            CreateHUD();
        }
        else
        {
            // Re-target display in case the cameras were recreated
            var camController = FindAnyObjectByType<UFOCameraController>();
            if (camController != null && camController.GetActiveCamera() != null)
            {
                _hudCanvas.targetDisplay = camController.GetActiveCamera().targetDisplay;
            }
        }

        SetupWorldText();
        SetupListeners();
        UpdateUI();
    }

    private void SetupListeners()
    {
        // Subscribe to ExchangeStation dispense event
        var exchange = FindAnyObjectByType<ExchangeStation>();
        if (exchange != null)
        {
            exchange.onDispenseComplete.RemoveListener(OnExchangeDispenseComplete);
            exchange.onDispenseComplete.AddListener(OnExchangeDispenseComplete);
        }

        // Subscribe to ShopBallController purchase event
        var shop = FindAnyObjectByType<ShopBallController>();
        if (shop != null)
        {
            shop.onPurchase.RemoveListener(OnBallPurchased);
            shop.onPurchase.AddListener(OnBallPurchased);
        }
    }

    private void SetupWorldText()
    {
        if (worldRoundText != null) return;

        // Try to automatically find the ExchangeStation and place a 3D TextMeshPro above it
        var exchange = FindAnyObjectByType<ExchangeStation>();
        if (exchange != null)
        {
            GameObject worldTextGo = new GameObject("WorldRoundText");
            worldTextGo.transform.SetParent(exchange.transform, false);
            
            // Position it 1.5 meters above the exchange station to be visible in the room
            worldTextGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            
            var tmp = worldTextGo.AddComponent<TextMeshPro>();
            tmp.fontSize = 5;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.yellow;
            worldRoundText = tmp;
        }
    }

    private void CreateHUD()
    {
        // Screen overlay HUD for display on player's camera screen
        GameObject canvasGo = new GameObject("RoundHUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        _hudCanvas = canvasGo.GetComponent<Canvas>();
        _hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _hudCanvas.sortingOrder = 30000;
        
        // Target player's camera display (Display 4 is index 3)
        var camController = FindAnyObjectByType<UFOCameraController>();
        if (camController != null && camController.GetActiveCamera() != null)
        {
            _hudCanvas.targetDisplay = camController.GetActiveCamera().targetDisplay;
        }
        else
        {
            _hudCanvas.targetDisplay = 3; 
        }

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Top-left text overlay
        GameObject textGo = new GameObject("RoundText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(canvasGo.transform, false);
        _hudRoundText = textGo.GetComponent<TextMeshProUGUI>();
        _hudRoundText.fontSize = 26;
        _hudRoundText.fontStyle = FontStyles.Bold;
        _hudRoundText.color = Color.cyan;

        RectTransform rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(30f, -30f);
        rt.sizeDelta = new Vector2(600f, 120f);

        var shadow = textGo.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(2f, -2f);
    }

    private void Update()
    {
        if (!isTurnSystemEnabled)
        {
            if (_hudCanvas != null && _hudCanvas.gameObject.activeSelf)
            {
                _hudCanvas.gameObject.SetActive(false);
            }
            if (worldRoundText != null && worldRoundText.gameObject.activeSelf)
            {
                worldRoundText.gameObject.SetActive(false);
            }
            return;
        }

        if (_hudCanvas != null && !_hudCanvas.gameObject.activeSelf)
        {
            _hudCanvas.gameObject.SetActive(true);
        }
        if (worldRoundText != null && !worldRoundText.gameObject.activeSelf)
        {
            worldRoundText.gameObject.SetActive(true);
        }

        // Handle UFO -> Pinball phase transition
        if (currentPhase == RoundPhase.UfoPhase)
        {
            var ufoCam = UFOCameraController.Instance;
            if (ufoCam != null)
            {
                // Once the player has played at least once and exited the UFO catcher screen
                if (ufoCam.PaymentCount > 0 && !UFOCameraController.IsPlayingUfo)
                {
                    currentPhase = RoundPhase.PinballPhase;
                    _purchasedBallCountThisRound = 0;
                    UpdateUI();
                    Debug.Log($"[RoundManager] Transition to PINBALL Phase. Round: {currentRound}");
                }
            }
        }
        else if (currentPhase == RoundPhase.PinballPhase)
        {
            CheckRoundEnd();
        }

        // Billboard rotation for World Round Text to face player camera
        if (worldRoundText != null && worldRoundText.gameObject.activeInHierarchy)
        {
            Camera activeCam = UFOCameraController.Instance != null ? UFOCameraController.Instance.GetActiveCamera() : Camera.main;
            if (activeCam != null)
            {
                Vector3 fwd = activeCam.transform.forward;
                Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);
                rot *= Quaternion.Euler(0f, 180f, 0f); // Face the viewer
                worldRoundText.transform.rotation = rot;
            }
        }

        UpdateUI();
    }

    private void OnBallPurchased(ShopBallSlot slot)
    {
        if (!isTurnSystemEnabled) return;
        _purchasedBallCountThisRound++;
        UpdateUI();
        Debug.Log($"[RoundManager] Ball purchased this round: {_purchasedBallCountThisRound}");
    }

    private void CheckRoundEnd()
    {
        if (!isTurnSystemEnabled || currentPhase != RoundPhase.PinballPhase) return;

        // Get count of active balls in the pinball system
        int activeBalls = PinballBallManager.Instance != null ? PinballBallManager.Instance.ActiveBallCount : 0;

        // Check if exchange station is cashing out
        var exchange = FindAnyObjectByType<ExchangeStation>();
        bool isDispensing = exchange != null && exchange.IsDispensing;

        // Has the player bought their ball and is the play session finished?
        bool hasPlayedBall = _purchasedBallCountThisRound >= 1;
        bool isPlayFinished = activeBalls == 0 && !isDispensing;

        if (hasPlayedBall && isPlayFinished)
        {
            float totalValue = exchange != null ? exchange.CurrentTotalValue : 0f;
            
            // If they got no score/value, or if they successfully cashed out (totalValue reset to 0)
            if (totalValue <= 0f)
            {
                EndRound();
            }
        }
    }

    private void OnExchangeDispenseComplete()
    {
        CheckRoundEnd();
    }

    private void EndRound()
    {
        currentRound++;
        currentPhase = RoundPhase.UfoPhase;
        _purchasedBallCountThisRound = 0;

        // Reset the UFO Catcher play count so they can play again
        var ufoCam = UFOCameraController.Instance;
        if (ufoCam != null)
        {
            ufoCam.ResetPaymentCount();
        }

        UpdateUI();
        Debug.Log($"[RoundManager] Round ended. Starting Round {currentRound} - UFO Catcher Phase");
    }

    private void UpdateUI()
    {
        string phaseStr = "";
        if (currentPhase == RoundPhase.UfoPhase)
        {
            phaseStr = "UFO Catcher Phase";
        }
        else
        {
            int activeBalls = PinballBallManager.Instance != null ? PinballBallManager.Instance.ActiveBallCount : 0;
            var exchange = FindAnyObjectByType<ExchangeStation>();
            float totalValue = exchange != null ? exchange.CurrentTotalValue : 0f;

            if (_purchasedBallCountThisRound == 0)
            {
                phaseStr = "Pinball Phase: Buy a Ball";
            }
            else if (activeBalls > 0)
            {
                phaseStr = "Pinball Phase: Playing...";
            }
            else if (totalValue > 0f)
            {
                phaseStr = "Pinball Phase: Cash Out Now!";
            }
            else
            {
                phaseStr = "Pinball Phase: Finished";
            }
        }

        string displayTextStr = $"Round: {currentRound}\n{phaseStr}";

        if (_hudRoundText != null)
        {
            _hudRoundText.text = isTurnSystemEnabled ? displayTextStr : "";
        }

        if (worldRoundText != null)
        {
            worldRoundText.text = isTurnSystemEnabled ? $"Round {currentRound}\n{phaseStr}" : "";
        }
    }

    /// <summary>Checks if UFO catcher is playable (in UfoPhase)</summary>
    public bool CanPlayUfo()
    {
        if (!isTurnSystemEnabled) return true;
        return currentPhase == RoundPhase.UfoPhase;
    }

    /// <summary>Checks if balls can be bought (in PinballPhase and limit not reached)</summary>
    public bool CanBuyBalls()
    {
        if (!isTurnSystemEnabled) return true;
        return currentPhase == RoundPhase.PinballPhase && _purchasedBallCountThisRound < 1;
    }

    /// <summary>Checks if exchange button is interactable (in PinballPhase)</summary>
    public bool CanExchange()
    {
        if (!isTurnSystemEnabled) return true;
        return currentPhase == RoundPhase.PinballPhase;
    }
}
