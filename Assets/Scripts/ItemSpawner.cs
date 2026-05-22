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

    [Header("グリッド設定")]
    [Tooltip("グリッド分割数 (4面 または 9面)")]
    public GridType gridType = GridType.Grid4;

    [Tooltip("毎ウェーブでグリッド分割数をランダムに変えるかどうか")]
    public bool randomGridType = false;

    [Header("確率パターン設定 (nパターン)")]
    [Tooltip("コインと時計の降る確率のベースパターンリスト。空の場合はデフォルトの10パターンが自動生成されます。")]
    public System.Collections.Generic.List<SpawnRatePattern> ratePatterns = new System.Collections.Generic.List<SpawnRatePattern>();

    public static bool IsSpawning { get; private set; } = false;

    // 現在のウェーブの設定
    private GridType _currentWaveGridType;
    private int _activeAreaMask = 0;
    private SpawnRatePattern[] _activeRates; // 割り当てられたパターン (サイズは 4 または 9)

    void Awake()
    {
        // インスペクターで設定されていない場合、デフォルトの10パターンを生成
        if (ratePatterns == null || ratePatterns.Count == 0)
        {
            ratePatterns = new System.Collections.Generic.List<SpawnRatePattern>()
            {
                new SpawnRatePattern("Pattern 1 (標準)", 60f, 25f, 10f, 5f),
                new SpawnRatePattern("Pattern 2 (コイン多め)", 40f, 35f, 20f, 5f),
                new SpawnRatePattern("Pattern 3 (高レア多め)", 20f, 40f, 30f, 10f),
                new SpawnRatePattern("Pattern 4 (時計特化)", 30f, 30f, 15f, 25f),
                new SpawnRatePattern("Pattern 5 (フィーバー)", 10f, 20f, 40f, 30f),
                new SpawnRatePattern("Pattern 6 (銅特化)", 90f, 8f, 1f, 1f),
                new SpawnRatePattern("Pattern 7 (銀特化)", 10f, 75f, 10f, 5f),
                new SpawnRatePattern("Pattern 8 (金特化)", 5f, 15f, 75f, 5f),
                new SpawnRatePattern("Pattern 9 (バランス時計)", 25f, 25f, 25f, 25f),
                new SpawnRatePattern("Pattern 10 (スーパーフィーバー)", 0f, 10f, 50f, 40f)
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

        // ウェーブ設定（4面/9面の決定、15通り/511通りの組み合わせとnC4/nC9確率割り当て）を準備
        PrepareWaveSettings();

        while (spawnedCount < totalItems)
        {
            // 経過時間から「今何個生成されているべきか」を計算
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
        IsSpawning = false;
        CoinOptimizer.freezeStartTime = Time.time + 3.0f;
        Debug.Log($"[ItemSpawner] スポーン完了。{CoinOptimizer.freezeStartTime:F1}秒後から凍結チェック開始");
    }

    private void PrepareWaveSettings()
    {
        // 1. グリッド分割モードの決定
        if (randomGridType)
        {
            _currentWaveGridType = (GridType)Random.Range(0, 2);
        }
        else
        {
            _currentWaveGridType = gridType;
        }

        int cellCount = (_currentWaveGridType == GridType.Grid4) ? 4 : 9;

        // 2. エリアの組み合わせを決定 (1〜2^cellCount - 1のランダム数値)
        _activeAreaMask = Random.Range(1, 1 << cellCount);

        // 3. 確率パターンの選択 (nC_cellCount通りに対応する割り当て)
        if (ratePatterns != null && ratePatterns.Count > 0)
        {
            // n個のパターンからランダムにcellCount個を非重複で選択する
            System.Collections.Generic.List<SpawnRatePattern> selected = GetRandomCombinations(ratePatterns, cellCount);
            _activeRates = selected.ToArray();
        }
        else
        {
            // フォールバック用のデフォルトレート
            var fallback = new SpawnRatePattern("Fallback", copperRate, silverRate, goldRate, hourglassRate);
            _activeRates = new SpawnRatePattern[cellCount];
            for (int i = 0; i < cellCount; i++)
            {
                _activeRates[i] = fallback;
            }
        }

        // ログ出力用テキスト生成
        string gridModeName = (_currentWaveGridType == GridType.Grid4) ? "4面 (2x2)" : "9面 (3x3)";
        string areaStr = "";
        
        if (_currentWaveGridType == GridType.Grid4)
        {
            if ((_activeAreaMask & 1) != 0) areaStr += "左上 ";
            if ((_activeAreaMask & 2) != 0) areaStr += "右上 ";
            if ((_activeAreaMask & 4) != 0) areaStr += "右下 ";
            if ((_activeAreaMask & 8) != 0) areaStr += "左下 ";
        }
        else // Grid9
        {
            string[] cellNames = { "左上", "中央上", "右上", "左中央", "中央", "右中央", "左下", "中央下", "右下" };
            for (int i = 0; i < 9; i++)
            {
                if ((_activeAreaMask & (1 << i)) != 0)
                {
                    areaStr += cellNames[i] + " ";
                }
            }
        }

        // 確率割り当てログの生成
        System.Text.StringBuilder ratesLog = new System.Text.StringBuilder();
        if (_currentWaveGridType == GridType.Grid4)
        {
            ratesLog.Append($"[左上:{_activeRates[0].name}] [右上:{_activeRates[1].name}] [右下:{_activeRates[2].name}] [左下:{_activeRates[3].name}]");
        }
        else
        {
            string[] cellNames = { "左上", "中央上", "右上", "左中央", "中央", "右中央", "左下", "中央下", "右下" };
            for (int i = 0; i < 9; i++)
            {
                ratesLog.Append($"[{cellNames[i]}:{_activeRates[i].name}] ");
            }
        }

        Debug.Log($"[ItemSpawner] ウェーブ設定完了: \n" +
                  $"グリッドモード: {gridModeName}\n" +
                  $"有効エリア: {areaStr}(マスク値: {_activeAreaMask})\n" +
                  $"確率割当: {ratesLog.ToString()}");
    }

    private System.Collections.Generic.List<SpawnRatePattern> GetRandomCombinations(System.Collections.Generic.List<SpawnRatePattern> list, int count)
    {
        var result = new System.Collections.Generic.List<SpawnRatePattern>();
        
        if (list.Count >= count)
        {
            // 重複なしで選択 (nC4/nC9の組み合わせ選択に相当)
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
            // 要素数nが割り当て先数より少ない場合は重複を許容して割り当て
            for (int i = 0; i < count; i++)
            {
                result.Add(list[Random.Range(0, list.Count)]);
            }
        }

        return result;
    }

    private void SpawnSingleItem()
    {
        int cellCount = (_currentWaveGridType == GridType.Grid4) ? 4 : 9;

        // 1. 有効なエリア（マスク）からランダムに1つのセルを選択
        var activeCells = new System.Collections.Generic.List<int>();
        for (int i = 0; i < cellCount; i++)
        {
            if ((_activeAreaMask & (1 << i)) != 0)
            {
                activeCells.Add(i);
            }
        }

        int chosenCell = 0;
        if (activeCells.Count > 0)
        {
            chosenCell = activeCells[Random.Range(0, activeCells.Count)];
        }
        else
        {
            // 万が一マスクが0の場合は全セルから選択
            chosenCell = Random.Range(0, cellCount);
        }

        // 2. 選択されたセルに割り当てられた確率パターンを取得
        if (_activeRates == null || chosenCell >= _activeRates.Length) return;
        SpawnRatePattern ratePattern = _activeRates[chosenCell];

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

        if (_currentWaveGridType == GridType.Grid4)
        {
            float halfWidth = spawnArea.x;
            float halfHeight = spawnArea.y;

            switch (chosenCell)
            {
                case 0: // 左上 (X: [-halfWidth, 0], Z: [0, halfHeight])
                    offsetX = Random.Range(-halfWidth, 0f);
                    offsetZ = Random.Range(0f, halfHeight);
                    break;
                case 1: // 右上 (X: [0, halfWidth], Z: [0, halfHeight])
                    offsetX = Random.Range(0f, halfWidth);
                    offsetZ = Random.Range(0f, halfHeight);
                    break;
                case 2: // 右下 (X: [0, halfWidth], Z: [-halfHeight, 0])
                    offsetX = Random.Range(0f, halfWidth);
                    offsetZ = Random.Range(-halfHeight, 0f);
                    break;
                case 3: // 左下 (X: [-halfWidth, 0], Z: [-halfHeight, 0])
                    offsetX = Random.Range(-halfWidth, 0f);
                    offsetZ = Random.Range(-halfHeight, 0f);
                    break;
            }
        }
        else // Grid9
        {
            float thirdWidth = spawnArea.x / 3f;
            float thirdHeight = spawnArea.y / 3f;

            // Column: 0 = Left, 1 = Center, 2 = Right
            int col = chosenCell % 3;
            // Row: 0 = Top, 1 = Center, 2 = Bottom
            int row = chosenCell / 3;

            float minX = 0f, maxX = 0f;
            if (col == 0) // Left
            {
                minX = -spawnArea.x;
                maxX = -thirdWidth;
            }
            else if (col == 1) // Center
            {
                minX = -thirdWidth;
                maxX = thirdWidth;
            }
            else // Right
            {
                minX = thirdWidth;
                maxX = spawnArea.x;
            }

            float minZ = 0f, maxZ = 0f;
            if (row == 0) // Top
            {
                minZ = thirdHeight;
                maxZ = spawnArea.y;
            }
            else if (row == 1) // Center
            {
                minZ = -thirdHeight;
                maxZ = thirdHeight;
            }
            else // Bottom
            {
                minZ = -spawnArea.y;
                maxZ = -thirdHeight;
            }

            offsetX = Random.Range(minX, maxX);
            offsetZ = Random.Range(minZ, maxZ);
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

public enum GridType
{
    Grid4,
    Grid9
}