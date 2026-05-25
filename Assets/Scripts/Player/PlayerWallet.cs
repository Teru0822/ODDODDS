using UnityEngine;

/// <summary>
/// Player にアタッチする「財布」コンポーネント。
/// 未洗浄金 / 洗浄金 / 徳ポイントなど、プレイヤー個人ごとの所持データを一元管理する。
/// マルチプレイ化を見据えて、UnwashedMoneyManager / MoneyManager は本クラスへの委譲となる。
/// シングルプレイ時は唯一の Player にこれを 1 個アタッチし、PlayerWallet.Local で全コードから参照可能。
/// </summary>
[DisallowMultipleComponent]
public class PlayerWallet : MonoBehaviour
{
    [Header("初期値")]
    [SerializeField, Tooltip("未洗浄金の初期残高")]
    private float _unwashedAmount = 0f;

    [SerializeField, Tooltip("洗浄金 (通常お金) の初期残高")]
    private float _washedAmount = 10000f;

    [SerializeField, Tooltip("徳ポイント (ローグライク) の初期値")]
    private int _virtuePoints = 0;

    /// <summary>未洗浄金残高</summary>
    public float UnwashedAmount
    {
        get => _unwashedAmount;
        set
        {
            float clamped = Mathf.Max(0f, value);
            if (Mathf.Approximately(_unwashedAmount, clamped)) return;
            _unwashedAmount = clamped;
            OnUnwashedChanged?.Invoke(_unwashedAmount);
        }
    }

    /// <summary>洗浄金 (通常お金) 残高</summary>
    public float WashedAmount
    {
        get => _washedAmount;
        set
        {
            float clamped = Mathf.Max(0f, value);
            if (Mathf.Approximately(_washedAmount, clamped)) return;
            _washedAmount = clamped;
            OnWashedChanged?.Invoke(_washedAmount);
        }
    }

    /// <summary>徳ポイント (ローグライク)</summary>
    public int VirtuePoints
    {
        get => _virtuePoints;
        set
        {
            int clamped = Mathf.Max(0, value);
            if (_virtuePoints == clamped) return;
            _virtuePoints = clamped;
            OnVirtuePointsChanged?.Invoke(_virtuePoints);
        }
    }

    public event System.Action<float> OnUnwashedChanged;
    public event System.Action<float> OnWashedChanged;
    public event System.Action<int> OnVirtuePointsChanged;

    // ----- 未洗浄金 -----

    public void AddUnwashed(float amount)
    {
        if (amount <= 0f) return;
        UnwashedAmount = _unwashedAmount + amount;
    }

    public bool TrySpendUnwashed(float cost)
    {
        if (cost <= 0f) return true;
        if (_unwashedAmount < cost) return false;
        UnwashedAmount = _unwashedAmount - cost;
        return true;
    }

    public bool CanAffordUnwashed(float cost) => cost <= 0f || _unwashedAmount >= cost;

    // ----- 洗浄金 -----

    public void AddWashed(float amount)
    {
        if (amount <= 0f) return;
        WashedAmount = _washedAmount + amount;
    }

    public void ReduceWashed(float amount)
    {
        if (amount <= 0f) return;
        WashedAmount = _washedAmount - amount;
    }

    public bool CanAffordWashed(float cost) => cost <= 0f || _washedAmount >= cost;

    // ----- ローカルプレイヤー取得 -----

    private static PlayerWallet _cachedLocal;

    /// <summary>
    /// ローカルプレイヤーの財布を取得する。
    /// シングルプレイ時はシーン唯一の PlayerWallet を返す。
    /// マルチプレイ化したら IsOwner 等で識別するロジックに置き換える。
    /// </summary>
    public static PlayerWallet Local
    {
        get
        {
            if (_cachedLocal != null) return _cachedLocal;
            _cachedLocal = FindAnyObjectByType<PlayerWallet>();
            return _cachedLocal;
        }
    }

    private void OnDestroy()
    {
        if (_cachedLocal == this) _cachedLocal = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            _unwashedAmount = Mathf.Max(0f, _unwashedAmount);
            _washedAmount = Mathf.Max(0f, _washedAmount);
            _virtuePoints = Mathf.Max(0, _virtuePoints);
            OnUnwashedChanged?.Invoke(_unwashedAmount);
            OnWashedChanged?.Invoke(_washedAmount);
            OnVirtuePointsChanged?.Invoke(_virtuePoints);
        }
    }
#endif
}
