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

    [Header("除外セル設定")]
    [Tooltip("コインを降らさないセル番号のリスト。\nGrid4: 0(左上) 1(右上) 2(右下) 3(左下)\nGrid9: 0(左上) 1(中上) 2(右上) 3(左中) 4(中央) 5(右中) 6(左下) 7(中下) 8(右下)")]
    public System.Collections.Generic.List<int> excludedCellIndices = new System.Collections.Generic.List<int>();

    [Header("セルサイズカスタマイズ (Grid4)")]
    [Tooltip("列幅の比率 [左, 右]。値が大きいほどその列が広くなります。")]
    public float[] grid4ColumnWeights = new float[] { 1f, 1f };
    [Tooltip("行高さの比率 [奥（上）, 手前（下）]。値が大きいほどその行が長くなります。")]
    public float[] grid4RowWeights    = new float[] { 1f, 1f };

    [Header("セルサイズカスタマイズ (Grid9)")]
    [Tooltip("列幅の比率 [左, 中, 右]。値が大きいほどその列が広くなります。")]
    public float[] grid9ColumnWeights = new float[] { 1f, 1f, 1f };
    [Tooltip("行高さの比率 [奥（上）, 中, 手前（下）]。値が大きいほどその行が長くなります。")]
    public float[] grid9RowWeights    = new float[] { 1f, 1f, 1f };

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

    void Update()
    {
        // Pキー入力でデバッグ用のアイテムスポーンをトリガーする (新Input System対応)
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.pKey.wasPressedThisFrame)
        {
            if (!IsSpawning)
            {
                Debug.Log("[ItemSpawner] Pキーが押されました。デバッグ用のスポーンを開始します。");
                StartCoroutine(SpawnRoutine());
            }
            else
            {
                Debug.LogWarning("[ItemSpawner] 現在すでにスポーン中であるため、Pキー入力を無視します。");
            }
        }
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

        // 2. エリアの組み合わせを決定 (1～2^cellCount - 1のランダム数値)
        _activeAreaMask = Random.Range(1, 1 << cellCount);

        // 2b. 除外セルのビットをクリア
        if (excludedCellIndices != null)
        {
            foreach (int idx in excludedCellIndices)
            {
                if (idx >= 0 && idx < cellCount)
                    _activeAreaMask &= ~(1 << idx);
            }
        }
        // 全セルが除外された場合は、除外されていない最初のセルを強制使用
        if (_activeAreaMask == 0)
        {
            for (int i = 0; i < cellCount; i++)
            {
                if (excludedCellIndices == null || !excludedCellIndices.Contains(i))
                {
                    _activeAreaMask = 1 << i;
                    break;
                }
            }
            // 全セル除外されている場合はフォールバック
            if (_activeAreaMask == 0) _activeAreaMask = 1;
        }

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

        var bounds = ComputeCellBounds(chosenCell, _currentWaveGridType);
        float offsetX = Random.Range(bounds.minX, bounds.maxX);
        float offsetZ = Random.Range(bounds.minZ, bounds.maxZ);

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

    /// <summary>
    /// セル番号とグリッドタイプから、そのセルの XZ 範囲（グリッド中心からのオフセット）を返す
    /// 列幅・行高さは Inspector の Weight で等比分割する
    /// </summary>
    private (float minX, float maxX, float minZ, float maxZ) ComputeCellBounds(int cellIndex, GridType gridType)
    {
        float totalW = spawnArea.x * 2f;
        float totalH = spawnArea.y * 2f;

        if (gridType == GridType.Grid4)
        {
            // 列幅の比率計算
            float[] cw = (grid4ColumnWeights != null && grid4ColumnWeights.Length >= 2)
                ? grid4ColumnWeights : new float[] { 1f, 1f };
            float cwSum = Mathf.Max(cw[0] + cw[1], 0.0001f);
            float leftW  = (cw[0] / cwSum) * totalW;

            // 行高さの比率計算
            float[] rw = (grid4RowWeights != null && grid4RowWeights.Length >= 2)
                ? grid4RowWeights : new float[] { 1f, 1f };
            float rwSum = Mathf.Max(rw[0] + rw[1], 0.0001f);
            float topH  = (rw[0] / rwSum) * totalH;

            // X境界：-spawnArea.x から右方向に積み上げ
            float colStart0 = -spawnArea.x;
            float colEnd0   = colStart0 + leftW;           // = -spawnArea.x + leftW
            float colStart1 = colEnd0;
            float colEnd1   = spawnArea.x;

            // Z境界：spawnArea.y から下方向に積み上げ
            float rowEnd0   = spawnArea.y;
            float rowStart0 = rowEnd0 - topH;              // 奥側（正Z）
            float rowEnd1   = rowStart0;
            float rowStart1 = -spawnArea.y;                // 手前側（負Z）

            // cell 0:左上  1:右上  2:右下  3:左下
            bool isRight  = (cellIndex == 1 || cellIndex == 2);
            bool isBottom = (cellIndex >= 2);
            float minX = isRight  ? colStart1 : colStart0;
            float maxX = isRight  ? colEnd1   : colEnd0;
            float minZ = isBottom ? rowStart1  : rowStart0;
            float maxZ = isBottom ? rowEnd1    : rowEnd0;
            return (minX, maxX, minZ, maxZ);
        }
        else // Grid9
        {
            float[] cw = (grid9ColumnWeights != null && grid9ColumnWeights.Length >= 3)
                ? grid9ColumnWeights : new float[] { 1f, 1f, 1f };
            float cwSum = Mathf.Max(cw[0] + cw[1] + cw[2], 0.0001f);
            float leftW   = (cw[0] / cwSum) * totalW;
            float centerW = (cw[1] / cwSum) * totalW;

            float[] rw = (grid9RowWeights != null && grid9RowWeights.Length >= 3)
                ? grid9RowWeights : new float[] { 1f, 1f, 1f };
            float rwSum = Mathf.Max(rw[0] + rw[1] + rw[2], 0.0001f);
            float topH    = (rw[0] / rwSum) * totalH;
            float midH    = (rw[1] / rwSum) * totalH;

            // 列境界
            float[] colStarts = { -spawnArea.x, -spawnArea.x + leftW, -spawnArea.x + leftW + centerW };
            float[] colEnds   = { colStarts[1],  colStarts[2],          spawnArea.x };

            // 行境界（上から下へ）
            float[] rowEnds   = { spawnArea.y,        spawnArea.y - topH, spawnArea.y - topH - midH };
            float[] rowStarts = { spawnArea.y - topH,  spawnArea.y - topH - midH, -spawnArea.y };

            int col = cellIndex % 3;
            int row = cellIndex / 3;
            return (colStarts[col], colEnds[col], rowStarts[row], rowEnds[row]);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Vector3 center = (armRoot != null) ? armRoot.position : transform.position;
        center.y += spawnYOffset;

        // 実行中は _currentWaveGridType を使用、エディタ時は gridType を参照
        GridType drawGrid = Application.isPlaying ? _currentWaveGridType : gridType;
        int cellCount = (drawGrid == GridType.Grid4) ? 4 : 9;

        string[] names4 = { "0:左上", "1:右上", "2:右下", "3:左下" };
        string[] colName = { "左", "中", "右" };
        string[] rowName = { "上", "中", "下" };

        for (int i = 0; i < cellCount; i++)
        {
            var b = ComputeCellBounds(i, drawGrid);
            string label = (drawGrid == GridType.Grid4)
                ? names4[i]
                : $"{i}:{colName[i % 3]}{rowName[i / 3]}";
            DrawCell(center, b.minX, b.maxX, b.minZ, b.maxZ, i, cellCount, label);
        }

        // 中心点を描画
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(center, 0.1f);
    }

    private void DrawCell(Vector3 center, float minX, float maxX, float minZ, float maxZ,
                          int cellIndex, int cellCount, string label)
    {
        // 除外セルは常に赤で表示
        bool isExcluded = excludedCellIndices != null && excludedCellIndices.Contains(cellIndex);

        bool isActive = !isExcluded && (Application.isPlaying
            ? ((_activeAreaMask & (1 << cellIndex)) != 0)
            : true);

        Color fillColor;
        Color wireColor;

        if (isExcluded)
        {
            fillColor = new Color(1f, 0f, 0f, 0.12f);
            wireColor = new Color(1f, 0f, 0f, 0.85f);
        }
        else if (Application.isPlaying)
        {
            fillColor = isActive ? new Color(0f, 1f, 0f, 0.15f) : new Color(1f, 0.6f, 0f, 0.08f);
            wireColor = isActive ? new Color(0f, 1f, 0f, 0.9f)  : new Color(1f, 0.6f, 0f, 0.5f);
        }
        else
        {
            fillColor = new Color(0.7f, 0.7f, 0.7f, 0.12f);
            wireColor = new Color(0.8f, 0.8f, 0.8f, 0.8f);
        }

        float cx = center.x + (minX + maxX) * 0.5f;
        float cz = center.z + (minZ + maxZ) * 0.5f;
        float sizeX = Mathf.Abs(maxX - minX);
        float sizeZ = Mathf.Abs(maxZ - minZ);
        Vector3 cellCenter = new Vector3(cx, center.y, cz);
        Vector3 cellSize   = new Vector3(sizeX, 0.01f, sizeZ);

        // 塗りつぶし
        Gizmos.color = fillColor;
        Gizmos.DrawCube(cellCenter, cellSize);

        // 枠線
        Gizmos.color = wireColor;
        Gizmos.DrawWireCube(cellCenter, cellSize);

        // ラベル
        UnityEditor.Handles.color = wireColor;
        UnityEditor.Handles.Label(cellCenter + Vector3.up * 0.05f, label);
    }
#endif
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