using UnityEngine;

/// <summary>
/// Rigidbody に、Unity 標準の下向き重力は残したまま、追加で「指定オブジェクトのローカル -Y 方向」へ
/// 一定の力を常にかける。ピンボール台のように傾いた面で、台基準の下方向へ押し付ける用途。
///
/// gravitySource のローカル +Y（= transform.up）の逆向き（-Y）へ ForceMode.Force で力を加える。
/// 「N（ニュートン）の力」なので質量の影響を受ける（F=ma）。標準重力は切らない（useGravity は ON のまま）。
/// Kinematic 中は力が効かないので何もしない（落下開始＝isKinematic=false になってから効く）。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class LocalGravityBody : MonoBehaviour
{
    [Tooltip("追加の力の向きの基準。この Transform のローカル -Y 方向へ力を加える（ピンボール台など）。" +
             "null の場合は fallbackDirection を使う")]
    public Transform gravitySource;

    [Tooltip("gravitySource が null のときの力の方向（ワールド座標）")]
    public Vector3 fallbackDirection = Vector3.down;

    [Min(0f)]
    [Tooltip("追加で加える力の大きさ（N）。台ローカル -Y 方向。標準重力とは別に上乗せされる")]
    public float extraForce = 9f;

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = true; // 標準の下向き重力は残す
    }

    private void FixedUpdate()
    {
        if (_rb.isKinematic) return; // 固定中は無視
        if (extraForce <= 0f) return;

        Vector3 dir = gravitySource != null
            ? -gravitySource.up                 // 台のローカル -Y（ワールド換算）
            : fallbackDirection.normalized;

        // ForceMode.Force: N（ニュートン）の力。標準重力に上乗せして加える
        _rb.AddForce(dir * extraForce, ForceMode.Force);
    }
}
