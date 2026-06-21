using System.Collections;
using UnityEngine;

/// <summary>
/// ピンボールのボールコントローラ (全世代共通)。
/// - Rigidbody + SphereCollider + OnCollisionEnter で動作 (純 Unity 物理)
/// - Splitter と衝突すると Manager に分裂子 (Rigidbody + Collider 付き) の生成を依頼し、自身を破棄
/// - 各ボールは generation を保持し、Manager は parentGen から nextGen を計算する
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class PinballBallController : MonoBehaviour
{
    [Header("ボール設定")]
    public float mass = 0.5f;

    public float bounciness = 0.5f;

    [Tooltip("ボールの摩擦係数。0 + frictionCombine=Maximum でボール同士の摩擦のみ0、他面とは面側摩擦が適用される。")]
    [Range(0f, 1f)]
    public float friction = 0f;

    [Tooltip("摩擦の合成方法。Maximum 推奨 (Maximum は Combine の最高優先度なので、相手が何でも Maximum 合成が採用される)")]
    public PhysicsMaterialCombine frictionCombine = PhysicsMaterialCombine.Maximum;

    [Header("分裂設定")]
    [Tooltip("衝突時に分裂するオブジェクトのタグ")]
    public string splitTargetTag = "Splitter";

    [Tooltip("分裂後に左右へ広がる速度（m/s）")]
    public float splitSpread = 1f;

    [Tooltip("分裂数（2以上）")]
    [Min(2)]
    public int splitCount = 2;

    [Tooltip("分裂後のスケール倍率（0〜1）")]
    [Range(0.01f, 1f)]
    public float splitScaleRatio = 0.5f;

    [Tooltip("分裂後のスポーン位置をY軸上方向にずらす量（互換用。Managerは floorY+radius を使用）")]
    public float spawnUpOffset = 0.5f;

    [Tooltip("一回目の分裂時の左右Xオフセット（以降の世代は自動的に半減）")]
    public float spawnXOffset = 0.5f;

    [Tooltip("Splitter との衝突を無視する時間（秒）")]
    public float ignoreCollisionDuration = 0.4f;

    [Header("検知半径")]
    [Tooltip("gen 1 分裂子の検知半径。以降の世代は splitScaleRatio で縮小")]
    public float initialDetectionRadius = 0.08f;

    // 互換用フィールド: Inspector からは隠す（設定不要）。PinballBallManager が gen≥1 ボールの
    // 範囲（空間ハッシュ／境界）の算出にまだ参照するため、フィールド自体は残している。
    [HideInInspector] public float manualBoundsXMin = 0.81f;
    [HideInInspector] public float manualBoundsXMax = 2.4257f;
    [HideInInspector] public float manualBoundsZMax = 4.468f;
    [HideInInspector] public float manualBoundsZMin = -2f;

    [Header("ボール同士の衝突")]
    [Tooltip("gen ≥ 1 同士の位置分離 + 法線方向の弱い反発を行うか")]
    public bool enableBallBallCollision = true;

    [Tooltip("gen ≥ 1 ボールの跳ね返り係数（0〜1）")]
    [Range(0f, 1f)]
    public float manualBounceFactor = 0.6f;

    [Tooltip("gen ≥ 1 ボールの寿命（秒）。0以下で無効")]
    public float manualLifetime = 5f;

    [Header("最適化設定")]
    [Tooltip("分裂ボールが配置されるレイヤー名（同レイヤー同士の衝突は自動で無効化される）")]
    public string ballLayerName = "Ball";

    [Tooltip("この世代以降は ParticleSystem バーストで表現する（負の値で無効）")]
    public int particleGeneration = 4;

    [Tooltip("particleGeneration 以降に使用するパーティクルプレハブ")]
    public ParticleSystem splitParticlePrefab;

    [Header("パーティクル非表示領域")]
    public float hideParticleXMax = 0.75f;
    public float hideParticleZMin = 4.515f;

    [HideInInspector]
    [Tooltip("このボールの世代 (0 = 初期、Manager がスポーン時に上書きする)")]
    public int generation = 0;

    [Header("Enforce 発色")]
    [Tooltip("Enforce Pin に当たるたびに 1 つ進む。index 0 = 初期 (無発光) ... 後ろほど強い色\n推奨: [黒, 緑, 青, 紫, 金, 赤]")]
    [SerializeField] private Color[] enforceColors = new Color[]
    {
        Color.black,                               // 0: 無発光
        new Color(0f, 1f, 0f),                     // 1: 緑
        new Color(0f, 0.5f, 1f),                   // 2: 青
        new Color(0.7f, 0f, 1f),                   // 3: 紫
        new Color(1f, 0.84f, 0f),                  // 4: 金
        new Color(1f, 0f, 0f),                     // 5: 赤
    };

    [Tooltip("玉の発光強度倍率")]
    [SerializeField, Min(0f)] private float ballEmissionIntensity = 2f;

    [Tooltip("発光させる Renderer。未設定なら子から自動検索")]
    [SerializeField] private Renderer ballRenderer;

    /// <summary>Enforce Pin で上昇する発色レベル (Manager がスポーン時に親から継承する)</summary>
    [HideInInspector] public int enforceLevel = 0;

    [Header("価値 (Exchange モニター連動)")]
    [Tooltip("このボール固有の現在価値 (¥)。ShopBallController がスポーン時に slot.price で初期化、Enforce Pin で enforceValueMultiplier 倍、分裂時に子へ継承される")]
    public float currentValue = 0f;

    [Tooltip("Enforce Pin 1 回ヒットで currentValue に掛ける倍率 (デフォルト 2 = 2 倍)")]
    [SerializeField, Min(1f)] private float enforceValueMultiplier = 2f;

    private Material _ballMaterialInstance;

    // 互換用フィールド: PinballBallController 自体はもう重力を適用しない（標準 Unity 重力 + LocalGravityBody に委譲）。
    // PinballBallManager / PinballGravityZone / PinballConveyor がまだ参照・書き込みするため残しているが、本コンポーネントは未使用。
    [HideInInspector] public Vector3 gravity = new Vector3(0f, -9.81f, 9.81f);
    [HideInInspector] public int onConveyorCount = 0;
    public bool IsOnConveyor => onConveyorCount > 0;

    private bool _isSplitting = false;

    private Rigidbody rb;
    private SphereCollider sphereCol;

    private static int _ballLayer = -1;
    private static bool _ballLayerCollisionConfigured = false;

    /// <summary>現在シーンに存在する全ボール数 (デバッグ・初期累計用)</summary>
    public static int AliveGen0Count { get; private set; }

    void OnEnable() { AliveGen0Count++; }
    void OnDisable() { AliveGen0Count--; }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = mass;
        rb.useGravity = true; // 重力は標準 Unity 重力（+ 必要なら LocalGravityBody）に委譲
        rb.constraints = RigidbodyConstraints.None;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        sphereCol = GetComponent<SphereCollider>();
        if (sphereCol.sharedMaterial == null)
        {
            PhysicsMaterial mat = new PhysicsMaterial("BallPhysics");
            mat.bounciness = bounciness;
            mat.dynamicFriction = friction;
            mat.staticFriction = friction;
            mat.frictionCombine = frictionCombine;
            mat.bounceCombine = PhysicsMaterialCombine.Maximum;
            sphereCol.sharedMaterial = mat;
        }

        SetupBallLayer();
    }

    void SetupBallLayer()
    {
        if (string.IsNullOrEmpty(ballLayerName)) return;

        if (_ballLayer < 0)
            _ballLayer = LayerMask.NameToLayer(ballLayerName);

        if (_ballLayer < 0)
        {
            Debug.LogWarning($"[PinballBallController] Layer '{ballLayerName}' が見つかりません。");
            return;
        }

        gameObject.layer = _ballLayer;

        if (!_ballLayerCollisionConfigured)
        {
            // enableBallBallCollision = true ならボール同士の衝突 ON、false なら OFF
            // (純物理時はデフォルト ON、ピンボールっぽい弾き合いを許可)
            Physics.IgnoreLayerCollision(_ballLayer, _ballLayer, !enableBallBallCollision);
            _ballLayerCollisionConfigured = true;
        }
    }

    void FixedUpdate()
    {
        // 重力適用は廃止（標準 Unity 重力 + LocalGravityBody に委譲）。分裂ガードのフラグのみリセットする。
        _isSplitting = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_isSplitting) return;

        // PinballPin (Enforce / Split) を最優先でハンドル。
        // 衝突 SFX / 転がり SFX は BallSurfaceAudio 側で SurfaceTag を見て処理する。
        var pin = collision.collider.GetComponent<PinballPin>();
        if (pin == null) pin = collision.collider.GetComponentInParent<PinballPin>();
        if (pin != null && !pin.IsConsumed)
        {
            // 衝突点（取れなければ自分の位置）でこのピン個別のヒット SFX / VFX を再生
            Vector3 hitPos = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            switch (pin.Type)
            {
                case PinballPin.PinType.Enforce:
                    pin.TryConsume();
                    pin.PlayHitFX(hitPos);
                    EnforceLevelUp();
                    return;
                case PinballPin.PinType.Split:
                    pin.TryConsume();
                    pin.PlayHitFX(hitPos);
                    BeginSplit(collision);
                    return;
            }
        }

        // 互換: 旧 Splitter タグ (PinballPin が無いピン) は従来通り消費なしで分裂
        if (collision.gameObject.CompareTag(splitTargetTag))
        {
            BeginSplit(collision);
            return;
        }
    }

    void BeginSplit(Collision collision)
    {
        _isSplitting = true;
        Vector3 posAtCollision = transform.position;
        StartCoroutine(SplitNextFrame(posAtCollision, collision.collider));
    }

    /// <summary>Enforce Pin に触れた時に呼ぶ。発色レベル+1 (上限まで) して emission を更新。
    /// さらに 1 回ヒットごとに currentValue を enforceValueMultiplier 倍する (レベル上限到達後も価値は増え続ける)。</summary>
    public void EnforceLevelUp()
    {
        // 価値は毎回掛け算 (レベル上限と独立)
        currentValue *= enforceValueMultiplier;

        if (enforceColors == null || enforceColors.Length == 0) return;
        int max = enforceColors.Length - 1;
        if (enforceLevel < max)
        {
            enforceLevel++;
            ApplyBallEmission();
        }
    }

    /// <summary>現在の enforceLevel に応じて玉の Renderer の emission を更新する。Manager からスポーン時にも呼ばれる。</summary>
    public void ApplyBallEmission()
    {
        if (_ballMaterialInstance == null)
        {
            if (ballRenderer == null) ballRenderer = GetComponentInChildren<Renderer>();
            if (ballRenderer == null) return;
            _ballMaterialInstance = ballRenderer.material;
        }
        if (enforceColors == null || enforceColors.Length == 0) return;
        int idx = Mathf.Clamp(enforceLevel, 0, enforceColors.Length - 1);
        Color c = enforceColors[idx] * ballEmissionIntensity;
        _ballMaterialInstance.EnableKeyword("_EMISSION");
        _ballMaterialInstance.SetColor("_EmissionColor", c);
    }

    IEnumerator SplitNextFrame(Vector3 posAtCollision, Collider splitTargetCollider)
    {
        yield return new WaitForFixedUpdate();

        float sphereRadiusWorld = sphereCol.bounds.extents.y;
        PinballBallManager.Instance.ProduceChildren(this, generation, posAtCollision, splitTargetCollider, sphereRadiusWorld);

        Destroy(gameObject);
    }
}
