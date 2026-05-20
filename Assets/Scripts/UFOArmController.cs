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

    // ─────────────────────────────────────
    [Header("昇降設定")]
    [Tooltip("自動下降時の StretchRope の速度倍率")]
    public float descentSpeedMultiplier = 1.5f;
    [Tooltip("掴んでから上昇を開始するまでの待機秒数")]
    public float grabWaitSeconds = 0.5f;
    [Tooltip("何かにぶつかった後、さらに下降を続ける秒数（コインをしっかり掴むため）")]
    public float postCollisionDescentSeconds = 0.15f;

    [Header("コイン最適化解除（WakeUp）設定")]
    [Tooltip("アームが下降する際、どれくらいの範囲のコインを叩き起こすか")]
    public float wakeUpRadius = 1.0f;
    [Tooltip("叩き起こし処理を実行する間隔（秒）。処理落ちを防ぐため毎フレームは行いません")]
    public float wakeUpInterval = 0.2f;

    [Header("【新規】第3アーム用 吸着(マグネット)機能")]
    [Tooltip("オンにすると、爪が閉じる時と上昇する時に周囲のコインを吸着します")]
    public bool isMagnetMode = false;
    [Tooltip("吸着する範囲（半径）")]
    public float magnetRadius = 3.0f;
    [Tooltip("中心に引き寄せる力")]
    public float magnetForce = 50f;

    // ─────────────────────────────────────
    // 内部状態
    private ArmState _state = ArmState.Idle;
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

    // ─────────────────────────────────────
    [Header("揺れ（Sway）設定")]
    [Tooltip("揺れ（慣性・振り子挙動）を有効にするか")]
    public bool enableSway = true;

    [Tooltip("指以外の、一緒に揺らしたいパーツ（6番のロープなど）を指定します")]
    public Transform[] extraSwayParts;
    private Quaternion[] _extraSwayDefaultRot;

    [Header("ロープの揺れ設定（Extra Sway Parts用）")]
    public float extraSwaySensitivity = 2f;
    public float extraSwayDamping = 3f;
    public float extraSwaySpringForce = 15f;

    [Header("爪の揺れ設定（Finger Parts用）")]
    [Tooltip("爪の土台（finger）など、開閉アニメはないが爪と同じ強さで揺れてほしいパーツ")]
    public Transform[] clawBaseParts;
    private Quaternion[] _clawBaseDefaultRot;

    [UnityEngine.Serialization.FormerlySerializedAs("swaySensitivity")]
    public float clawSwaySensitivity = 2f;
    [UnityEngine.Serialization.FormerlySerializedAs("swayDamping")]
    public float clawSwayDamping = 3f;
    [UnityEngine.Serialization.FormerlySerializedAs("swaySpringForce")]
    public float clawSwaySpringForce = 15f;

    private Vector3 _lastWorldPos;
    
    // Extra Sway State
    private Vector3 _ropeSwayAngle;
    private Vector3 _ropeSwayVelocity;
    public Quaternion ropeSwayRot { get; private set; } = Quaternion.identity;

    // Claw Sway State
    private Vector3 _clawSwayAngle;
    private Vector3 _clawSwayVelocity;
    public Quaternion clawSwayRot { get; private set; } = Quaternion.identity;

    // ─────────────────────────────────────
    void Start()
    {
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

        // その他揺らすパーツの初期回転を記録
        if (extraSwayParts != null && extraSwayParts.Length > 0)
        {
            _extraSwayDefaultRot = new Quaternion[extraSwayParts.Length];
            for (int i = 0; i < extraSwayParts.Length; i++)
            {
                if (extraSwayParts[i] != null)
                    _extraSwayDefaultRot[i] = extraSwayParts[i].localRotation;
            }
        }

        // 爪土台の初期回転を記録
        if (clawBaseParts != null && clawBaseParts.Length > 0)
        {
            _clawBaseDefaultRot = new Quaternion[clawBaseParts.Length];
            for (int i = 0; i < clawBaseParts.Length; i++)
            {
                if (clawBaseParts[i] != null)
                    _clawBaseDefaultRot[i] = clawBaseParts[i].localRotation;
            }
        }

        if (armRoot != null) _lastWorldPos = armRoot.position;
        if (rail1 != null) _rail1InitialPos = rail1.position;
        if (rail2 != null) _rail2InitialPos = rail2.position;
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
        if (armRoot == null) return;

        // コインが降っている最中、および落下中の時間帯を判定する
        bool shouldDisable = ItemSpawner.IsSpawning || (Time.time < CoinOptimizer.freezeStartTime);

        if (shouldDisable != _collidersDisabledForSpawn)
        {
            _collidersDisabledForSpawn = shouldDisable;
            Collider[] colliders = armRoot.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
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
        UpdateSwayPhysics();
        UpdateRailFollow();
    }

    void WakeUpNearbyCoins()
    {
        // 処理落ちを防ぐため、常に実行するのではなく一定時間ごと（例: 0.2秒ごと）に実行する
        _wakeUpTimer -= Time.deltaTime;
        if (_wakeUpTimer > 0f) return;
        _wakeUpTimer = wakeUpInterval;

        if (fingerParts == null || fingerParts.Length == 0 || fingerParts[0] == null) return;

        // まずアームの中心（指の親オブジェクト）の周囲を起こす
        Transform parentFolder = fingerParts[0].parent;
        Vector3 centerPos = (parentFolder != null) ? parentFolder.position : transform.position;
        WakeUpInSphere(centerPos, wakeUpRadius);

        // さらに、それぞれの指の周囲も起こす（爪が大きく広がっているLv2やLv3の形状でも確実に起こすため）
        foreach (Transform finger in fingerParts)
        {
            if (finger != null)
            {
                WakeUpInSphere(finger.position, wakeUpRadius);
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

    void UpdateSwayPhysics()
    {
        if (armRoot == null) return;
        
        float dt = Time.fixedDeltaTime;
        if (dt == 0f) return;

        if (!enableSway)
        {
            _ropeSwayAngle = Vector3.zero;
            _ropeSwayVelocity = Vector3.zero;
            _clawSwayAngle = Vector3.zero;
            _clawSwayVelocity = Vector3.zero;
            ropeSwayRot = Quaternion.identity;
            clawSwayRot = Quaternion.identity;
            _lastWorldPos = (_armRigidbody != null) ? _armRigidbody.position : armRoot.position;
            return;
        }

        // 座標から現在の移動速度（Velocity）を取得
        Vector3 currentPos = (_armRigidbody != null) ? _armRigidbody.position : armRoot.position;
        Vector3 currentVel = (currentPos - _lastWorldPos) / dt;
        _lastWorldPos = currentPos;

        // 【ロープ（Extra）側の揺れ計算】
        Vector3 ropeTargetSway = new Vector3(currentVel.z, 0f, currentVel.x) * extraSwaySensitivity;
        Vector3 ropeAngleDiff = ropeTargetSway - _ropeSwayAngle;
        Vector3 ropeSpringAccel = (ropeAngleDiff * extraSwaySpringForce) - (_ropeSwayVelocity * extraSwayDamping);
        _ropeSwayVelocity += ropeSpringAccel * dt;
        _ropeSwayAngle += _ropeSwayVelocity * dt;
        _ropeSwayAngle.x = Mathf.Clamp(_ropeSwayAngle.x, -50f, 50f);
        _ropeSwayAngle.z = Mathf.Clamp(_ropeSwayAngle.z, -50f, 50f);
        ropeSwayRot = Quaternion.Euler(_ropeSwayAngle.x, 0f, _ropeSwayAngle.z);

        // 【爪（Claw）側の揺れ計算】
        Vector3 clawTargetSway = new Vector3(currentVel.z, 0f, currentVel.x) * clawSwaySensitivity;
        Vector3 clawAngleDiff = clawTargetSway - _clawSwayAngle;
        Vector3 clawSpringAccel = (clawAngleDiff * clawSwaySpringForce) - (_clawSwayVelocity * clawSwayDamping);
        _clawSwayVelocity += clawSpringAccel * dt;
        _clawSwayAngle += _clawSwayVelocity * dt;
        _clawSwayAngle.x = Mathf.Clamp(_clawSwayAngle.x, -50f, 50f);
        _clawSwayAngle.z = Mathf.Clamp(_clawSwayAngle.z, -50f, 50f);
        clawSwayRot = Quaternion.Euler(_clawSwayAngle.x, 0f, _clawSwayAngle.z);
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

    void UpdateFingersAndSway()
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

                // 2. 爪（finger）の個別揺れは適用しない（親オブジェクトである structure の揺れをそのまま引き継ぐため）
            }
        }

        // 爪土台に対する揺れの適用（Claw設定）
        if (clawBaseParts != null && clawBaseParts.Length > 0)
        {
            for (int i = 0; i < clawBaseParts.Length; i++)
            {
                if (clawBaseParts[i] == null) continue;
                clawBaseParts[i].localRotation = _clawBaseDefaultRot[i];
                clawBaseParts[i].rotation = clawSwayRot * clawBaseParts[i].rotation;
            }
        }

        // その他（6番ロープなど）に対する揺れの適用（Extra設定）
        if (extraSwayParts != null && extraSwayParts.Length > 0)
        {
            for (int i = 0; i < extraSwayParts.Length; i++)
            {
                if (extraSwayParts[i] == null) continue;

                // 1. 本来のローカル回転をセット
                extraSwayParts[i].localRotation = _extraSwayDefaultRot[i];
                // 2. ロープ用の個別揺れを適用
                extraSwayParts[i].rotation = ropeSwayRot * extraSwayParts[i].rotation;
            }
        }
    }

    public void OnClawCollided()
    {
        // 下降中に景品や床にぶつかったら、すぐに上昇せずに少しだけ下降を継続する
        if (_state == ArmState.Descending)
        {
            _state = ArmState.PostCollisionDescending;
            _stateTimer = postCollisionDescentSeconds;
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

            if (data.invertFingerAngle != null && data.invertFingerAngle.Length > 0)
                this.invertFingerAngle = data.invertFingerAngle;

            if (data.fingerAngleOffsets != null && data.fingerAngleOffsets.Length > 0)
                this.fingerAngleOffsets = data.fingerAngleOffsets;

            if (data.customOpenLocalPositions != null && data.customOpenLocalPositions.Length > 0)
                this.customOpenLocalPositions = data.customOpenLocalPositions;

            if (data.customOpenLocalRotations != null && data.customOpenLocalRotations.Length > 0)
                this.customOpenLocalRotations = data.customOpenLocalRotations;
        }

        // 子オブジェクトたち（finger1, finger2...）を配列に登録し直す
        int childCount = activeClawObj.transform.childCount;
        fingerParts = new Transform[childCount];
        for (int i = 0; i < childCount; i++)
        {
            fingerParts[i] = activeClawObj.transform.GetChild(i);
        }

        // もう一度初期化処理を走らせる
        InitializeFingers();
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
            this.invertFingerAngle = data.invertFingerAngle;
            this.fingerAngleOffsets = data.fingerAngleOffsets;
            this.useCustomOpenTransform = data.useCustomOpenTransform;
            this.customOpenLocalPositions = data.customOpenLocalPositions;
            this.customOpenLocalRotations = data.customOpenLocalRotations;
        }

        // プレハブの「子オブジェクト」たち（finger1, finger2...）を配列に登録し直す
        int childCount = newClawObj.transform.childCount;
        fingerParts = new Transform[childCount];
        for (int i = 0; i < childCount; i++)
        {
            fingerParts[i] = newClawObj.transform.GetChild(i);
        }

        // もう一度初期化処理を走らせる
        InitializeFingers();
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
}
