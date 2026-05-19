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

    public static bool IsSpawning { get; private set; } = false;

    void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        IsSpawning = true;
        int spawnedCount = 0;
        float startTime = Time.time;
        float totalRate = copperRate + silverRate + goldRate + hourglassRate;

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
                SpawnSingleItem(totalRate);
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

    private void SpawnSingleItem(float totalRate)
    {
        // 確率計算
        float rand = Random.Range(0f, totalRate);
        GameObject prefabToSpawn = null;

        if (rand < copperRate) 
        {
            prefabToSpawn = copperCoinPrefab;
        }
        else if (rand < copperRate + silverRate) 
        {
            prefabToSpawn = silverCoinPrefab;
        }
        else if (rand < copperRate + silverRate + goldRate) 
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

        // ばらつき（散らばり）を加える
        Vector3 randomPos = center + new Vector3(
            Random.Range(-spawnArea.x, spawnArea.x),
            Random.Range(-0.5f, 0.5f), // 上下のブレも少しだけ加える
            Random.Range(-spawnArea.y, spawnArea.y)
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