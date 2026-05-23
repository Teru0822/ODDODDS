using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ピンボール盤面の払い出し口に置く「cup」コンポーネント。
/// 子に置いた Trigger Collider が PinballBallController を検出して内部リストに記録する。
/// レティクルで照準されると（ボールが1個以上ある時のみ）水色にハイライト。
/// クリックで Bin プレハブが Player に渡り、cup 自身は破棄される。
/// </summary>
[DisallowMultipleComponent]
public class BallCup : InteractableHighlight
{
    [Header("ピックアップ設定")]
    [Tooltip("拾い上げ時にプレイヤーの手に持たせる Bin プレハブ")]
    public GameObject binPrefab;

    [Tooltip("ボール検出: tag が空なら PinballBallController コンポーネントで判定。指定すると CompareTag で判定")]
    public string ballTag = "";

    [Tooltip("中身が空でもハイライト/ピックアップ可能にする")]
    public bool allowEmptyPickup = false;

    [Header("起動時スキャン")]
    [Tooltip("Start 時に cup 内部のボールを Physics.OverlapBox でスキャンして検出する (シーン上に最初から置かれたボールも拾える)")]
    public bool scanBallsOnStart = true;

    [Tooltip("起動時スキャン後も定期的にリスキャンする間隔 (秒)。0 で再スキャンなし")]
    public float rescanInterval = 0f;

    [Header("デバッグ")]
    [Tooltip("ボール検出やリスト変更を Console に出力")]
    public bool logEvents = false;

    private readonly List<GameObject> _balls = new List<GameObject>();
    private float _nextRescanTime;

    public int BallCount
    {
        get { _balls.RemoveAll(b => b == null); return _balls.Count; }
    }

    public IReadOnlyList<GameObject> Balls => _balls;
    public GameObject BinPrefab => binPrefab;

    protected override void Awake()
    {
        base.Awake();
        WarnIfNoColliders();
    }

    private void Start()
    {
        if (scanBallsOnStart) RescanBalls();
    }

    private void OnTriggerStay(Collider other)
    {
        // OnTriggerEnter を取りこぼしたボールを毎フレーム拾い直す保険
        if (!IsBall(other)) return;
        var go = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
        if (!_balls.Contains(go))
        {
            _balls.Add(go);
            if (logEvents) Debug.Log($"[BallCup] '{name}' ball stay (late detect): {go.name} (count={_balls.Count})", this);
        }
    }

    private void Update()
    {
        if (rescanInterval > 0f && Time.time >= _nextRescanTime)
        {
            _nextRescanTime = Time.time + rescanInterval;
            RescanBalls();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsBall(other)) return;
        var go = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
        if (!_balls.Contains(go))
        {
            _balls.Add(go);
            if (logEvents) Debug.Log($"[BallCup] '{name}' ball enter: {go.name} (count={_balls.Count})", this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsBall(other)) return;
        var go = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
        if (_balls.Remove(go))
        {
            if (logEvents) Debug.Log($"[BallCup] '{name}' ball exit: {go.name} (count={_balls.Count})", this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Trigger Collider の AABB を Scene ビューに可視化 (オレンジ)
        var triggers = GetComponentsInChildren<Collider>(true);
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.4f);
        foreach (var c in triggers)
        {
            if (c == null || !c.isTrigger) continue;
            Bounds b = c.bounds;
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }

    /// <summary>Trigger Collider の AABB 内を Physics.OverlapBox でスキャンしてボールを取り直す。</summary>
    public void RescanBalls()
    {
        _balls.RemoveAll(b => b == null);
        var triggers = GetComponentsInChildren<Collider>(false);
        int found = 0;
        foreach (var tcol in triggers)
        {
            if (tcol == null || !tcol.enabled || !tcol.isTrigger) continue;
            Bounds b = tcol.bounds;
            // OverlapBox は AABB 限定なので回転した cup でも近似で拾える
            Collider[] hits = Physics.OverlapBox(b.center, b.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (!IsBall(h)) continue;
                var go = h.attachedRigidbody != null ? h.attachedRigidbody.gameObject : h.gameObject;
                if (!_balls.Contains(go))
                {
                    _balls.Add(go);
                    found++;
                }
            }
        }
        if (logEvents) Debug.Log($"[BallCup] '{name}' rescan: 追加={found}, total={_balls.Count}", this);
    }

    private bool IsBall(Collider other)
    {
        if (!string.IsNullOrEmpty(ballTag))
        {
            return other.CompareTag(ballTag);
        }
        return other.GetComponentInParent<PinballBallController>() != null;
    }

    public override bool IsInteractable(CupPickupController pickup)
    {
        if (pickup == null) return false;
        if (pickup.IsHoldingBin) return false; // 既に Bin を持っているなら拾えない
        if (allowEmptyPickup) return true;
        return BallCount > 0;
    }

    /// <summary>cup の中身を取り出して返し、ボールを SetActive(false) で隠す。リストは空になる。</summary>
    public List<GameObject> TakeContents()
    {
        _balls.RemoveAll(b => b == null);
        foreach (var b in _balls)
        {
            if (b == null) continue;
            // 物理を止めて隠す
            var rb = b.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            b.SetActive(false);
        }
        var copy = new List<GameObject>(_balls);
        _balls.Clear();
        return copy;
    }

    /// <summary>
    /// この cup を deltaEuler で指定した分だけ duration 秒かけてスムーズに回転させる。
    /// worldSpace=true ならワールド空間、false ならローカル空間。
    /// duration が 0 以下なら 0.1 秒に強制（即時 Rotate だと「回転しない」と誤認されやすいため）。
    /// 既に同じ cup で動いているコルーチンがあれば停止して新しいものに置き換える。
    /// Rigidbody がアタッチされている場合は MoveRotation を使い物理エンジンと衝突しないようにする。
    /// </summary>
    public Coroutine StartSmoothRotation(Vector3 deltaEuler, float duration, bool worldSpace, AnimationCurve curve = null)
    {
        if (deltaEuler == Vector3.zero)
        {
            Debug.LogWarning($"[BallCup] '{name}' StartSmoothRotation: deltaEuler=(0,0,0) のため回転をスキップ", this);
            return null;
        }
        // 旧シリアライズデータで duration=0 の場合に「回転しない」ように見える事故を防止
        if (duration <= 0f) duration = 1.0f;
        // 既存の回転コルーチンがあれば停止
        if (_rotationCoroutine != null) StopCoroutine(_rotationCoroutine);
        _rotationCoroutine = StartCoroutine(SmoothRotateCoroutine(deltaEuler, duration, worldSpace, curve));
        Debug.Log($"[BallCup] '{name}' 回転開始: delta={deltaEuler}, duration={duration}s, worldSpace={worldSpace}", this);
        return _rotationCoroutine;
    }

    private Coroutine _rotationCoroutine;

    private IEnumerator SmoothRotateCoroutine(Vector3 deltaEuler, float duration, bool worldSpace, AnimationCurve curve)
    {
        // Rigidbody 検出 (非 kinematic な場合は MoveRotation で物理側を駆動)
        var rb = GetComponent<Rigidbody>();
        bool usePhysics = rb != null && !rb.isKinematic;
        if (rb != null && usePhysics)
        {
            // 物理側で回転中に重力で吹っ飛ばないよう角速度も止めておく
            rb.angularVelocity = Vector3.zero;
        }

        // 終了角度を決定
        Quaternion startRot = worldSpace ? transform.rotation : transform.localRotation;
        Quaternion delta = Quaternion.Euler(deltaEuler);
        Quaternion endRot = worldSpace ? delta * startRot : startRot * delta;

        // カーブが null/空ならリニア相当
        bool useCurve = curve != null && curve.length >= 2;

        float t = 0f;
        while (t < duration)
        {
            // dt が 0 になり得るケース (timeScale=0 等) は最低 0.0001 を入れて完了する
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            t += dt;
            float u = Mathf.Clamp01(t / duration);
            if (useCurve) u = curve.Evaluate(u);
            Quaternion cur = Quaternion.Slerp(startRot, endRot, u);
            ApplyRotation(cur, worldSpace, rb, usePhysics);
            yield return null;
        }
        ApplyRotation(endRot, worldSpace, rb, usePhysics);
        _rotationCoroutine = null;
        Debug.Log($"[BallCup] '{name}' 回転完了", this);
    }

    private void ApplyRotation(Quaternion rot, bool worldSpace, Rigidbody rb, bool usePhysics)
    {
        if (usePhysics && rb != null)
        {
            // Rigidbody は MoveRotation で駆動 (物理エンジンと整合)
            if (worldSpace)
            {
                rb.MoveRotation(rot);
            }
            else
            {
                Quaternion parentRot = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
                rb.MoveRotation(parentRot * rot);
            }
        }
        else
        {
            if (worldSpace) transform.rotation = rot;
            else transform.localRotation = rot;
        }
    }

    /// <summary>外部 (exchange) から既存のボール参照を流し込んで cup の中身として確定させる。</summary>
    public void ReceiveContents(List<GameObject> balls, float scatterRadius = 0.1f)
    {
        if (balls == null) return;
        foreach (var b in balls)
        {
            if (b == null) continue;
            Vector3 pos = transform.position + Random.insideUnitSphere * scatterRadius;
            b.transform.position = pos;
            var rb = b.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            b.SetActive(true);
            if (!_balls.Contains(b)) _balls.Add(b);
        }
    }
}
