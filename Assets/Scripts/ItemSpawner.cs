using System.Collections;
using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    [Header("生成基準")]
    [Tooltip("UFOキャッチャーのアーム（ArmRootなど、中心の基準にしたいもの）をセットしてください")]
    public Transform armRoot;
    [Tooltip("基準オブジェクトからどれくらい高い位置(Y座標)から落とすか")]
    public float spawnYOffset = 2.0f;
    [Tooltip("生成位置の散らばり具合（X=左右、Y=奥手前）")]
    public Vector2 spawnArea = new Vector2(3.0f, 3.0f);
    
    [Header("生成設定")]
    [Tooltip("合計生成数")]
    public int totalItems = 500;
    [Tooltip("何秒かけてパラパラと生成するか")]
    public float spawnDuration = 10f;
    [Tooltip("生成されたアイテムを入れるフォルダ（空のオブジェクト）")]
    public Transform parentFolder;

    [Header("アイテムと排出率（合計が100にならなくても比率で計算されます）")]
    public GameObject copperCoinPrefab;
    public float copperRate = 60f;

    public GameObject silverCoinPrefab;
    public float silverRate = 25f;

    public GameObject goldCoinPrefab;
    public float goldRate = 10f;

    public GameObject hourglassPrefab;
    public float hourglassRate = 5f;

    [Header("スポーンパターン設定")]
    [Tooltip("降らせるごとに順番に切り替わるパターンのリスト。空の場合はデフォルトの4パターンが自動生成されます。")]
    public System.Collections.Generic.List<SpawnPattern> patterns = new System.Collections.Generic.List<SpawnPattern>();

    public static bool IsSpawning { get; private set; } = false;

    private static int _currentPatternIndex = 0;
    private SpawnPattern _activePattern;

    void Awake()
    {
        // インスペクターで設定されていない場合、デフォルトの4パターンを生成
        if (patterns == null || patterns.Count == 0)
        {
            patterns = new System.Collections.Generic.List<SpawnPattern>()
            {
                new SpawnPattern("Pattern 1 (右上 - 標準)", false, true, false, false, 60f, 25f, 10f, 5f),
                new SpawnPattern("Pattern 2 (左下 - コイン多め)", false, false, true, false, 40f, 35f, 20f, 5f),
                new SpawnPattern("Pattern 3 (右下 - 高レア多め)", false, false, false, true, 20f, 40f, 30f, 10f),
                new SpawnPattern("Pattern 4 (左下 - 時計特化)", false, false, true, false, 30f, 30f, 15f, 25f)
            };
        }
    }

    void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        IsSpawning = true;
        int spawnedCount = 0;
        float startTime = Time.time;

        // パターンの決定とローテーション
        if (patterns != null && patterns.Count > 0)
        {
            int index = _currentPatternIndex % patterns.Count;
            _activePattern = patterns[index];
            Debug.Log($"[ItemSpawner] パターン {index + 1}/{patterns.Count} を適用: {_activePattern.name} " +
                      $"(右上:{_activePattern.topRight}, 左下:{_activePattern.bottomLeft}, 右下:{_activePattern.bottomRight}, 左上:{_activePattern.topLeft})");
            _currentPatternIndex++;
        }
        else
        {
            _activePattern = null;
            Debug.Log("[ItemSpawner] スポーンパターンが空のため、既存の設定で均等に生成します。");
        }

        float copper = _activePattern != null ? _activePattern.copperRate : copperRate;
        float silver = _activePattern != null ? _activePattern.silverRate : silverRate;
        float gold = _activePattern != null ? _activePattern.goldRate : goldRate;
        float hourglass = _activePattern != null ? _activePattern.hourglassRate : hourglassRate;
        float totalRate = copper + silver + gold + hourglass;

        // 排出率がすべて0の場合はエラーを防ぐ
        if (totalRate <= 0f)
        {
            Debug.LogError("アイテムの排出率がすべて0になっています！");
            IsSpawning = false;
            yield break;
        }

        while (spawnedCount < totalItems)
        {
            // 経過時間から「今何個生成されているべきか」を計算（10秒で500個なら1秒に50個ペース）
            float elapsedTime = Time.time - startTime;
            float progress = Mathf.Clamp01(elapsedTime / spawnDuration);
            int targetCount = Mathf.FloorToInt(progress * totalItems);

            // 目標数に達するまでこのフレームで生成を繰り返す
            while (spawnedCount < targetCount)
            {
                SpawnSingleItem(totalRate, copper, silver, gold);
                spawnedCount++;
            }

            yield return null; // 次のフレームまで待機
        }

        // スポーン完了！全コインに「今から凍結チェックを始めていいよ」と通知する
        // 3秒の余裕を加えて、最後のコインが落ち切るのを待つ
        IsSpawning = false;
        CoinOptimizer.freezeStartTime = Time.time + 3.0f;
        Debug.Log($"[ItemSpawner] スポーン完了。{CoinOptimizer.freezeStartTime:F1}秒後から凍結チェック開始");
    }

    private void SpawnSingleItem(float totalRate, float copper, float silver, float gold)
    {
        // 確率計算
        float rand = Random.Range(0f, totalRate);
        GameObject prefabToSpawn = null;

        if (rand < copper) 
        {
            prefabToSpawn = copperCoinPrefab;
        }
        else if (rand < copper + silver) 
        {
            prefabToSpawn = silverCoinPrefab;
        }
        else if (rand < copper + silver + gold) 
        {
            prefabToSpawn = goldCoinPrefab;
        }
        else 
        {
            prefabToSpawn = hourglassPrefab;
        }

        if (prefabToSpawn == null) return;

        // 座標計算
        Vector3 center = (armRoot != null) ? armRoot.position : transform.position;
        center.y += spawnYOffset;

        // エリアの偏りを計算
        float offsetX = 0f;
        float offsetZ = 0f;

        if (_activePattern != null)
        {
            // 有効なクアドラントをリストアップ
            var activeQuads = new System.Collections.Generic.List<int>();
            if (_activePattern.topRight) activeQuads.Add(0);    // 右上: X[0, spawnArea.x], Z[0, spawnArea.y]
            if (_activePattern.topLeft) activeQuads.Add(1);     // 左上: X[-spawnArea.x, 0], Z[0, spawnArea.y]
            if (_activePattern.bottomLeft) activeQuads.Add(2);  // 左下: X[-spawnArea.x, 0], Z[-spawnArea.y, 0]
            if (_activePattern.bottomRight) activeQuads.Add(3); // 右下: X[0, spawnArea.x], Z[-spawnArea.y, 0]

            if (activeQuads.Count > 0)
            {
                // 有効なクアドラントからランダムに1つ選択
                int chosenQuad = activeQuads[Random.Range(0, activeQuads.Count)];
                switch (chosenQuad)
                {
                    case 0: // 右上
                        offsetX = Random.Range(0f, spawnArea.x);
                        offsetZ = Random.Range(0f, spawnArea.y);
                        break;
                    case 1: // 左上
                        offsetX = Random.Range(-spawnArea.x, 0f);
                        offsetZ = Random.Range(0f, spawnArea.y);
                        break;
                    case 2: // 左下
                        offsetX = Random.Range(-spawnArea.x, 0f);
                        offsetZ = Random.Range(-spawnArea.y, 0f);
                        break;
                    case 3: // 右下
                        offsetX = Random.Range(0f, spawnArea.x);
                        offsetZ = Random.Range(-spawnArea.y, 0f);
                        break;
                }
            }
            else
            {
                // 有効なクアドラント指定がない場合は、前面から均等に落下
                offsetX = Random.Range(-spawnArea.x, spawnArea.x);
                offsetZ = Random.Range(-spawnArea.y, spawnArea.y);
            }
        }
        else
        {
            // 従来の均等落下
            offsetX = Random.Range(-spawnArea.x, spawnArea.x);
            offsetZ = Random.Range(-spawnArea.y, spawnArea.y);
        }

        // ばらつき（散らばり）を加える
        Vector3 randomPos = center + new Vector3(
            offsetX,
            Random.Range(-0.5f, 0.5f), // 上下のブレも少しだけ加える
            offsetZ
        );

        // 生成
        GameObject spawnedObj = Instantiate(prefabToSpawn, randomPos, Random.rotation, parentFolder);

        // プレハブのisKinematicがtrueになっていても必ず落下するよう強制解除する
        Rigidbody spawnedRb = spawnedObj.GetComponent<Rigidbody>();
        if (spawnedRb != null)
        {
            spawnedRb.isKinematic = false;
            spawnedRb.useGravity = true;
        }
    }
}

[System.Serializable]
public class SpawnPattern
{
    public string name;
    
    [Header("有効にするエリア")]
    public bool topLeft = true;
    public bool topRight = true;
    public bool bottomLeft = true;
    public bool bottomRight = true;

    [Header("確率（排出比率）")]
    public float copperRate = 60f;
    public float silverRate = 25f;
    public float goldRate = 10f;
    public float hourglassRate = 5f;

    public SpawnPattern(string name, bool topLeft, bool topRight, bool bottomLeft, bool bottomRight, float copper, float silver, float gold, float hourglass)
    {
        this.name = name;
        this.topLeft = topLeft;
        this.topRight = topRight;
        this.bottomLeft = bottomLeft;
        this.bottomRight = bottomRight;
        this.copperRate = copper;
        this.silverRate = silver;
        this.goldRate = gold;
        this.hourglassRate = hourglass;
    }
}