using UnityEngine;

/// <summary>
/// exchange の吸い込み口に置く Trigger Collider 用コンポーネント。
/// Trigger に入ったボールを検出し、価値を判定して ExchangeStation の累計値に加算 → ボール削除。
/// 通常は exchange の子オブジェクトとして配置 (BoxCollider/SphereCollider, Is Trigger=ON)。
/// </summary>
[RequireComponent(typeof(Collider))]
public class ExchangeIntakeTrigger : MonoBehaviour
{
    [Header("接続")]
    [Tooltip("価値を加算する ExchangeStation。null なら親階層から自動取得")]
    public ExchangeStation owner;

    [Header("ボール判定")]
    [Tooltip("ボール検出: tag が空なら PinballBallController または UFOItem コンポーネントで判定")]
    public string ballTag = "";

    [Tooltip("ボールの UFOItem.baseValue を優先採用する (false なら常に defaultBallValueOverride を使う)")]
    public bool useUFOItemValue = true;

    [Tooltip("UFOItem が無い場合の基本価値。負なら ExchangeStation.defaultBallValue を使用")]
    public float defaultBallValueOverride = -1f;

    [Tooltip("価値倍率 (例: 1.5 で全ボールの価値が 1.5 倍になる)")]
    public float valueMultiplier = 1f;

    [Header("挙動")]
    [Tooltip("吸い込み後にボールを破棄する")]
    public bool destroyBallOnIntake = true;

    [Tooltip("破棄前に再生する破棄エフェクト prefab (任意)")]
    public GameObject destroyEffectPrefab;

    [Header("デバッグ")]
    [Tooltip("吸い込みイベントを Console に出力")]
    public bool logEvents = false;

    private void Awake()
    {
        // Collider が Trigger になっていないと OnTriggerEnter は発火しない
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[ExchangeIntakeTrigger] '{name}' の Collider は Is Trigger=OFF です。ON にしてください。", this);
        }
        if (owner == null) owner = GetComponentInParent<ExchangeStation>();
        if (owner == null)
        {
            Debug.LogError($"[ExchangeIntakeTrigger] '{name}' に対応する ExchangeStation が親階層に見つかりません。Owner を Inspector で指定してください。", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsBall(other)) return;
        var ballRoot = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;

        float value = EvaluateValue(ballRoot) * valueMultiplier;
        if (owner != null) owner.AddValue(value);

        if (logEvents)
        {
            Debug.Log($"[ExchangeIntakeTrigger] '{name}' 吸い込み: {ballRoot.name} 価値={value} 累計={(owner != null ? owner.CurrentTotalValue : 0)}", this);
        }

        if (destroyEffectPrefab != null)
        {
            Instantiate(destroyEffectPrefab, ballRoot.transform.position, Quaternion.identity);
        }

        if (destroyBallOnIntake)
        {
            Destroy(ballRoot);
        }
    }

    private bool IsBall(Collider other)
    {
        if (!string.IsNullOrEmpty(ballTag))
        {
            return other.CompareTag(ballTag);
        }
        // PinballBallController または UFOItem のどちらかが付いていればボールとみなす
        if (other.GetComponentInParent<PinballBallController>() != null) return true;
        if (other.GetComponentInParent<UFOItem>() != null) return true;
        return false;
    }

    private float EvaluateValue(GameObject ballRoot)
    {
        // 1. UFOItem の baseValue を優先 (個別価値)
        if (useUFOItemValue)
        {
            var item = ballRoot.GetComponentInChildren<UFOItem>();
            if (item != null) return item.baseValue;
        }
        // 2. Override が設定されていればそれ
        if (defaultBallValueOverride >= 0f) return defaultBallValueOverride;
        // 3. それ以外は owner の defaultBallValue
        if (owner != null) return owner.defaultBallValue;
        // 4. フォールバック
        return 100f;
    }

    private void OnDrawGizmosSelected()
    {
        // Trigger 範囲を Scene ビューに可視化 (黄)
        var col = GetComponent<Collider>();
        if (col == null) return;
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.4f);
        Bounds b = col.bounds;
        Gizmos.DrawWireCube(b.center, b.size);
    }
}
