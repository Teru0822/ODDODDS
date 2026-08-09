using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.SceneManagement;
using UniRx;
using System;
using UnityEngine.SocialPlatforms.Impl;


#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 分裂後 (gen ≥ 1) のボールを NativeArray で一括管理し、
/// C# Jobs + Burst で並列に物理更新・Splitter検知・寿命判定を行うマネージャ。
/// シーンに 1 つだけ存在。初回アクセス時に自動生成される。
/// </summary>
public class PinballBallManager : MonoBehaviour
{
    // ---- 全 Inspector 項目は PinballBallConfig に分離 ----
    // シーンに PinballBallConfig をアタッチすれば Manager が起動時に参照する。
    // 見つからない場合はデフォルト値で動作。
    private PinballBallConfig _config;

    private int _totalGenerated = 0;
    private GUIStyle _moneyStyle;
    private float _nextLogTime = 0f;

    // 所持金ポップ演出
    private long _lastMoneyPopStep = 0;
    private float _popStartTime = -999f;

    private static PinballBallManager _instance;
    public static PinballBallManager Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = FindFirstObjectByType<PinballBallManager>();
            if (_instance == null)
            {
                var go = new GameObject("[PinballBallManager]");
                _instance = go.AddComponent<PinballBallManager>();
            }
            return _instance;
        }
    }

    /// <summary>
    /// 初回起動時とその後のシーンロードの両方で Manager を生成しておく (R キー等の入力受付のため)。
    /// [RuntimeInitializeOnLoadMethod] はゲーム起動時の一度しか走らないので、
    /// SceneManager.sceneLoaded に subscribe してリロード後も復活させる。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceOnSceneLoad()
    {
        if (FindFirstObjectByType<PinballBallConfig>() != null || FindFirstObjectByType<PinballBallController>() != null)
        {
            _ = Instance;
        }
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindFirstObjectByType<PinballBallConfig>() != null || FindFirstObjectByType<PinballBallController>() != null)
        {
            _ = Instance;
        }
    }

    // ボール配列
    private NativeList<float3> _positions;
    private NativeList<float3> _velocities;
    private NativeList<float> _radii;
    private NativeList<int> _generations;
    private NativeList<int> _ignoredSplitterIdx;
    private NativeList<float> _ignoreUntil;
    private NativeList<float> _expireAt;
    private NativeList<byte> _splitFlags;
    private NativeList<int> _splitterHitIdx;
    private TransformAccessArray _transforms;

    // Splitter キャッシュ
    private Transform[] _splitterTransforms;
    private Collider[] _splitterColliders;
    private NativeArray<float3> _splitterPositions;
    private int _splitterCount = 0;

    // 床レベル
    private bool _floorYSet = false;
    private float _floorY = 0f;

    // Controller から一度だけ取り込む設定値
    private bool _configCached = false;
    private GameObject _ballTemplate;
    private float _boundsXMin, _boundsXMax, _boundsZMax, _boundsZMin;
    private float _bounceFactor;
    private float _lifetime;
    private float _splitScaleRatio;
    private int _splitCount;

    bool isActiveSkill3 = false;
    /// <summary>実効分裂数 = 設定値とローグライク強化値の大きい方。報酬「分裂数 2→3」等で増える。</summary>
    private int EffectiveSplitCount()
    {
        int num = isActiveSkill3 ? 3 : 2;
        return Mathf.Max(_splitCount, num);
    } 
    private float _splitSpread;
    private float _spawnXOffset;
    private int _particleGeneration;
    private ParticleSystem _splitParticlePrefab;
    private float _hideParticleXMax;
    private float _hideParticleZMin;
    private string _splitTargetTag = "Splitter";
    private float _ignoreCollisionDuration;
    private bool _enableBallBallCollision;
    private float _maxBallRadius = 0.08f;

    // 位置・スケール追従: pinballRoot が移動/スケールした分の補正値
    private float _scaleFactor = 1f;

    //スキルがアンロックされた際のイベント
    private RoguelikeManager _roguelikeManager;
    private Subject<RoguelikeManager> _initEvent = new Subject<RoguelikeManager>();
    public IObserver<RoguelikeManager> OnInitEvent { get { return _initEvent; } }

    /// <summary>他スクリプト (PinballBallController など) から読める最新の scaleFactor。EnsureConfigured 後に有効。</summary>
    public static float RuntimeScaleFactor { get; private set; } = 1f;

    Vector3 TransformAuthored(Vector3 authored)
    {
        if (_config == null) return authored;
        return _config.TransformAuthoredPoint(authored);
    }

    // 空間ハッシュ (ボール同士の衝突を O(N²) → O(N·k) に高速化)
    private NativeList<int> _cellKeys;
    private NativeList<int> _sortedIdx;
    private NativeArray<int> _cellCounts;
    private NativeArray<int> _cellStart;
    private int _gridWidth, _gridHeight, _gridSize;
    private float _cellSize;
    private float2 _gridOrigin;
    private bool _gridReady = false;

    private bool _initialized = false;
    private JobHandle _lastJobHandle;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        ResolveConfig();
        Initialize();
        _initEvent.Subscribe(manager => _roguelikeManager = manager);
    }

    void Start()
    {
        // この時点でシーン上の gen 0 は OnEnable 済み。初期個数を累積カウンタの起点にする。
        _totalGenerated = PinballBallController.AliveGen0Count;
        // 開始時の所持金ではポップさせない (現ステップを起点に設定)
        _lastMoneyPopStep = CurrentMoneyStep();

        //初期化処理
        Observable.EveryUpdate()
            .Select(_ => _roguelikeManager)
            .Where(target => target != null)
            .First()
            .Subscribe(target =>
            {
                //TODO:各スキルごとにスキルアンロック時の処理を記述していく
                _roguelikeManager.OnUnlockSkillEvent
                .Subscribe(data =>
                {
                    if (data.id == SkillId.PinBall_SplitPinUpgrade)//When a ball hits a split pin, it now splits into 3 instead of 2.
                    {
                        if(data.isActive)
                            isActiveSkill3 = true;

                        Debug.LogError("分裂数が2 -> 3に変更された。");
                    }
                }).AddTo(this);
            })
            .AddTo(this);
    }

    long CurrentMoneyStep()
    {
        if (_config == null) return 0;
        long money = (long)_totalGenerated * _config.moneyPerBall;
        int thr = Mathf.Max(1, _config.moneyPopThreshold);
        return money / thr;
    }

    void ResolveConfig()
    {
        _config = FindFirstObjectByType<PinballBallConfig>();
        if (_config == null)
        {
            // 見つからなければデフォルト値の一時 Config を生成
            var go = new GameObject("[PinballBallConfigDefault]");
            go.hideFlags = HideFlags.HideAndDontSave;
            _config = go.AddComponent<PinballBallConfig>();
        }
    }

    void Initialize()
    {
        if (_initialized) return;
        int cap = _config != null ? _config.initialCapacity : 256;
        _positions = new NativeList<float3>(cap, Allocator.Persistent);
        _velocities = new NativeList<float3>(cap, Allocator.Persistent);
        _radii = new NativeList<float>(cap, Allocator.Persistent);
        _generations = new NativeList<int>(cap, Allocator.Persistent);
        _ignoredSplitterIdx = new NativeList<int>(cap, Allocator.Persistent);
        _ignoreUntil = new NativeList<float>(cap, Allocator.Persistent);
        _expireAt = new NativeList<float>(cap, Allocator.Persistent);
        _splitFlags = new NativeList<byte>(cap, Allocator.Persistent);
        _splitterHitIdx = new NativeList<int>(cap, Allocator.Persistent);
        _cellKeys = new NativeList<int>(cap, Allocator.Persistent);
        _sortedIdx = new NativeList<int>(cap, Allocator.Persistent);
        _transforms = new TransformAccessArray(cap);
        _initialized = true;
    }

    /// <summary>Controller から一度だけ設定を取り込み、Splitter キャッシュを構築する。</summary>
    public void EnsureConfigured(PinballBallController c)
    {
        if (_configCached) return;

        // gen 0 は衝突後に Destroy されるので、永続テンプレートとして非アクティブ複製を保持する
        // 純物理仕様: Rigidbody / SphereCollider / PinballBallController を残したままクローン
        GameObject src = c.gameObject;
        bool wasActive = src.activeSelf;
        src.SetActive(false);
        _ballTemplate = Instantiate(src);
        _ballTemplate.name = "[PinballBallTemplate]";
        _ballTemplate.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(_ballTemplate);
        src.SetActive(wasActive);

        // pinballRoot に追従するスケール倍率 (root が null なら 1)
        _scaleFactor = _config != null ? _config.CurrentScaleFactor : 1f;
        RuntimeScaleFactor = _scaleFactor;

        // authored world 値 → 現在 root ポーズに合わせた world 値へ変換
        Vector3 boundsMin = TransformAuthored(new Vector3(c.manualBoundsXMin, 0f, c.manualBoundsZMin));
        Vector3 boundsMax = TransformAuthored(new Vector3(c.manualBoundsXMax, 0f, c.manualBoundsZMax));
        _boundsXMin = boundsMin.x;
        _boundsXMax = boundsMax.x;
        _boundsZMin = boundsMin.z;
        _boundsZMax = boundsMax.z;

        _maxBallRadius = Mathf.Max(0.001f, c.initialDetectionRadius * _scaleFactor);
        _bounceFactor = c.manualBounceFactor;
        _lifetime = c.manualLifetime;
        _splitScaleRatio = c.splitScaleRatio;
        _splitCount = Mathf.Max(2, c.splitCount);
        _splitSpread = c.splitSpread * _scaleFactor;
        _spawnXOffset = c.spawnXOffset * _scaleFactor;
        _particleGeneration = c.particleGeneration;
        _splitParticlePrefab = c.splitParticlePrefab;
        Vector3 hideRef = TransformAuthored(new Vector3(c.hideParticleXMax, 0f, c.hideParticleZMin));
        _hideParticleXMax = hideRef.x;
        _hideParticleZMin = hideRef.z;
        _splitTargetTag = c.splitTargetTag;
        _ignoreCollisionDuration = c.ignoreCollisionDuration;
        _enableBallBallCollision = c.enableBallBallCollision;

        GameObject[] splitters = GameObject.FindGameObjectsWithTag(_splitTargetTag);
        _splitterCount = splitters.Length;
        _splitterTransforms = new Transform[_splitterCount];
        _splitterColliders = new Collider[_splitterCount];
        _splitterPositions = new NativeArray<float3>(Mathf.Max(1, _splitterCount), Allocator.Persistent);
        for (int i = 0; i < _splitterCount; i++)
        {
            _splitterTransforms[i] = splitters[i].transform;
            _splitterColliders[i] = splitters[i].GetComponent<Collider>();
        }

        // 空間ハッシュのグリッドを構築 (セル = 最大半径 × 2)
        _cellSize = _maxBallRadius * 2f;
        float pad = _cellSize;
        _gridOrigin = new float2(_boundsXMin - pad, _boundsZMin - pad);
        float spanX = (_boundsXMax - _boundsXMin) + 2f * pad;
        float spanZ = (_boundsZMax - _boundsZMin) + 2f * pad;
        _gridWidth = Mathf.Max(1, Mathf.CeilToInt(spanX / _cellSize));
        _gridHeight = Mathf.Max(1, Mathf.CeilToInt(spanZ / _cellSize));
        _gridSize = _gridWidth * _gridHeight;
        _cellCounts = new NativeArray<int>(_gridSize, Allocator.Persistent);
        _cellStart = new NativeArray<int>(_gridSize + 1, Allocator.Persistent);
        _gridReady = true;

        _configCached = true;
    }

    public float FloorY => _floorY;
    public bool IsFloorYSet => _floorYSet;
    public int ActiveBallCount => (_positions.IsCreated ? _positions.Length : 0) + PinballBallController.AliveGen0Count;

    public void SetFloorY(float y)
    {
        _floorY = y;
        _floorYSet = true;
    }

    /// <summary>Splitter Collider から内部 index を取得。未知なら -1。</summary>
    public int GetSplitterIndex(Collider col)
    {
        if (_splitterColliders == null) return -1;
        for (int i = 0; i < _splitterCount; i++)
            if (_splitterColliders[i] == col) return i;
        return -1;
    }

    /// <summary>
    /// 任意世代のボールが Splitter に当たった時に呼ばれる。子ボール (Rigidbody + Collider 付き) を
    /// count 個スポーンし、Unity 物理に任せる (NativeList には登録しない)。
    /// nextGen が particleGeneration 以上なら ParticleBurst にフォールバック。
    /// </summary>
    public void ProduceChildren(PinballBallController source, int parentGen, Vector3 collisionPos, Collider splitterCol, float parentSphereRadius)
    {
        EnsureConfigured(source);
        if (!_floorYSet) SetFloorY(collisionPos.y - parentSphereRadius);

        // 分裂エフェクト (火花 + 効果音)
        if (PinballSplitFXManager.Instance != null) PinballSplitFXManager.Instance.OnSplit(collisionPos);

        int nextGen = parentGen + 1;
        if (_particleGeneration >= 0 && nextGen >= _particleGeneration && _splitParticlePrefab != null)
        {
            SpawnParticleBurst(collisionPos);
            return;
        }

        int count = EffectiveSplitCount();
        // ボールがスケール付き親の中にいる場合 (例: shop_ball の ball_spawn_point の中) に
        // localScale だけで計算すると親の lossyScale 分を見落として子が極端に小さくなる。
        // 子ボールは scene root (親なし) で生成されるので、ワールドスケール (lossyScale) を直接使う。
        Vector3 parentWorldScale = source.transform.lossyScale;
        Vector3 childScale = parentWorldScale * _splitScaleRatio;
        // 世代が深くなるほどスポーン左右オフセットも縮小 (親スケールに追従)
        float xOffset = _spawnXOffset * Mathf.Pow(_splitScaleRatio, parentGen);
        // 親の現在 gravity を引き継ぐ (コンベアで上書きされた状態を子も保持)
        Vector3 inheritedGravity = source != null ? source.gravity : new Vector3(0f, -9.81f, 9.81f);
        // Enforce Pin で上昇した発色レベルも子に継承する
        int inheritedEnforceLevel = source != null ? source.enforceLevel : 0;
        // ボール固有の価値も子に継承 (各子は親と同じ価値で枝分かれする)
        float inheritedValue = source != null ? source.currentValue : 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (count == 1) ? 0f : ((float)i / (count - 1)) * 2f - 1f;
            Vector3 spawnPos = new Vector3(
                collisionPos.x + xOffset * t,
                collisionPos.y,
                collisionPos.z
            );
            Vector3 vel = new Vector3(_splitSpread * t, 0f, 0f);
            SpawnPhysicsChild(spawnPos, vel, childScale, nextGen, splitterCol, inheritedGravity, inheritedEnforceLevel, inheritedValue);
        }
    }

    /// <summary>
    /// 互換用エイリアス: 旧 API (gen 0 から呼ばれる前提) を ProduceChildren へ転送。
    /// </summary>
    public void ProduceGen1Children(PinballBallController source, Vector3 collisionPos, Collider splitterCol, float gen0SphereRadius)
    {
        ProduceChildren(source, source != null ? source.generation : 0, collisionPos, splitterCol, gen0SphereRadius);
    }

    /// <summary>テンプレートから RB + Collider + Controller 付きの子ボールを 1 個生成する。NativeList には登録しない。</summary>
    void SpawnPhysicsChild(Vector3 pos, Vector3 vel, Vector3 localScale, int generation, Collider ignoredSplitter, Vector3 inheritedGravity, int inheritedEnforceLevel, float inheritedValue = 0f)
    {
        GameObject child = Instantiate(_ballTemplate, pos, Quaternion.identity);
        child.hideFlags = HideFlags.None;
        child.transform.localScale = localScale;
        child.SetActive(true);

        var ctrl = child.GetComponent<PinballBallController>();
        if (ctrl != null)
        {
            ctrl.generation = generation;
            // 親の現在 gravity を継承 (Awake で Config から再初期化されるが、ここで上書き)
            ctrl.gravity = inheritedGravity;
            // Enforce 発色レベルを継承 + 即時 emission 反映
            ctrl.enforceLevel = inheritedEnforceLevel;
            ctrl.ApplyBallEmission();
            // ボール固有の価値を継承
            ctrl.currentValue = inheritedValue;
        }

        // 生成元 splitter とは衝突しないよう永続的に Ignore (このボールが destroy されたらペアも消える)
        var childCol = child.GetComponent<Collider>();
        if (childCol != null && ignoredSplitter != null)
            Physics.IgnoreCollision(childCol, ignoredSplitter, true);

        // 初速 (Awake 後の rb を取り直し)
        var rb = child.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = vel;

        _totalGenerated++;
    }

    void Register(Transform t, float3 pos, float3 vel, float radius, int generation, int ignoredSplitter)
    {
        if (!_initialized) Initialize();
        _positions.Add(pos);
        _velocities.Add(new float3(vel.x, 0f, vel.z));
        _radii.Add(radius);
        _generations.Add(generation);
        _ignoredSplitterIdx.Add(ignoredSplitter);
        _ignoreUntil.Add(Time.time + _ignoreCollisionDuration);
        _expireAt.Add(_lifetime > 0f ? Time.time + _lifetime : float.PositiveInfinity);
        _splitFlags.Add(0);
        _splitterHitIdx.Add(-1);
        _transforms.Add(t);
        _totalGenerated++;
    }

    void Unregister(int index)
    {
        _positions.RemoveAtSwapBack(index);
        _velocities.RemoveAtSwapBack(index);
        _radii.RemoveAtSwapBack(index);
        _generations.RemoveAtSwapBack(index);
        _ignoredSplitterIdx.RemoveAtSwapBack(index);
        _ignoreUntil.RemoveAtSwapBack(index);
        _expireAt.RemoveAtSwapBack(index);
        _splitFlags.RemoveAtSwapBack(index);
        _splitterHitIdx.RemoveAtSwapBack(index);
        _transforms.RemoveAtSwapBack(index);
    }

    void Update()
    {
        if (!_initialized || !_configCached || !_floorYSet) return;
        int count = _positions.Length;
        if (count == 0) return;

        for (int i = 0; i < _splitterCount; i++)
        {
            if (_splitterTransforms[i] == null) continue;
            _splitterPositions[i] = _splitterTransforms[i].position;
        }
        for (int i = 0; i < count; i++) _splitFlags[i] = 0;

        float dt = Time.deltaTime;
        // Config に設定された gravity ベクトルの XZ 成分を用いる (Y は floor 固定なので無視)
        // スケールと同じ倍率で重力強度も引き上げる (視覚的タイミングを維持)
        Vector3 effGravity = _config != null ? _config.EffectiveGravity : new Vector3(0f, -Mathf.Abs(Physics.gravity.y), Mathf.Abs(Physics.gravity.y));

        var integrateJob = new IntegrateJob
        {
            positions = _positions.AsArray(),
            velocities = _velocities.AsArray(),
            radii = _radii.AsArray(),
            dt = dt,
            gravityX = effGravity.x * _scaleFactor,
            gravityZ = effGravity.z * _scaleFactor,
            floorY = _floorY,
        };
        JobHandle h = integrateJob.Schedule(count, 64);

        var boundsJob = new BoundsBounceJob
        {
            positions = _positions.AsArray(),
            velocities = _velocities.AsArray(),
            radii = _radii.AsArray(),
            xmin = _boundsXMin,
            xmax = _boundsXMax,
            zmax = _boundsZMax,
            bounce = _bounceFactor,
        };
        h = boundsJob.Schedule(count, 64, h);

        if (_enableBallBallCollision && count > 1 && _gridReady)
        {
            _cellKeys.ResizeUninitialized(count);
            _sortedIdx.ResizeUninitialized(count);

            var cellKeysJob = new ComputeCellKeysJob
            {
                positions = _positions.AsArray(),
                cellKeys = _cellKeys.AsArray(),
                sortedIdx = _sortedIdx.AsArray(),
                gridOrigin = _gridOrigin,
                cellSize = _cellSize,
                gridWidth = _gridWidth,
                gridHeight = _gridHeight,
            };
            h = cellKeysJob.Schedule(count, 64, h);

            var buildJob = new BuildGridJob
            {
                cellKeys = _cellKeys.AsArray(),
                cellCounts = _cellCounts,
                cellStart = _cellStart,
                sortedIdx = _sortedIdx.AsArray(),
                count = count,
                gridSize = _gridSize,
            };
            h = buildJob.Schedule(h);

            var separateJob = new BallBallSeparateSpatialJob
            {
                positions = _positions.AsArray(),
                velocities = _velocities.AsArray(),
                radii = _radii.AsArray(),
                sortedIdx = _sortedIdx.AsArray(),
                cellStart = _cellStart,
                count = count,
                gridWidth = _gridWidth,
                gridHeight = _gridHeight,
                cellSize = _cellSize,
                gridOrigin = _gridOrigin,
                restitution = _config.ballBallRestitution,
                // 重なり距離 (slop) は長さ次元なのでスケール倍率に追従
                positionSlop = _config.ballBallPositionSlop * _scaleFactor,
                positionCorrection = _config.ballBallPositionCorrection,
            };
            h = separateJob.Schedule(h);
        }

        var splitterJob = new SplitterDetectJob
        {
            positions = _positions.AsArray(),
            radii = _radii.AsArray(),
            ignoredSplitterIdx = _ignoredSplitterIdx.AsArray(),
            ignoreUntil = _ignoreUntil.AsArray(),
            splitterPositions = _splitterPositions,
            splitterCount = _splitterCount,
            currentTime = Time.time,
            splitFlags = _splitFlags.AsArray(),
            splitterHitIdx = _splitterHitIdx.AsArray(),
        };
        h = splitterJob.Schedule(count, 64, h);

        var lifetimeJob = new LifetimeCheckJob
        {
            expireAt = _expireAt.AsArray(),
            currentTime = Time.time,
            splitFlags = _splitFlags.AsArray(),
        };
        h = lifetimeJob.Schedule(count, 64, h);

        var applyJob = new ApplyTransformsJob
        {
            positions = _positions.AsArray(),
        };
        _lastJobHandle = applyJob.Schedule(_transforms, h);
        _lastJobHandle.Complete();

        // 結果処理 (逆順で swap-remove 安全に)
        for (int i = _positions.Length - 1; i >= 0; i--)
        {
            byte flag = _splitFlags[i];
            if (flag == 1) SpawnChildrenAndDestroy(i);
            else if (flag == 2) DestroyAt(i);
        }
    }

    void LateUpdate()
    {
        if (IsResetKeyPressed())
        {
            ResetGame();
            return;
        }

        int managed = _initialized ? _positions.Length : 0;
        int total = managed + PinballBallController.AliveGen0Count;
        if (_config != null)
        {
            _config.debugManagedCount = managed;
            _config.debugTotalCount = total;
            _config.debugTotalGenerated = _totalGenerated;

            // 所持金ポップ判定: ステップが 1 つ以上進んだら演出開始
            long step = CurrentMoneyStep();
            if (step > _lastMoneyPopStep)
            {
                _lastMoneyPopStep = step;
                _popStartTime = Time.unscaledTime;
            }
        }

        if (_config == null || !_config.logBallCount) return;
        if (Time.unscaledTime < _nextLogTime) return;
        _nextLogTime = Time.unscaledTime + Mathf.Max(0.05f, _config.logInterval);
        Debug.Log($"[PinballBall] total={total} (gen0={PinballBallController.AliveGen0Count}, gen≥1={managed})");
    }

    bool IsResetKeyPressed()
    {
        KeyCode key = _config != null ? _config.resetKey : KeyCode.R;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            var k = KeyCodeToKey(key);
            if (k != Key.None && Keyboard.current[k].wasPressedThisFrame) return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(key)) return true;
#endif
        return false;
    }

#if ENABLE_INPUT_SYSTEM
    static Key KeyCodeToKey(KeyCode code)
    {
        // 主要キーのみサポート。必要に応じて拡張。
        if (code >= KeyCode.A && code <= KeyCode.Z) return Key.A + (int)(code - KeyCode.A);
        if (code >= KeyCode.Alpha0 && code <= KeyCode.Alpha9) return Key.Digit0 + (int)(code - KeyCode.Alpha0);
        switch (code)
        {
            case KeyCode.Space: return Key.Space;
            case KeyCode.Return: return Key.Enter;
            case KeyCode.Escape: return Key.Escape;
            case KeyCode.Tab: return Key.Tab;
            default: return Key.None;
        }
    }
#endif

    public void ResetGame()
    {
        _lastJobHandle.Complete();
        Scene active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.buildIndex);
    }

/*
    void OnGUI()
    {
        if (_config == null || !_config.showMoneyLabel) return;
        int fontSize = _config.moneyFontSize;
        if (_moneyStyle == null || _moneyStyle.fontSize != fontSize)
        {
            _moneyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = TextAnchor.UpperRight,
                fontStyle = FontStyle.Bold,
            };
        }
        _moneyStyle.normal.textColor = _config.moneyColor;

        long money = (long)_totalGenerated * _config.moneyPerBall;
        string text = $"{_config.moneyLabelPrefix}{money:N0}{_config.moneyLabelSuffix}";

        // 文字サイズに応じて矩形サイズも追従させる
        Vector2 size = _moneyStyle.CalcSize(new GUIContent(text));
        float width = size.x + 8f;
        float height = size.y + 8f;
        Rect rect = new Rect(Screen.width - width - _config.moneyPadding.x, _config.moneyPadding.y, width, height);

        // ポップ演出: 経過時間に応じて ease-out で 1.0 → (1 + bonus) → 1.0 に戻る
        float scale = 1f;
        float duration = Mathf.Max(0.01f, _config.moneyPopDuration);
        float elapsed = Time.unscaledTime - _popStartTime;
        if (elapsed >= 0f && elapsed < duration)
        {
            float t = elapsed / duration;
            // 立ち上がりをすばやく (0→0.15) → ゆっくり減衰 させるため二段構え
            float rise = Mathf.Clamp01(t / 0.15f);
            float fall = Mathf.Pow(1f - Mathf.Clamp01((t - 0.15f) / 0.85f), 2f);
            float curve = (t < 0.15f) ? rise : fall;
            scale = 1f + (_config.moneyPopScale - 1f) * curve;
        }

        // 右上を基準にスケール (画面外に出ないよう右端をピボットに)
        Matrix4x4 prevMatrix = GUI.matrix;
        if (!Mathf.Approximately(scale, 1f))
        {
            Vector2 pivot = new Vector2(rect.xMax, rect.y);
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), pivot);
        }

        // 影 (視認性向上) — フォントサイズに比例してオフセットを調整
        float shadow = Mathf.Max(2f, fontSize * 0.05f);
        var prevColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.Label(new Rect(rect.x + shadow, rect.y + shadow, rect.width, rect.height), text, _moneyStyle);
        GUI.color = prevColor;

        GUI.Label(rect, text, _moneyStyle);

        GUI.matrix = prevMatrix;
    }
*/

    void SpawnChildrenAndDestroy(int index)
    {
        int parentGen = _generations[index];
        int nextGen = parentGen + 1;
        float3 parentPos = _positions[index];
        float parentRadius = _radii[index];
        int hitSplitterIdx = _splitterHitIdx[index];

        // 分裂エフェクト (火花 + 効果音)
        Vector3 worldParentPos = new Vector3(parentPos.x, parentPos.y, parentPos.z);
        if (PinballSplitFXManager.Instance != null) PinballSplitFXManager.Instance.OnSplit(worldParentPos);

        if (_particleGeneration >= 0 && nextGen >= _particleGeneration && _splitParticlePrefab != null)
        {
            SpawnParticleBurst(worldParentPos);
            DestroyAt(index);
            return;
        }

        int count = EffectiveSplitCount();
        float childRadius = parentRadius * _splitScaleRatio;
        // 一貫性のためここも lossyScale で計算 (gen >=1 は通常 scene root にいるが安全側に倒す)
        Vector3 parentWorldScale = _transforms[index].lossyScale;
        Vector3 childScale = parentWorldScale * _splitScaleRatio;
        float xOffset = _spawnXOffset * Mathf.Pow(0.5f, parentGen);

        for (int i = 0; i < count; i++)
        {
            float t = (count == 1) ? 0f : ((float)i / (count - 1)) * 2f - 1f;
            Vector3 spawnPos = new Vector3(
                parentPos.x + xOffset * t,
                _floorY + childRadius,
                parentPos.z
            );
            Vector3 vel = new Vector3(_splitSpread * t, 0f, 0f);
            // 純物理仕様への切替後はこのパスは到達しない (NativeList が空のため) が、
            // コンパイル維持のため SpawnPhysicsChild にフォワード (gravity は Config 既定を渡す)
            Collider ignored = (hitSplitterIdx >= 0 && hitSplitterIdx < _splitterCount) ? _splitterColliders[hitSplitterIdx] : null;
            Vector3 defaultGravity = _config != null ? _config.EffectiveGravity : new Vector3(0f, -9.81f, 9.81f);
            // NativeList 経由のため親 ctrl 参照は無い。価値継承は不可なので 0 を渡す。
            SpawnPhysicsChild(spawnPos, vel, childScale, nextGen, ignored, defaultGravity, 0, 0f);
        }

        DestroyAt(index);
    }

    void DestroyAt(int index)
    {
        GameObject go = _transforms[index].gameObject;
        Unregister(index);
        Destroy(go);
    }

    public void SpawnParticleBurst(Vector3 position)
    {
        if (_splitParticlePrefab == null) return;
        if (position.x <= _hideParticleXMax && position.z >= _hideParticleZMin) return;
        ParticleSystem ps = Instantiate(_splitParticlePrefab, position, _splitParticlePrefab.transform.rotation);
        var culler = ps.gameObject.AddComponent<ParticleRegionCuller>();
        culler.hideXMax = _hideParticleXMax;
        culler.hideZMin = _hideParticleZMin;
        ps.Play();
    }

    void OnDestroy()
    {
        _lastJobHandle.Complete();
        if (_ballTemplate != null) Destroy(_ballTemplate);
        if (_positions.IsCreated) _positions.Dispose();
        if (_velocities.IsCreated) _velocities.Dispose();
        if (_radii.IsCreated) _radii.Dispose();
        if (_generations.IsCreated) _generations.Dispose();
        if (_ignoredSplitterIdx.IsCreated) _ignoredSplitterIdx.Dispose();
        if (_ignoreUntil.IsCreated) _ignoreUntil.Dispose();
        if (_expireAt.IsCreated) _expireAt.Dispose();
        if (_splitFlags.IsCreated) _splitFlags.Dispose();
        if (_splitterHitIdx.IsCreated) _splitterHitIdx.Dispose();
        if (_cellKeys.IsCreated) _cellKeys.Dispose();
        if (_sortedIdx.IsCreated) _sortedIdx.Dispose();
        if (_cellCounts.IsCreated) _cellCounts.Dispose();
        if (_cellStart.IsCreated) _cellStart.Dispose();
        if (_transforms.isCreated) _transforms.Dispose();
        if (_splitterPositions.IsCreated) _splitterPositions.Dispose();
        if (_instance == this) _instance = null;
    }

    // ---------------- Jobs ----------------

    [BurstCompile]
    struct IntegrateJob : IJobParallelFor
    {
        public NativeArray<float3> positions;
        public NativeArray<float3> velocities;
        [ReadOnly] public NativeArray<float> radii;
        public float dt;
        public float gravityX;
        public float gravityZ;
        public float floorY;

        public void Execute(int i)
        {
            float3 v = velocities[i];
            v.y = 0f;
            v.x += gravityX * dt;
            v.z += gravityZ * dt;
            velocities[i] = v;

            float3 p = positions[i];
            p.x += v.x * dt;
            p.z += v.z * dt;
            p.y = floorY + radii[i];
            positions[i] = p;
        }
    }

    [BurstCompile]
    struct BoundsBounceJob : IJobParallelFor
    {
        public NativeArray<float3> positions;
        public NativeArray<float3> velocities;
        [ReadOnly] public NativeArray<float> radii;
        public float xmin, xmax, zmax, bounce;

        public void Execute(int i)
        {
            float3 p = positions[i];
            float3 v = velocities[i];
            float r = radii[i];

            // 球面が境界に触れる位置 (中心 = 境界 ± 半径) でクランプ
            float xLo = xmin + r;
            float xHi = xmax - r;
            float zHi = zmax - r;

            if (p.x < xLo) { p.x = xLo; if (v.x < 0f) v.x = -v.x * bounce; }
            else if (p.x > xHi) { p.x = xHi; if (v.x > 0f) v.x = -v.x * bounce; }
            if (p.z > zHi) { p.z = zHi; if (v.z > 0f) v.z = -v.z * bounce; }

            positions[i] = p;
            velocities[i] = v;
        }
    }

    /// <summary>
    /// 各ボールの所属セル key = cz * gridWidth + cx を求め、sortedIdx を i で初期化する。
    /// </summary>
    [BurstCompile]
    struct ComputeCellKeysJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> positions;
        [WriteOnly] public NativeArray<int> cellKeys;
        [WriteOnly] public NativeArray<int> sortedIdx;
        public float2 gridOrigin;
        public float cellSize;
        public int gridWidth;
        public int gridHeight;

        public void Execute(int i)
        {
            float3 p = positions[i];
            int cx = (int)math.floor((p.x - gridOrigin.x) / cellSize);
            int cz = (int)math.floor((p.z - gridOrigin.y) / cellSize);
            cx = math.clamp(cx, 0, gridWidth - 1);
            cz = math.clamp(cz, 0, gridHeight - 1);
            cellKeys[i] = cz * gridWidth + cx;
            sortedIdx[i] = i;
        }
    }

    /// <summary>
    /// カウンティングソート方式で cellStart を構築し、sortedIdx をセル順に並べ替える。
    /// cellStart[k]..cellStart[k+1] が cell k に所属するボールの sortedIdx 範囲となる。
    /// </summary>
    [BurstCompile]
    struct BuildGridJob : IJob
    {
        [ReadOnly] public NativeArray<int> cellKeys;
        public NativeArray<int> cellCounts;
        public NativeArray<int> cellStart;
        public NativeArray<int> sortedIdx;
        public int count;
        public int gridSize;

        public void Execute()
        {
            for (int k = 0; k < gridSize; k++) cellCounts[k] = 0;
            for (int i = 0; i < count; i++) cellCounts[cellKeys[i]]++;

            int sum = 0;
            for (int k = 0; k < gridSize; k++)
            {
                cellStart[k] = sum;
                sum += cellCounts[k];
            }
            cellStart[gridSize] = sum;

            for (int k = 0; k < gridSize; k++) cellCounts[k] = 0;
            for (int i = 0; i < count; i++)
            {
                int k = cellKeys[i];
                int slot = cellStart[k] + cellCounts[k];
                sortedIdx[slot] = i;
                cellCounts[k]++;
            }
        }
    }

    /// <summary>
    /// 空間グリッドを使って 3x3 近傍セルだけを走査し、O(N·k) で位置分離 + 反発を行う。
    /// cell size = 2 * maxBallRadius にしているため、3x3 セルに入るボールだけが最大接触相手になる。
    /// </summary>
    [BurstCompile]
    struct BallBallSeparateSpatialJob : IJob
    {
        public NativeArray<float3> positions;
        public NativeArray<float3> velocities;
        [ReadOnly] public NativeArray<float> radii;
        [ReadOnly] public NativeArray<int> sortedIdx;
        [ReadOnly] public NativeArray<int> cellStart;
        public int count;
        public int gridWidth;
        public int gridHeight;
        public float cellSize;
        public float2 gridOrigin;
        public float restitution;
        public float positionSlop;
        public float positionCorrection;

        public void Execute()
        {
            for (int i = 0; i < count; i++)
            {
                float3 pi = positions[i];
                float3 vi = velocities[i];
                float ri = radii[i];

                int cx = (int)math.floor((pi.x - gridOrigin.x) / cellSize);
                int cz = (int)math.floor((pi.z - gridOrigin.y) / cellSize);
                cx = math.clamp(cx, 0, gridWidth - 1);
                cz = math.clamp(cz, 0, gridHeight - 1);

                int zMin = math.max(0, cz - 1);
                int zMax = math.min(gridHeight - 1, cz + 1);
                int xMin = math.max(0, cx - 1);
                int xMax = math.min(gridWidth - 1, cx + 1);

                for (int zz = zMin; zz <= zMax; zz++)
                {
                    int rowBase = zz * gridWidth;
                    for (int xx = xMin; xx <= xMax; xx++)
                    {
                        int key = rowBase + xx;
                        int start = cellStart[key];
                        int end = cellStart[key + 1];
                        for (int s = start; s < end; s++)
                        {
                            int j = sortedIdx[s];
                            if (j <= i) continue;
                            float3 pj = positions[j];
                            float3 vj = velocities[j];
                            float rj = radii[j];
                            float dx = pj.x - pi.x;
                            float dz = pj.z - pi.z;
                            float distSq = dx * dx + dz * dz;
                            float minDist = ri + rj;
                            if (distSq >= minDist * minDist || distSq < 1e-8f) continue;

                            float dist = math.sqrt(distSq);
                            float nx = dx / dist;
                            float nz = dz / dist;

                            float penetration = minDist - dist;
                            float corr = math.max(0f, penetration - positionSlop) * positionCorrection * 0.5f;
                            if (corr > 0f)
                            {
                                pi.x -= nx * corr;
                                pi.z -= nz * corr;
                                pj.x += nx * corr;
                                pj.z += nz * corr;
                            }

                            float rvx = vj.x - vi.x;
                            float rvz = vj.z - vi.z;
                            float velAlongNormal = rvx * nx + rvz * nz;
                            if (velAlongNormal < 0f)
                            {
                                float jImpulse = -(1f + restitution) * velAlongNormal * 0.5f;
                                float ix = jImpulse * nx;
                                float iz = jImpulse * nz;
                                vi.x -= ix;
                                vi.z -= iz;
                                vj.x += ix;
                                vj.z += iz;
                            }

                            positions[j] = pj;
                            velocities[j] = vj;
                        }
                    }
                }
                positions[i] = pi;
                velocities[i] = vi;
            }
        }
    }

    [BurstCompile]
    struct SplitterDetectJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> positions;
        [ReadOnly] public NativeArray<float> radii;
        [ReadOnly] public NativeArray<int> ignoredSplitterIdx;
        [ReadOnly] public NativeArray<float> ignoreUntil;
        [ReadOnly] public NativeArray<float3> splitterPositions;
        public int splitterCount;
        public float currentTime;
        [NativeDisableParallelForRestriction] public NativeArray<byte> splitFlags;
        [NativeDisableParallelForRestriction] public NativeArray<int> splitterHitIdx;

        public void Execute(int i)
        {
            if (splitFlags[i] != 0) return;
            float3 p = positions[i];
            float r = radii[i];
            float r2 = r * r;
            int ignored = ignoredSplitterIdx[i];
            float ignoreEnd = ignoreUntil[i];
            bool stillIgnoring = currentTime < ignoreEnd;

            for (int j = 0; j < splitterCount; j++)
            {
                if (stillIgnoring && j == ignored) continue;
                float3 s = splitterPositions[j];
                float dx = s.x - p.x;
                float dz = s.z - p.z;
                if (dx * dx + dz * dz < r2)
                {
                    splitFlags[i] = 1;
                    splitterHitIdx[i] = j;
                    return;
                }
            }
        }
    }

    [BurstCompile]
    struct LifetimeCheckJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> expireAt;
        public float currentTime;
        [NativeDisableParallelForRestriction] public NativeArray<byte> splitFlags;

        public void Execute(int i)
        {
            if (splitFlags[i] != 0) return;
            if (currentTime >= expireAt[i]) splitFlags[i] = 2;
        }
    }

    [BurstCompile]
    struct ApplyTransformsJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<float3> positions;

        public void Execute(int i, TransformAccess t)
        {
            float3 p = positions[i];
            t.position = new Vector3(p.x, p.y, p.z);
        }
    }
}
