using UnityEngine;

/// <summary>
/// Eye prefab 用アニメーション。
/// 1) Shape key (まばたき): 0s→5s→5.5s→6s で 0→0→100→0 と遷移、6 秒周期で繰り返し。
/// 2) Point light: 親 (orbitCenter) を中心に Z 軸まわりで揺れる。
///    light 周期 12 秒 (shape key 2 周期に 1 回)、6〜8 秒の 2 秒間に
///    0°→-30°→0°→30°→0° と振れて元に戻る。
/// Shape key と light の位相は Time.time を共有するので自然に同期する。
/// </summary>
[DisallowMultipleComponent]
public class EyeAnimator : MonoBehaviour
{
    [Header("Shape Key (まばたき)")]
    [Tooltip("Shape key を持つ SkinnedMeshRenderer。null なら子から自動取得")]
    public SkinnedMeshRenderer skinnedMesh;

    [Tooltip("使用する Blend Shape 名。空の場合は blendShapeIndex を使う")]
    public string shapeKeyName = "";

    [Tooltip("shapeKeyName が空 / 見つからない時に使う Blend Shape のインデックス")]
    public int blendShapeIndex = 0;

    [Tooltip("0 で保持する時間 (秒)")]
    public float blinkHoldDuration = 5.0f;

    [Tooltip("0 → 最大値 へ立ち上がる時間 (秒)")]
    public float blinkRiseDuration = 0.5f;

    [Tooltip("最大値 → 0 へ戻す時間 (秒)")]
    public float blinkFallDuration = 0.5f;

    [Tooltip("Blend Shape の最大値 (Unity の SkinnedMesh は 0〜100)")]
    public float shapeKeyMaxValue = 100f;

    [Header("Point Light (Z 軸まわり左右揺れ)")]
    [Tooltip("揺らす Point Light の Transform。null なら子から自動取得")]
    public Transform pointLight;

    [Tooltip("公転中心の Transform。null なら自身 (Eye prefab root) を使う")]
    public Transform orbitCenter;

    [Tooltip("回転軸 (orbitCenter のローカル空間)。デフォルトは local Z")]
    public Vector3 orbitAxis = Vector3.forward;

    [Tooltip("light 全体の周期 (秒)。デフォルト 12 = shape key 2 周期分")]
    public float lightPeriod = 12.0f;

    [Tooltip("light period 内で揺れ始めるタイミング (秒)")]
    public float wiggleStartTime = 6.0f;

    [Tooltip("揺れの 1/4 区間の長さ (秒)。0.5 なら 0→-A→0→A→0 で計 2 秒")]
    public float wiggleQuarterDuration = 0.5f;

    [Tooltip("揺れの振幅 (度)")]
    public float wiggleAngle = 30f;

    private int _resolvedShapeIndex = -1;
    private Vector3 _restLightLocalPos;
    private bool _lightInitialized;

    private float BlinkPeriod => blinkHoldDuration + blinkRiseDuration + blinkFallDuration;

    private void Awake()
    {
        if (skinnedMesh == null) skinnedMesh = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (pointLight == null)
        {
            var light = GetComponentInChildren<Light>(true);
            if (light != null) pointLight = light.transform;
        }
        if (orbitCenter == null) orbitCenter = transform;

        ResolveShapeIndex();
        CaptureLightRest();
    }

    private void ResolveShapeIndex()
    {
        _resolvedShapeIndex = blendShapeIndex;
        if (skinnedMesh == null || skinnedMesh.sharedMesh == null) return;
        if (!string.IsNullOrEmpty(shapeKeyName))
        {
            int idx = skinnedMesh.sharedMesh.GetBlendShapeIndex(shapeKeyName);
            if (idx >= 0) _resolvedShapeIndex = idx;
            else Debug.LogWarning($"[EyeAnimator] Shape key '{shapeKeyName}' が見つかりません。blendShapeIndex={blendShapeIndex} を使用", this);
        }
    }

    private void CaptureLightRest()
    {
        if (pointLight == null || orbitCenter == null) return;
        // orbit 中心のローカル空間で初期位置を覚えておく (中心が動いても追従)
        _restLightLocalPos = orbitCenter.InverseTransformPoint(pointLight.position);
        _lightInitialized = true;
    }

    private void Update()
    {
        UpdateBlink();
        UpdateLight();
    }

    private void UpdateBlink()
    {
        if (skinnedMesh == null || _resolvedShapeIndex < 0) return;
        float period = BlinkPeriod;
        if (period <= 0f) return;
        float t = Time.time % period;

        float value;
        if (t < blinkHoldDuration)
        {
            value = 0f;
        }
        else
        {
            float u = t - blinkHoldDuration;
            if (u < blinkRiseDuration)
            {
                value = Mathf.Lerp(0f, shapeKeyMaxValue, u / Mathf.Max(0.0001f, blinkRiseDuration));
            }
            else
            {
                u -= blinkRiseDuration;
                value = Mathf.Lerp(shapeKeyMaxValue, 0f, u / Mathf.Max(0.0001f, blinkFallDuration));
            }
        }
        skinnedMesh.SetBlendShapeWeight(_resolvedShapeIndex, value);
    }

    private void UpdateLight()
    {
        if (!_lightInitialized || pointLight == null || orbitCenter == null) return;
        if (lightPeriod <= 0f) return;
        float t = Time.time % lightPeriod;
        float angle = ComputeLightAngle(t);

        Vector3 axis = orbitAxis.sqrMagnitude > 0f ? orbitAxis.normalized : Vector3.forward;
        Quaternion rot = Quaternion.AngleAxis(angle, axis);
        Vector3 rotatedLocal = rot * _restLightLocalPos;
        pointLight.position = orbitCenter.TransformPoint(rotatedLocal);
    }

    private float ComputeLightAngle(float t)
    {
        float u = t - wiggleStartTime;
        float q = Mathf.Max(0.0001f, wiggleQuarterDuration);
        if (u < 0f || u >= q * 4f) return 0f;

        if (u < q)      return Mathf.Lerp(0f, -wiggleAngle, u / q);                       // 0 → -A
        if (u < q * 2f) return Mathf.Lerp(-wiggleAngle, 0f, (u - q) / q);                 // -A → 0
        if (u < q * 3f) return Mathf.Lerp(0f, wiggleAngle, (u - q * 2f) / q);             //  0 → +A
        return                Mathf.Lerp(wiggleAngle, 0f, (u - q * 3f) / q);              // +A → 0
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (orbitCenter == null) return;
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
        if (pointLight != null) Gizmos.DrawLine(orbitCenter.position, pointLight.position);
        Gizmos.DrawWireSphere(orbitCenter.position, 0.02f);
    }
#endif
}
