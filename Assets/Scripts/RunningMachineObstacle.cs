using UnityEngine;

/// <summary>
/// レール上を迫ってくる障害物の制御スクリプト。
/// プレイヤーにぶつかるとプレイヤーを押し戻し、自身は消滅します。
/// </summary>
public class RunningMachineObstacle : MonoBehaviour
{
    [Header("障害物の移動設定")]
    [Tooltip("手前に向かって迫ってくる速度")]
    [SerializeField] private float speed = 8f;

    [Tooltip("移動する方向ベクトル。Vector3.back (0, 0, -1) は手前（Z軸マイナス方向）に進みます")]
    [SerializeField] private Vector3 moveDirection = Vector3.back;

    [Tooltip("ローカル座標系を使用するか（オンにすると、オブジェクトの向きに合わせて進みます）")]
    [SerializeField] private bool useLocalSpace = true;

    [Header("プレイヤーへのペナルティ")]
    [Tooltip("衝突した際にプレイヤーを後ろに押し戻す距離（Z軸のマイナス移動量）")]
    [SerializeField] private float pushBackAmount = 2f;

    [Header("自動クリーンアップ")]
    [Tooltip("画面外に出てから自動消滅するまでの生存時間（秒）")]
    [SerializeField] private float lifetime = 5f;

    void Start()
    {
        // 画面外に流れたオブジェクトが溜まらないように、一定時間後に自動で削除
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        // 指定された方向とスペースに従って移動
        if (useLocalSpace)
        {
            transform.Translate(moveDirection.normalized * speed * Time.deltaTime, Space.Self);
        }
        else
        {
            transform.Translate(moveDirection.normalized * speed * Time.deltaTime, Space.World);
        }
    }

    /// <summary>
    /// 衝突判定（コライダーの IsTrigger がオンである必要があります）
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // ぶつかった相手がプレイヤー（LaneMovementController）かチェック
        LaneMovementController player = other.GetComponent<LaneMovementController>();
        
        // 子オブジェクト等にアタッチされているコライダーへの衝突も考慮
        if (player == null)
        {
            player = other.GetComponentInParent<LaneMovementController>();
        }

        // プレイヤーに衝突した場合
        if (player != null)
        {
            // プレイヤーを後ろに押し戻す
            player.PushBack(pushBackAmount);
            
            // 障害物自身は消滅
            Destroy(gameObject);
        }
    }
}
