using UnityEngine;

/// <summary>
/// オブジェクトにアタッチするとふわふわと浮遊するようになる汎用スクリプト。
/// 上下/左右の正弦波合成で位置を、別周期の正弦波で回転を揺らす。
/// Awake 時点の localPosition / localRotation を「基準姿勢」として保持し、それに対して
/// オフセットを足し続けるため、初期姿勢は崩れない。
/// 同一オブジェクトを他スクリプトで動かす場合は競合に注意 (FloatingMotion が localPosition を毎フレーム上書きする)。
/// </summary>
[DisallowMultipleComponent]
public class FloatingMotion : MonoBehaviour
{
    [Header("上下浮遊")]
    [Tooltip("上下浮遊の振幅 (m)")]
    public float verticalAmplitude = 0.05f;

    [Tooltip("上下浮遊の周期 (秒)")]
    public float verticalPeriod = 2.5f;

    [Tooltip("上下方向の軸 (親空間)。デフォルトは local Y")]
    public Vector3 verticalAxis = Vector3.up;

    [Header("左右浮遊")]
    [Tooltip("左右浮遊の振幅 (m)")]
    public float horizontalAmplitude = 0.03f;

    [Tooltip("左右浮遊の周期 (秒)。上下と異なる値にすると Lissajous 風になる")]
    public float horizontalPeriod = 3.2f;

    [Tooltip("左右方向の軸 (親空間)。デフォルトは local X")]
    public Vector3 horizontalAxis = Vector3.right;

    [Header("回転揺れ")]
    [Tooltip("回転揺れの振幅 (度)。0 で回転なし")]
    public float rotationAmplitude = 3f;

    [Tooltip("回転揺れの周期 (秒)")]
    public float rotationPeriod = 3.5f;

    [Tooltip("回転軸 (自身のローカル空間)。デフォルトは local Z")]
    public Vector3 rotationAxis = Vector3.forward;

    [Header("その他")]
    [Tooltip("有効化時にランダム位相を割り当てる (同じ FloatingMotion を複数置いた時に動きを揃えない)")]
    public bool randomPhase = true;

    [Tooltip("シーン上での全体スピード倍率")]
    [Range(0.1f, 5f)]
    public float speedMultiplier = 1f;

    private Vector3 _baseLocalPos;
    private Quaternion _baseLocalRot;
    private float _phase;

    private void Awake()
    {
        _baseLocalPos = transform.localPosition;
        _baseLocalRot = transform.localRotation;
        _phase = randomPhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
    }

    /// <summary>外部からアニメ続行中に基準姿勢を上書きしたい時に呼ぶ (例: ワープ移動後)。</summary>
    public void RebaseToCurrentTransform()
    {
        _baseLocalPos = transform.localPosition;
        _baseLocalRot = transform.localRotation;
    }

    private void Update()
    {
        float now = Time.time * speedMultiplier;

        float angleV = (now / Mathf.Max(0.001f, verticalPeriod)) * Mathf.PI * 2f + _phase;
        float angleH = (now / Mathf.Max(0.001f, horizontalPeriod)) * Mathf.PI * 2f + _phase + Mathf.PI * 0.5f;

        Vector3 axisV = verticalAxis.sqrMagnitude > 0f ? verticalAxis.normalized : Vector3.up;
        Vector3 axisH = horizontalAxis.sqrMagnitude > 0f ? horizontalAxis.normalized : Vector3.right;

        Vector3 offset = axisV * (Mathf.Sin(angleV) * verticalAmplitude)
                       + axisH * (Mathf.Sin(angleH) * horizontalAmplitude);
        transform.localPosition = _baseLocalPos + offset;

        if (rotationAmplitude > 0f)
        {
            float angleR = (now / Mathf.Max(0.001f, rotationPeriod)) * Mathf.PI * 2f + _phase;
            float deg = Mathf.Sin(angleR) * rotationAmplitude;
            Vector3 rAxis = rotationAxis.sqrMagnitude > 0f ? rotationAxis.normalized : Vector3.forward;
            transform.localRotation = _baseLocalRot * Quaternion.AngleAxis(deg, rAxis);
        }
    }
}
