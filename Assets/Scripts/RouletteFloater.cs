using UnityEngine;

/// <summary>
/// ルーレットオブジェクトに「浮遊」と「指定オブジェクト回避」の挙動を付加する。
///
/// 回避の仕組み（3層構造）:
///   外側ゾーン（avoidExtents 楕円体内）: 逆数比例の反発力で自然に押しのける
///   内側ゾーン（contactRadius 以内）  : 位置を直接補正し接触を物理的に不可能にする
///   速度予測（lookAheadTime）        : ターゲットの移動先を先読みして事前回避
/// </summary>
public class RouletteFloater : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // 列挙型
    // -----------------------------------------------------------------------

    public enum RangeShape { Sphere, Box }

    // -----------------------------------------------------------------------
    // Inspector フィールド
    // -----------------------------------------------------------------------

    [Header("浮遊")]
    [Tooltip("Y 軸方向の上下振れ幅（m）")]
    [SerializeField] private float floatAmplitude = 0.2f;

    [Tooltip("浮遊の周期速度")]
    [SerializeField] private float floatFrequency = 0.8f;

    [Tooltip("XZ 方向の微振動の振れ幅（m）。0 で無効")]
    [SerializeField] private float wobbleAmplitude = 0.05f;

    [Tooltip("XZ 微振動の周期速度")]
    [SerializeField] private float wobbleFrequency = 1.3f;

    [Header("回避 — 外側ゾーン（楕円体）")]
    [Tooltip("避け続ける対象の Transform")]
    [SerializeField] private Transform avoidTarget;

    [Tooltip("回避を開始する楕円体の半径（X/Y/Z 個別指定）。\n" +
             "avoidIgnoreY が true のときは Y 値を無視する")]
    [SerializeField] private Vector3 avoidExtents = new Vector3(3f, 3f, 3f);

    [Tooltip("反発力の強さ。楕円体表面では 0、中心に近いほど強くなる")]
    [SerializeField, Min(0f)] private float avoidStrength = 8f;

    [Tooltip("回避計算で Y 軸を無視する（高さが違っても水平方向だけ避けたい場合に有効）")]
    [SerializeField] private bool avoidIgnoreY = false;

    [Header("回避 — 内側ゾーン（接触防止クランプ）")]
    [Tooltip("この距離内に入ったら位置を直接補正して接触を防ぐ（m）。\n" +
             "avoidExtents の最小値より小さい値を推奨。0 で無効")]
    [SerializeField, Min(0f)] private float contactRadius = 0.8f;

    [Header("速度予測（Look-ahead）")]
    [Tooltip("速度予測を使ってターゲットの移動先を先読みする")]
    [SerializeField] private bool usePrediction = true;

    [Tooltip("何秒先の位置を予測するか。大きいほど早めに逃げ始める")]
    [SerializeField, Min(0f)] private float lookAheadTime = 0.3f;

    [Tooltip("速度推定値の平滑化速度。小さいほど滑らかだが反応が遅れる")]
    [SerializeField, Min(0.1f)] private float velocitySmoothSpeed = 10f;

    [Header("活動範囲")]
    [Tooltip("活動範囲の中心となる Transform。null なら起動時の位置を使う")]
    [SerializeField] private Transform rangeCenter;

    [Tooltip("活動範囲の形状")]
    [SerializeField] private RangeShape rangeShape = RangeShape.Box;

    [Tooltip("[Box のみ] 各軸の活動半幅（m）")]
    [SerializeField] private Vector3 rangeExtents = new Vector3(5f, 2f, 5f);

    [Tooltip("[Sphere のみ] 活動できる球の半径（m）")]
    [SerializeField, Min(0.1f)] private float rangeRadius = 8f;

    [Tooltip("境界の何 % 外側から復元力を加え始めるか（0〜1）")]
    [SerializeField, Range(0f, 0.99f)] private float boundaryBufferRatio = 0.3f;

    [Tooltip("境界への引き戻し力")]
    [SerializeField, Min(0f)] private float boundaryStrength = 6f;

    [Header("移動制御")]
    [Tooltip("速度の減衰率。大きいほどすぐ止まる")]
    [SerializeField, Min(0f)] private float damping = 3f;

    [Header("カメラ追従（Y軸ビルボード）")]
    [Tooltip("常に向き続けるカメラの Transform。null の場合は Front カメラ優先で自動指定されます")]
    [SerializeField] private Transform targetCamera;

    [Tooltip("カメラ追従を有効にする")]
    [SerializeField] private bool enableCameraFacing = true;

    [Tooltip("正面カメラ (FrontCamera) のみを固定追従対象にする。サブカメラ視点切替（Left/Right/Back）に追従して回らない")]
    [SerializeField] private bool faceFrontCameraOnly = true;

    [Tooltip("カメラ方向への回転追従速度（度/秒的なイメージ）。0 で即座にスナップ")]
    [SerializeField, Min(0f)] private float cameraFacingSpeed = 5f;

    [Tooltip("カメラ方向に向いたときの Y 軸補正角度（度）。モデルの正面がズレている場合に調整する")]
    [SerializeField] private float cameraFacingYOffset = -90f;

    // -----------------------------------------------------------------------
    // 内部状態
    // -----------------------------------------------------------------------

    private Vector3 _basePosition;

    /// <summary>演出シーケンスがアクティブかどうか。アクティブ時は活動範囲境界への引き戻し・強制作制をスキップします</summary>
    public bool IsSequenceActive { get; set; } = false;

    /// <summary>
    /// 浮遊・回避計算の基準位置（ワールド空間）。
    /// アニメーション演出時に外部から更新しても、浮遊揺れや回避力は基準位置に対して継続して適用されます。
    /// </summary>
    public Vector3 BasePosition
    {
        get => _basePosition;
        set => _basePosition = value;
    }

    /// <summary>
    /// 親に対するローカル空間での基準位置（演出アニメーション制御用）。
    /// </summary>
    public Vector3 BaseLocalPosition
    {
        get => transform.parent != null ? transform.parent.InverseTransformPoint(_basePosition) : _basePosition;
        set => _basePosition = transform.parent != null ? transform.parent.TransformPoint(value) : value;
    }
    private Vector3 _centerPosition;
    private Vector3 _velocity;
    private float   _timeOffset;

    // 速度予測用
    private Vector3 _avoidTargetPrevPos;
    private Vector3 _avoidTargetVelocity;
    private bool    _avoidTargetPrevPosSet;

    // -----------------------------------------------------------------------
    // Unity ライフサイクル
    // -----------------------------------------------------------------------

    private void Awake()
    {
        _basePosition   = transform.position;
        _centerPosition = rangeCenter != null ? rangeCenter.position : transform.position;
        _timeOffset     = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        if (rangeCenter != null)
            _centerPosition = rangeCenter.position;

        UpdateAvoidTargetVelocity();

        // ---- 力の計算 --------------------------------------------------
        Vector3 force = Vector3.zero;
        force += CalcAvoidForce();
        force += CalcBoundaryForce();

        // ---- 速度・基準位置の更新 --------------------------------------
        _velocity += force * Time.deltaTime;
        _velocity  = Vector3.Lerp(_velocity, Vector3.zero, damping * Time.deltaTime);
        _basePosition += _velocity * Time.deltaTime;

        // 境界ハードクランプ
        HardClampBoundary();

        // 接触防止クランプ（力では防ぎきれない最後の砦）
        ApplyContactClamp();

        // ---- 浮遊アニメーション ----------------------------------------
        float time    = Time.time;
        float floatY  = Mathf.Sin(time * floatFrequency + _timeOffset) * floatAmplitude;
        float wobbleX = Mathf.Sin(time * wobbleFrequency + _timeOffset + 1f) * wobbleAmplitude;
        float wobbleZ = Mathf.Cos(time * wobbleFrequency * 0.9f + _timeOffset) * wobbleAmplitude;

        transform.position = _basePosition + new Vector3(wobbleX, floatY, wobbleZ);

        // ---- カメラ追従 ------------------------------------------------
        if (enableCameraFacing) ApplyCameraFacing();
    }

    // -----------------------------------------------------------------------
    // カメラ追従（Y軸ビルボード）
    // -----------------------------------------------------------------------

    private void ApplyCameraFacing()
    {
        Transform cam = null;

        if (targetCamera != null)
        {
            cam = targetCamera;
        }
        else if (faceFrontCameraOnly && UFOCameraController.Instance != null && UFOCameraController.Instance.FrontCamera != null)
        {
            cam = UFOCameraController.Instance.FrontCamera.transform;
        }
        else if (UFOCameraController.Instance != null && UFOCameraController.Instance.GetActiveCamera() != null)
        {
            cam = UFOCameraController.Instance.GetActiveCamera().transform;
        }
        else
        {
            cam = Camera.main?.transform;
        }

        if (cam == null) return;

        // カメラへの方向を水平面（XZ）に投影して Y 軸のみ回転
        Vector3 dir = cam.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-4f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, cameraFacingYOffset, 0f);
        transform.rotation = cameraFacingSpeed <= 0f
            ? targetRot
            : Quaternion.Slerp(transform.rotation, targetRot, cameraFacingSpeed * Time.deltaTime);
    }

    // -----------------------------------------------------------------------
    // 速度予測
    // -----------------------------------------------------------------------

    private void UpdateAvoidTargetVelocity()
    {
        if (avoidTarget == null) return;

        if (_avoidTargetPrevPosSet)
        {
            Vector3 raw = (avoidTarget.position - _avoidTargetPrevPos) / Time.deltaTime;
            _avoidTargetVelocity = Vector3.Lerp(
                _avoidTargetVelocity, raw, velocitySmoothSpeed * Time.deltaTime);
        }

        _avoidTargetPrevPos    = avoidTarget.position;
        _avoidTargetPrevPosSet = true;
    }

    /// <summary>速度予測を加味した実効的なターゲット位置を返す。</summary>
    private Vector3 GetEffectiveAvoidPos()
    {
        if (avoidTarget == null) return Vector3.zero;
        if (!usePrediction) return avoidTarget.position;
        return avoidTarget.position + _avoidTargetVelocity * lookAheadTime;
    }

    // -----------------------------------------------------------------------
    // 回避力（外側ゾーン：楕円体 × 逆数比例力）
    // -----------------------------------------------------------------------

    private Vector3 CalcAvoidForce()
    {
        if (avoidTarget == null) return Vector3.zero;

        Vector3 effectivePos = GetEffectiveAvoidPos();
        Vector3 diff         = _basePosition - effectivePos;
        if (avoidIgnoreY) diff.y = 0f;

        // 楕円体空間での正規化距離（1.0 = 表面、< 1.0 = 内側）
        float ellipsoidDist = EllipsoidDist(diff, avoidExtents, avoidIgnoreY);
        if (ellipsoidDist >= 1f) return Vector3.zero;

        // 楕円体の勾配ベクトル（= 楕円体表面の法線方向）を押しのけ方向とする
        Vector3 gradient = EllipsoidGradient(diff, avoidExtents, avoidIgnoreY);
        if (gradient.sqrMagnitude < 1e-6f)
        {
            // 中心付近：ランダム方向に押し出す
            gradient = Random.insideUnitSphere;
            if (avoidIgnoreY) gradient.y = 0f;
        }

        // 逆数比例力：表面付近は弱く、内側に入るほど急激に強くなる
        // f = S * (1/d - 1)  ← d=1 で 0、d→0 で ∞
        float safeDist  = Mathf.Max(ellipsoidDist, 0.05f); // ゼロ除算防止
        float forceMag  = avoidStrength * (1f / safeDist - 1f);

        return gradient.normalized * forceMag;
    }

    // -----------------------------------------------------------------------
    // 接触防止クランプ（内側ゾーン：位置直接補正）
    // -----------------------------------------------------------------------

    private void ApplyContactClamp()
    {
        if (avoidTarget == null || contactRadius <= 0f) return;

        Vector3 effectivePos = GetEffectiveAvoidPos();
        Vector3 diff         = _basePosition - effectivePos;
        float   savedY       = _basePosition.y;
        if (avoidIgnoreY) diff.y = 0f;

        float dist = diff.magnitude;
        if (dist >= contactRadius || dist < 1e-4f) return;

        // 接触禁止距離の外まで位置を直接押し出す
        Vector3 pushDir   = diff.normalized;
        Vector3 corrected = effectivePos + pushDir * contactRadius;
        if (avoidIgnoreY) corrected.y = savedY;
        _basePosition = corrected;

        // 押し出し方向の速度成分を消す（跳ね返りを防ぐ）
        Vector3 velDir = pushDir; if (avoidIgnoreY) velDir.y = 0f;
        _velocity -= Vector3.Project(_velocity, velDir);
    }

    // -----------------------------------------------------------------------
    // 活動範囲（境界復元力 + ハードクランプ）
    // -----------------------------------------------------------------------

    private Vector3 CalcBoundaryForce()
    {
        if (IsSequenceActive) return Vector3.zero;

        return rangeShape == RangeShape.Sphere
            ? CalcSphereBoundaryForce()
            : CalcBoxBoundaryForce();
    }

    private Vector3 CalcSphereBoundaryForce()
    {
        Vector3 fromCenter  = _basePosition - _centerPosition;
        float   dist        = fromCenter.magnitude;
        float   innerRadius = rangeRadius * (1f - boundaryBufferRatio);
        if (dist <= innerRadius) return Vector3.zero;
        float excess = Mathf.InverseLerp(innerRadius, rangeRadius, dist);
        return -fromCenter.normalized * (boundaryStrength * excess);
    }

    private Vector3 CalcBoxBoundaryForce()
    {
        Vector3 fromCenter = _basePosition - _centerPosition;
        Vector3 force      = Vector3.zero;
        for (int axis = 0; axis < 3; axis++)
        {
            float half  = GetExtent(axis);
            float inner = half * (1f - boundaryBufferRatio);
            float abs   = Mathf.Abs(fromCenter[axis]);
            if (abs <= inner) continue;
            float excess = Mathf.InverseLerp(inner, half, abs);
            Vector3 v = Vector3.zero;
            v[axis]   = -Mathf.Sign(fromCenter[axis]) * boundaryStrength * excess;
            force    += v;
        }
        return force;
    }

    private void HardClampBoundary()
    {
        if (IsSequenceActive) return;

        if (rangeShape == RangeShape.Sphere)
        {
            Vector3 dir = _basePosition - _centerPosition;
            if (dir.magnitude > rangeRadius)
            {
                _basePosition = _centerPosition + dir.normalized * rangeRadius;
                _velocity    -= Vector3.Project(_velocity, dir.normalized);
            }
        }
        else
        {
            for (int axis = 0; axis < 3; axis++)
            {
                float   half  = GetExtent(axis);
                float   local = _basePosition[axis] - _centerPosition[axis];
                if (Mathf.Abs(local) <= half) continue;
                Vector3 pos  = _basePosition;
                pos[axis]    = _centerPosition[axis] + Mathf.Clamp(local, -half, half);
                _basePosition = pos;
                Vector3 vel  = _velocity;
                vel[axis]    = 0f;
                _velocity    = vel;
            }
        }
    }

    private float GetExtent(int axis) => axis switch
    {
        0 => rangeExtents.x,
        1 => rangeExtents.y,
        _ => rangeExtents.z,
    };

    // -----------------------------------------------------------------------
    // 楕円体ユーティリティ
    // -----------------------------------------------------------------------

    /// <summary>楕円体空間における正規化距離（1=表面、0=中心）を返す。</summary>
    private static float EllipsoidDist(Vector3 diff, Vector3 extents, bool ignoreY)
    {
        float sx = extents.x > 0f ? diff.x / extents.x : 0f;
        float sy = (!ignoreY && extents.y > 0f) ? diff.y / extents.y : 0f;
        float sz = extents.z > 0f ? diff.z / extents.z : 0f;
        return Mathf.Sqrt(sx * sx + sy * sy + sz * sz);
    }

    /// <summary>楕円体の勾配ベクトル（法線方向）を返す（正規化前）。</summary>
    private static Vector3 EllipsoidGradient(Vector3 diff, Vector3 extents, bool ignoreY)
    {
        return new Vector3(
            extents.x > 0f ? diff.x / (extents.x * extents.x) : 0f,
            (!ignoreY && extents.y > 0f) ? diff.y / (extents.y * extents.y) : 0f,
            extents.z > 0f ? diff.z / (extents.z * extents.z) : 0f
        );
    }

    // -----------------------------------------------------------------------
    // デバッグ Gizmo
    // -----------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying
            ? _centerPosition
            : (rangeCenter != null ? rangeCenter.position : transform.position);

        // 活動範囲
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        DrawRangeGizmo(center, false);
        Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
        DrawRangeGizmo(center, true);

        if (avoidTarget == null) return;

        Vector3 effectivePos = Application.isPlaying ? GetEffectiveAvoidPos() : avoidTarget.position;

        // 回避ゾーン（楕円体・オレンジ）
        Gizmos.color  = new Color(1f, 0.6f, 0f, 0.5f);
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(effectivePos, Quaternion.identity, avoidExtents * 2f);
        Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
        Gizmos.matrix = old;

        // 接触防止ゾーン（赤）
        if (contactRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(effectivePos, contactRadius);
        }

        // 速度予測ベクトル（黄）
        if (Application.isPlaying && usePrediction && _avoidTargetVelocity.sqrMagnitude > 0.01f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(avoidTarget.position, effectivePos);
            Gizmos.DrawSphere(effectivePos, 0.1f);
        }
    }

    private void DrawRangeGizmo(Vector3 center, bool inner)
    {
        float ratio = inner ? (1f - boundaryBufferRatio) : 1f;
        if (rangeShape == RangeShape.Sphere)
        {
            Gizmos.DrawWireSphere(center, rangeRadius * ratio);
        }
        else
        {
            Gizmos.DrawWireCube(center, rangeExtents * 2f * ratio);
        }
    }
}
