using UnityEngine;
using TMPro;

/// <summary>
/// 未洗浄金（未洗浄メダルの累計）の操作 API を提供するシングルトン。
/// 実データは Player にアタッチされた PlayerWallet に保存される (マルチプレイ対応の布石)。
/// 本クラスは UI 表示と互換 API (Add / TrySpend / CanAfford / CurrentAmount / OnAmountChanged) を提供する委譲層。
/// </summary>
public class UnwashedMoneyManager : MonoBehaviour
{
    public static UnwashedMoneyManager Instance { get; private set; }

    [Header("接続")]
    [Tooltip("対象 PlayerWallet。null なら local player から自動取得 (PlayerWallet.Local)")]
    [SerializeField] private PlayerWallet wallet;

    [Header("画面表示 (UI)")]
    [Tooltip("未洗浄金を表示するUIテキスト (任意)")]
    public TextMeshProUGUI displayText;

    [Tooltip("表示フォーマット ({0} に金額が入る)")]
    public string displayFormat = "Unwashed: ¥{0:N0}";

    /// <summary>残高変動通知 (PlayerWallet のイベントを中継)</summary>
    public event System.Action<float> OnAmountChanged;

    public PlayerWallet Wallet
    {
        get
        {
            if (wallet == null) wallet = PlayerWallet.Local;
            return wallet;
        }
    }

    /// <summary>現在の未洗浄金残高 (PlayerWallet 経由)</summary>
    public float CurrentAmount
    {
        get => Wallet != null ? Wallet.UnwashedAmount : 0f;
        set { if (Wallet != null) Wallet.UnwashedAmount = value; }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        UpdateUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }


    private void HandleWalletChanged(float newAmount)
    {
        UpdateUI();
        OnAmountChanged?.Invoke(newAmount);
    }

    /// <summary>未洗浄金を加算する。amount <= 0 は無視。</summary>
    public void Add(float amount)
    {
        if (Wallet == null) return;
        Wallet.AddUnwashed(amount);
    }

    /// <summary>金・銀・銅貨をそれぞれ加算する</summary>
    public void AddCoins(int bronze, int silver, int gold)
    {
        if (Wallet == null) return;
        Wallet.AddCoins(bronze, silver, gold);
    }

    /// <summary>支払い可能ならその額を引いて true を返す。残高不足なら false。</summary>
    public bool TrySpend(float cost)
    {
        if (Wallet == null) return false;
        return Wallet.TrySpendUnwashed(cost);
    }

    /// <summary>残高が cost 以上かを判定（消費せずに参照したい時用）</summary>
    public bool CanAfford(float cost)
    {
        if (Wallet == null) return false;
        return Wallet.CanAffordUnwashed(cost);
    }

    private void UpdateUI()
    {
        if (displayText != null && Wallet != null)
        {
            displayText.text = string.Format(displayFormat, Mathf.FloorToInt(Wallet.UnwashedAmount));
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying && Instance == this)
        {
            UpdateUI();
        }
    }
#endif
}
