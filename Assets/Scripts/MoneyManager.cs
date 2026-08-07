using TMPro;
using UniRx;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.ComponentModel;

/// <summary>
/// ゲーム内のお金（洗浄金）操作 API を提供するシングルトン。
/// 実データ (CurrentMoney / VirtuePoints) は Player の PlayerWallet に保存され、本クラスは委譲する。
/// ターン管理 (悪魔の取り立て) と徳ポイント算出ロジックは引き続き本クラスで処理する。
/// </summary>
public class MoneyManager : MonoBehaviour, IsaveDataProvider
{
    public static MoneyManager Instance { get; private set; }

    [Header("接続")]
    [Tooltip("対象 PlayerWallet。null なら local player から自動取得 (PlayerWallet.Local)")]
    [SerializeField] private PlayerWallet wallet;

    [Header("画面表示(UI)")]
    [Tooltip("現在のお金を表示するUIテキスト")]
    public TextMeshProUGUI moneyText;

    [Tooltip("表示フォーマット ({0} に金額が入る)")]
    public string moneyFormat = "Money:\n¥{0:N0}";

    [Header("ターン管理")]//TODO: 今後、ターン管理を担うスクリプトを作成してそちらに譲渡
    [SerializeField] private float _exponentialRate = 2.0f;
    [SerializeField] private float _initialDecreaseAmount = 100f;
    [SerializeField] private int _debtCollectionFrequency = 1;
    private int _debtClearTimes = 0;
    public int DebtClearTimes => _debtClearTimes;

    ReactiveProperty<int> _currentTurnCount = new ReactiveProperty<int>(1);
    ReactiveProperty<int> _nextDebtCollectionTurnCount = new ReactiveProperty<int>(4);
    public int CurrentTurnCount { get { return _currentTurnCount.Value; } }
    public int NextDebtCollectionTurnCount { get { return _nextDebtCollectionTurnCount.Value; } }
    public IObservable<int> OnCurrentTurnChange { get { return _currentTurnCount; } }//最初に変数の作成、Loadで2回更新が発生するため、必要に応じてSkip(2)を追加してください
    public IObservable<int> OnNextDebtCollectionTurnChange { get { return _nextDebtCollectionTurnCount; } }

    [Header("ローグライク要素（徳ポイント）")]
    [SerializeField, Tooltip("徳ポイント算出の基準倍率。大きいほど獲得量が増える")]
    private float _virtueMultiplier = 2.0f;

    public PlayerWallet Wallet
    {
        get
        {
            if (wallet == null) wallet = PlayerWallet.Local;
            return wallet;
        }
    }

    /// <summary>現在の洗浄金残高 (PlayerWallet 経由)</summary>
    public float CurrentMoney => Wallet != null ? Wallet.WashedAmount : 0f;

    /// <summary>徳ポイント累計 (PlayerWallet 経由)</summary>
    public int VirtuePoints
    {
        get => Wallet != null ? Wallet.VirtuePoints : 0;
        private set { if (Wallet != null) Wallet.VirtuePoints = value; }
    }

    //ノルマの金額
    ReactiveProperty<float> _quotaAmount = new ReactiveProperty<float>(0);
    public float PreviousDecreaseAmount => _quotaAmount.Value;
    public IObservable<float> OnQuotaAmount { get { return _quotaAmount; } }

    //借金の残りの金額
    ReactiveProperty<float> _leftDebtAmount = new ReactiveProperty<float>(10000000000);
    public IObservable<float> OnLeftDebtAmount { get { return _leftDebtAmount; } }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 初期化
        _quotaAmount.Value = _initialDecreaseAmount;

        // セーブデータのロード (PlayerWallet 経由で値を入れる必要があるので Start で実施)
    }

    private void Start()
    {
        //RoguelikeSaveManager.Load();
        UpdateMoneyUI();

        //ターン処理を開始
        _currentTurnCount
            .Pairwise()
            .Subscribe(x =>
            {
                Debug.LogError("呼ばれたよー");
                //経過ターンに応じて、次回の取り立てターンを計算
                var diff = x.Current - x.Previous;
                _nextDebtCollectionTurnCount.Value-= diff;

                //0未満になれば、リセット
                if(_nextDebtCollectionTurnCount.Value < 0)
                    _nextDebtCollectionTurnCount.Value = _debtCollectionFrequency - 1;
                    
            }).AddTo(gameObject);

        _nextDebtCollectionTurnCount.Value = _debtCollectionFrequency;
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Keyboard.current.iKey.wasPressedThisFrame)
        {
            _currentTurnCount.Value++;
            Debug.Log("試しにターンを経過させます。");
        }
#endif
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }


    private void HandleWashedChanged(float newAmount)
    {
        UpdateMoneyUI();
    }


    private void UpdateMoneyUI()
    {
        if (moneyText != null && Wallet != null)
        {
            moneyText.text = string.Format(moneyFormat, Mathf.FloorToInt(Wallet.WashedAmount));
        }
    }

    /// <summary>
    /// お金を増加させる
    /// </summary>
    /// <param name="amount">基本額</param>
    /// <param name="multiplier">倍率（デフォルトは1.0）</param>
    public void AddMoney(float amount, float multiplier = 1.0f)
    {
        if (amount <= 0 || Wallet == null) return;

        bool hasBellBuff = EffectManager.Instance.IsHasEffect(0);
        bool hasGargantuaDebuff = EffectManager.Instance.IsHasEffect(2);

        float bellBuff = hasBellBuff ? 2f : 1f;
        float gargantuaDebuff = hasGargantuaDebuff ? 0.5f : 1f;

        if (hasBellBuff)
            Debug.Log("ベルの加護で獲得料金が2倍");

        if (hasGargantuaDebuff)
            Debug.Log("ガルガンチュアのデバフで獲得料金が半分");

        float finalAmount = amount * multiplier * bellBuff * gargantuaDebuff;

        Wallet.AddWashed(finalAmount);
        Debug.Log($"お金が増加しました: +{finalAmount} (現在: {Wallet.WashedAmount})");
    }

    /// <summary>
    /// お金を減少させる
    /// </summary>
    /// <param name="amount">基本額</param>
    /// <param name="multiplier">倍率（デフォルトは1.0）</param>
    public void ReduceMoney(float amount, float multiplier = 1.0f)
    {
        if (amount <= 0 || Wallet == null) return;
        float finalAmount = amount * multiplier;
        Wallet.ReduceWashed(finalAmount);
        Debug.Log($"お金が減少しました: -{finalAmount} (現在: {Wallet.WashedAmount})");
    }

    /// <summary>ターンを1進める。</summary>
    public void AdvanceTurn(int num = 1)
    {
        _currentTurnCount.Value+= num;
    }

    /// <summary>
    /// 「悪魔の取り立て」によるお金の減少処理
    /// </summary>
    public void ApplyTurnDecrease()
    {
        //借金の返済処理
        ReduceMoney(GetQuotaThisTime());
        _leftDebtAmount.Value -= _quotaAmount.Value;

        // 次回の減少額を指数関数的に増加させて記憶
        _quotaAmount.Value *= _exponentialRate;
    }

    public int GetQuotaThisTime()
    {
        float GargantuaDebuff = EffectManager.Instance.IsHasEffect(1) ? 1.5f : 1.0f;
        float candyBuff = EffectManager.Instance.IsHasEffect(6) ? 0.9500f : 1.0f;

        return (int)(_quotaAmount.Value * GargantuaDebuff * candyBuff);
    }
    

    /// <summary>
    /// 経過ターン数に応じて獲得できる徳ポイントを算出し、加算する
    /// </summary>
    public int CalculateAndAddVirtuePoints()
    {
        if (Wallet == null) return 0;
        int earnedVirtue = Mathf.FloorToInt(_virtueMultiplier * Mathf.Sqrt(_currentTurnCount.Value));
        Wallet.VirtuePoints = Wallet.VirtuePoints + earnedVirtue;
        Debug.Log($"徳ポイントを獲得しました: {earnedVirtue} (累計: {Wallet.VirtuePoints} / 経過ターン: {_currentTurnCount})");

        return earnedVirtue;
    }

    public void WriteSaveData(RoguelikeSaveData saveData)
    {
        //徳ポイントとターン関連の情報をセーブ
        saveData.virtuePoints = Wallet.VirtuePoints;
        saveData.nowTurn = _currentTurnCount.Value;
        saveData.nextDebtTurn = _nextDebtCollectionTurnCount.Value;
        saveData.nextDebtPrice = (int)_quotaAmount.Value;
        saveData.leftDebtAmount = (int)_leftDebtAmount.Value;
        saveData.debtClearTimes = _debtClearTimes;
    }
    public void ReadSaveData(RoguelikeSaveData saveData)
    {
        if (Wallet == null) return;
        Wallet.VirtuePoints = saveData.virtuePoints;

        //現在のターンを初期化
        if(saveData.nowTurn == 0)
            _currentTurnCount.Value = 1;
        else
            _currentTurnCount.Value = saveData.nowTurn; 
        
        //次の取り立てターンを初期化(nextDebtTurnが0の時も十分にあるので現在のターンを参照する)
        if(saveData.nowTurn == 0 && saveData.nextDebtTurn == 0)
            _nextDebtCollectionTurnCount.Value = _debtCollectionFrequency - 1;
        else
            _nextDebtCollectionTurnCount.Value = saveData.nextDebtTurn;

        //次の取り立てる金額を初期化
        if(saveData.nextDebtPrice == 0)
            _quotaAmount.Value = _initialDecreaseAmount;
        else
            _quotaAmount.Value = saveData.nextDebtPrice;

        //残りの借金の金額を初期化
        if(saveData.leftDebtAmount == 0)
            _leftDebtAmount.Value = 10000000000;
        else
            _leftDebtAmount.Value = saveData.leftDebtAmount;

        //借金の取り立てを耐えた回数を初期化
        _debtClearTimes = saveData.debtClearTimes;

        Debug.LogError(
        $@"===== MoneyManager Load Result=====

        Turn : {_currentTurnCount.Value}
        nextTurn : {_nextDebtCollectionTurnCount.Value}
        nextDebtPrice : {_quotaAmount.Value}
        leftDebtAmount : {_leftDebtAmount.Value}
        debtClearTimes : {_debtClearTimes}

        ============================");
        Debug.Log($"セーブデータをロードしました。現在の徳ポイント: {Wallet.VirtuePoints}");
    }

    /// <summary>
    /// 所持金が0以下になった場合にゲームオーバーを告知する
    /// </summary>
    /// <returns>true: ゲームオーバー, false: クリア</returns>
    public bool CheckGameOver()
    {
        if (Wallet == null) return false;

        if (Wallet.WashedAmount <= 0f)
        {
            Wallet.WashedAmount = 0f;
            Debug.Log("所持金額が0になりました。ゲームオーバーです。");

            // ゲームオーバー時に徳ポイントを算出して付与する
            CalculateAndAddVirtuePoints();

            //TODO:ここに詳細なゲームオーバー時の処理を実装
            return true;
        }
        else //耐えた時
        {
            _debtClearTimes++;
            if(EffectManager.Instance.IsHasEffect(1))//ガルガンチュアのデバフを持っている場合は削除
                EffectManager.Instance.RemoveEffect(1);
            return false;
        }
    }
}
