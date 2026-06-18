using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ピンボールのフリッパー操作。マウスのボタンで2つのオブジェクトをローカルZ軸で回転させる。
///   - 左クリック押下中: 左フリッパー(newpinball_rebar22)をローカルZ -pressAngle°
///   - 右クリック押下中: 右フリッパー(newpinball_rebar21)をローカルZ +pressAngle°
///   - 離すと元の角度へ戻る。
///
/// 【ボールをリアルに弾くために】
/// フリッパーに「Kinematic な Rigidbody」を付けると、回転を Rigidbody.MoveRotation で
/// FixedUpdate に適用する。これにより物理エンジンが接触時の速度を計算し、ボールに正しく
/// 力が伝わる（Transform 直接回転だとコライダーが瞬間移動扱いになり、すり抜け・めり込みが起きる）。
/// Rigidbody が無い場合は従来どおり Transform を回す（見た目のみ・物理は不正確）。
/// </summary>
public class PinballFlipperController : MonoBehaviour
{
    [Header("フリッパー")]
    [Tooltip("左クリックで動く（newpinball_rebar22）。Kinematic Rigidbody 推奨")]
    public Transform leftFlipper;

    [Tooltip("右クリックで動く（newpinball_rebar21）。Kinematic Rigidbody 推奨")]
    public Transform rightFlipper;

    [Header("回転")]
    [Tooltip("押し込み角度（度）。ローカルZ軸。左は -、右は + 方向に適用")]
    public float pressAngle = 66f;

    [Min(0f)]
    [Tooltip("回転の速さ（度/秒）。速いほど強く弾く。0 で瞬間切り替え")]
    public float rotateSpeed = 1200f;

    private Rigidbody _leftRb, _rightRb;
    private Quaternion _leftRestLocal, _leftPressedLocal;
    private Quaternion _rightRestLocal, _rightPressedLocal;
    private bool _leftHeld, _rightHeld;

    private void Start()
    {
        SetupFlipper(leftFlipper, -pressAngle, out _leftRb, out _leftRestLocal, out _leftPressedLocal);
        SetupFlipper(rightFlipper, pressAngle, out _rightRb, out _rightRestLocal, out _rightPressedLocal);
    }

    private void SetupFlipper(Transform flipper, float angle, out Rigidbody rb,
                              out Quaternion restLocal, out Quaternion pressedLocal)
    {
        rb = null;
        restLocal = Quaternion.identity;
        pressedLocal = Quaternion.identity;
        if (flipper == null) return;

        restLocal = flipper.localRotation;
        pressedLocal = restLocal * Quaternion.Euler(0f, 0f, angle);

        rb = flipper.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // フリッパーは Kinematic で MoveRotation 駆動（接触速度がボールに伝わる）
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
    }

    private void Update()
    {
        // 入力読み取りのみ（押下中＝作動）。実際の回転は FixedUpdate で物理に同期させる。
        var mouse = Mouse.current;
        if (mouse != null)
        {
            _leftHeld = mouse.leftButton.isPressed;
            _rightHeld = mouse.rightButton.isPressed;
        }
    }

    private void FixedUpdate()
    {
        ApplyRotation(leftFlipper, _leftRb, _leftHeld ? _leftPressedLocal : _leftRestLocal);
        ApplyRotation(rightFlipper, _rightRb, _rightHeld ? _rightPressedLocal : _rightRestLocal);
    }

    private void ApplyRotation(Transform flipper, Rigidbody rb, Quaternion targetLocal)
    {
        if (flipper == null) return;

        float maxStep = rotateSpeed > 0f ? rotateSpeed * Time.fixedDeltaTime : 360f;

        if (rb != null)
        {
            // ローカル目標 → ワールド回転に変換して MoveRotation（物理的に回す）
            Quaternion parentRot = flipper.parent != null ? flipper.parent.rotation : Quaternion.identity;
            Quaternion targetWorld = parentRot * targetLocal;
            Quaternion next = Quaternion.RotateTowards(rb.rotation, targetWorld, maxStep);
            rb.MoveRotation(next);
        }
        else
        {
            // Rigidbody が無い場合は従来どおり Transform を回す（物理は不正確）
            flipper.localRotation = Quaternion.RotateTowards(flipper.localRotation, targetLocal, maxStep);
        }
    }
}
