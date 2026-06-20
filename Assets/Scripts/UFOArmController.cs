using UnityEngine;

/// <summary>
/// UFOキャッチャーの状態機械と全体制御。
/// 空のGameObjectにアタッチし、各Transformをインスペクターで設定してください。
/// </summary>
public class UFOArmController : MonoBehaviour
{
    public enum ArmState { Idle, Moving, OpeningClaw, Descending, PostCollisionDescending, Grabbing, Ascending }

    // ─────────────────────────────────────
    [Header("アーム参照")]
    [Tooltip("UFOキャッチャー本体の大元のオブジェクト（枠の基準になります）")]
    public Transform machineRoot;

    [Tooltip("アーム全体を動かす親Transform（オブジェクト3 か、3を子に持つ空オブジェクト）")]
    public Transform armRoot;

    [Tooltip("縦レール（Object 1）: Z方向をアームに追従させる")]
    public Transform rail1;

    [Tooltip("横レール（Object 2）: X方向をアームに追従させる")]
    public Transform rail2;

    [Tooltip("StretchRope が付いているオブジェクト（6）")]
    public StretchRope stretchRope;

    // ─────────────────────────────────────
    [Header("XZ 移動設定")]
    [Tooltip("レバー入力に対するアーム移動速度")]
    public float moveSpeed = 3f;
    [Header("移動範囲の指定（Sceneビューで赤い枠が見えます）")]
    public Vector2 playAreaCenter = new Vector2(0f, 0f); // 中心からのズレ（Xが左右、Yが奥手前）
    public Vector2 playAreaSize   = new Vector2(9f, 9f);

    // ─────────────────────────────────────
    [Header("爪（finger）設定")]
    [Tooltip("finger.001〜.004 を配列に設定してください")]
    public Transform[] fingerParts;
    [Tooltip("爪が開いたときのローカルX軸回転角度（開き幅）")]
    public float fingerOpenAngle = 40f;
    [Tooltip("逆に閉まる方向に動いてしまう指がある場合、ここのListにチェックを入れて反転させてください（0が001用、1が002用など）")]
    public bool[] invertFingerAngle;

    [Tooltip("爪ごとの開く角度の微調整（X, Y, Z）。001と002が上にズレる場合などは、ここのYやZの数値を少し（-20〜20など）変えて手動で調整できます。")]
    public Vector3[] fingerAngleOffsets;

    [Tooltip("指の開閉スピード")]
    public float fingerSpeed = 4f;

    [Tooltip("爪の閉じ切り度合い（1.0で完全に閉じる。0.8など少し小さくすると、完全に閉じきらずに隙間を残すことで、中のコインが過度に圧迫されてはみ出したり弾け飛んだりするのを防ぎます）")]
    [Range(0.1f, 1.0f)]
    public float closeLimitRatio = 1.0f;

    [Header("【新規】爪の開いた状態の直接指定")]
    [Tooltip("チェックを入れると、下のリストの座標・角度を開いた状態として使用します")]
    public bool useCustomOpenTransform = true;
    
    public Vector3[] customOpenLocalPositions = new Vector3[] {
        new Vector3(0.5732661f, 0.9492433f, 3.3848f),
        new Vector3(0.6466999f, 0.948375f, 3.315662f),
        new Vector3(0.6466999f, 0.9295762f, 3.459017f),
        new Vector3(0.7165977f, 0.9459755f, 3.3848f)
    };
    
    public Vector3[] customOpenLocalRotations = new Vector3[] {
        new Vector3(-124.606f, 90f, 0f),
        new Vector3(-122.817f, 0f, 0f),
        new Vector3(-52.183f, 0f, 0f),
        new Vector3(-56.53f, 90f, 0f)
    };

    [Header("【新規】爪の Hinge Joint 物理開閉設定")]
    [Tooltip("オンにすると、Transform直接書き換えではなく、HingeJointのMotorを使って物理的に開閉します（爪にHingeJointとRigidbodyが必要です）")]
    public bool useHingeJointPhysics = false;

    [Tooltip("開閉時のモーターの目標回転速度（度/秒）")]
    public float jointMotorSpeed = 150f;

    [Tooltip("開閉時のモーターの最大出力トルク")]
    public float jointMotorForce = 50f;

    private HingeJoint[] _fingerJoints;

    // ─────────────────────────────────────
    [Header("昇降設定")]
    [Tooltip("自動下降時の StretchRope の速度倍率")]
    public float descentSpeedMultiplier = 1.5f;
    [Tooltip("掴んでから上昇を開始するまでの待機秒数")]
    public float grabWaitSeconds = 0.5f;
    [Tooltip("何かにぶつかった後、さらに下降を続ける秒数（コインをしっかり掴むため）")]
    public float postCollisionDescentSeconds = 0.15f;
    [Tooltip("指定されたコライダーに入った場合、追加下降をスキップして即座に爪を閉じて上昇します")]
    public Collider immediateGrabArea;

    [Header("【新規】ClawCarrieZone / Claw_RiseCollider 接触設定")]
    [Tooltip("アーム側の当たり判定ゾーン（ClawCarrieZone等）")]
    public Collider clawCarrieZone;
    [Tooltip("ステージ側の判定コライダー（Claw_RiseCollider等）")]
    public Collider clawRiseCollider;

    [Header("【新規】物理ジョイント揺れ（Sway）設定")]
    [Tooltip("アームを吊り下げている Configurable Joint")]
    public ConfigurableJoint swayJoint;
    [Tooltip("移動時の最大揺れ角度")]
    public float swayRangeAngle = 25f;
    [Tooltip("非移動時にジョイントを締めてまっすぐに戻す速さ")]
    public float jointStabilizeSpeed = 10f;
    [Tooltip("下降時にジョイントの揺れを抑えて垂直に安定化させるか（オフの場合、下降中も揺れます）")]
    public bool stabilizeOnDescent = false;
    [Tooltip("揺れを垂直（中心）に引き戻すバネの強さ（値が小さいほど大きく揺れ、大きいほど引き戻しが強くなります）")]
    public float jointSpringForce = 2f;
    [Tooltip("揺れのバネのダンパー（揺れの収束のしやすさ）")]
    public float jointSpringDamper = 1f;

    [Header("コイン最適化解除（WakeUp）設定")]
    [Tooltip("アームが下降する際、どれくらいの範囲のコインを叩き起こすか")]
    public float wakeUpRadius = 1.0f;
    [Tooltip("指（爪パーツ）のローカル座標から見た、判定範囲の中心オフセット")]
    public Vector3 wakeUpOffset = Vector3.zero;
    [Tooltip("アーム動作中（降下〜上昇中）に、アーム直下のコインを起こす判定の長さ（下方向の距離）")]
    public float wakeUpDownwardsLength = 0.3f;
    [Tooltip("アーム動作中（降下〜上昇中）に、アーム直下のコインを起こす判定の横幅（半径）")]
    public float wakeUpDownwardsWidth = 1.2f;
    [Tooltip("アーム動作中（降下〜上昇中）に、アーム直下のコインを起こす判定の中心オフセット")]
    public Vector3 wakeUpDownwardsOffset = Vector3.zero;
    [Tooltip("叩き起こし処理を実行する間隔（秒）。処理落ちを防ぐため毎フレームは行いません")]
    public float wakeUpInterval = 0.2f;

    [Header("【新規】第3アーム用 吸着(マグネット)機能")]
    [Tooltip("オンにすると、爪が閉じる時と上昇する時に周囲のコインを吸着します")]
    public bool isMagnetMode = false;
    [Tooltip("吸着する範囲（半径）")]
    public float magnetRadius = 3.0f;
    [Tooltip("中心に引き寄せる力")]
    public float magnetForce = 50f;

    [Header("【新規】降下衝突時のコインがさがさ効果音")]
    [Tooltip("アームがコインの山に衝突した時のがさがさ音")]
    [SerializeField] private AudioClip descentRustleSound;
    [Tooltip("がさがさ音の最大音量 (1.0より大きい値で音量増幅可能)")]
    [Range(0f, 10f)]
    [SerializeField] private float rustleVolume = 0.8f;
    [Tooltip("がさがさ音を鳴らすために必要な最低コイン枚数")]
    [SerializeField] private int minCoinsForRustle = 3;
    [Tooltip("がさがさ音用のコイン検知半径")]
    [SerializeField] private float rustleDetectRadius = 1.5f;

    [Header("【新規】音量増幅設定（音が小さい場合）")]
    [Tooltip("音源を重ねて再生して音量を限界突破させます（1で通常、2で2倍、3で3倍）")]
    [Range(1, 4)]
    [SerializeField] private int volumeBoost = 1;

    private AudioSource _audioSourceForJingle;

    // ─────────────────────────────────────
    // 内部状態
    private ArmState _state = ArmState.Idle;
    public ArmState CurrentState => _state;
    private Vector2  _leverInput;
    private Vector3  _machineBasePos; // スクリプトがついているオブジェクトの初期座標
    private Vector3  _armInitialPos;
    private Vector3  _rail1InitialPos;
    private Vector3  _rail2InitialPos;
    private Vector3  _visualOffset; // ピボットと実際の見た目の中心（ロープ）とのズレ
    private float    _stateTimer; // 様々な待機タイマー兼用
    private float    _wakeUpTimer;
    private Rigidbody _armRigidbody;

    private Quaternion[] _fingerDefaultRot;
    private Quaternion[] _fingerOpenRot;
    private Quaternion[] _fingerCurrentBaseRot; // 開閉の純粋な回転を保持

    private Vector3[] _fingerDefaultPos;
    private Vector3[] _fingerOpenPos;
    private Vector3[] _fingerCurrentBasePos; // 開閉の純粋な座標を保持

    private bool         _wantFingerOpen = false;
    public bool WantFingerOpen => _wantFingerOpen;

    /// <summary>
    /// アームが下降・掴み・上昇などの一連の動作中（Busy）であるかどうか。
    /// </summary>
    public bool IsBusy => _state != ArmState.Idle && _state != ArmState.Moving;

    // ─────────────────────────────────────


    // ─────────────────────────────────────
    void Start()
    {
        if (stretchRope == null)
        {
            stretchRope = GetComponentInChildren<StretchRope>(true);
        }

        // 基準点を明確にする（指定があればそれ、なければ自分自身）
        _machineBasePos = (machineRoot != null) ? machineRoot.position : transform.position;

        if (armRoot != null)
        {
            _armInitialPos = armRoot.position;
            _armRigidbody = armRoot.GetComponent<Rigidbody>();
            if (_armRigidbody == null)
            {
                _armRigidbody = armRoot.gameObject.AddComponent<Rigidbody>();
                _armRigidbody.isKinematic = true;
            }
        }
        if (armRoot != null && stretchRope != null)
        {
            // armRoot（ピボット）と実際の見た目の中心（ロープ）のズレを計算
            _visualOffset = stretchRope.transform.position - armRoot.position;
        }
        else
        {
            _visualOffset = Vector3.zero;
        }

        // 爪の初期/開いた回転と座標を記録
        InitializeFingers();
        InitializeFingerJoints();

        // 【新規】ClawCarrieZone と Claw_RiseCollider に自動的に検知スクリプトをアタッチする
        if (clawCarrieZone != null)
        {
            UFOClawCollisionDetector detector = clawCarrieZone.GetComponent<UFOClawCollisionDetector>();
            if (detector == null)
            {
                detector = clawCarrieZone.gameObject.AddComponent<UFOClawCollisionDetector>();
            }
            detector.armController = this;
            Debug.Log($"[UFOArmController] Auto-attached UFOClawCollisionDetector to clawCarrieZone ({clawCarrieZone.name})");
        }

        if (clawRiseCollider != null)
        {
            UFOClawCollisionDetector detector = clawRiseCollider.GetComponent<UFOClawCollisionDetector>();
            if (detector == null)
            {
                detector = clawRiseCollider.gameObject.AddComponent<UFOClawCollisionDetector>();
            }
            detector.armController = this;
            Debug.Log($"[UFOArmController] Auto-attached UFOClawCollisionDetector to clawRiseCollider ({clawRiseCollider.name})");
        }



        if (rail1 != null) _rail1InitialPos = rail1.position;
        if (rail2 != null) _rail2InitialPos = rail2.position;

        if (stretchRope != null)
        {
            Debug.Log($"[UFOArmController] Referencing stretchRope: {stretchRope.gameObject.name} (Parent: {stretchRope.transform.parent?.name})");
        }
        else
        {
            Debug.LogError("[UFOArmController] stretchRope reference is NULL!");
        }

        // 物理ジョイント（SwayJoint）の初期化
        if (swayJoint != null)
        {
            // Transformの親子関係による物理挙動の競合（揺れが逆になる現象）を防ぐため、
            // 実行時に親オブジェクトから切り離し、静的な親（あるいはルート）に配置します。
            // ※ 時間経過によるジョイント累積誤差やねじれを防ぐため、現在は切り離し処理を無効化しています。
            // if (swayJoint.transform.parent != null)
            // {
            //     swayJoint.transform.SetParent(armRoot != null ? armRoot.parent : null, true);
            //     Debug.Log($"[UFOArmController] Auto-unparented {swayJoint.name} to prevent double-transform physics conflicts.");
            // }

            Rigidbody swayRb = swayJoint.GetComponent<Rigidbody>();
            if (swayRb != null)
            {
                if (swayRb.isKinematic)
                {
                    swayRb.isKinematic = false;
                    Debug.Log($"[UFOArmController] Auto-set {swayRb.name}'s Rigidbody to dynamic (isKinematic = false) for physical sway.");
                }
            }
            else
            {
                Debug.LogError($"[UFOArmController] The swayJoint object ({swayJoint.name}) does not have a Rigidbody component! Physics sway will not work.");
            }

            if (swayJoint.connectedBody == null)
            {
                Debug.LogWarning($"[UFOArmController] swayJoint.connectedBody is null. Please make sure to add a Rigidbody (isKinematic=true) to your pole tip (e.g., poll3) and assign it to the Connected Body of the Configurable Joint.");
            }

            if (swayJoint.angularXMotion == ConfigurableJointMotion.Locked)
                swayJoint.angularXMotion = ConfigurableJointMotion.Limited;
            if (swayJoint.angularYMotion == ConfigurableJointMotion.Locked)
                swayJoint.angularYMotion = ConfigurableJointMotion.Limited;
            if (swayJoint.angularZMotion == ConfigurableJointMotion.Locked)
                swayJoint.angularZMotion = ConfigurableJointMotion.Limited;

            // バネドライブ（自動復元力）の設定
            swayJoint.rotationDriveMode = RotationDriveMode.XYAndZ;

            JointDrive xDrive = swayJoint.angularXDrive;
            xDrive.positionSpring = jointSpringForce;
            xDrive.positionDamper = jointSpringDamper;
            xDrive.maximumForce = 10000f;
            swayJoint.angularXDrive = xDrive;

            JointDrive yzDrive = swayJoint.angularYZDrive;
            yzDrive.positionSpring = jointSpringForce;
            yzDrive.positionDamper = jointSpringDamper;
            yzDrive.maximumForce = 10000f;
            swayJoint.angularYZDrive = yzDrive;

            _currentSwayLimit = swayRangeAngle;
            SetJointLimits(_currentSwayLimit);
            Debug.Log($"[UFOArmController] Initialized swayJoint: {swayJoint.name} with angular range limit: {swayRangeAngle} degrees and auto-centering spring: {jointSpringForce}.");
        }
    }

    // ─────────────────────────────────────
    /// <summary>LeverController から呼ばれる（x: -1〜1 左右, z: -1〜1 前後）</summary>
    public void SetLeverInput(float x, float z)
    {
        _leverInput = new Vector2(x, z);
    }

    /// <summary>ButtonController から呼ばれる：下降サイクルを開始</summary>
    public void StartDescentCycle()
    {
        if (_state != ArmState.Idle && _state != ArmState.Moving) return;
        
        // すぐ下降せず、まず爪を上に開くフェーズに入る
        _state = ArmState.OpeningClaw;
        _wantFingerOpen = true;
        _stateTimer = 1.0f; // 1秒間待機する
    }

    /// <summary>ButtonController から呼ばれる：アームを手動で開閉する（トグル）</summary>
    public void ToggleClaw()
    {
        // 今の状態の逆にする（開いていれば閉じ、閉じていれば開く）
        _wantFingerOpen = !_wantFingerOpen;
        Debug.Log($"[UFOArmController] 手動開閉ボタンが押されました！ 開く={_wantFingerOpen}");
    }

    private bool _collidersDisabledForSpawn = false;

    void Update()
    {
        UpdateColliderStateForSpawning();
        UpdateFingersAndSway();
        UpdateStateMachine();
        WakeUpNearbyCoins();
        UpdateMagnet();
    }

    void UpdateColliderStateForSpawning()
    {
        // armRoot と swayJoint（実行時に親子関係が切り離されるため）の両方からコライダーを集める
        var colliders = new System.Collections.Generic.List<Collider>();
        if (armRoot != null)
        {
            colliders.AddRange(armRoot.GetComponentsInChildren<Collider>(true));
        }
        if (swayJoint != null)
        {
            colliders.AddRange(swayJoint.GetComponentsInChildren<Collider>(true));
        }

        // コインが降っている最中、および落下中の時間帯を判定する
        bool shouldDisable = ItemSpawner.IsSpawning || (Time.time < CoinOptimizer.freezeStartTime);

        if (shouldDisable != _collidersDisabledForSpawn)
        {
            _collidersDisabledForSpawn = shouldDisable;
            foreach (var col in colliders)
            {
                if (col == null) continue;
                // トリガー（UFOClawCarrierなど運搬用の領域判定コライダー）は動作を維持するため除外し、
                // 物理衝突判定用のコライダーのみ無効化する
                if (col.isTrigger) continue;
                
                col.enabled = !shouldDisable;
            }
            Debug.Log($"[UFOArmController] Spawning coin collision state changed: Arm Solid Colliders Enabled = {!shouldDisable}");
        }
    }

    void FixedUpdate()
    {
        UpdateMovement();
        UpdateRailFollow();
    }

    void WakeUpNearbyCoins()
    {
        // 処理落ちを防ぐため、常に実行するのではなく一定時間ごと（例: 0.2秒ごと）に実行する
        _wakeUpTimer -= Time.deltaTime;
        if (_wakeUpTimer > 0f) return;
        _wakeUpTimer = wakeUpInterval;

        if (fingerParts == null || fingerParts.Length == 0 || fingerParts[0] == null) return;

        // アームの中心（指の親オブジェクト）の位置を取得
        Transform parentFolder = fingerParts[0].parent;
        Vector3 centerPos = (parentFolder != null) ? parentFolder.position : transform.position;

        // アーム降下中・爪閉じ中（下降開始〜掴み完了まで）は、アーム直下のコインを徐々にスリープ解除
        // 上昇中（Ascending）は無駄な物理演算を防ぐため、この直下ボックス判定は行いません
        bool isDescendingOrGrabbing = (_state == ArmState.OpeningClaw || 
                                       _state == ArmState.Descending || 
                                       _state == ArmState.PostCollisionDescending || 
                                       _state == ArmState.Grabbing);

        if (isDescendingOrGrabbing)
        {
            // 手首位置から指定のオフセットだけズラした位置を基準にする
            Vector3 boxCenter = centerPos + wakeUpDownwardsOffset;
            boxCenter.y -= wakeUpDownwardsLength * 0.5f; // 中心点を真下にずらす
            
            Vector3 halfExtents = new Vector3(
                wakeUpDownwardsWidth,
                wakeUpDownwardsLength * 0.5f,
                wakeUpDownwardsWidth
            );
            
            Collider[] hits = Physics.OverlapBox(boxCenter, halfExtents, Quaternion.identity);
            foreach (var hit in hits)
            {
                CoinOptimizer coin = hit.GetComponent<CoinOptimizer>();
                if (coin != null)
                {
                    coin.WakeUp();
                }
            }
        }
        else
        {
            // 待機中/移動中/上昇中は従来通り、爪のパーツ（指パーツ）の周囲のみ
            foreach (Transform finger in fingerParts)
            {
                if (finger != null)
                {
                    // 指（爪）のローカル座標オフセットを加味した位置を判定中心とする
                    Vector3 targetPos = finger.TransformPoint(wakeUpOffset);
                    WakeUpInSphere(targetPos, wakeUpRadius);
                }
            }
        }
    }

    private void WakeUpInSphere(Vector3 pos, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(pos, radius);
        foreach (var hit in hits)
        {
            CoinOptimizer coin = hit.GetComponent<CoinOptimizer>();
            if (coin != null)
            {
                coin.WakeUp();
            }
        }
    }

    void UpdateMagnet()
    {
        if (!isMagnetMode) return;
        
        // 爪が強制的に開かれている（リリースボタンを押した等）場合は吸着をやめる
        if (_wantFingerOpen) return;

        // 爪が閉まる待機中、または上昇中のみ吸着を有効にする
        if (_state != ArmState.Grabbing && _state != ArmState.Ascending) return;
        if (fingerParts == null || fingerParts.Length == 0 || fingerParts[0] == null) return;

        // 吸着の中心点はアームの根本（fingerの親）
        Transform parentFolder = fingerParts[0].parent;
        Vector3 centerPos = (parentFolder != null) ? parentFolder.position : transform.position;

        // 中心を少し下にする（爪の空間の中心に集めるため）
        centerPos.y -= 0.5f;

        Collider[] hits = Physics.OverlapSphere(centerPos, magnetRadius);
        foreach (var hit in hits)
        {
            Rigidbody rb = hit.GetComponent<Rigidbody>();
            CoinOptimizer coin = hit.GetComponent<CoinOptimizer>();
            
            if (rb != null && coin != null)
            {
                // 吸着するためにまず叩き起こす
                coin.WakeUp();
                
                // コインを中心に向かって吸い寄せる
                Vector3 directionToCenter = (centerPos - hit.transform.position).normalized;
                
                // 距離が近いほど強く吸着する
                float distance = Vector3.Distance(centerPos, hit.transform.position);
                float forceMultiplier = Mathf.Clamp01(1.0f - (distance / magnetRadius));
                
                rb.AddForce(directionToCenter * magnetForce * forceMultiplier, ForceMode.Acceleration);
            }
        }
    }

    void UpdateMovement()
    {
        // 下降中・掴み中・上昇中はXZ移動しない
        if (_state == ArmState.Descending ||
            _state == ArmState.PostCollisionDescending ||
            _state == ArmState.Grabbing   ||
            _state == ArmState.Ascending) return;
        if (armRoot == null) return;

        Vector3 pos = (_armRigidbody != null) ? _armRigidbody.position : armRoot.position;
        pos.x += _leverInput.x * moveSpeed * Time.fixedDeltaTime;
        pos.z += _leverInput.y * moveSpeed * Time.fixedDeltaTime;

        // ピボットではなく、「実際の見た目の中心座標（visualPos）」を算出してClamp判定を行う
        Vector3 visualPos = pos + _visualOffset;

        // 移動範囲の中心座標を決定
        Vector3 centerPos = _machineBasePos;

        float halfX = playAreaSize.x / 2f;
        float halfZ = playAreaSize.y / 2f;
        float limitCenterX = centerPos.x + playAreaCenter.x;
        float limitCenterZ = centerPos.z + playAreaCenter.y;

        visualPos.x = Mathf.Clamp(visualPos.x, limitCenterX - halfX, limitCenterX + halfX);
        visualPos.z = Mathf.Clamp(visualPos.z, limitCenterZ - halfZ, limitCenterZ + halfZ);

        // Clampされた見た目の座標から、再びピボットの座標を逆算して適用する
        pos = visualPos - _visualOffset;

        if (_armRigidbody != null)
        {
            _armRigidbody.MovePosition(pos);
        }
        else
        {
            armRoot.position = pos;
        }
        
        // コントロール中のみ状態をMovingにする（操作不可ステート時は維持）
        if (_state == ArmState.Idle || _state == ArmState.Moving)
        {
            _state = (_leverInput.sqrMagnitude > 0.01f) ? ArmState.Moving : ArmState.Idle;
        }
    }



    void UpdateRailFollow()
    {
        if (armRoot == null) return;

        // アームが初期位置からどれだけ移動したか（差分）を計算
        Vector3 currentPos = (_armRigidbody != null) ? _armRigidbody.position : armRoot.position;
        Vector3 delta = currentPos - _armInitialPos;

        // Rail1: 左右（X方向）移動をアームに合わせる
        if (rail1 != null)
        {
            Vector3 p = _rail1InitialPos;
            p.x += delta.x;
            rail1.position = p;
        }

        // Rail2: 上下・奥手前（Z方向）移動をアームに合わせる
        if (rail2 != null)
        {
            Vector3 p = _rail2InitialPos;
            p.z += delta.z;
            rail2.position = p;
        }
    }

    private void UpdateFingerKinematicState()
    {
        if (fingerParts == null) return;
        for (int i = 0; i < fingerParts.Length; i++)
        {
            if (fingerParts[i] == null) continue;
            Rigidbody rb = fingerParts[i].GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = !useHingeJointPhysics;
            }
        }
    }

    void UpdateFingersAndSway()
    {
        UpdateFingerKinematicState();

        if (useHingeJointPhysics)
        {
            UpdateFingersPhysics();
        }
        else
        {
            UpdateFingersKinematic();
        }

        UpdateSwayJointLimit();
    }

    private float _currentSwayLimit = 0f;

    private void UpdateSwayJointLimit()
    {
        if (swayJoint == null) return;

        // プレイ中や静止中は swayRangeAngle まで揺れることを許可
        // stabilizeOnDescent が true でかつ自動昇降動作中 (IsBusy) の場合のみ 0 に締める
        float targetAngle = swayRangeAngle;
        if (stabilizeOnDescent && IsBusy)
        {
            targetAngle = 0f;
        }

        _currentSwayLimit = Mathf.Lerp(_currentSwayLimit, targetAngle, Time.deltaTime * jointStabilizeSpeed);
        if (Mathf.Abs(_currentSwayLimit - targetAngle) < 0.05f)
        {
            _currentSwayLimit = targetAngle;
        }

        SetJointLimits(_currentSwayLimit);

        // 垂直に固定する際、残った物理的な速度を減衰（ダンピング）させて揺れを速やかに抑える
        if (targetAngle == 0f)
        {
            Rigidbody rb = swayJoint.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, Time.deltaTime * jointStabilizeSpeed);
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.deltaTime * jointStabilizeSpeed);
            }
        }
    }

    private void SetJointLimits(float angle)
    {
        if (swayJoint == null) return;

        // X軸の回転制限を設定
        SoftJointLimit xHigh = swayJoint.highAngularXLimit;
        xHigh.limit = angle;
        swayJoint.highAngularXLimit = xHigh;

        SoftJointLimit xLow = swayJoint.lowAngularXLimit;
        xLow.limit = -angle;
        swayJoint.lowAngularXLimit = xLow;

        // Y軸の回転制限を設定（主にねじれ防止）
        SoftJointLimit yLimit = swayJoint.angularYLimit;
        yLimit.limit = angle;
        swayJoint.angularYLimit = yLimit;

        // Z軸の回転制限を設定
        SoftJointLimit zLimit = swayJoint.angularZLimit;
        zLimit.limit = angle;
        swayJoint.angularZLimit = zLimit;
    }

    private void UpdateFingersKinematic()
    {
        // 爪（finger）に対する開閉と揺れの合成
        if (fingerParts != null && fingerParts.Length > 0)
        {
            for (int i = 0; i < fingerParts.Length; i++)
            {
                if (fingerParts[i] == null) continue;

                // 開閉アニメーションの補間（純粹なローカル状態）
                Quaternion targetBaseRot = _wantFingerOpen ? _fingerOpenRot[i] : Quaternion.Slerp(_fingerOpenRot[i], _fingerDefaultRot[i], closeLimitRatio);
                Vector3 targetBasePos = _wantFingerOpen ? _fingerOpenPos[i] : Vector3.Lerp(_fingerOpenPos[i], _fingerDefaultPos[i], closeLimitRatio);

                _fingerCurrentBaseRot[i] = Quaternion.Lerp(_fingerCurrentBaseRot[i], targetBaseRot, Time.deltaTime * fingerSpeed);
                _fingerCurrentBasePos[i] = Vector3.Lerp(_fingerCurrentBasePos[i], targetBasePos, Time.deltaTime * fingerSpeed);

                // 1. 純粋なローカル回転と座標（開閉）をセット
                fingerParts[i].localRotation = _fingerCurrentBaseRot[i];
                fingerParts[i].localPosition = _fingerCurrentBasePos[i];
            }
        }
    }

    private void UpdateFingersPhysics()
    {
        if (_fingerJoints == null) return;

        for (int i = 0; i < _fingerJoints.Length; i++)
        {
            HingeJoint joint = _fingerJoints[i];
            if (joint == null) continue;

            JointMotor motor = joint.motor;

            bool invert = (invertFingerAngle != null && i < invertFingerAngle.Length && invertFingerAngle[i]);
            
            float speed = _wantFingerOpen ? jointMotorSpeed : -jointMotorSpeed;
            if (invert)
            {
                speed = -speed;
            }

            motor.targetVelocity = speed;
            motor.force = jointMotorForce;
            joint.motor = motor;
            joint.useMotor = true;
        }
    }

    public void OnClawCollided()
    {
        OnClawCollided(null);
    }

    public void OnClawCollided(Collider hitCollider)
    {
        if (hitCollider == null) return;

        // 1. ユーザー指定の即時掴みエリア判定
        bool isImmediateArea = false;
        if (immediateGrabArea != null)
        {
            // コインや景品アイテムの場合は、即時掴みエリアとしての接触判定から除外する
            bool isCoinOrItem = hitCollider.GetComponent<UFOItem>() != null || 
                               hitCollider.GetComponentInParent<UFOItem>() != null ||
                               hitCollider.GetComponent<CoinOptimizer>() != null ||
                               hitCollider.GetComponentInParent<CoinOptimizer>() != null;

            if (!isCoinOrItem)
            {
                if (hitCollider == immediateGrabArea || 
                    hitCollider.gameObject == immediateGrabArea.gameObject ||
                    hitCollider.transform.IsChildOf(immediateGrabArea.transform))
                {
                    isImmediateArea = true;
                }
            }
        }

        // 2. ClawCarrieZone と Claw_RiseCollider の接触判定
        bool isClawRiseContact = false;
        if (clawCarrieZone != null && clawRiseCollider != null)
        {
            if (hitCollider == clawRiseCollider || 
                hitCollider.gameObject == clawRiseCollider.gameObject || 
                hitCollider.transform.IsChildOf(clawRiseCollider.transform) ||
                hitCollider == clawCarrieZone || 
                hitCollider.gameObject == clawCarrieZone.gameObject || 
                hitCollider.transform.IsChildOf(clawCarrieZone.transform))
            {
                isClawRiseContact = true;
            }
        }

        if (isImmediateArea || isClawRiseContact)
        {
            // 指定エリアに入った場合、下降中または少しだけ下降中のステートであれば、即座に掴む（Grabbing）状態に移行する
            if (_state == ArmState.Descending || _state == ArmState.PostCollisionDescending)
            {
                Debug.Log($"[UFOArmController] Entered grab trigger area (immediateArea={isImmediateArea}, clawRiseContact={isClawRiseContact}) with {hitCollider.name}. Bypassing extra descent. State: {_state} -> Grabbing");
                _state = ArmState.Grabbing;
                _wantFingerOpen = false;
                _stateTimer = grabWaitSeconds;

                // コインの山に衝突したときのがさがさ音を再生
                PlayDescentRustleSound();

                if (stretchRope != null) stretchRope.PauseExternalControl(); // 掴み中はピタッと停止

                // 物理的な速度と慣性をクリアしてその場で即時停止させる
                ResetArmAndClawPhysicsVelocity();
            }
        }
    }

    private void ResetArmAndClawPhysicsVelocity()
    {
        if (_armRigidbody != null)
        {
            _armRigidbody.linearVelocity = Vector3.zero;
            _armRigidbody.angularVelocity = Vector3.zero;
        }

        if (swayJoint != null)
        {
            Rigidbody swayRb = swayJoint.GetComponent<Rigidbody>();
            if (swayRb != null)
            {
                swayRb.linearVelocity = Vector3.zero;
                swayRb.angularVelocity = Vector3.zero;
            }
        }

        if (fingerParts != null)
        {
            foreach (var finger in fingerParts)
            {
                if (finger != null)
                {
                    Rigidbody rb = finger.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }
            }
        }
    }

    void UpdateStateMachine()
    {
        switch (_state)
        {
            case ArmState.OpeningClaw:
                // 指定時間（1秒）待ってから下降をスタートする
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f)
                {
                    Debug.Log("[UFOArmController] State changed: OpeningClaw -> Descending. Calling stretchRope.StartExternalDescent().");
                    _state = ArmState.Descending;
                    if (stretchRope == null) Debug.LogError("[UFOArmController] stretchRope is NULL!");
                    stretchRope?.StartExternalDescent(descentSpeedMultiplier);
                }
                break;

            case ArmState.Descending:
                // StretchRope が最大まで伸びたら「掴む」へ
                if (stretchRope != null && stretchRope.IsAtMax())
                {
                    Debug.Log("[UFOArmController] State changed: Descending -> Grabbing. Rope is at Max.");
                    _state = ArmState.Grabbing;
                    _wantFingerOpen = false;
                    _stateTimer = grabWaitSeconds;
                    if (stretchRope != null) stretchRope.PauseExternalControl(); // 掴み中はピタッと停止
                }
                break;

            case ArmState.PostCollisionDescending:
                // 少しだけ下降を継続する
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f || (stretchRope != null && stretchRope.IsAtMax()))
                {
                    Debug.Log("[UFOArmController] State changed: PostCollisionDescending -> Grabbing.");
                    _state = ArmState.Grabbing;
                    _wantFingerOpen = false;
                    _stateTimer = grabWaitSeconds;
                    
                    if (stretchRope != null) stretchRope.PauseExternalControl(); // 掴み中はピタッと停止
                }
                break;

            case ArmState.Grabbing:
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f)
                {
                    Debug.Log("[UFOArmController] State changed: Grabbing -> Ascending.");
                    _state = ArmState.Ascending;
                    if (stretchRope != null) stretchRope.StartExternalAscent(descentSpeedMultiplier);
                }
                break;

            case ArmState.Ascending:
                // StretchRope が完全に縮んだら IDLE へ
                if (stretchRope != null && stretchRope.IsAtMin())
                {
                    Debug.Log("[UFOArmController] State changed: Ascending -> Idle.");
                    _state = ArmState.Idle;
                }
                break;
        }
    }

    // ─────────────────────────────────────
    // アーム（指）の動的交換処理（シーン上のオブジェクトを切り替える版）
    // ─────────────────────────────────────
    public void ChangeClaw_InScene(GameObject activeClawObj)
    {
        if (activeClawObj == null) return;

        // UFOClawData がついていれば、各種設定（開く角度など）を上書きする
        UFOClawData data = activeClawObj.GetComponent<UFOClawData>();
        if (data != null)
        {
            this.fingerOpenAngle = data.fingerOpenAngle;
            this.useCustomOpenTransform = data.useCustomOpenTransform;

            this.invertFingerAngle = (data.invertFingerAngle != null && data.invertFingerAngle.Length > 0) ? data.invertFingerAngle : null;
            this.fingerAngleOffsets = (data.fingerAngleOffsets != null && data.fingerAngleOffsets.Length > 0) ? data.fingerAngleOffsets : null;
            this.customOpenLocalPositions = (data.customOpenLocalPositions != null && data.customOpenLocalPositions.Length > 0) ? data.customOpenLocalPositions : null;
            this.customOpenLocalRotations = (data.customOpenLocalRotations != null && data.customOpenLocalRotations.Length > 0) ? data.customOpenLocalRotations : null;

            // インスペクターで指パーツが明示されている場合はそれを使う
            if (data.fingerParts != null && data.fingerParts.Length > 0)
            {
                this.fingerParts = data.fingerParts;
            }
            else
            {
                // 指定されていなければ、従来通り直下の子オブジェクトを登録する
                int childCount = activeClawObj.transform.childCount;
                this.fingerParts = new Transform[childCount];
                for (int i = 0; i < childCount; i++)
                {
                    this.fingerParts[i] = activeClawObj.transform.GetChild(i);
                }
            }
        }
        else
        {
            // 新しいアームに設定が無い場合は、前回の設定を引き継がずにリセットする
            this.useCustomOpenTransform = false;
            this.invertFingerAngle = null;
            this.fingerAngleOffsets = null;
            this.customOpenLocalPositions = null;
            this.customOpenLocalRotations = null;

            // 従来通り直下の子オブジェクトを登録する
            int childCount = activeClawObj.transform.childCount;
            this.fingerParts = new Transform[childCount];
            for (int i = 0; i < childCount; i++)
            {
                this.fingerParts[i] = activeClawObj.transform.GetChild(i);
            }
        }

        // もう一度初期化処理を走らせる
        InitializeFingers();
        InitializeFingerJoints();
    }

    // ─────────────────────────────────────
    // アーム（指）の動的交換処理（プレハブ生成版・旧方式）
    // ─────────────────────────────────────
    public void ChangeClaw(GameObject newClawPrefab)
    {
        if (fingerParts == null || fingerParts.Length == 0 || fingerParts[0] == null) return;

        // 今のfinger達の親（通常は "finger" という名前のオブジェクト）を取得
        Transform parentFolder = fingerParts[0].parent;

        // 既存の指（finger1〜4など）をすべて削除
        foreach (Transform child in parentFolder)
        {
            Destroy(child.gameObject);
        }

        // 新しいアーム（プレハブ）を生成
        GameObject newClawObj = Instantiate(newClawPrefab, parentFolder);
        newClawObj.transform.localPosition = Vector3.zero;
        newClawObj.transform.localRotation = Quaternion.identity;

        // 新しいプレハブの中に UFOClawData がついていれば、各種設定（開く角度など）を上書きする
        UFOClawData data = newClawObj.GetComponent<UFOClawData>();
        if (data != null)
        {
            this.fingerOpenAngle = data.fingerOpenAngle;
            this.useCustomOpenTransform = data.useCustomOpenTransform;

            this.invertFingerAngle = (data.invertFingerAngle != null && data.invertFingerAngle.Length > 0) ? data.invertFingerAngle : null;
            this.fingerAngleOffsets = (data.fingerAngleOffsets != null && data.fingerAngleOffsets.Length > 0) ? data.fingerAngleOffsets : null;
            this.customOpenLocalPositions = (data.customOpenLocalPositions != null && data.customOpenLocalPositions.Length > 0) ? data.customOpenLocalPositions : null;
            this.customOpenLocalRotations = (data.customOpenLocalRotations != null && data.customOpenLocalRotations.Length > 0) ? data.customOpenLocalRotations : null;

            // インスペクターで指パーツが明示されている場合はそれを使う
            if (data.fingerParts != null && data.fingerParts.Length > 0)
            {
                this.fingerParts = data.fingerParts;
            }
            else
            {
                // 指定されていなければ、従来通り直下の子オブジェクトを登録する
                int childCount = newClawObj.transform.childCount;
                this.fingerParts = new Transform[childCount];
                for (int i = 0; i < childCount; i++)
                {
                    this.fingerParts[i] = newClawObj.transform.GetChild(i);
                }
            }
        }
        else
        {
            // 新しいアームに設定が無い場合は、前回の設定を引き継がずにリセットする
            this.useCustomOpenTransform = false;
            this.invertFingerAngle = null;
            this.fingerAngleOffsets = null;
            this.customOpenLocalPositions = null;
            this.customOpenLocalRotations = null;

            // 従来通り直下の子オブジェクトを登録する
            int childCount = newClawObj.transform.childCount;
            this.fingerParts = new Transform[childCount];
            for (int i = 0; i < childCount; i++)
            {
                this.fingerParts[i] = newClawObj.transform.GetChild(i);
            }
        }

        // もう一度初期化処理を走らせる
        InitializeFingers();
        InitializeFingerJoints();
    }

    public void InitializeFingers()
    {
        if (fingerParts != null && fingerParts.Length > 0)
        {
            _fingerDefaultRot = new Quaternion[fingerParts.Length];
            _fingerOpenRot    = new Quaternion[fingerParts.Length];
            _fingerCurrentBaseRot = new Quaternion[fingerParts.Length];

            _fingerDefaultPos = new Vector3[fingerParts.Length];
            _fingerOpenPos = new Vector3[fingerParts.Length];
            _fingerCurrentBasePos = new Vector3[fingerParts.Length];
            
            for (int i = 0; i < fingerParts.Length; i++)
            {
                if (fingerParts[i] == null) continue;
                _fingerDefaultRot[i] = fingerParts[i].localRotation;
                _fingerDefaultPos[i] = fingerParts[i].localPosition;
                
                _fingerCurrentBaseRot[i] = _fingerDefaultRot[i];
                _fingerCurrentBasePos[i] = _fingerDefaultPos[i];

                if (useCustomOpenTransform && i < customOpenLocalPositions.Length && i < customOpenLocalRotations.Length)
                {
                    _fingerOpenPos[i] = customOpenLocalPositions[i];
                    _fingerOpenRot[i] = Quaternion.Euler(customOpenLocalRotations[i]);
                }
                else
                {
                    _fingerOpenPos[i] = _fingerDefaultPos[i];
                    
                    float angle = fingerOpenAngle;
                    if (invertFingerAngle != null && i < invertFingerAngle.Length && invertFingerAngle[i])
                    {
                        angle = -fingerOpenAngle;
                    }

                    Vector3 euler = new Vector3(angle, 0f, 0f);
                    if (fingerAngleOffsets != null && i < fingerAngleOffsets.Length)
                    {
                        euler += fingerAngleOffsets[i];
                    }
                    _fingerOpenRot[i] = Quaternion.Euler(euler) * _fingerDefaultRot[i];
                }
            }
        }
    }

    private void InitializeFingerJoints()
    {
        if (fingerParts != null && fingerParts.Length > 0)
        {
            _fingerJoints = new HingeJoint[fingerParts.Length];
            for (int i = 0; i < fingerParts.Length; i++)
            {
                if (fingerParts[i] != null)
                {
                    _fingerJoints[i] = fingerParts[i].GetComponent<HingeJoint>();
                }
            }

            // 爪パーツ（指同士）の衝突を自動で無視する設定を適用
            IgnoreCollisionsBetweenClaws();
        }
    }

    private void IgnoreCollisionsBetweenClaws()
    {
        var collidersList = new System.Collections.Generic.List<Collider>();
        for (int i = 0; i < fingerParts.Length; i++)
        {
            if (fingerParts[i] != null)
            {
                var cols = fingerParts[i].GetComponentsInChildren<Collider>(true);
                collidersList.AddRange(cols);
            }
        }

        for (int i = 0; i < collidersList.Count; i++)
        {
            for (int j = i + 1; j < collidersList.Count; j++)
            {
                if (collidersList[i] != null && collidersList[j] != null)
                {
                    Physics.IgnoreCollision(collidersList[i], collidersList[j], true);
                }
            }
        }
        Debug.Log($"[UFOArmController] Ignored collisions between {collidersList.Count} claw colliders.");
    }

    private void OnDrawGizmosSelected()
    {
        // 基準座標の決定
        Vector3 basePos = (machineRoot != null) ? machineRoot.position : transform.position;

        // レバー等のプレイ中ではない時の高さ基準として少し浮かせる（あればアームルートの高さに合わせる）
        float y = (armRoot != null) ? armRoot.position.y : basePos.y;
        Vector3 center = new Vector3(basePos.x + playAreaCenter.x, y, basePos.z + playAreaCenter.y);
        Vector3 size = new Vector3(playAreaSize.x, 1f, playAreaSize.y); // 厚み（高さ）を1m持たせて視認しやすくする

        // 輪郭線（赤）
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(center, size);

        // 半透明の赤い塗りつぶし（見やすさ向上）
        Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
        Gizmos.DrawCube(center, size);

        // WakeUpRadiusの可視化 (黄色)
        if (wakeUpRadius > 0f)
        {
            Gizmos.color = Color.yellow;

            // エディタ非実行時は、調整しやすいように両方（指先球体 ＆ 直下判定ボックス）を薄く描画します
            if (!Application.isPlaying)
            {
                // 1. 指先（爪）の球体判定
                if (fingerParts != null)
                {
                    foreach (Transform finger in fingerParts)
                    {
                        if (finger != null)
                        {
                            Gizmos.DrawWireSphere(finger.TransformPoint(wakeUpOffset), wakeUpRadius);
                        }
                    }
                }

                // 2. 直下ボックス判定
                if (fingerParts != null && fingerParts.Length > 0 && fingerParts[0] != null)
                {
                    Transform parentFolder = fingerParts[0].parent;
                    Vector3 centerPos = (parentFolder != null) ? parentFolder.position : transform.position;

                    Vector3 boxCenter = centerPos + wakeUpDownwardsOffset;
                    boxCenter.y -= wakeUpDownwardsLength * 0.5f;
                    Vector3 boxSize = new Vector3(wakeUpDownwardsWidth * 2f, wakeUpDownwardsLength, wakeUpDownwardsWidth * 2f);

                    // 枠線
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireCube(boxCenter, boxSize);

                    // 半透明の塗りつぶし（見やすさ向上）
                    Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.08f);
                    Gizmos.DrawCube(boxCenter, boxSize);
                }
            }
            else
            {
                // ゲーム実行中：ステート（動作中か否か）に応じて切り替え
                bool isDescendingOrGrabbing = (_state == ArmState.OpeningClaw || 
                                               _state == ArmState.Descending || 
                                               _state == ArmState.PostCollisionDescending || 
                                               _state == ArmState.Grabbing);
                if (isDescendingOrGrabbing)
                {
                    // 降下中・掴み中は直下ボックスのみ
                    if (fingerParts != null && fingerParts.Length > 0 && fingerParts[0] != null)
                    {
                        Transform parentFolder = fingerParts[0].parent;
                        Vector3 centerPos = (parentFolder != null) ? parentFolder.position : transform.position;

                        Vector3 boxCenter = centerPos + wakeUpDownwardsOffset;
                        boxCenter.y -= wakeUpDownwardsLength * 0.5f;
                        Vector3 boxSize = new Vector3(wakeUpDownwardsWidth * 2f, wakeUpDownwardsLength, wakeUpDownwardsWidth * 2f);

                        // 枠線
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawWireCube(boxCenter, boxSize);

                        // 半透明の塗りつぶし（動作中は少し濃くして視認性を上げる）
                        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.15f);
                        Gizmos.DrawCube(boxCenter, boxSize);
                    }
                }
                else
                {
                    // 待機/移動中は指先球体のみ
                    if (fingerParts != null)
                    {
                        foreach (Transform finger in fingerParts)
                        {
                            if (finger != null)
                            {
                                Gizmos.DrawWireSphere(finger.TransformPoint(wakeUpOffset), wakeUpRadius);
                            }
                        }
                    }
                }
            }
        }
    }



    /// <summary>
    /// 降下衝突時にコインの山に当たった際のがさがさ効果音を再生する
    /// </summary>
    private void PlayDescentRustleSound()
    {
        if (descentRustleSound == null) return;
        if (fingerParts == null || fingerParts.Length == 0 || fingerParts[0] == null) return;

        // 爪の中心点（底付近）を取得
        Transform parentFolder = fingerParts[0].parent;
        Vector3 centerPos = (parentFolder != null) ? parentFolder.position : transform.position;
        centerPos.y -= 0.5f;

        // 周囲のコインを数える
        Collider[] hits = Physics.OverlapSphere(centerPos, rustleDetectRadius);
        int coinCount = 0;
        foreach (var hit in hits)
        {
            if (hit.GetComponent<UFOItem>() != null)
            {
                coinCount++;
            }
        }

        // 最底枚数以上のコインが下にあればがさがさ音を鳴らす
        if (coinCount >= minCoinsForRustle)
        {
            // コインの数に応じて音量を調整（枚数が多いほど音が大きくなる）
            // 例：minCoinsForRustle枚で最小、10枚以上で最大音量
            float coinFactor = Mathf.Clamp01((float)(coinCount - minCoinsForRustle) / (10f - minCoinsForRustle));
            float volume = Mathf.Lerp(rustleVolume * 0.3f, rustleVolume, coinFactor);

            if (_audioSourceForJingle == null)
            {
                _audioSourceForJingle = GetComponent<AudioSource>();
                if (_audioSourceForJingle == null)
                {
                    _audioSourceForJingle = gameObject.AddComponent<AudioSource>();
                    _audioSourceForJingle.playOnAwake = false;
                    _audioSourceForJingle.spatialBlend = 0f;
                }
            }

            _audioSourceForJingle.pitch = Random.Range(0.9f, 1.1f);
            
            // volumeBoostの回数だけ重ねて再生して音量を限界突破
            for (int i = 0; i < volumeBoost; i++)
            {
                _audioSourceForJingle.PlayOneShot(descentRustleSound, volume);
            }
            Debug.Log($"[UFOArmController] コインの山に衝突！がさがさ音を再生。枚数: {coinCount}, 音量: {volume:F2}, 重ね数: {volumeBoost}");
        }
        else
        {
            Debug.Log($"[UFOArmController] 衝突しましたが、範囲内のコインが少なすぎるためがさがさ音はスキップします。枚数: {coinCount}");
        }
    }
}
