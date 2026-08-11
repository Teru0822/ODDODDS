using UnityEngine;
using UniRx;

/// <summary>
/// Player にアタッチする「財布」コンポーネント。
/// 未洗浄金 / 洗浄金 / 徳ポイントなど、プレイヤー個人ごとの所持データを一元管理する。
/// マルチプレイ化を見据えて、UnwashedMoneyManager / MoneyManager は本クラスへの委譲となる。
/// シングルプレイ時は唯一の Player にこれを 1 個アタッチし、PlayerWallet.Local で全コードから参照可能。
/// </summary>
[DisallowMultipleComponent]
public class PlayerWallet : MonoBehaviour, IsaveDataProvider
{
    [Header("初期値")]
    [SerializeField, Tooltip("未洗浄金の初期残高の設定")]
    private float _unwashedAmount = 0f;

    [SerializeField, Tooltip("洗浄金 (通常お金) の初期残高の設定")]
    private float _washedAmount = 10000f;

    [SerializeField, Tooltip("徳ポイント (ローグライク) の初期値の設定")]
    private int _virtuePoints = 0;

    [Header("初期コイン枚数 (デバッグ用)")]
    [SerializeField, Tooltip("金貨の初期枚数")]
    private int _initialGoldCoins = 0;
    [SerializeField, Tooltip("銀貨の初期枚数")]
    private int _initialSilverCoins = 0;
    [SerializeField, Tooltip("銅貨の初期枚数")]
    private int _initialBronzeCoins = 0;

    //お金関連の変数
    private ReactiveProperty<float> _washedMoneyAmount = new ReactiveProperty<float>();
    private ReactiveProperty<float> _unwashedMoneyAmount = new ReactiveProperty<float>();
    private ReactiveProperty<int> _virtuePointAmount = new ReactiveProperty<int>();
    private ReactiveProperty<int> _bronzeCoins = new ReactiveProperty<int>(0);
    private ReactiveProperty<int> _silverCoins = new ReactiveProperty<int>(0);
    private ReactiveProperty<int> _goldCoins = new ReactiveProperty<int>(0);
    private ReactiveProperty<int> _blackDiamonds = new ReactiveProperty<int>(0);

    //外部でSubscribeするための変数
    public IReadOnlyReactiveProperty<float> OnWashedMoneyAmountChange { get { return _washedMoneyAmount; } }
    public IReadOnlyReactiveProperty<float> OnUnwashedMoneyAmountChange { get { return _unwashedMoneyAmount; } }
    public IReadOnlyReactiveProperty<int> OnvirtuePointAmountChange { get { return _virtuePointAmount; } }
    public IReadOnlyReactiveProperty<int> OnBronzeCoinsChange => _bronzeCoins;
    public IReadOnlyReactiveProperty<int> OnSilverCoinsChange => _silverCoins;
    public IReadOnlyReactiveProperty<int> OnGoldCoinsChange => _goldCoins;
    public IReadOnlyReactiveProperty<int> OnBlackDiamondsChange => _blackDiamonds;


    //外部で数値参照できるための関数
    /// <summary>未洗浄金残高</summary>
    public float UnwashedAmount
    {
        get => _unwashedMoneyAmount.Value;
        set
        {
            //Debug.LogError($"MyFunction Called\n{System.Environment.StackTrace}");
            float clamped = Mathf.Max(0f, value);
            if (Mathf.Approximately(_unwashedMoneyAmount.Value, clamped)) return;
            _unwashedMoneyAmount.Value = clamped;
        }
    }

    /// <summary>未洗浄銅貨の枚数</summary>
    public int BronzeCoins
    {
        get => _bronzeCoins.Value;
        set
        {
            int clamped = Mathf.Max(0, value);
            if (_bronzeCoins.Value == clamped) return;
            _bronzeCoins.Value = clamped;
        }
    }

    /// <summary>未洗浄銀貨の枚数</summary>
    public int SilverCoins
    {
        get => _silverCoins.Value;
        set
        {
            int clamped = Mathf.Max(0, value);
            if (_silverCoins.Value == clamped) return;
            _silverCoins.Value = clamped;
        }
    }

    /// <summary>未洗浄金貨の枚数</summary>
    public int GoldCoins
    {
        get => _goldCoins.Value;
        set
        {
            int clamped = Mathf.Max(0, value);
            if (_goldCoins.Value == clamped) return;
            _goldCoins.Value = clamped;
        }
    }

    /// <summary>ブラックダイヤモンドの所持個数</summary>
    public int BlackDiamonds
    {
        get => _blackDiamonds.Value;
        set
        {
            int clamped = Mathf.Max(0, value);
            if (_blackDiamonds.Value == clamped) return;
            _blackDiamonds.Value = clamped;
        }
    }

    /// <summary>洗浄金 (通常お金) 残高</summary>
    public float WashedAmount
    {
        get => _washedMoneyAmount.Value;
        set
        {
            float clamped = Mathf.Max(0f, value);
            if (Mathf.Approximately(_washedMoneyAmount.Value, clamped)) return;
            _washedMoneyAmount.Value = clamped;
        }
    }

    /// <summary>徳ポイント (ローグライク)</summary>
    public int VirtuePoints
    {
        get => _virtuePointAmount.Value;
        set
        {
            int clamped = Mathf.Max(0, value);
            if (_virtuePointAmount.Value == clamped) return;
            _virtuePointAmount.Value = clamped;
        }
    }



    // ----- 未洗浄金 -----

    public void AddUnwashed(float amount)
    {
        if (amount <= 0f) return;
        UnwashedAmount = _unwashedMoneyAmount.Value + amount;
    }

    /// <summary>金・銀・銅貨およびブラックダイヤモンドをそれぞれ加算する</summary>
    public void AddCoins(int bronze, int silver, int gold, int blackDiamonds = 0)
    {
        if (bronze > 0) BronzeCoins += bronze;
        if (silver > 0) SilverCoins += silver;
        if (gold > 0) GoldCoins += gold;
        if (blackDiamonds > 0) BlackDiamonds += blackDiamonds;
    }

    public bool TrySpendUnwashed(float cost)
    {
        if (cost <= 0f) return true;
        if (_unwashedMoneyAmount.Value < cost) return false;
        UnwashedAmount = _unwashedMoneyAmount.Value - cost;
        return true;
    }

    public bool CanAffordUnwashed(float cost) => cost <= 0f || _unwashedMoneyAmount.Value >= cost;

    // ----- 洗浄金 -----

    public void AddWashed(float amount)
    {
        if (amount <= 0f) return;
        WashedAmount = _washedMoneyAmount.Value + amount;
    }

    public void ReduceWashed(float amount)
    {
        if (amount <= 0f) return;
        WashedAmount = _washedMoneyAmount.Value - amount;
    }

    public bool CanAffordWashed(float cost) => cost <= 0f || _washedMoneyAmount.Value >= cost;

    public void ReadSaveData(RoguelikeSaveData saveData)
    {
        _washedMoneyAmount.Value = saveData.money;
        _unwashedMoneyAmount.Value = saveData.unwashedMoney;
        _virtuePointAmount.Value = saveData.virtuePoints;
        _bronzeCoins.Value = saveData.bronzeCoin;
        _silverCoins.Value = saveData.silverCoin;
        _goldCoins.Value = saveData.goldCoin;
        _blackDiamonds.Value = saveData.blackDiamond;
        Debug.LogError("PlayerWalletの読み込み終了\n" + _washedMoneyAmount.Value + "\n" + _unwashedMoneyAmount.Value + "\n" + _bronzeCoins.Value + "\n" + _silverCoins.Value + "\n" + _goldCoins.Value + "\n" + _blackDiamonds.Value + "\n");
    } 

    public void WriteSaveData(RoguelikeSaveData saveData)
    {
        saveData.money         = (int)_washedMoneyAmount.Value;
        saveData.unwashedMoney = (int)_unwashedMoneyAmount.Value;
        saveData.virtuePoints  = (int)_virtuePointAmount.Value;
        saveData.bronzeCoin    = (int)_bronzeCoins.Value;
        saveData.silverCoin    = (int)_silverCoins.Value;
        saveData.goldCoin      = (int)_goldCoins.Value;
        saveData.blackDiamond  = (int)_blackDiamonds.Value;
    }

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

    private void Awake()
    {
        /*
        _washedMoneyAmount.Value = _washedAmount;
        _unwashedMoneyAmount.Value = _unwashedAmount;
        _virtuePointAmount.Value = _virtuePoints;
        _bronzeCoins.Value = _initialBronzeCoins;
        _silverCoins.Value = _initialSilverCoins;
        _goldCoins.Value = _initialGoldCoins;
        _blackDiamonds.Value = 0;
        */
    }
    private void OnDestroy()
    {
        if (_cachedLocal == this) _cachedLocal = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        //if (Application.isPlaying)
        //{
        //    _unwashedAmount = Mathf.Max(0f, _unwashedAmount);
        //    _washedAmount = Mathf.Max(0f, _washedAmount);
        //    _virtuePoints = Mathf.Max(0, _virtuePoints);
        //}
    }
#endif
}
