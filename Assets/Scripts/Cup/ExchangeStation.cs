using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// Bin を受け取って中身の cup を tube 上部に出現させる「exchange」コンポーネント。
/// レティクル照準 + プレイヤーが Bin を持っている時のみハイライト。
/// クリックで Bin を破棄し、cupPrefab を tube 上部に生成、即座に Z 軸 120° 回転を加える。
/// </summary>
[DisallowMultipleComponent]
public class ExchangeStation : InteractableHighlight
{
    [Header("Exchange 設定")]
    [Tooltip("Bin を受け取った時に出現させる cup プレハブ (BallCup 付き)")]
    public GameObject cupPrefab;

    [Tooltip("cup を出現させる位置 Transform (通常: tube 子オブジェクトの上端)")]
    public Transform tubeSpawnPoint;

    [Tooltip("出現直後に cup に加える回転 (deg)。デフォルト Z 軸 120°")]
    public Vector3 spawnRotationEuler = new Vector3(0f, 0f, 120f);

    [Tooltip("回転をワールド空間で適用 (false ならローカル空間)")]
    public bool rotateInWorldSpace = false;

    [Tooltip("回転にかける秒数。0 で瞬時、1.0 で 1 秒かけてゆっくり回転")]
    public float rotationDuration = 1.0f;

    [Tooltip("回転の補間カーブ (任意)。空ならリニア。EaseInOut にすると始終なめらか")]
    public AnimationCurve rotationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("受け取った Bin を子に置く親 (null なら破棄前のシーンルート)")]
    public Transform spawnedCupParent;

    [Tooltip("ボール再配置の散らばり半径 (m)")]
    public float ballScatterRadius = 0.08f;

    [Header("価値モニター (ex_disp)")]
    [Tooltip("価値を表示する TMP_Text (TextMeshPro 3D / UI どちらでも可)。null なら子から自動検索")]
    public TMP_Text valueDisplayText;

    [Tooltip("値表示フォーマット ({0} に累計値が入る)")]
    public string valueDisplayFormat = "¥{0:N0}";

    [Tooltip("起動時の累計値 (テスト用シード)")]
    public float currentTotalValue = 0f;

    [Tooltip("吸い込み時にデフォルトのボール価値 (UFOItem が無いボール用)")]
    public float defaultBallValue = 100f;

    [Tooltip("吸い込んだ価値を UnwashedMoneyManager にも加算する (ゲーム通貨と連動させたい場合)")]
    public bool addToUnwashedMoney = false;

    [Header("イベント")]
    [Tooltip("交換成立時に発火 (生成された cup の BallCup を引数に)")]
    public ExchangeEvent onExchange;

    [Tooltip("ボール吸い込みで累計値が変化した時に発火 (引数: 新累計値)")]
    public UnityEvent<float> onTotalValueChanged;

    [System.Serializable] public class ExchangeEvent : UnityEvent<BallCup> { }

    /// <summary>現在の累計価値 (吸い込みごとに加算)</summary>
    public float CurrentTotalValue => currentTotalValue;

    private void Start()
    {
        // valueDisplayText が未指定なら子から自動検索 (TMP_Text 派生: TextMeshPro / TextMeshProUGUI 両対応)
        if (valueDisplayText == null)
        {
            valueDisplayText = GetComponentInChildren<TMP_Text>(true);
        }
        UpdateValueDisplay();
    }

    public override bool IsInteractable(CupPickupController pickup)
    {
        if (pickup == null) return false;
        return pickup.IsHoldingBin && cupPrefab != null && tubeSpawnPoint != null;
    }

    /// <summary>ボール 1 個分の価値を累計に加算してモニター表示を更新する。</summary>
    public void AddValue(float amount)
    {
        if (amount == 0f) return;
        currentTotalValue += amount;
        UpdateValueDisplay();
        if (addToUnwashedMoney && UnwashedMoneyManager.Instance != null && amount > 0f)
        {
            UnwashedMoneyManager.Instance.Add(amount);
        }
        onTotalValueChanged?.Invoke(currentTotalValue);
    }

    /// <summary>累計値を 0 にリセット (デバッグ・ステージ再開用)</summary>
    public void ResetValue()
    {
        currentTotalValue = 0f;
        UpdateValueDisplay();
        onTotalValueChanged?.Invoke(currentTotalValue);
    }

    private void UpdateValueDisplay()
    {
        if (valueDisplayText == null) return;
        valueDisplayText.text = string.Format(valueDisplayFormat, Mathf.FloorToInt(currentTotalValue));
    }

    /// <summary>Bin を受け取り、cup を出現させて中身を移す。生成された BallCup を返す。</summary>
    public BallCup AcceptBin(CupBin bin)
    {
        if (bin == null || cupPrefab == null || tubeSpawnPoint == null) return null;

        // tube 上部に cup を生成
        Vector3 pos = tubeSpawnPoint.position;
        Quaternion rot = tubeSpawnPoint.rotation;
        Transform parent = spawnedCupParent != null ? spawnedCupParent : null;
        GameObject cupGo = parent != null
            ? Instantiate(cupPrefab, pos, rot, parent)
            : Instantiate(cupPrefab, pos, rot);

        // 中身のボールを cup に流し込む
        var newCup = cupGo.GetComponent<BallCup>();
        if (newCup != null)
        {
            newCup.ReceiveContents(bin.heldBalls, ballScatterRadius);
        }
        else
        {
            Debug.LogWarning("[ExchangeStation] 生成した cupPrefab に BallCup が付いていません。ボールが移されません。", cupGo);
        }

        // 出現直後に Z 軸 120° (任意の) 回転をゆっくり加える。
        // 既存シリアライズ済み Inspector で値が壊れているケース (旧バージョンに無かったフィールド) のセーフティ:
        // - rotationDuration が 0 以下なら 1.0 秒に強制
        // - rotationCurve が null/空ならデフォルト EaseInOut を使う
        float effectiveDuration = rotationDuration > 0f ? rotationDuration : 1.0f;
        AnimationCurve effectiveCurve = (rotationCurve != null && rotationCurve.length >= 2)
            ? rotationCurve
            : AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        if (newCup != null)
        {
            newCup.StartSmoothRotation(spawnRotationEuler, effectiveDuration, rotateInWorldSpace, effectiveCurve);
        }
        else
        {
            // BallCup が無くても回転だけはする
            if (rotateInWorldSpace) cupGo.transform.Rotate(spawnRotationEuler, Space.World);
            else cupGo.transform.Rotate(spawnRotationEuler, Space.Self);
        }

        // bin の参照をクリアして破棄 (heldBalls はもう新 cup 側にあるので OnDestroy で消されないよう外す)
        bin.heldBalls = null;
        Destroy(bin.gameObject);

        onExchange?.Invoke(newCup);
        return newCup;
    }
}
