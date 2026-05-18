using System;
using UnityEngine;
using TMPro; // 追加

/// <summary>
/// ゲーム内のお金を管理するシングルトンクラス
/// </summary>
public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance { get; private set; }

    [Header("初期設定")]
    [SerializeField] private float _currentMoney = 10000;
    public float CurrentMoney => _currentMoney;

    [Header("画面表示(UI)")]
    [Tooltip("現在のお金を表示するUIテキスト")]
    public TextMeshProUGUI moneyText;


    [Header("ターン管理")]
    [SerializeField] private int _decreaseInterval = 1;
    [SerializeField] private float _exponentialRate = 1.5f;
    [SerializeField] private float _initialDecreaseAmount = 100f;

    [Header("ローグライク要素（徳ポイント）")]
    [SerializeField, Tooltip("徳ポイント算出の基準倍率。大きいほど獲得量が増える")] 
    private float _virtueMultiplier = 2.0f;

    /// <summary>
    /// 獲得した徳ポイントの累計
    /// </summary>
    public int VirtuePoints { get; private set; } = 0;

    private int _currentTurnCount = 0;
    private float _previousDecreaseAmount = 0;


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

        // 初期化
        _previousDecreaseAmount = _initialDecreaseAmount;

        // セーブデータのロード
        LoadRoguelikeData();
    }

    private void LoadRoguelikeData()
    {
        RoguelikeSaveData saveData = RoguelikeSaveManager.Load();
        VirtuePoints = saveData.virtuePoints;
        Debug.Log($"セーブデータをロードしました。現在の徳ポイント: {VirtuePoints}");
    }

    private void Start()
    {
        UpdateMoneyUI();
    }

    private void UpdateMoneyUI()
    {
        if (moneyText != null)
        {
            // \nで改行を入れて数字を見やすくする例
            moneyText.text = $"Money:\n¥{Mathf.FloorToInt(_currentMoney):N0}";
        }
    }

    /// <summary>
    /// お金を増加させる
    /// </summary>
    /// <param name="amount">基本額</param>
    /// <param name="multiplier">倍率（デフォルトは1.0）</param>
    public void AddMoney(float amount, float multiplier = 1.0f)
    {
        if (amount <= 0) return;

        float finalAmount = amount * multiplier;
        _currentMoney += finalAmount;
        UpdateMoneyUI();
        Debug.Log($"お金が増加しました: +{finalAmount} (現在: {_currentMoney})");
    }

    /// <summary>
    /// お金を減少させる
    /// </summary>
    /// <param name="amount">基本額</param>
    /// <param name="multiplier">倍率（デフォルトは1.0）</param>
    public void ReduceMoney(float amount, float multiplier = 1.0f)
    {
        if (amount <= 0) return;

        float finalAmount = amount * multiplier;
        _currentMoney -= finalAmount;
        UpdateMoneyUI();
        Debug.Log($"お金が減少しました: -{finalAmount} (現在: {_currentMoney})");

        CheckGameOver();
    }

    /// <summary>
    /// 規定のターン数に応じた減少処理を行うための関数
    /// </summary>
    public void AdvanceTurn()
    {
        _currentTurnCount++;

        // 指定ターンごとに減少処理を実行
        if (_currentTurnCount % _decreaseInterval == 0)
        {
            ApplyTurnDecrease();
        }
    }

    /// <summary>
    /// ターン経過によるお金の減少処理
    /// </summary>
    private void ApplyTurnDecrease()
    {
        ReduceMoney(_previousDecreaseAmount);

        // 次回の減少額を指数関数的に増加させて記憶
        _previousDecreaseAmount *= _exponentialRate;
    }

    /// <summary>
    /// 経過ターン数に応じて獲得できる徳ポイントを算出し、加算する
    /// 平方根（Sqrt）を利用することで、ターン数が多いほど増加のペースがなだらかになる。
    /// </summary>
    /// <returns>今回獲得した徳ポイント</returns>
    public int CalculateAndAddVirtuePoints()
    {
        // 算出式: 倍率 * √(経過ターン数)
        // 例(_virtueMultiplier=2の場合): 
        //  10ターンの時 -> 2 * 3.16 ≒ 6pt
        //  50ターンの時 -> 2 * 7.07 ≒ 14pt
        // 100ターンの時 -> 2 * 10.0 = 20pt
        int earnedVirtue = Mathf.FloorToInt(_virtueMultiplier * Mathf.Sqrt(_currentTurnCount));
        
        VirtuePoints += earnedVirtue;
        Debug.Log($"徳ポイントを獲得しました: {earnedVirtue} (累計: {VirtuePoints} / 経過ターン: {_currentTurnCount})");

        // ローグライク要素をセーブする
        SaveRoguelikeData();

        return earnedVirtue;
    }

    private void SaveRoguelikeData()
    {
        RoguelikeSaveData data = new RoguelikeSaveData
        {
            virtuePoints = this.VirtuePoints
        };
        RoguelikeSaveManager.Save(data);
    }

    /// <summary>
    /// 所持金が0以下になった場合にゲームオーバーを告知する
    /// </summary>
    private void CheckGameOver()
    {
        if (_currentMoney <= 0)
        {
            _currentMoney = 0;
            Debug.Log("所持金額が0になりました。ゲームオーバーです。");

            // ゲームオーバー時に徳ポイントを算出して付与する
            CalculateAndAddVirtuePoints();
            
            //TODO:ここに詳細なゲームオーバー時の処理を実装
        }
    }
}
