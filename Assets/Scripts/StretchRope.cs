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

    [Header("Pole Slide Settings")]
    [Tooltip("ポールスライドによる伸縮を使用するか")]
    public bool usePoleSlide = true;

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

    [Header("Spaceキー操作")]
    [Tooltip("Spaceキーによる手動伸縮を許可するか")]
    public bool allowSpaceKey = true;

    // ─────────────────────────────────────
    // 内部状態
    private Vector3 _originalScale;
    private Vector3 _originalPosition;
    private float   _stretchTime;       // 0(縮み) 〜 1(最大)

    private Vector3[] _originalAttachedLocalPos;

    private Vector3 _poll2InitialLocalPos;
    private Vector3 _poll3InitialLocalPos;

    // 外部制御
    private bool  _externalControl  = false;  // true = UFOArmController が制御中
    private float _externalDir      = 0f;     // +1:伸びる  -1:縮む
    private float _externalSpeedMul = 1f;

    // ─────────────────────────────────────
    void Start()
    {
        _originalScale    = transform.localScale;
        _originalPosition = transform.localPosition;

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

        float t = Mathf.SmoothStep(0f, 1f, _stretchTime);

        // ── 回転による振り子運動のための準備 ──
        UFOArmController arm = FindAnyObjectByType<UFOArmController>();
        Quaternion ropeSwayRot = (arm != null) ? arm.ropeSwayRot : Quaternion.identity;
        Quaternion clawSwayRot = (arm != null) ? arm.clawSwayRot : Quaternion.identity;
        
        // ロープの根本（クレーン本体の中心）をすべての揺れの「共通の支点（Pivot）」として扱う
        Vector3 universalPivot = (arm != null && arm.armRoot != null) ? arm.armRoot.position : transform.position;

        if (usePoleSlide)
        {
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

            // ── attachedObjects の追従（Sway位置反映） ──
            if (attachedObjects != null && attachedObjects.Length > 0)
            {
                float totalMove = (poll2MaxDistance + poll3MaxDistance) * t;

                for (int i = 0; i < attachedObjects.Length; i++)
                {
                    if (attachedObjects[i] == null) continue;

                    // もし揺れていなかった場合の「本来の真下」にあるワールド座標
                    Vector3 baseWorldPos = (attachedObjects[i].parent != null)
                                         ? attachedObjects[i].parent.TransformPoint(_originalAttachedLocalPos[i])
                                         : _originalAttachedLocalPos[i];

                    // 合計スライド移動量だけ下に落とす
                    baseWorldPos.y -= totalMove;

                    // 共通の支点・同じ揺れ角度（ropeSwayRot）を使って位置をスイングさせる
                    Vector3 downwardVec = baseWorldPos - universalPivot;
                    Vector3 swayedVec = ropeSwayRot * downwardVec;

                    attachedObjects[i].position = universalPivot + swayedVec;
                }
            }
        }
        else
        {
            // ── 従来のスケール伸縮 ──
            float scaleAdd = stretchIntensity * t;

            // ロープ本体のスケール
            Vector3 newScale = _originalScale;
            switch (scaleAxis)
            {
                case Axis.X: newScale.x += scaleAdd; break;
                case Axis.Y: newScale.y += scaleAdd; break;
                case Axis.Z: newScale.z += scaleAdd; break;
            }
            transform.localScale = newScale;

            // デバッグ用ログ
            if (Time.frameCount % 60 == 0 && (_externalControl || _stretchTime > 0f))
            {
                Debug.Log($"[StretchRope] Scale Mode - _stretchTime: {_stretchTime}, Scale: {newScale}, isExternal: {_externalControl}");
            }

            // ロープ本体の位置（中心補正＋Sway位置反映）
            if (moveAxis != Axis.None)
            {
                float dir  = moveNegative ? -1f : 1f;
                float move = scaleAdd * moveRatio;
                
                Vector3 baseWorldPos = (transform.parent != null) 
                                     ? transform.parent.TransformPoint(_originalPosition) 
                                     : _originalPosition;

                baseWorldPos.y -= Mathf.Abs(move);

                Vector3 downwardVec = baseWorldPos - universalPivot;
                Vector3 swayedVec = ropeSwayRot * downwardVec;

                transform.position = universalPivot + swayedVec;
            }

            // finger等の追従（Sway位置反映）
            if (attachedObjects != null && attachedObjects.Length > 0)
            {
                float dir     = moveNegative ? -1f : 1f;
                float moveAdd = scaleAdd * fingerRatio * dir;

                for (int i = 0; i < attachedObjects.Length; i++)
                {
                    if (attachedObjects[i] == null) continue;

                    Vector3 baseWorldPos = (attachedObjects[i].parent != null)
                                         ? attachedObjects[i].parent.TransformPoint(_originalAttachedLocalPos[i])
                                         : _originalAttachedLocalPos[i];

                    baseWorldPos.y -= Mathf.Abs(moveAdd);

                    Vector3 downwardVec = baseWorldPos - universalPivot;
                    Vector3 swayedVec = ropeSwayRot * downwardVec;

                    attachedObjects[i].position = universalPivot + swayedVec;
                }
            }
        }
    }
}
