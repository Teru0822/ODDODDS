using UnityEngine;

/// <summary>
/// 3つのレールのいずれかに、プレイヤーの前方から迫ってくる障害物を定期的に生成するスポナースクリプト。
/// 地上オブジェクトだけでなく、ジャンプで避ける（またはジャンプすると当たってしまう）空中オブジェクトも生成可能です。
/// </summary>
public class ObstacleSpawner : MonoBehaviour
{
    [Header("スポナーの参照設定")]
    [Tooltip("3つのレールオブジェクト")]
    [SerializeField] private Transform[] rails;

    [Tooltip("地上の障害物のプレハブ")]
    [SerializeField] private GameObject obstaclePrefab;

    [Tooltip("プレイヤーのTransform（プレイヤーの前方に出現させるために使用）")]
    [SerializeField] private Transform playerTransform;

    [Header("生成設定")]
    [Tooltip("何秒ごとに障害物を生成するか")]
    [SerializeField] private float spawnInterval = 2.0f;

    [Tooltip("プレイヤーの何m前方に障害物を出現させるか")]
    [SerializeField] private float spawnDistanceAhead = 25f;

    [Header("空中（飛行型）障害物の設定")]
    [Tooltip("空中に出現する確率 (0: 常に地上, 1: 常に空中)")]
    [Range(0f, 1f)]
    [SerializeField] private float aerialChance = 0.3f;

    [Tooltip("空中に出現させる際の、レールからの高さ（Y座標のオフセット値）")]
    [SerializeField] private float aerialHeight = 2.5f;

    [Tooltip("空中の障害物用プレハブ（空欄の場合は地上の障害物と同じプレハブを使用します）")]
    [SerializeField] private GameObject aerialObstaclePrefab;

    [Header("回転の設定")]
    [Tooltip("オンにすると、レールの回転（傾き）に合わせて障害物を生成します。オフにするとプレハブのデフォルトの向きで生成します。")]
    [SerializeField] private bool alignWithRailRotation = false;

    private float timer = 0f;

    void Start()
    {
        // レールのセットアップ確認
        if (rails == null || rails.Length == 0)
        {
            Debug.LogError("[ObstacleSpawner] レール（Rails）が登録されていません。インスペクターで3つのレールをアタッチしてください。");
        }

        // 障害物プレハブのセットアップ確認
        if (obstaclePrefab == null)
        {
            Debug.LogError("[ObstacleSpawner] 地上の障害物プレハブ（Obstacle Prefab）が登録されていません。");
        }

        // プレイヤーのセットアップ確認
        if (playerTransform == null)
        {
            var controller = FindFirstObjectByType<LaneMovementController>();
            if (controller != null)
            {
                playerTransform = controller.transform;
            }
            else
            {
                Debug.LogError("[ObstacleSpawner] プレイヤー（LaneMovementController）が見つかりません。");
            }
        }
    }

    void Update()
    {
        if (rails == null || rails.Length == 0 || obstaclePrefab == null || playerTransform == null) return;

        // タイマーで定期生成
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            SpawnObstacle();
        }
    }

    /// <summary>
    /// ランダムなレール上に障害物を生成する（確率で空中にも生成）
    /// </summary>
    private void SpawnObstacle()
    {
        int randomLane = Random.Range(0, rails.Length);
        Transform targetRail = rails[randomLane];

        if (targetRail != null)
        {
            // 出現するオブジェクトと高さの初期化（デフォルトは地上）
            GameObject prefabToSpawn = obstaclePrefab;
            float heightOffset = 0.5f;

            // 確率で空中（ジャンプ回避・またはしゃがみ回避用）にする
            if (Random.value < aerialChance)
            {
                heightOffset = aerialHeight;
                if (aerialObstaclePrefab != null)
                {
                    prefabToSpawn = aerialObstaclePrefab;
                }
            }

            // プレイヤーの現在のZ位置＋前方への出現距離で生成座標を算出
            float spawnZ = playerTransform.position.z + spawnDistanceAhead;
            
            // Xは選択されたレールのX、Yはレールからの高さ
            Vector3 spawnPos = new Vector3(targetRail.position.x, targetRail.position.y + heightOffset, spawnZ);

            // 生成時の回転値の決定（レールに合わせるか、プレハブのデフォルト値を使用するか）
            Quaternion spawnRotation = alignWithRailRotation ? targetRail.rotation : prefabToSpawn.transform.rotation;

            // プレハブからインスタンスを生成
            Instantiate(prefabToSpawn, spawnPos, spawnRotation);
            
            string obstacleType = (heightOffset > 1.0f) ? "空中" : "地上";
            Debug.Log($"[ObstacleSpawner] レーン {randomLane} に【{obstacleType}】障害物をスポーンさせました。座標: {spawnPos}");
        }
    }
}
