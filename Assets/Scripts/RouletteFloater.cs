using UnityEngine;

/// <summary>
/// ルーレットオブジェクトに「浮遊」と「指定オブジェクト回避」の挙動を付加する。
/// RouletteController とは独立しており、どの GameObject にも単体でアタッチできる。
///
/// 動作概要:
///   - 正弦波で Y 軸をふわふわと上下させる（浮遊）
///   - XZ 方向にも小さな正弦波で有機的なゆらぎを加える（微振動）
///   - avoidTarget が avoidRadius 内に入ると押しのける（回避）
///   - 活動範囲（球 or 箱）の境界付近で内側へ復元力を加える（範囲制限）
/// </summary>
public class RouletteFloater : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // 列挙型
    // -----------------------------------------------------------------------

    public enum RangeShape
    {
        Sphere,  // 球形（rangeRadius で制御）
        Box,     // 直方体（rangeExtents で X/Y/Z を個別に制御）
    }

    // -----------------------------------------------------------------------
    // Inspector フィールド
    // -----------------------------------------------------------------------

    [Header("浮遊")]
    [Tooltip("Y 軸方向の上下振れ幅（m）")]
    [SerializeField] private float floatAmplitude = 0.2f;

    [Tooltip("浮遊の周期速度。大きいほど速く揺れる")]
    [SerializeField] private float floatFrequency = 0.8f;

    [Tooltip("XZ 方向の微振動の振れ幅（m）。0 で無効")]
    [SerializeField] private float wobbleAmplitude = 0.05f;

    [Tooltip("XZ 微振動の周期速度")]
    [SerializeField] private float wobbleFrequency = 1.3f;

    [Header("回避")]
    [Tooltip("避け続ける対象の Transform（プレイヤーなど）")]
    [SerializeField] private Transform avoidTarget;

    [Tooltip("この距離内に avoidTarget が入ると押しのける（m）")]
    [SerializeField, Min(0f)] private float avoidRadius = 3f;

    [Tooltip("押しのける力の強さ。大きいほど素早く逃げる")]
    [SerializeField, Min(0f)] private float avoidStrength = 5f;

    [Tooltip("回避計算で Y 軸を無視する（同じ高さでなくても避けたいときは false）")]
    [SerializeField] private bool avoidIgnoreY = false;

    [Header("活動範囲")]
    [Tooltip("活動範囲の中心となる Transform。null なら起動時の自身の位置を使う")]
    [SerializeField] private Transform rangeCenter;

    [Tooltip("活動範囲の形状。Sphere=球、Box=直方体")]
    [SerializeField] private RangeShape rangeShape = RangeShape.Box;

    [Tooltip("[Box のみ] 各軸の活動半幅（m）。X/Y/Z を個別に設定できる")]
    [SerializeField] private Vector3 rangeExtents = new Vector3(5f, 2f, 5f);

    [Tooltip("[Sphere のみ] 活動できる球の半径（m）")]
    [SerializeField, Min(0.1f)] private float rangeRadius = 8f;

    [Tooltip("境界の何 % 外側から復元力を加え始めるか（0〜1）。小さいほど壁際まで自由に動ける")]
    [SerializeField, Range(0f, 0.99f)] private float boundaryBufferRatio = 0.3f;

    [Tooltip("境界に近づいたときの引き戻し力。大きいほど壁に当たりにくくなる")]
    [SerializeField, Min(0f)] private float boundaryStrength = 6f;

    [Header("移動制御")]
    [Tooltip("速度の減衰率。大きいほどすぐ止まり、小さいほど慣性が残る")]
    [SerializeField, Min(0f)] private float damping = 3f;

    // -----------------------------------------------------------------------
    // 内部状態
    // -----------------------------------------------------------------------

    private Vector3 _basePosition;
    private Vector3 _centerPosition;
    private Vector3 _velocity;
    private float _timeOffset;

    // -----------------------------------------------------------------------
    // Unity ライフサイクル
    // -----------------------------------------------------------------------

    private void Awake()
    {
        _basePosition = transform.position;
        _centerPosition = rangeCenter != null ? rangeCenter.position : transform.position;
        _timeOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        if (rangeCenter != null)
            _centerPosition = rangeCenter.position;

        // ---- 力の計算 --------------------------------------------------

        Vector3 force = Vector3.zero;
        force += CalcAvoidForce();
        force += CalcBoundaryForce();

        // ---- 速度・基準位置の更新 --------------------------------------

        _velocity += force * Time.deltaTime;
        _velocity = Vector3.Lerp(_velocity, Vector3.zero, damping * Time.deltaTime);
        _basePosition += _velocity * Time.deltaTime;

        // ハードクランプ（力だけでは境界を超えることがある）
        HardClamp();

        // ---- 浮遊アニメーション ----------------------------------------

        float time = Time.time;
        float floatY  = Mathf.Sin(time * floatFrequency + _timeOffset) * floatAmplitude;
        float wobbleX = Mathf.Sin(time * wobbleFrequency + _timeOffset + 1f) * wobbleAmplitude;
        float wobbleZ = Mathf.Cos(time * wobbleFrequency * 0.9f + _timeOffset) * wobbleAmplitude;

        transform.position = _basePosition + new Vector3(wobbleX, floatY, wobbleZ);
    }

    // -----------------------------------------------------------------------
    // 力の計算
    // -----------------------------------------------------------------------

    private Vector3 CalcAvoidForce()
    {
        if (avoidTarget == null) return Vector3.zero;

        Vector3 away = _basePosition - avoidTarget.position;
        if (avoidIgnoreY) away.y = 0f;

        float dist = away.magnitude;
        if (dist >= avoidRadius || dist < 0.001f) return Vector3.zero;

        float t = 1f - dist / avoidRadius;
        return away.normalized * (avoidStrength * t);
    }

    private Vector3 CalcBoundaryForce()
    {
        if (rangeShape == RangeShape.Sphere)
            return CalcSphereBoundaryForce();
        else
            return CalcBoxBoundaryForce();
    }

    /// <summary>球形境界：中心からの距離が一定以上で内向きに力を返す</summary>
    private Vector3 CalcSphereBoundaryForce()
    {
        Vector3 fromCenter = _basePosition - _centerPosition;
        float dist = fromCenter.magnitude;
        float innerRadius = rangeRadius * (1f - boundaryBufferRatio);

        if (dist <= innerRadius) return Vector3.zero;

        float excess = Mathf.InverseLerp(innerRadius, rangeRadius, dist);
        return -fromCenter.normalized * (boundaryStrength * excess);
    }

    /// <summary>箱形境界：各軸ごとに独立して内向きの力を返す</summary>
    private Vector3 CalcBoxBoundaryForce()
    {
        Vector3 fromCenter = _basePosition - _centerPosition;
        Vector3 force = Vector3.zero;

        for (int axis = 0; axis < 3; axis++)
        {
            float half = GetExtent(axis);
            float inner = half * (1f - boundaryBufferRatio);
            float abs = Mathf.Abs(fromCenter[axis]);

            if (abs <= inner) continue;

            float excess = Mathf.InverseLerp(inner, half, abs);
            float push = -Mathf.Sign(fromCenter[axis]) * boundaryStrength * excess;
            Vector3 v = Vector3.zero;
            v[axis] = push;
            force += v;
        }

        return force;
    }

    // -----------------------------------------------------------------------
    // ハードクランプ
    // -----------------------------------------------------------------------

    private void HardClamp()
    {
        if (rangeShape == RangeShape.Sphere)
        {
            Vector3 dir = _basePosition - _centerPosition;
            if (dir.magnitude > rangeRadius)
            {
                _basePosition = _centerPosition + dir.normalized * rangeRadius;
                _velocity -= Vector3.Project(_velocity, dir.normalized);
            }
        }
        else
        {
            for (int axis = 0; axis < 3; axis++)
            {
                float half = GetExtent(axis);
                float local = _basePosition[axis] - _centerPosition[axis];
                if (Mathf.Abs(local) > half)
                {
                    float clamped = Mathf.Clamp(local, -half, half);
                    Vector3 pos = _basePosition;
                    pos[axis] = _centerPosition[axis] + clamped;
                    _basePosition = pos;

                    // 境界方向の速度を殺す
                    Vector3 vel = _velocity;
                    vel[axis] = 0f;
                    _velocity = vel;
                }
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
    // デバッグ Gizmo（Scene ビューで範囲・回避距離を可視化）
    // -----------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying
            ? _centerPosition
            : (rangeCenter != null ? rangeCenter.position : transform.position);

        if (rangeShape == RangeShape.Sphere)
        {
            // 活動範囲（シアン）
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
            Gizmos.DrawWireSphere(center, rangeRadius);

            // 復元力が始まる内側の境界（薄いシアン）
            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawWireSphere(center, rangeRadius * (1f - boundaryBufferRatio));
        }
        else
        {
            // 活動範囲（シアン）
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
            Gizmos.DrawWireCube(center, rangeExtents * 2f);

            // 復元力が始まる内側の境界（薄いシアン）
            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawWireCube(center, rangeExtents * 2f * (1f - boundaryBufferRatio));
        }

        // 回避範囲（赤）
        if (avoidTarget != null)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(avoidTarget.position, avoidRadius);
        }
    }
}
