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

    [Header("アイテムと排出率（デフォルト値）")]
    public GameObject copperCoinPrefab;
    public float copperRate = 60f;

    public GameObject silverCoinPrefab;
    public float silverRate = 25f;

    public GameObject goldCoinPrefab;
    public float goldRate = 10f;

    public GameObject hourglassPrefab;
    public float hourglassRate = 5f;

    [Header("確率パターン設定 (nパターン)")]
    [Tooltip("コインと時計の降る確率のベースパターンリスト。空の場合はデフォルトの5パターンが自動生成されます。")]
    public System.Collections.Generic.List<SpawnRatePattern> ratePatterns = new System.Collections.Generic.List<SpawnRatePattern>();

    public static bool IsSpawning { get; private set; } = false;

    // 現在のウェーブの設定
    private int _activeAreaMask = 15; // 4ビットフラグ (1=左上, 2=右上, 4=右下, 8=左下)
    private SpawnRatePattern _topLeftRate;
    private SpawnRatePattern _topRightRate;
    private SpawnRatePattern _bottomLeftRate;
    private SpawnRatePattern _bottomRightRate;

    void Awake()
    {
        // インスペクターで設定されていない場合、デフォルトの5パターンを生成
        if (ratePatterns == null || ratePatterns.Count == 0)
        {
            ratePatterns = new System.Collections.Generic.List<SpawnRatePattern>()
            {
                new SpawnRatePattern("Pattern 1 (標準)", 60f, 25f, 10f, 5f),
                new SpawnRatePattern("Pattern 2 (コイン多め)", 40f, 35f, 20f, 5f),
                new SpawnRatePattern("Pattern 3 (高レア多め)", 20f, 40f, 30f, 10f),
                new SpawnRatePattern("Pattern 4 (時計特化)", 30f, 30f, 15f, 25f),
                new SpawnRatePattern("Pattern 5 (フィーバー)", 10f, 20f, 40f, 30f)
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

        // ウェーブ設定（15通りの組み合わせとnC4確率割り当て）を準備
        PrepareWaveSettings();

        while (spawnedCount < totalItems)
        {
            // 経過時間から「今何個生成されているべきか」を計算（10秒で500個なら1秒に50個ペース）
            float elapsedTime = Time.time - startTime;
            float progress = Mathf.Clamp01(elapsedTime / spawnDuration);
            int targetCount = Mathf.FloorToInt(progress * totalItems);

            // 目標数に達するまでこのフレームで生成を繰り返す
            while (spawnedCount < targetCount)
            {
                SpawnSingleItem();
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

    private void PrepareWaveSettings()
    {
        // 1. エリアの組み合わせを決定 (1〜15のランダム数値で全15パターン)
        _activeAreaMask = Random.Range(1, 16);

        // 2. 確率パターンの選択 (nC4通りに対応する割り当て)
        if (ratePatterns != null && ratePatterns.Count > 0)
        {
            // n個のパターンからランダムに4つを非重複で選択する
            System.Collections.Generic.List<SpawnRatePattern> selected = GetRandomCombinations(ratePatterns, 4);
            _topLeftRate = selected[0];
            _topRightRate = selected[1];
            _bottomLeftRate = selected[2];
            _bottomRightRate = selected[3];
        }
        else
        {
            // フォールバック用のデフォルトレート
            var fallback = new SpawnRatePattern("Fallback", copperRate, silverRate, goldRate, hourglassRate);
            _topLeftRate = fallback;
            _topRightRate = fallback;
            _bottomLeftRate = fallback;
            _bottomRightRate = fallback;
        }

        // ログ出力用テキスト生成
        string areaStr = "";
        if ((_activeAreaMask & 1) != 0) areaStr += "左上 ";
        if ((_activeAreaMask & 2) != 0) areaStr += "右上 ";
        if ((_activeAreaMask & 4) != 0) areaStr += "右下 ";
        if ((_activeAreaMask & 8) != 0) areaStr += "左下 ";

        Debug.Log($"[ItemSpawner] ウェーブ設定完了: \n" +
                  $"有効エリア: {areaStr}(マスク値: {_activeAreaMask})\n" +
                  $"確率割当: [左上:{_topLeftRate.name}] [右上:{_topRightRate.name}] [右下:{_bottomRightRate.name}] [左下:{_bottomLeftRate.name}]");
    }

    private System.Collections.Generic.List<SpawnRatePattern> GetRandomCombinations(System.Collections.Generic.List<SpawnRatePattern> list, int count)
    {
        var result = new System.Collections.Generic.List<SpawnRatePattern>();
        
        if (list.Count >= count)
        {
            // 重複なしで選択 (nC4の組み合わせ選択に相当)
            var indices = new System.Collections.Generic.List<int>();
            for (int i = 0; i < list.Count; i++) indices.Add(i);

            for (int i = 0; i < count; i++)
            {
                int randIdx = Random.Range(0, indices.Count);
                result.Add(list[indices[randIdx]]);
                indices.RemoveAt(randIdx);
            }
        }
        else
        {
            // 要素数nが割り当て先数(4)より少ない場合は重複を許容して割り当て
            for (int i = 0; i < count; i++)
            {
                result.Add(list[Random.Range(0, list.Count)]);
            }
        }

        return result;
    }

    private void SpawnSingleItem()
    {
        // 1. 有効なエリア（マスク）からランダムに1つのクアドラントを選択
        var activeQuads = new System.Collections.Generic.List<int>();
        if ((_activeAreaMask & 1) != 0) activeQuads.Add(1); // 左上
        if ((_activeAreaMask & 2) != 0) activeQuads.Add(0); // 右上
        if ((_activeAreaMask & 4) != 0) activeQuads.Add(3); // 右下
        if ((_activeAreaMask & 8) != 0) activeQuads.Add(2); // 左下

        int chosenQuad = 0;
        if (activeQuads.Count > 0)
        {
            chosenQuad = activeQuads[Random.Range(0, activeQuads.Count)];
        }
        else
        {
            // 万が一マスクが0の場合は全クアドラントから選択
            chosenQuad = Random.Range(0, 4);
        }

        // 2. 選択されたクアドラントに割り当てられた確率パターンを取得
        SpawnRatePattern ratePattern = null;
        switch (chosenQuad)
        {
            case 0: ratePattern = _topRightRate; break;
            case 1: ratePattern = _topLeftRate; break;
            case 2: ratePattern = _bottomLeftRate; break;
            case 3: ratePattern = _bottomRightRate; break;
        }

        if (ratePattern == null) return;

        float copper = ratePattern.copperRate;
        float silver = ratePattern.silverRate;
        float gold = ratePattern.goldRate;
        float hourglass = ratePattern.hourglassRate;
        float totalRate = copper + silver + gold + hourglass;

        if (totalRate <= 0f) return;

        // 3. 確率計算
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

        // 4. 座標計算
        Vector3 center = (armRoot != null) ? armRoot.position : transform.position;
        center.y += spawnYOffset;

        float offsetX = 0f;
        float offsetZ = 0f;

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
public class SpawnRatePattern
{
    public string name;
    
    [Header("確率（排出比率）")]
    public float copperRate = 60f;
    public float silverRate = 25f;
    public float goldRate = 10f;
    public float hourglassRate = 5f;

    public SpawnRatePattern(string name, float copper, float silver, float gold, float hourglass)
    {
        this.name = name;
        this.copperRate = copper;
        this.silverRate = silver;
        this.goldRate = gold;
        this.hourglassRate = hourglass;
    }
}