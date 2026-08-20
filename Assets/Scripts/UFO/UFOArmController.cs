using UnityEngine;

/// <summary>
/// UFOキャッチャーの状態機械と全体制御。
/// 空のGameObjectにアタッチし、各Transformをインスペクターで設定してください。
/// </summary>
public class UFOArmController : MonoBehaviour
{
    public enum ArmState { Idle, Moving, OpeningClaw, Descending, Grabbing, Ascending }

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

    [Header("スポーン連携")]
    [Tooltip("このアームが属する機体のItemSpawner（実機側アームには実機のItemSpawner、練習機側アームには" +
             "練習機のItemSpawnerを設定する）。ItemSpawner.IsInitialSpawning等はstaticで実機・練習機共有のため、" +
             "片方の機体のスポーン完了でもう片方の当たり判定が誤って復活してしまうのを防ぐため、" +
             "このインスタンス固有の状態（IsInitialSpawningInstance）を見るのに使う。未設定ならstaticにフォールバックする")]
    public ItemSpawner itemSpawner;

    // ─────────────────────────────────────
    [Header("XZ 移動設定")]
    [Tooltip("レバー入力に対するアーム移動速度")]
    public float moveSpeed = 3f;
    [Header("移動範囲の指定（Sceneビューで赤い枠が見えます）")]
    public Vector2 playAreaCenter = new Vector2(0f, 0f); // 中心からのズレ（Xが左右、Yが奥手前）
    public Vector2 playAreaSize   = new Vector2(9f, 9f);

    // ─────────────────────────────────────
    [Header("強化要素: 降下地点マーカー（タイプライター id32「アームの降下地点をわかるようにする」で解放）")]
    [Tooltip("ON: アーム移動中（Moving状態）に、爪の真下の降下予定地点に薄い円盤マーカーを表示する")]
    public bool descentLaserUnlocked = false;

    [Tooltip("降下予定地点に表示するマーカー（円盤）。未設定の場合は自動生成する")]
    public Transform descentLaserMarker;

    [Tooltip("マーカーが当たる対象のレイヤー（床・アイテムなど）")]
    public LayerMask descentLaserLayerMask = ~0;

    [Tooltip("マーカーの半径（m）")]
    public float descentLaserMarkerRadius = 0.3f;

    [Tooltip("マーカーの厚み（m）。薄い円盤として表示するための高さ")]
    public float descentLaserMarkerThickness = 0.01f;

    [Tooltip("マーカーの色。Sprites/Defaultシェーダーを使うため、アルファ値を下げると実際に半透明になります")]
    public Color descentLaserColor = new Color(1f, 0.4f, 0.4f, 0.5f);

    [Tooltip("Raycastの最大距離（m）")]
    public float descentLaserMaxDistance = 20f;

    // 掴んだコインがClawCarrierZone内にあるとマーカーがそこで止まってしまうため、
    // Raycastを複数ヒット取得できるようにするバッファ（毎フレームのGCアロケーションを防ぐ）
    private readonly RaycastHit[] _descentLaserHitBuffer = new RaycastHit[16];
    private UFOClawCarrier _cachedClawCarrier;

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
    [Tooltip("叩き起こし対象のレイヤー（コイン等の物理演算対象レイヤーのみに絞ることで負荷を抑えます）")]
    public LayerMask wakeUpLayerMask = 1 << 12;

    [Header("【新規】第3アーム用 吸着(マグネット)機能")]
    [Tooltip("オンにすると、爪が閉じる時と上昇する時に周囲のコインを吸着します")]
    public bool isMagnetMode = false;
    [Tooltip("吸着する範囲（半径）")]
    public float magnetRadius = 3.0f;
    [Tooltip("中心に引き寄せる力")]
    public float magnetForce = 50f;

    [Header("【新規】降下衝突時のコインがさがさ効果音")]
    [Tooltip("がさがさ音の最大音量 (1.0より大きい値で音量増幅可能)。実際のクリップはUFOSEManagerで一元管理")]
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

    private bool _wasArmMoving = false;

    [Header("【デバッグ設定】")]
    [Tooltip("オンにすると、アームの揺れ（Sway）を完全に無効化（揺れなし）にします")]
    [SerializeField] private bool disableSway = false;

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
    private Rigidbody _armRigidbody;

    private Quaternion[] _fingerDefaultRot;
    private Quaternion[] _fingerOpenRot;
    private Quaternion[] _fingerCurrentBaseRot; // 開閉の純粋な回転を保持

    private Vector3[] _fingerDefaultPos;
    private Vector3[] _fingerOpenPos;
    private Vector3[] _fingerCurrentBasePos; // 開閉の純粋な座標を保持

    private bool         _wantFingerOpen = false;
    public bool WantFingerOpen => _wantFingerOpen;

    private float _wakeUpTimer = 0f;
    // Physics.OverlapBox 用の使い回しバッファ（毎フレームの配列アロケーションを防ぐ）
    private readonly Collider[] _wakeUpBuffer = new Collider[32];



    /// <summary>
    /// アームが下降・掴み・上昇などの一連の動作中（Busy）であるかどうか。
    /// </summary>
    public bool IsBusy => _state != ArmState.Idle && _state != ArmState.Moving;

    /// <summary>
    /// アームが下降・掴み・上昇などの動作中（IsBusy）、または手動開閉コルーチンが実行中であるかを示します。
    /// </summary>
    public bool IsInputLocked => IsBusy || (_toggleClawCoroutine != null);

    /// <summary>Start Descentの下降→掴み→上昇の一連動作が本当に完了した瞬間（Ascending→Idle）に発火する。
    /// ForceResetToIdle()による強制リセットではここは発火しない（実際に完了したわけではないため）</summary>
    public event System.Action OnDescentCycleCompleted;

    /// <summary>Toggle Clawによる爪の手動開閉が一連の動作を終えた瞬間に発火する。
    /// ForceReleaseToggleClawLock()による強制リセットではここは発火しない</summary>
    public event System.Action OnClawToggleCompleted;

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
            // 一旦親子関係の解除を停止して挙動を確認するためコメントアウトします
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
                swayRb.sleepThreshold = 0f;
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

        // 初期化時にスキルによる速度等のパラメータ適用を行う
        UpdateArmSpeedBySkills();
        UpdateSwayBySkills();

        InitializeDescentLaser();
    }

    /// <summary>
    /// ローグライクのアンロックされたスキル（ID: 22, 23）をチェックし、アームの速度・下降・待機パラメータを更新する
    /// </summary>
    public void UpdateArmSpeedBySkills()
    {
        var manager = FindFirstObjectByType<RoguelikeManager>();
        if (manager == null)
        {
            moveSpeed = 1.2f;
            grabWaitSeconds = 2.0f;
            descentSpeedMultiplier = 0.65f;
            return;
        }

        var skills = manager.GetUnlockSkillDictionary;
        if (skills == null)
        {
            moveSpeed = 1.2f;
            grabWaitSeconds = 2.0f;
            descentSpeedMultiplier = 0.65f;
            return;
        }

        bool hasSkill22 = skills.ContainsKey(SkillId.UFO_ArmSpeedUp1);
        bool hasSkill23 = skills.ContainsKey(SkillId.UFO_ArmSpeedUp2);

        if (hasSkill22 && hasSkill23)
        {
            moveSpeed = 3.0f;
            grabWaitSeconds = 1.0f;
            descentSpeedMultiplier = 1.5f;
            Debug.Log("[UFOArmController] スキル2段階目のアーム速度を適用: MoveSpeed=3.0, GrabWait=1.0s, DescentMult=1.5");
        }
        else if (hasSkill22)
        {
            moveSpeed = 2.0f;
            grabWaitSeconds = 1.5f;
            descentSpeedMultiplier = 1.0f;
            Debug.Log("[UFOArmController] スキル1段階目のアーム速度を適用: MoveSpeed=2.0, GrabWait=1.5s, DescentMult=1.0");
        }
        else
        {
            moveSpeed = 1.2f;
            grabWaitSeconds = 2.0f;
            descentSpeedMultiplier = 0.65f;
            Debug.Log("[UFOArmController] アーム速度の初期値を適用: MoveSpeed=1.2, GrabWait=2.0s, DescentMult=0.65");
        }
    }

    /// <summary>
    /// ローグライクのアンロックされたスキル（ID: 20, 21, 25）をチェックし、アームの揺れパラメータを更新する
    /// </summary>
    public void UpdateSwayBySkills()
    {
        var manager = FindFirstObjectByType<RoguelikeManager>();
        if (manager == null)
        {
            ApplySwayLevel(0);
            return;
        }

        var skills = manager.GetUnlockSkillDictionary;
        if (skills == null)
        {
            ApplySwayLevel(0);
            return;
        }

        int unlockedSwaySkills = 0;
        if (skills.ContainsKey(SkillId.UFO_ReduceSway1)) unlockedSwaySkills++;
        if (skills.ContainsKey(SkillId.UFO_ReduceSway2)) unlockedSwaySkills++;
        if (skills.ContainsKey(SkillId.UFO_ReduceSway3)) unlockedSwaySkills++;

        ApplySwayLevel(unlockedSwaySkills);
    }

    private void ApplySwayLevel(int level)
    {
        switch (level)
        {
            case 1:
                swayRangeAngle = 10f;
                jointStabilizeSpeed = 3f;
                jointSpringForce = 12f;
                jointSpringDamper = 0.6f;
                Debug.Log("[UFOArmController] 揺れ抑制1段階目を適用: Angle=10, Stabilize=3, Force=12, Damper=0.6");
                break;
            case 2:
                swayRangeAngle = 5f;
                jointStabilizeSpeed = 2f;
                jointSpringForce = 14f;
                jointSpringDamper = 0.9f;
                Debug.Log("[UFOArmController] 揺れ抑制2段階目を適用: Angle=5, Stabilize=2, Force=14, Damper=0.9");
                break;
            case 3:
                swayRangeAngle = 1f;
                jointStabilizeSpeed = 1f;
                jointSpringForce = 16f;
                jointSpringDamper = 1.2f;
                Debug.Log("[UFOArmController] 揺れ抑制3段階目を適用: Angle=1, Stabilize=1, Force=16, Damper=1.2");
                break;
            default: // level 0 またはそれ以外
                swayRangeAngle = 15f;
                jointStabilizeSpeed = 4f;
                jointSpringForce = 10f;
                jointSpringDamper = 0.3f;
                Debug.Log("[UFOArmController] 揺れ抑制の初期値を適用: Angle=15, Stabilize=4, Force=10, Damper=0.3");
                break;
        }

        // ConfigurableJointのバネパラメータとリミットを即座に更新する
        if (swayJoint != null)
        {
            JointDrive xDrive = swayJoint.angularXDrive;
            xDrive.positionSpring = jointSpringForce;
            xDrive.positionDamper = jointSpringDamper;
            swayJoint.angularXDrive = xDrive;

            JointDrive yzDrive = swayJoint.angularYZDrive;
            yzDrive.positionSpring = jointSpringForce;
            yzDrive.positionDamper = jointSpringDamper;
            swayJoint.angularYZDrive = yzDrive;

            // ジョイントのリミットも更新する
            _currentSwayLimit = swayRangeAngle;
            SetJointLimits(_currentSwayLimit);
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
        if (IsInputLocked) return;
        
        // すぐ下降せず、まず爪を上に開くフェーズに入る
        _state = ArmState.OpeningClaw;
        _wantFingerOpen = true;
        _stateTimer = 1.0f; // 1秒間待機する
    }

    private Coroutine _toggleClawCoroutine;

    /// <summary>ButtonController から呼ばれる：アームを手動で開閉する</summary>
    public void ToggleClaw()
    {
        if (IsInputLocked) return;
        _toggleClawCoroutine = StartCoroutine(ToggleClawRoutine());
    }

    private System.Collections.IEnumerator ToggleClawRoutine()
    {
        _wantFingerOpen = true;
        Debug.Log($"[UFOArmController] 手動開閉ボタンが押されました！ 開きます。");

        // アームが開ききるまでの時間（0.5秒） ＋ 開ききってからの待機（0.3秒）＝合計 0.8秒
        yield return new WaitForSeconds(0.8f);

        _wantFingerOpen = false;
        Debug.Log($"[UFOArmController] 自動的に閉じます。");

        // アームが閉じきる時間（0.5秒）待ってからコルーチンを解放する
        yield return new WaitForSeconds(0.5f);
        _toggleClawCoroutine = null;
        OnClawToggleCompleted?.Invoke();
    }

    /// <summary>
    /// 手動開閉コルーチン（_toggleClawCoroutine）が実行中のまま残ってしまっている場合に、強制的に
    /// 停止してIsInputLockedを解除する。コンポーネントの無効化などでコルーチンが後片付けコード
    /// （_toggleClawCoroutine = null の代入）を実行できないまま強制終了すると、実際には動いていない
    /// のにIsInputLockedがtrueのまま固定され、以降StartDescentCycle()等が静かに無視され続けることがある。
    /// チュートリアルなど、確実にクリーンな状態から操作させたい場面の直前で呼ぶ。
    /// </summary>
    public void ForceReleaseToggleClawLock()
    {
        if (_toggleClawCoroutine != null)
        {
            StopCoroutine(_toggleClawCoroutine);
            _toggleClawCoroutine = null;
            _wantFingerOpen = false;
        }
    }

    /// <summary>
    /// ForceReleaseToggleClawLockに加えて、_state自体が連打等でIdle以外のまま固まってしまっている場合も
    /// 強制的にIdleへ戻す。_stateが中途半端な状態のまま残っていると、IsBusy（ひいてはIsInputLocked）が
    /// ずっとtrueのままになり、以降のStartDescentCycle()が静かに無視され続けることがある。
    /// アームが下降中だった場合は見た目が不自然にならないよう、ロープを上昇させて回収する。
    /// </summary>
    public void ForceResetToIdle()
    {
        ForceReleaseToggleClawLock();

        if (_state != ArmState.Idle)
        {
            bool wasLowered = _state == ArmState.Descending || _state == ArmState.Grabbing;
            _state = ArmState.Idle;
            _wantFingerOpen = false;

            if (wasLowered && stretchRope != null)
            {
                stretchRope.StartExternalAscent(descentSpeedMultiplier);
            }
        }
    }
    private bool _collidersDisabledForSpawn = false;

    void Update()
    {
        UpdateColliderStateForSpawning();
        UpdateFingersAndSway();
        UpdateStateMachine();
        UpdateMagnet();
        WakeUpNearbyCoins();
        UpdateDescentLaser();
        UpdateArmMovementSound();
    }

    /// <summary>アームが実際に動いている（XZ移動・下降・上昇中の）状態かどうか</summary>
    private bool IsArmMoving => _state == ArmState.Moving || _state == ArmState.Descending || _state == ArmState.Ascending;

    /// <summary>
    /// アームの移動状態が切り替わった瞬間に開始・停止音を鳴らし、
    /// 動いている間はループ音を再生する。
    /// </summary>
    private void UpdateArmMovementSound()
    {
        bool isMoving = IsArmMoving;

        if (isMoving && !_wasArmMoving)
        {
            UFOSEManager.Instance?.PlayArmMoveStart();
            UFOSEManager.Instance?.StartArmMoveLoop();
        }
        else if (!isMoving && _wasArmMoving)
        {
            UFOSEManager.Instance?.StopArmMoveLoop();
            UFOSEManager.Instance?.PlayArmMoveStop();
        }

        _wasArmMoving = isMoving;
    }

    /// <summary>
    /// 降下・掴み中のみ、爪の直下にあるコイン（wakeUpLayerMask対象）を強制的に起こす。
    /// CoinOptimizer廃止に伴い、寝ているRigidbodyが爪の接触だけでは反応しないケースがあるため、
    /// 掴む瞬間だけ狭い範囲・低頻度で叩き起こす。
    /// </summary>
    void WakeUpNearbyCoins()
    {
        if (_state != ArmState.Descending && _state != ArmState.Grabbing) return;
        if (fingerParts == null || fingerParts.Length == 0 || fingerParts[0] == null) return;

        _wakeUpTimer -= Time.deltaTime;
        if (_wakeUpTimer > 0f) return;
        _wakeUpTimer = wakeUpInterval;

        // OnDrawGizmosSelected の黄色ボックスと同じ計算式（揺れを防ぐためX・ZはarmRoot基準、Yは親フォルダ基準）
        Transform parentFolder = fingerParts[0].parent;
        Vector3 centerPos;
        if (parentFolder != null)
        {
            Transform refTransform = (armRoot != null) ? armRoot : transform;
            centerPos = new Vector3(refTransform.position.x, parentFolder.position.y, refTransform.position.z);
        }
        else
        {
            centerPos = transform.position;
        }

        Vector3 boxCenter = centerPos + wakeUpDownwardsOffset;
        boxCenter.y -= wakeUpDownwardsLength * 0.5f;
        Vector3 halfExtents = new Vector3(wakeUpDownwardsWidth, wakeUpDownwardsLength * 0.5f, wakeUpDownwardsWidth);

        int hitCount = Physics.OverlapBoxNonAlloc(
            boxCenter,
            halfExtents,
            _wakeUpBuffer,
            Quaternion.identity,
            wakeUpLayerMask
        );

        for (int i = 0; i < hitCount; i++)
        {
            // ルーレットアイテム・ダイヤモンド等、まだCoinOptimizerが付いているものはKinematic凍結を解除する
            CoinOptimizer optimizer = _wakeUpBuffer[i].GetComponent<CoinOptimizer>();
            if (optimizer != null)
            {
                optimizer.WakeUp();
                continue;
            }

            Rigidbody rb = _wakeUpBuffer[i].attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                rb.WakeUp();
            }
        }
    }

    /// <summary>
    /// ローグライクスキル「アームの降下地点をわかるようにする」(id32) 取得時に呼ばれる。
    /// </summary>
    public void UnlockDescentLaser()
    {
        descentLaserUnlocked = true;
    }

    /// <summary>降下地点マーカー（薄い円盤）を準備する。未設定なら自動生成する。</summary>
    void InitializeDescentLaser()
    {
        if (descentLaserMarker == null)
        {
            // 円柱を薄く潰すことで円盤に見せる（平面メッシュだと裏面カリングで見えない角度が
            // 出てしまうため、あえて立体の円柱を使い、どの角度からでも確実に見えるようにする）
            GameObject markerObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            markerObj.name = "DescentLaserMarker";
            markerObj.transform.SetParent(transform, false);

            // 見た目だけのマーカーなので、物理的な当たり判定は不要（Raycastの邪魔にもなる）
            Collider col = markerObj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            MeshRenderer mr = markerObj.GetComponent<MeshRenderer>();
            // Sprites/Default はアルファ値をそのまま半透明として扱ってくれるため、薄さの調整に向いている
            Shader markerShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            mr.sharedMaterial = new Material(markerShader);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            descentLaserMarker = markerObj.transform;
        }

        descentLaserMarker.localScale = new Vector3(descentLaserMarkerRadius * 2f, descentLaserMarkerThickness, descentLaserMarkerRadius * 2f);

        var renderer = descentLaserMarker.GetComponent<MeshRenderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            renderer.sharedMaterial.color = descentLaserColor;
        }

        descentLaserMarker.gameObject.SetActive(false);
    }

    /// <summary>
    /// 強化要素が解放されていて、かつアーム移動中（Moving）のときだけ、爪の真下の降下予定地点に
    /// 薄い円盤マーカーを表示する。レーザービーム自体は表示しない。それ以外の状態
    /// （降下中・掴み中など）では非表示にする。
    /// </summary>
    void UpdateDescentLaser()
    {
        if (descentLaserMarker == null) return;

        bool shouldShow = descentLaserUnlocked && _state == ArmState.Moving
            && fingerParts != null && fingerParts.Length > 0 && fingerParts[0] != null;

        if (!shouldShow)
        {
            if (descentLaserMarker.gameObject.activeSelf) descentLaserMarker.gameObject.SetActive(false);
            return;
        }

        // WakeUpNearbyCoins() とは異なり、こちらは見た目のマーカーなのでアームの物理的な揺れに
        // そのまま追従させたい。fingerParts[0].parent（安定用フォルダ）は揺れを反映せず中心からも
        // ズレていたため、実際に描画されている指メッシュ自体の位置を使う。4本の指の平均（重心）を
        // 取ることで、爪の中心かつ実際の揺れをそのまま反映した位置になる。
        Vector3 origin = Vector3.zero;
        int fingerCount = 0;
        foreach (var finger in fingerParts)
        {
            if (finger == null) continue;
            origin += finger.position;
            fingerCount++;
        }
        origin = (fingerCount > 0) ? origin / fingerCount : ((armRoot != null) ? armRoot.position : transform.position);

        // 実際の降下はワールド座標でまっすぐ下に落ちる動きなので、Raycastの向きは単純に真下でよい
        // （揺れの傾きに沿わせると、実際の降下地点とズレたマーカー位置になってしまう）。
        // 揺れの反映は「原点（爪の現在位置）」の方だけで十分。
        //
        // 爪がコインを掴んでいる間は、そのコインがClawCarrierZone（UFOClawCarrierが付いた領域）の
        // 内側に来るため、素直にRaycastすると掴んだコイン自体にマーカーが当たってしまう。
        // ここでは名前・パス検索は行わず、現在有効なUFOClawCarrierコンポーネントの位置・サイズから
        // その領域を判定し、領域内のヒットは透過して地面まで貫通させる（アーム強化でstructure_lv1/2/3が
        // 切り替わっても、コンポーネントの存在で追従するためパスの書き換えは不要）。
        BoxCollider clawCarrierZone = GetActiveClawCarrierCollider();

        int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, _descentLaserHitBuffer, descentLaserMaxDistance, descentLaserLayerMask, QueryTriggerInteraction.Ignore);

        bool foundHit = false;
        RaycastHit closestValidHit = default;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = _descentLaserHitBuffer[i];

            if (clawCarrierZone != null && IsPointInsideBox(candidate.point, clawCarrierZone))
            {
                continue;
            }

            if (candidate.distance < closestDistance)
            {
                closestDistance = candidate.distance;
                closestValidHit = candidate;
                foundHit = true;
            }
        }

        if (foundHit)
        {
            descentLaserMarker.gameObject.SetActive(true);
            // 地面に埋まらないよう、法線方向にわずかに浮かせる
            descentLaserMarker.position = closestValidHit.point + closestValidHit.normal * (descentLaserMarkerThickness * 0.5f);
            // 円柱のローカルY軸（上面・底面の向き）を、当たった面の法線に合わせる
            descentLaserMarker.rotation = Quaternion.FromToRotation(Vector3.up, closestValidHit.normal);
        }
        else
        {
            descentLaserMarker.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 現在有効なClawCarrierZoneのBoxColliderを取得する。パス文字列ではなくUFOClawCarrier
    /// コンポーネントの存在で判定するため、アーム強化でstructure_lv1→lv2→lv3と対象が
    /// 切り替わっても、armRoot配下を探し直すだけで自動的に追従する。
    /// </summary>
    private BoxCollider GetActiveClawCarrierCollider()
    {
        if (_cachedClawCarrier == null || !_cachedClawCarrier.isActiveAndEnabled)
        {
            Transform searchRoot = armRoot != null ? armRoot : transform;
            _cachedClawCarrier = searchRoot.GetComponentInChildren<UFOClawCarrier>(true);
        }

        return _cachedClawCarrier != null ? _cachedClawCarrier.GetComponent<BoxCollider>() : null;
    }

    /// <summary>ワールド座標の点が、指定したBoxColliderの領域内（回転・スケール込み）にあるかを判定する。</summary>
    private static bool IsPointInsideBox(Vector3 worldPoint, BoxCollider box)
    {
        Vector3 localPoint = box.transform.InverseTransformPoint(worldPoint) - box.center;
        Vector3 halfSize = box.size * 0.5f;
        return Mathf.Abs(localPoint.x) <= halfSize.x
            && Mathf.Abs(localPoint.y) <= halfSize.y
            && Mathf.Abs(localPoint.z) <= halfSize.z;
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

        // 初期ウェーブ生成中（およびその後の3秒間の沈殿待ち時間）のみコライダーを無効化する。
        // itemSpawnerが設定されていれば、このアーム自身の機体のスポーン状態（インスタンス版）を見る。
        // 未設定の場合のみ、従来のstatic（実機・練習機共有、片方の完了に巻き込まれる恐れがある）にフォールバックする
        bool shouldDisable = itemSpawner != null ? itemSpawner.IsInitialSpawningInstance : ItemSpawner.IsInitialSpawning;

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

            if (rb != null)
            {
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
        // アーム動作中、または手動開閉中は移動を禁止する
        if (IsInputLocked) return;
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
        // デバッグ用 disableSway がオンの場合、または stabilizeOnDescent が有効で自動昇降動作中 (IsBusy) の場合は揺れ限界を 0 に固定する
        float targetAngle = swayRangeAngle;
        if (disableSway)
        {
            targetAngle = 0f;
        }
        else if (stabilizeOnDescent && IsBusy)
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
        else
        {
            // プレイ中や静止中（揺れを許容する状態）は物理演算の休止（Sleep）を防止し、微弱な揺れを維持し続ける
            Rigidbody rb = swayJoint.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.sleepThreshold = 0f;
                if (rb.IsSleeping())
                {
                    rb.WakeUp();
                }
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
            if (_state == ArmState.Descending)
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
        if (_armRigidbody != null && !_armRigidbody.isKinematic)
        {
            _armRigidbody.linearVelocity = Vector3.zero;
            _armRigidbody.angularVelocity = Vector3.zero;
        }

        if (swayJoint != null)
        {
            Rigidbody swayRb = swayJoint.GetComponent<Rigidbody>();
            if (swayRb != null && !swayRb.isKinematic)
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
                    if (rb != null && !rb.isKinematic)
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
                    Debug.Log($"[TutorialDebug] OnDescentCycleCompleted invoking on {gameObject.name} (id={GetInstanceID()}), subscriberCount={OnDescentCycleCompleted?.GetInvocationList().Length ?? 0}");
                    OnDescentCycleCompleted?.Invoke();
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
                    
                    // 揺れを防ぐため、X・Z座標は揺れない armRoot (または transform) を使い、Y座標（高さ）は親フォルダを使用
                    Vector3 centerPos;
                    if (parentFolder != null)
                    {
                        Transform refTransform = (armRoot != null) ? armRoot : transform;
                        centerPos = new Vector3(refTransform.position.x, parentFolder.position.y, refTransform.position.z);
                    }
                    else
                    {
                        centerPos = transform.position;
                    }

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
                                               _state == ArmState.Grabbing);
                if (isDescendingOrGrabbing)
                {
                    // 降下中・掴み中は直下ボックスのみ
                    if (fingerParts != null && fingerParts.Length > 0 && fingerParts[0] != null)
                    {
                        Transform parentFolder = fingerParts[0].parent;
                        
                        // 揺れを防ぐため、X・Z座標は揺れない armRoot (または transform) を使い、Y座標（高さ）は親フォルダを使用
                        Vector3 centerPos;
                        if (parentFolder != null)
                        {
                            Transform refTransform = (armRoot != null) ? armRoot : transform;
                            centerPos = new Vector3(refTransform.position.x, parentFolder.position.y, refTransform.position.z);
                        }
                        else
                        {
                            centerPos = transform.position;
                        }

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

        // 【新規】swayJoint の Anchor / Connected Anchor の可視化
        if (swayJoint != null)
        {
            // ジョイントのローカルアンカーをワールド座標に変換 (緑の球体)
            Vector3 anchorWorldPos = swayJoint.transform.TransformPoint(swayJoint.anchor);
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(anchorWorldPos, 0.05f);

            // 接続相手（ConnectedBody）のローカルアンカーをワールド座標に変換 (青の球体)
            Vector3 connectedAnchorWorldPos;
            if (swayJoint.connectedBody != null)
            {
                connectedAnchorWorldPos = swayJoint.connectedBody.transform.TransformPoint(swayJoint.connectedAnchor);
            }
            else
            {
                connectedAnchorWorldPos = swayJoint.transform.TransformPoint(swayJoint.connectedAnchor);
            }

            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(connectedAnchorWorldPos, 0.05f);

            // 二つのアンカーを赤い線で結ぶ
            Gizmos.color = Color.red;
            Gizmos.DrawLine(anchorWorldPos, connectedAnchorWorldPos);
        }
    }



    /// <summary>
    /// 降下衝突時にコインの山に当たった際のがさがさ効果音を再生する
    /// </summary>
    private void PlayDescentRustleSound()
    {
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
            float pitch = Random.Range(0.9f, 1.1f);

            // volumeBoostの回数だけ重ねて再生して音量を限界突破
            UFOSEManager.Instance?.PlayArmDescentRustle(volume, pitch, volumeBoost);
            Debug.Log($"[UFOArmController] コインの山に衝突！がさがさ音を再生。枚数: {coinCount}, 音量: {volume:F2}, 重ね数: {volumeBoost}");
        }
        else
        {
            Debug.Log($"[UFOArmController] 衝突しましたが、範囲内のコインが少なすぎるためがさがさ音はスキップします。枚数: {coinCount}");
        }
    }
}
