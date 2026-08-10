using UnityEngine;

/// <summary>
/// ポールオブジェクト（poll2, poll3）のスライド伸縮を担う。
/// UFOArmController からの外部制御で伸縮する。
/// </summary>
public class StretchRope : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("伸縮スピード")]
    public float stretchSpeed = 1f;

    [Header("Pole Slide Settings")]
    [Tooltip("スライドさせる第2ポール")]
    public Transform poll2;

    [Tooltip("スライドさせる第3ポール")]
    public Transform poll3;

    [Tooltip("各ポールのローカルスライド方向")]
    public Vector3 slideDirection = new Vector3(0, 0, 1);

    [Tooltip("poll2の最大移動距離")]
    public float poll2MaxDistance = 1.5f;

    [Tooltip("poll3の最大移動距離")]
    public float poll3MaxDistance = 1.5f;

    [Header("附属オブジェクト（爪など）")]
    [Tooltip("ポール先端に合わせて連動させたいオブジェクト群（親子関係になっていない場合に使用）")]
    public Transform[] attachedObjects;

    // ─────────────────────────────────────
    // 内部状態
    private float   _stretchTime;       // 0(縮み) 〜 1(最大)

    private Vector3 _poll2InitialLocalPos;
    private Vector3 _poll3InitialLocalPos;
    private Vector3[] _originalAttachedLocalPos;

    // 外部制御
    private bool  _externalControl  = false;  // true = UFOArmController が制御中
    private float _externalDir      = 0f;     // +1:伸びる  -1:縮む
    private float _externalSpeedMul = 1f;

    // ─────────────────────────────────────
    void Start()
    {
        if (poll2 != null)
        {
            _poll2InitialLocalPos = poll2.localPosition;
        }
        if (poll3 != null)
        {
            _poll3InitialLocalPos = poll3.localPosition;
        }

        if (attachedObjects != null && attachedObjects.Length > 0)
        {
            _originalAttachedLocalPos = new Vector3[attachedObjects.Length];
            for (int i = 0; i < attachedObjects.Length; i++)
            {
                if (attachedObjects[i] != null)
                    _originalAttachedLocalPos[i] = attachedObjects[i].localPosition;
            }
        }
    }

    // ─────────────────────────────────────
    // 外部制御 API（UFOArmController から呼ぶ）

    /// <summary>自動下降を開始（ stretchTime を 1 に向けて動かす）</summary>
    public void StartExternalDescent(float speedMultiplier = 1f)
    {
        Debug.Log($"[StretchRope] StartExternalDescent called on {gameObject.name}. SpeedMul: {speedMultiplier}, current stretchTime: {_stretchTime}, poll2: {(poll2 != null ? poll2.name : "null")}, poll3: {(poll3 != null ? poll3.name : "null")}");
        _externalControl  = true;
        _externalDir      = 1f;
        _externalSpeedMul = speedMultiplier;
    }

    /// <summary>自動上昇を開始（ stretchTime を 0 に向けて動かす）</summary>
    public void StartExternalAscent(float speedMultiplier = 1f)
    {
        Debug.Log($"[StretchRope] StartExternalAscent called on {gameObject.name}. SpeedMul: {speedMultiplier}, current stretchTime: {_stretchTime}");
        _externalControl  = true;
        _externalDir      = -1f;
        _externalSpeedMul = speedMultiplier;
    }

    /// <summary>外部制御を解除する</summary>
    public void StopExternalControl()
    {
        _externalControl = false;
        _externalDir     = 0f;
    }

    /// <summary>外部制御のまま、昇降を一時停止する</summary>
    public void PauseExternalControl()
    {
        _externalControl = true;
        _externalDir     = 0f;
    }

    /// <summary>最大まで伸びているか</summary>
    public bool IsAtMax() => _stretchTime >= 1f;

    /// <summary>完全に縮んでいるか</summary>
    public bool IsAtMin() => _stretchTime <= 0f;

    // ─────────────────────────────────────
    void FixedUpdate()
    {
        // ── 伸縮量の更新 ──
        if (_externalControl)
        {
            // 外部制御（自動昇降）
            _stretchTime += _externalDir * Time.deltaTime * stretchSpeed * _externalSpeedMul;
            _stretchTime  = Mathf.Clamp01(_stretchTime);

            // 上昇して完全に縮みきった（最上点に達した）時のみ、自動で外部制御を解除する
            if (_externalDir < 0f && _stretchTime <= 0f)
            {
                StopExternalControl();
            }
        }

        float t = Mathf.SmoothStep(0f, 1f, _stretchTime);

        // ── ポールスライドによる伸縮 ──
        if (poll2 != null)
        {
            poll2.localPosition = _poll2InitialLocalPos + slideDirection * (poll2MaxDistance * t);
        }
        if (poll3 != null)
        {
            poll3.localPosition = _poll3InitialLocalPos + slideDirection * (poll3MaxDistance * t);
        }

        // デバッグ用ログ（1秒間に数回だけ出すように調整）
        if (Time.frameCount % 60 == 0 && (_externalControl || _stretchTime > 0f))
        {
            Debug.Log($"[StretchRope] Pole Slide Mode - _stretchTime: {_stretchTime}, t: {t}, isExternal: {_externalControl}");
        }

        // ── attachedObjects の追従（真下への移動のみ） ──
        if (attachedObjects != null && attachedObjects.Length > 0)
        {
            float totalMove = (poll2MaxDistance + poll3MaxDistance) * t;

            for (int i = 0; i < attachedObjects.Length; i++)
            {
                if (attachedObjects[i] == null) continue;

                // 物理演算（Configurable Joint等）が有効な非KinematicなRigidbodyが自身または親にある場合は、
                // 強制的な座標書き換えを行うと物理挙動と競合（ガタツキ等）するため処理をスキップする
                Rigidbody rb = attachedObjects[i].GetComponentInParent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    continue;
                }

                // 本来の真下にあるワールド座標
                Vector3 baseWorldPos = (attachedObjects[i].parent != null)
                                     ? attachedObjects[i].parent.TransformPoint(_originalAttachedLocalPos[i])
                                     : _originalAttachedLocalPos[i];

                // 合計スライド移動量だけ下に落とす
                baseWorldPos.y -= totalMove;

                attachedObjects[i].position = baseWorldPos;
            }
        }
    }
}
