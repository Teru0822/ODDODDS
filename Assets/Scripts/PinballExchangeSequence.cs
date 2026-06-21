using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ピンボール終了 → 換金（exchange）までの一連の自動シーケンスを統括する。
///
/// 流れ:
///   [監視] PinballSessionController が Playing(State==3) の間、BallWarpCatcher(BallWarpPoint1) が
///          盤上のボールを捕獲（非アクティブ化）。盤上の有効ボールが 0 になり、かつ 1 個以上捕獲済みなら
///          「全ボールが消えた」とみなして終了シーケンスを開始する。
///   1) メインカメラを Scene_Environment の ex_cam_pos1 へ slerp 移動・回転
///   2) cupSpawnPos の position/rotation/scale で cup prefab を設置
///   3) BallWarpPoint2 の座標から、捕獲したボールを 1 個ずつ「標準 Y 重力で自由落下」で再放出（cup に入る）
///   4) cup にボールが入るたびに cup を一瞬大きくしてすぐ戻すポップ演出
///   5) 全部入ったら cup を X 軸 -200°（delta）回転
///   6) 回転後、メインカメラを ex_cam_pos2 へ slerp
///   7) ボールが cup から零れて IntakeTrigger(ExchangeIntakeTrigger) に入り価値加算 + 破棄される。
///      全ボールが消えたら ex_button を自動押下（DispenseMoney）
///   8) 払い出し完了で元の Player 視点へ戻す（PinballSessionController.ReturnToIdle）
///
/// 別シーン(Scene_Environment)のオブジェクト（ex_cam_pos1/2, BallWarpPoint2, cupSpawnPos）は
/// Inspector で割り当てられない場合があるため、名前での実行時解決にフォールバックする。
/// </summary>
[DisallowMultipleComponent]
public class PinballExchangeSequence : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("State を読み、最後に ReturnToIdle を呼ぶ PinballSessionController。null なら自動取得")]
    public PinballSessionController session;

    [Tooltip("動かすメインカメラ。null なら Camera.main")]
    public Camera mainCamera;

    [Tooltip("BallWarpPoint1 の捕獲コンポーネント。null なら自動取得")]
    public BallWarpCatcher warpCatcher;

    [Header("位置（別シーンは名前解決にフォールバック）")]
    [Tooltip("ボールが再放出される位置 BallWarpPoint2")]
    public Transform ballWarpPoint2;
    public string ballWarpPoint2Name = "BallWarpPoint2";

    [Tooltip("cup を設置する位置/回転/スケール cupSpawnPos")]
    public Transform cupSpawnPos;
    public string cupSpawnPosName = "cupSpawnPos";

    [Tooltip("終了時に移動するカメラ位置 ex_cam_pos1（Scene_Environment）")]
    public Transform exCamPos1;
    public string exCamPos1Name = "ex_cam_pos1";

    [Tooltip("cup 回転後に移動するカメラ位置 ex_cam_pos2（Scene_Environment）")]
    public Transform exCamPos2;
    public string exCamPos2Name = "ex_cam_pos2";

    [Tooltip("exchange 視点へ遷移する時に Player（FirstPersonController）をこの位置/角度へ移動させる Ex_exchange")]
    public Transform exExchange;
    public string exExchangeName = "Ex_exchange";

    [Header("Exchange")]
    [Tooltip("設置する cup prefab（BallCup 付き）")]
    public GameObject cupPrefab;

    [Tooltip("価値加算先 ExchangeStation。null なら自動取得")]
    public ExchangeStation exchangeStation;

    [Tooltip("自動押下する ex_button（ExchangeButton）。null なら自動取得（押下時は station.DispenseMoney にフォールバック）")]
    public ExchangeButton exchangeButton;

    [Header("カメラ移動")]
    [Min(0.01f)]
    [Tooltip("カメラ slerp 移動の所要時間（秒）")]
    public float camDuration = 1.2f;

    [Tooltip("カメラ移動の緩急")]
    public AnimationCurve camEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("ボール再放出")]
    [Min(0f)]
    [Tooltip("BallWarpPoint2 からボールを 1 個ずつ出す間隔（秒）")]
    public float reEmitInterval = 0.5f;

    [Min(0f)]
    [Tooltip("BallWarpPoint2 から全ボールを出し終えてから cup 回転を始めるまでの待機秒数")]
    public float delayBeforeCupRotate = 1f;

    [Header("再放出ボールの物理")]
    [Tooltip("再放出ボールに割り当てる PhysicsMaterial。null なら下の摩擦/反発値で自動生成する。" +
             "ピンボール用のボールは摩擦0で『転がらない』ため、cup 内で自然に転がる普通の摩擦に差し替える")]
    public PhysicsMaterial reEmitPhysicMaterial;

    [Range(0f, 1f)]
    [Tooltip("自動生成時の摩擦（0=転がらず滑るだけ。0.3〜0.6 で普通に転がる）")]
    public float reEmitFriction = 0.4f;

    [Range(0f, 1f)]
    [Tooltip("自動生成時の反発（弾み）")]
    public float reEmitBounciness = 0.15f;

    [Header("cup ポップ演出")]
    [Min(1f)]
    [Tooltip("ボールが入った瞬間の最大スケール倍率")]
    public float cupPopScale = 1.15f;

    [Min(0.01f)]
    [Tooltip("ポップ（拡大→縮小）の所要時間（秒）")]
    public float cupPopDuration = 0.18f;

    [Header("cup 回転")]
    [Tooltip("ボール再放出後に cup へ加える回転量（delta, deg）。既定 Y 軸 -120°")]
    public Vector3 cupRotateEuler = new Vector3(0f, -120f, 0f);

    [Min(0.01f)]
    [Tooltip("cup 回転の所要時間（秒）")]
    public float cupRotateDuration = 1.0f;

    [Header("払い出し待ち")]
    [Min(0.5f)]
    [Tooltip("全ボールが IntakeTrigger に消えるのを待つ最大秒数（保険）")]
    public float drainTimeout = 15f;

    [Min(0.5f)]
    [Tooltip("払い出し(DispenseMoney)完了を待つ最大秒数（保険）")]
    public float dispenseTimeout = 20f;

    [Header("デバッグ")]
    public bool logEvents = true;

    // --- 内部状態 ---
    private bool _running;
    private bool _watching;
    private GameObject _cup;
    private BallCup _cupBallCup;
    private Vector3 _cupBaseScale = Vector3.one;
    private int _lastCupCount;
    private Coroutine _popCo;

    private void Awake()
    {
        if (session == null) session = FindAnyObjectByType<PinballSessionController>();
        if (warpCatcher == null) warpCatcher = FindAnyObjectByType<BallWarpCatcher>();
        if (exchangeStation == null) exchangeStation = FindAnyObjectByType<ExchangeStation>();
        if (exchangeButton == null) exchangeButton = FindAnyObjectByType<ExchangeButton>();
    }

    private void Update()
    {
        // ---- フェーズ監視（終了シーケンス未起動時のみ） ----
        if (!_running)
        {
            if (session != null && session.PinBallState == 3) _watching = true;

            if (_watching && warpCatcher != null && warpCatcher.CaughtCount >= 1 && CountActiveBalls() == 0)
            {
                _watching = false;
                _running = true;
                StartCoroutine(RunSequence());
            }
            return;
        }

        // ---- cup ポップ検出（シーケンス中） ----
        if (_cupBallCup != null)
        {
            int c = _cupBallCup.BallCount;
            if (c > _lastCupCount) PlayCupPop();
            _lastCupCount = c;
        }
    }

    /// <summary>盤上に存在する（アクティブな）ピンボールボール数。</summary>
    private int CountActiveBalls()
    {
        var balls = FindObjectsByType<PinballBallController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int n = 0;
        for (int i = 0; i < balls.Length; i++)
            if (balls[i] != null && balls[i].gameObject.activeInHierarchy) n++;
        return n;
    }

    // ====================================================================
    private IEnumerator RunSequence()
    {
        if (warpCatcher != null) warpCatcher.catching = false; // 以降は捕獲しない

        // 参照解決（別シーンは名前で）
        Transform exCam1 = Resolve(exCamPos1, exCamPos1Name);
        Transform warp2 = Resolve(ballWarpPoint2, ballWarpPoint2Name);
        Transform cupPos = Resolve(cupSpawnPos, cupSpawnPosName);
        Transform exCam2 = Resolve(exCamPos2, exCamPos2Name);

        // 捕獲済みボールのプール
        var pool = new List<GameObject>(warpCatcher != null ? warpCatcher.Caught : new List<GameObject>());
        pool.RemoveAll(b => b == null);
        int total = pool.Count;
        if (logEvents) Debug.Log($"[PinballExchangeSequence] 終了シーケンス開始。捕獲ボール数={total}", this);

        // 1) カメラ → ex_cam_pos1（同時に Player を Ex_exchange へ移動）
        //    カメラは Player の子なので、Player を動かすとカメラ(子)も飛ぶ。見た目が飛ばないよう
        //    カメラのワールド姿勢を保持 → Player をテレポート → カメラ姿勢を戻してから slerp 開始する。
        if (mainCamera == null) mainCamera = Camera.main;
        Vector3 camKeepPos = Vector3.zero;
        Quaternion camKeepRot = Quaternion.identity;
        if (mainCamera != null) { camKeepPos = mainCamera.transform.position; camKeepRot = mainCamera.transform.rotation; }
        MovePlayerToExchange();
        if (mainCamera != null) mainCamera.transform.SetPositionAndRotation(camKeepPos, camKeepRot);

        yield return SlerpCamera(exCam1);
        if (Aborted()) { Cleanup(); yield break; }

        // 2) cup 設置
        SpawnCup(cupPos);

        // 3) BallWarpPoint2 から 1 個ずつ自由落下で再放出
        for (int i = 0; i < pool.Count; i++)
        {
            if (Aborted()) { Cleanup(); yield break; }
            ReEmitBall(pool[i], warp2);
            if (reEmitInterval > 0f) yield return new WaitForSeconds(reEmitInterval);
        }

        // 4) 全ボールを出し終えたら delayBeforeCupRotate 秒待ってから回転（ポップ演出は Update が検出）
        if (delayBeforeCupRotate > 0f) yield return new WaitForSeconds(delayBeforeCupRotate);
        if (Aborted()) { Cleanup(); yield break; }
        if (logEvents) Debug.Log($"[PinballExchangeSequence] 再放出完了 cup={CupCount()}/{total} → 回転開始", this);

        // 5) cup を回転（X -200° delta）
        yield return RotateCup();
        if (Aborted()) { Cleanup(); yield break; }

        // 6) カメラ → ex_cam_pos2
        yield return SlerpCamera(exCam2);
        if (Aborted()) { Cleanup(); yield break; }

        // 7) 全ボールが IntakeTrigger に消えるまで待つ
        float drainDeadline = Time.time + drainTimeout;
        while (CountRemaining(pool) > 0 && Time.time < drainDeadline)
        {
            if (Aborted()) { Cleanup(); yield break; }
            yield return null;
        }
        if (logEvents) Debug.Log("[PinballExchangeSequence] 全ボール吸い込み完了 → ex_button 押下", this);

        // 8) ex_button 自動押下 → 払い出し → 完了で Player 復帰
        yield return DispenseAndReturn();
    }

    // --- カメラ slerp ---
    private IEnumerator SlerpCamera(Transform target)
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null || target == null)
        {
            if (target == null) Debug.LogWarning("[PinballExchangeSequence] カメラ目標が見つかりません（名前解決失敗？）。", this);
            yield break;
        }
        Transform cam = mainCamera.transform;
        Vector3 p0 = cam.position;
        Quaternion r0 = cam.rotation;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, camDuration);
            float e = camEase.Evaluate(Mathf.Clamp01(t));
            cam.position = Vector3.Lerp(p0, target.position, e);
            cam.rotation = Quaternion.Slerp(r0, target.rotation, e);
            yield return null;
        }
        cam.position = target.position;
        cam.rotation = target.rotation;
    }

    // --- Player を Ex_exchange の位置/角度へ移動 ---
    private void MovePlayerToExchange()
    {
        Transform target = Resolve(exExchange, exExchangeName);
        if (target == null) return;

        var fpc = FindAnyObjectByType<App.Player.FirstPersonController>();
        if (fpc == null)
        {
            Debug.LogWarning("[PinballExchangeSequence] FirstPersonController が見つからず Player を移動できません。", this);
            return;
        }

        Transform player = fpc.transform;
        // CharacterController を切ってからテレポート（CC が transform を上書きするのを防ぐ）
        var cc = player.GetComponent<CharacterController>();
        bool ccWas = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;
        player.SetPositionAndRotation(target.position, target.rotation);
        if (cc != null) cc.enabled = ccWas;

        if (logEvents) Debug.Log($"[PinballExchangeSequence] Player を Ex_exchange へ移動: {target.position}", this);
    }

    // --- cup 設置 ---
    private void SpawnCup(Transform cupPos)
    {
        if (cupPrefab == null || cupPos == null)
        {
            Debug.LogWarning("[PinballExchangeSequence] cupPrefab または cupSpawnPos が未設定のため cup を設置できません。", this);
            return;
        }
        _cup = Instantiate(cupPrefab, cupPos.position, cupPos.rotation);
        _cup.transform.localScale = cupPos.lossyScale; // cupSpawnPos のスケールを反映
        _cupBaseScale = _cup.transform.localScale;

        // cup はその場で静止させる（ボールが落ちて溜まるように）
        var rb = _cup.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        _cupBallCup = _cup.GetComponent<BallCup>();
        _lastCupCount = 0;
    }

    // --- ボール再放出（標準 Rigidbody 重力のみ。追加の力は一切なし） ---
    private void ReEmitBall(GameObject ball, Transform warp2)
    {
        if (ball == null || warp2 == null) return;
        ball.transform.position = warp2.position;

        // 台ローカル重力（LocalGravityBody）を無効化して、標準 Y-down 重力だけにする
        var lgb = ball.GetComponent<LocalGravityBody>();
        if (lgb != null) lgb.enabled = false;

        // ピンボール用ボールは摩擦0で転がらないため、普通に転がる物理マテリアルへ差し替える
        var mat = GetReEmitMaterial();
        if (mat != null)
        {
            foreach (var col in ball.GetComponentsInChildren<Collider>(true))
            {
                if (col != null && !col.isTrigger) col.sharedMaterial = mat;
            }
        }

        ball.SetActive(true);

        var rb = ball.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;       // 重力は標準 Rigidbody.useGravity のみ
            rb.sleepThreshold = 0f;     // cup 内で眠って動かなくなる（=isKinematic 化に見える）のを防ぐ
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
        }
    }

    private PhysicsMaterial _reEmitMat;

    private PhysicsMaterial GetReEmitMaterial()
    {
        if (reEmitPhysicMaterial != null) return reEmitPhysicMaterial;
        if (_reEmitMat == null)
        {
            _reEmitMat = new PhysicsMaterial("ReEmitBall")
            {
                dynamicFriction = reEmitFriction,
                staticFriction = reEmitFriction,
                bounciness = reEmitBounciness,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Average
            };
        }
        return _reEmitMat;
    }

    private int CupCount() => _cupBallCup != null ? _cupBallCup.BallCount : 0;

    private int CountRemaining(List<GameObject> pool)
    {
        int n = 0;
        for (int i = 0; i < pool.Count; i++)
            if (pool[i] != null && pool[i].activeInHierarchy) n++;
        return n;
    }

    // --- cup ポップ演出 ---
    private void PlayCupPop()
    {
        if (_cup == null) return;
        if (_popCo != null) StopCoroutine(_popCo);
        _popCo = StartCoroutine(CupPopCo());
    }

    private IEnumerator CupPopCo()
    {
        Vector3 baseS = _cupBaseScale;
        Vector3 peak = baseS * cupPopScale;
        float half = Mathf.Max(0.001f, cupPopDuration * 0.5f);
        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            if (_cup == null) yield break;
            _cup.transform.localScale = Vector3.Lerp(baseS, peak, t / half);
            yield return null;
        }
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            if (_cup == null) yield break;
            _cup.transform.localScale = Vector3.Lerp(peak, baseS, t / half);
            yield return null;
        }
        if (_cup != null) _cup.transform.localScale = baseS;
        _popCo = null;
    }

    // --- cup 回転 ---
    // キネマティックな cup を Rigidbody.MoveRotation で物理的に回す。
    // transform 直接代入（テレポート）だと、カップ内で眠っている動的ボールを起こせず押し出せない
    // （= isKinematic になったように積み上がる）ため、MoveRotation + 毎ステップ WakeUp で確実に零す。
    private IEnumerator RotateCup()
    {
        if (cupRotateEuler == Vector3.zero || _cup == null) yield break;

        var rb = _cup.GetComponent<Rigidbody>();
        Quaternion startRot = _cup.transform.rotation;
        Quaternion endRot = startRot * Quaternion.Euler(cupRotateEuler); // ローカル軸での delta 回転
        float dur = Mathf.Max(0.01f, cupRotateDuration);
        float t = 0f;

        while (t < dur)
        {
            t += Time.fixedDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            Quaternion cur = Quaternion.Slerp(startRot, endRot, u);
            if (rb != null) rb.MoveRotation(cur);
            else _cup.transform.rotation = cur;
            WakeCupBalls();
            yield return new WaitForFixedUpdate();
        }

        if (rb != null) rb.MoveRotation(endRot);
        else _cup.transform.rotation = endRot;
        WakeCupBalls();
    }

    /// <summary>cup 内のボールが眠って動かなくなるのを防ぐため、回転中に起こし続ける。</summary>
    private void WakeCupBalls()
    {
        if (_cupBallCup == null) return;
        var balls = _cupBallCup.Balls;
        for (int i = 0; i < balls.Count; i++)
        {
            var b = balls[i];
            if (b == null) continue;
            var brb = b.GetComponent<Rigidbody>();
            if (brb != null && !brb.isKinematic) brb.WakeUp();
        }
    }

    // --- 払い出し → 完了で Player 復帰 ---
    private IEnumerator DispenseAndReturn()
    {
        bool dispensed = false;
        UnityAction handler = () => dispensed = true;
        bool subscribed = exchangeStation != null && exchangeStation.onDispenseComplete != null;
        if (subscribed) exchangeStation.onDispenseComplete.AddListener(handler);

        // ex_button 自動押下
        if (exchangeButton != null) exchangeButton.OnPressed();
        else if (exchangeStation != null) exchangeStation.DispenseMoney();
        else Debug.LogWarning("[PinballExchangeSequence] ExchangeButton / ExchangeStation 未設定のため払い出しできません。", this);

        // 払い出し完了（onDispenseComplete）まで待つ。購読できない場合はタイムアウトで進む
        float deadline = Time.time + dispenseTimeout;
        while (subscribed && !dispensed && Time.time < deadline) yield return null;
        if (subscribed) exchangeStation.onDispenseComplete.RemoveListener(handler);

        if (logEvents) Debug.Log("[PinballExchangeSequence] 払い出し完了 → Player 視点へ復帰", this);

        // 8) 元の Player 視点へ戻す
        if (session != null) session.ReturnToIdle();

        Cleanup();
    }

    // --- 後始末（次ラウンドに備える） ---
    private void Cleanup()
    {
        if (_popCo != null) { StopCoroutine(_popCo); _popCo = null; }
        if (_cup != null) { Destroy(_cup); _cup = null; }
        _cupBallCup = null;
        _lastCupCount = 0;

        if (warpCatcher != null) { warpCatcher.ClearCaught(); warpCatcher.catching = true; }

        _running = false;
        _watching = false;
    }

    /// <summary>シーケンス中に Escape 等で Idle(0) に戻されたら中断する。</summary>
    private bool Aborted() => session != null && session.PinBallState == 0;

    private Transform Resolve(Transform assigned, string objectName)
    {
        if (assigned != null) return assigned;
        if (string.IsNullOrEmpty(objectName)) return null;
        var all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == objectName) return all[i];
        Debug.LogWarning($"[PinballExchangeSequence] '{objectName}' が見つかりません（別シーン未ロード？ Inspector で直接割り当ててください）。", this);
        return null;
    }
}
