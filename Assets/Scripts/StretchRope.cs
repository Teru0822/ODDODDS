using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ロープ（オブジェクト6）の伸縮を担う。
/// Spaceキーによる手動操作と、UFOArmController からの外部制御の両方に対応。
/// </summary>
public class StretchRope : MonoBehaviour
{
    public enum Axis { X, Y, Z, None }

    [Header("Animation Settings")]
    [Tooltip("伸縮スピード（手動操作時）")]
    public float stretchSpeed = 2f;

    [Tooltip("伸縮の強さ（例: 35 に設定するとスケールが35伸びます）")]
    public float stretchIntensity = 35f;

    [Header("Axis Settings")]
    [Tooltip("どの軸方向にスケール(長さ)を伸ばすか")]
    public Axis scaleAxis = Axis.Z;

    [Tooltip("ロープ本体をどの方向に動かすか（中心補正用）")]
    public Axis moveAxis = Axis.Y;
    public bool moveNegative = true;

    [Tooltip("ロープ本体の位置補正比率（元の動作は 0.01）")]
    public float moveRatio = 0.01f;

    [Header("附属オブジェクト（finger/爪・4・5・6など）")]
    [Tooltip("ロープ先端の上下に合わせて連動させたいオブジェクト群を全て設定してください")]
    public Transform[] attachedObjects;

    [Tooltip("finger等の移動比率（元の動作は 0.0475）")]
    public float fingerRatio = 0.0475f;

    [Header("Spaceキー操作")]
    [Tooltip("Spaceキーによる手動伸縮を許可するか")]
    public bool allowSpaceKey = true;

    // ─────────────────────────────────────
    // 内部状態
    private Vector3 _originalScale;
    private Vector3 _originalPosition;
    private float   _stretchTime;       // 0(縮み) 〜 1(最大)

    private Vector3[] _originalAttachedLocalPos;

    // 外部制御
    private bool  _externalControl  = false;  // true = UFOArmController が制御中
    private float _externalDir      = 0f;     // +1:伸びる  -1:縮む
    private float _externalSpeedMul = 1f;

    // ─────────────────────────────────────
    void Start()
    {
        _originalScale    = transform.localScale;
        _originalPosition = transform.localPosition;

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
        _externalControl  = true;
        _externalDir      = 1f;
        _externalSpeedMul = speedMultiplier;
    }

    /// <summary>自動上昇を開始（ stretchTime を 0 に向けて動かす）</summary>
    public void StartExternalAscent(float speedMultiplier = 1f)
    {
        _externalControl  = true;
        _externalDir      = -1f;
        _externalSpeedMul = speedMultiplier;
    }

    /// <summary>外部制御を解除（Space キー操作に戻る）</summary>
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

            // 目標に到達したら外部制御を自動解除
            if ((_externalDir > 0f && _stretchTime >= 1f) ||
                (_externalDir < 0f && _stretchTime <= 0f))
            {
                StopExternalControl();
            }
        }
        else if (allowSpaceKey)
        {
            // 手動制御（Space キー）
            bool spaceDown = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            _stretchTime += (spaceDown ? 1f : -1f) * Time.deltaTime * stretchSpeed;
            _stretchTime  = Mathf.Clamp01(_stretchTime);
        }

        float t        = Mathf.SmoothStep(0f, 1f, _stretchTime);
        float scaleAdd = stretchIntensity * t;

        // ── ロープ本体のスケール ──
        Vector3 newScale = _originalScale;
        switch (scaleAxis)
        {
            case Axis.X: newScale.x += scaleAdd; break;
            case Axis.Y: newScale.y += scaleAdd; break;
            case Axis.Z: newScale.z += scaleAdd; break;
        }
        transform.localScale = newScale;

        // デバッグ用ログ（1秒間に数回だけ出すように調整）
        if (Time.frameCount % 60 == 0 && (_externalControl || _stretchTime > 0f))
        {
            Debug.Log($"[StretchRope] _stretchTime: {_stretchTime}, Scale: {newScale}, isExternal: {_externalControl}");
        }


        // ── 回転による振り子運動のための準備 ──
        UFOArmController arm = FindAnyObjectByType<UFOArmController>();
        Quaternion ropeSwayRot = (arm != null) ? arm.ropeSwayRot : Quaternion.identity;
        Quaternion clawSwayRot = (arm != null) ? arm.clawSwayRot : Quaternion.identity;
        
        // ロープの根本（クレーン本体の中心）をすべての揺れの「共通の支点（Pivot）」として扱う
        Vector3 universalPivot = (arm != null && arm.armRoot != null) ? arm.armRoot.position : transform.position;

        // ── ロープ本体の位置（中心補正＋Sway位置反映） ──
        if (moveAxis != Axis.None)
        {
            float   dir    = moveNegative ? -1f : 1f;
            float   move   = scaleAdd * moveRatio;
            
            // 親が動いた（横移動した）分を加味した現在の「本来あるべきワールド座標（揺れなし）」
            Vector3 baseWorldPos = (transform.parent != null) 
                                 ? transform.parent.TransformPoint(_originalPosition) 
                                 : _originalPosition;

            // どんなモデルの作りの向きであっても、重力に従って絶対に「ワールドの真下（Y軸マイナス方向）」に向かって落下・縮合させる
            baseWorldPos.y -= Mathf.Abs(move);

            // 支点から「真っ直ぐ下」へのベクトルを作り、揺れ角度で円を描くように振る
            Vector3 downwardVec = baseWorldPos - universalPivot;
            Vector3 swayedVec = ropeSwayRot * downwardVec;

            transform.position = universalPivot + swayedVec;
        }

        // ── finger等の追従（Sway位置反映） ──
        if (attachedObjects != null && attachedObjects.Length > 0)
        {
            float dir     = moveNegative ? -1f : 1f;
            float moveAdd = scaleAdd * fingerRatio * dir;

            for (int i = 0; i < attachedObjects.Length; i++)
            {
                if (attachedObjects[i] == null) continue;

                // もし揺れていなかった場合の「本来の真下」にあるワールド座標
                Vector3 baseWorldPos = (attachedObjects[i].parent != null)
                                     ? attachedObjects[i].parent.TransformPoint(_originalAttachedLocalPos[i])
                                     : _originalAttachedLocalPos[i];

                // 爪の本体のY座標（高さ）を強制的にロープと同じ距離だけ下に落とす
                baseWorldPos.y -= Mathf.Abs(moveAdd);

                // ロープ本体と完全に同じ支点・同じ揺れ角度（ropeSwayRot）を使って位置をスイングさせる！
                // これにより、6番が右に動けば絶対に爪も右に動く（絶対に分離しない）ようになる
                Vector3 downwardVec = baseWorldPos - universalPivot;
                Vector3 swayedVec = ropeSwayRot * downwardVec;

                // 最終的なワールド座標を更新
                attachedObjects[i].position = universalPivot + swayedVec;
            }
        }
    }
}
