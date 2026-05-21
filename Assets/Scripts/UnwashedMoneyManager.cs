using UnityEngine;
using TMPro;

/// <summary>
/// 未洗浄金（未洗浄メダルの累計）を一元管理するシングルトン。
/// UFOItemGoal などから加算し、ピンボールショップ等から減算する。
/// </summary>
public class UnwashedMoneyManager : MonoBehaviour
{
    public static UnwashedMoneyManager Instance { get; private set; }

    [Header("初期設定")]
    [SerializeField, Tooltip("起動時に保持する未洗浄金（テスト時のシード値）")]
    private float _currentAmount = 0f;

    /// <summary>現在の未洗浄金残高</summary>
    public float CurrentAmount => _currentAmount;

    [Header("画面表示 (UI)")]
    [Tooltip("未洗浄金を表示するUIテキスト (任意)")]
    public TextMeshProUGUI displayText;

    [Tooltip("表示フォーマット ({0} に金額が入る)")]
    public string displayFormat = "Unwashed: ¥{0:N0}";

    /// <summary>残高変動通知</summary>
    public event System.Action<float> OnAmountChanged;

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

    /// <summary>未洗浄金を加算する。amount <= 0 は無視。</summary>
    public void Add(float amount)
    {
        if (amount <= 0f) return;
        _currentAmount += amount;
        UpdateUI();
        OnAmountChanged?.Invoke(_currentAmount);
    }

    /// <summary>支払い可能ならその額を引いて true を返す。残高不足なら false。</summary>
    public bool TrySpend(float cost)
    {
        if (cost <= 0f) return true;
        if (_currentAmount < cost) return false;
        _currentAmount -= cost;
        UpdateUI();
        OnAmountChanged?.Invoke(_currentAmount);
        return true;
    }

    /// <summary>残高が cost 以上かを判定（消費せずに参照したい時用）</summary>
    public bool CanAfford(float cost)
    {
        return cost <= 0f || _currentAmount >= cost;
    }

    private void UpdateUI()
    {
        if (displayText != null)
        {
            displayText.text = string.Format(displayFormat, Mathf.FloorToInt(_currentAmount));
        }
    }
}
