using System.Collections;
using UnityEngine;

public class CoinOptimizer : MonoBehaviour
{
    [Header("最適化設定")]
    [Tooltip("MeshColliderを自動でSphereColliderに置き換えて物理演算を軽量化します")]
    public bool useSphereColliderAutoReplacement = false;

    [Tooltip("スポーン後に個別凍結判定を開始するまでの待機時間（秒）")]
    public float individualFreezeDelay = 2.0f;

    private Rigidbody rb;
    private Collider _col;
    private float checkInterval = 0.5f; // 静止判定の間隔
    private float sleepVelocity = 0.01f; // 静止とみなす速度しきい値 (めり込み凍結を防ぐため小さく設定)
    private float coinThickness = 0.05f;

    /// <summary>
    /// ItemSpawnerがスポーン完了時に設定する。（後方互換性維持のために残します）
    /// </summary>
    public static float freezeStartTime = float.MaxValue;

    // アームに叩き起こされている間は凍結を禁止するタイマー
    // WakeUp()が呼ばれるたびにリセットされ、2秒間はIsSleeping()でも凍結しない
    private float _keepAwakeTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();

        if (rb == null)
        {
            Debug.LogWarning($"[CoinOptimizer] {gameObject.name} に Rigidbody がアタッチされていません。最適化処理をスキップします。", this);
            enabled = false;
            return;
        }

        if (useSphereColliderAutoReplacement)
        {
            ReplaceWithSphereCollider();
        }
        
        // 1万枚が同時に計算しないよう、最初の確認タイミングを0〜2秒の間でランダムにばらけさせる
        float randomStartDelay = Random.Range(0f, 2.0f);
        StartCoroutine(CheckAndFreezeRoutine(randomStartDelay));
    }

    void Update()
    {
        if (_keepAwakeTimer > 0f)
            _keepAwakeTimer -= Time.deltaTime;
    }

    private void ReplaceWithSphereCollider()
    {
        MeshCollider meshCol = GetComponent<MeshCollider>();
        if (meshCol != null)
        {
            // メッシュの境界を基に球体の半径を算出
            // コインの厚さ方向ではなく、直径方向のサイズをカバーする球体にする
            Bounds bounds = meshCol.bounds;
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            if (radius <= 0f)
            {
                // boundsがまだ計算されていない場合のフォールバック（一般的なコインの半径目安）
                radius = 0.3f;
            }

            // MeshColliderを無効化し削除
            meshCol.enabled = false;
            Destroy(meshCol);

            // SphereColliderを追加
            SphereCollider sphereCol = gameObject.AddComponent<SphereCollider>();
            sphereCol.radius = radius;
            _col = sphereCol;
            
            // Physicsマテリアルがあれば引き継ぐ
            if (meshCol.sharedMaterial != null)
            {
                sphereCol.sharedMaterial = meshCol.sharedMaterial;
            }
        }
    }

    private IEnumerator CheckAndFreezeRoutine(float delay)
    {
        // 個々のコインが生成されてから、地面に落ちるまでの最低限の猶予時間を待つ
        // これにより「コイントスや落下中」に空中で即座に凍結するのを防ぐ
        yield return new WaitForSeconds(individualFreezeDelay + delay);
        
        // 毎回「new」するとゴミ（GC）が出るので、待機命令を使い回す
        WaitForSeconds wait = new WaitForSeconds(checkInterval);

        // Kinematicになるまでループし続ける
        while (!rb.isKinematic)
        {
            // Unity物理エンジンが「静止した」と判断、または速度・角速度が十分に低く、かつアームから離れて2秒経過した場合のみ凍結
            bool isNearStatic = rb.IsSleeping();
            if (!isNearStatic)
            {
                // IsSleeping()が判定されない場合のセーフティとして速度をチェック (Unity 6.0仕様)
                isNearStatic = rb.linearVelocity.sqrMagnitude < (sleepVelocity * sleepVelocity) &&
                               rb.angularVelocity.sqrMagnitude < (sleepVelocity * sleepVelocity);
            }

            if (isNearStatic && _keepAwakeTimer <= 0f)
            {
                rb.isKinematic = true;
                yield break;
            }

            // 指定インターバル休んでから再確認
            yield return wait;
        }
    }

    // アームが近づいた時に外部から叩き起こすための処理
    public void WakeUp(bool isChain = false)
    {
        // 叩き起こされたら2秒間は凍結しないようタイマーをリセット
        // アームが近くにある間はWakeUpNearbyCoinsから定期的に呼ばれ続けるため
        // アームから完全に離れるまで凍結しない
        _keepAwakeTimer = 2.0f;

        if (rb != null && rb.isKinematic)
        {
            rb.isKinematic = false;
            // 起動時の初期速度・角速度の微動をゼロクリア
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            StartCoroutine(CheckAndFreezeRoutine(1.0f));

            // 初回の呼び出し時のみ、周囲のコインも連鎖的に起こす（下のコインが抜けて上のコインが空中に浮いたままになるのを防ぐ）
            if (!isChain)
            {
                Collider[] hits = Physics.OverlapSphere(transform.position, coinThickness * 4f);
                foreach (var hit in hits)
                {
                    CoinOptimizer other = hit.GetComponent<CoinOptimizer>();
                    if (other != null && other != this)
                    {
                        other.WakeUp(true); // 連鎖フラグを立てて呼ぶ（無限ループ防止）
                    }
                }
            }
        }
    }
}