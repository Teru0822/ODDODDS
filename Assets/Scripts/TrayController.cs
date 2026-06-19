using UnityEngine;

/// <summary>
/// PinBallState に連動して「tray」を trayP1 ↔ trayP2 間でスライドさせる。
///
///   PinBallState == 1 になった時: tray を trayP1 → trayP2 へ移動。
///   PinBallState が 1 から別の値になった時: tray を trayP2 → trayP1 へ移動。
///
/// スライドの挙動（加速度＋壁反発で最終的に目標へ固定）:
///   - 目標方向へ一定の加速度で動く。
///   - 目標位置（= 壁）に到達すると、壁にぶつかったように反発（速度反転 × 反発係数）。
///   - 反発後も加速度は目標方向へかかり続けるので、減速→停止→再加速で再び壁へ。
///     反発のたびにエネルギーが減り、最終的に目標位置に収束して固定される。
///
/// tray に Kinematic Rigidbody があれば MovePosition で動かす（ボールを物理的に押せる）。
/// 無ければ Transform を直接動かす。参照は Scene_PinBall 内で割り当てる（シーンに閉じる）。
/// </summary>
public class TrayController : MonoBehaviour
{
    [Header("対象")]
    [Tooltip("スライドさせる tray の Transform")]
    public Transform tray;

    [Tooltip("始点（空オブジェクト trayP1）")]
    public Transform trayP1;

    [Tooltip("終点（空オブジェクト trayP2）")]
    public Transform trayP2;

    [Tooltip("PinBallState を読むセッション。空なら自動取得")]
    public PinballSessionController session;

    [Header("スライド挙動")]
    [Min(0f)]
    [Tooltip("目標方向への加速度 (m/s^2)")]
    public float acceleration = 30f;

    [Range(0f, 1f)]
    [Tooltip("壁(目標位置)での反発係数。0=反発なしで止まる、1=減衰なし")]
    public float restitution = 0.45f;

    [Min(0f)]
    [Tooltip("速度上限 (m/s)。0 で無制限")]
    public float maxSpeed = 0f;

    [Min(0f)]
    [Tooltip("壁到達時この速度未満なら反発せず目標位置に固定して収束完了とする")]
    public float settleSpeed = 0.05f;

    // セグメント A(=P1) → B(=P2)
    private Vector3 _a, _b, _dir;
    private float _length;

    private float _x;          // A からの距離 [0, _length]
    private float _vel;        // 軸方向の速度（+で A→B）
    private bool _targetIsP2;  // true: B(=P2) へ、false: A(=P1) へ
    private bool _settled;
    private Rigidbody _rb;

    private void Start()
    {
        if (session == null) session = FindAnyObjectByType<PinballSessionController>();
        if (tray != null) _rb = tray.GetComponent<Rigidbody>();

        CacheSegment();

        // 初期は trayP1 に固定（PinBallState=0 想定）
        _x = 0f;
        _vel = 0f;
        _targetIsP2 = false;
        _settled = true;
        ApplyPosition();
    }

    private void CacheSegment()
    {
        if (trayP1 == null || trayP2 == null) return;
        _a = trayP1.position;
        _b = trayP2.position;
        Vector3 ab = _b - _a;
        _length = ab.magnitude;
        _dir = _length > 1e-6f ? ab / _length : Vector3.forward;
    }

    private void FixedUpdate()
    {
        if (tray == null || trayP1 == null || trayP2 == null) return;

        // 端点が動く可能性に備えて毎回キャッシュ（基本は固定）
        CacheSegment();
        if (_length <= 1e-6f) return;

        // PinBallState による目標の切り替え
        bool wantP2 = session != null && session.PinBallState == 1;
        if (wantP2 != _targetIsP2)
        {
            _targetIsP2 = wantP2;
            _settled = false; // 現在の位置・速度を引き継いで動き出す（途中切替にも対応）
        }

        if (_settled)
        {
            ApplyPosition();
            return;
        }

        float dt = Time.fixedDeltaTime;
        float wallX = _targetIsP2 ? _length : 0f;
        float accelDir = _targetIsP2 ? 1f : -1f;

        // 加速度運動
        _vel += accelDir * acceleration * dt;
        if (maxSpeed > 0f) _vel = Mathf.Clamp(_vel, -maxSpeed, maxSpeed);
        _x += _vel * dt;

        // 壁（目標位置）に到達 → 反発、または十分遅ければ収束
        bool hitWall = _targetIsP2 ? (_x >= _length) : (_x <= 0f);
        if (hitWall)
        {
            _x = wallX;
            float impactSpeed = Mathf.Abs(_vel);
            if (impactSpeed < settleSpeed)
            {
                _vel = 0f;
                _settled = true; // 目標位置に固定して完了
            }
            else
            {
                _vel = -_vel * restitution; // 壁で反発（速度反転×減衰）
            }
        }

        ApplyPosition();
    }

    private void ApplyPosition()
    {
        Vector3 pos = _a + _dir * Mathf.Clamp(_x, 0f, _length);
        if (_rb != null && _rb.isKinematic)
            _rb.MovePosition(pos);
        else
            tray.position = pos;
    }
}
