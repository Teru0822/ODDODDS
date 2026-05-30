using UnityEngine;

/// <summary>
/// 切れかけ蛍光灯の「バチバチ」用の火花 VFX。実行時に ParticleSystem を組み立てて、
/// Emit(count) で外向きに火花を散らす。FlickerLight から呼ばれる想定。
/// 自分でも autoEmit=true にすればランダム間隔で連発できる。
/// </summary>
[DisallowMultipleComponent]
public class SparkBurstVFX : MonoBehaviour
{
    [Header("見た目")]
    [Tooltip("火花の色")]
    public Color sparkColor = new Color(1f, 0.85f, 0.4f, 1f);

    [Tooltip("エミッションの強さ (色 × 倍率)")]
    public float emissionIntensity = 5f;

    [Tooltip("発生サイズ (m)")]
    public float startSize = 0.015f;

    [Tooltip("発生時の初速範囲 (m/s)")]
    public Vector2 startSpeed = new Vector2(1.5f, 4f);

    [Tooltip("寿命 (秒)")]
    public Vector2 lifetime = new Vector2(0.15f, 0.5f);

    [Tooltip("コーン状に散らす半角 (度)。180 で全方向")]
    [Range(0f, 180f)]
    public float spreadAngle = 60f;

    [Tooltip("コーンの向き (この Transform のローカル空間)。default は +Y (上方向に飛ぶ)")]
    public Vector3 emitDirection = Vector3.up;

    [Tooltip("重力倍率 (1=通常)")]
    public float gravityModifier = 1f;

    [Header("Material (任意指定)")]
    [Tooltip("ParticleSystemRenderer に割り当てる Material。null なら URP Particles/Unlit を実行時に生成")]
    public Material sparkMaterial;

    [Header("自動エミット")]
    [Tooltip("ランダム間隔で自動的にバーストする (デバッグ用)。通常は FlickerLight 側から Emit を呼ぶ")]
    public bool autoEmit = false;

    [Tooltip("自動エミット間隔 (秒)")]
    public Vector2 autoEmitInterval = new Vector2(0.2f, 1f);

    [Tooltip("1 バーストあたりの粒子数")]
    public Vector2Int particlesPerBurst = new Vector2Int(4, 10);

    private ParticleSystem _ps;
    private float _nextAutoEmit;

    private void Awake()
    {
        BuildParticleSystem();
        _nextAutoEmit = Time.time + Random.Range(autoEmitInterval.x, autoEmitInterval.y);
    }

    private void BuildParticleSystem()
    {
        var go = new GameObject("SparkBurstPS");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        _ps = go.AddComponent<ParticleSystem>();

        // 一旦止めてから設定 (Awake 時に再生開始されるのを抑止)
        _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = _ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startColor = sparkColor * emissionIntensity;
        main.startSize = startSize;
        main.startSpeed = new ParticleSystem.MinMaxCurve(startSpeed.x, startSpeed.y);
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.gravityModifier = gravityModifier;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 256;

        var emission = _ps.emission;
        emission.enabled = false; // 手動 Emit のみ

        var shape = _ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = spreadAngle;
        shape.radius = 0.005f;
        if (emitDirection.sqrMagnitude > 0.0001f)
        {
            shape.rotation = Quaternion.LookRotation(emitDirection.normalized).eulerAngles;
        }

        // 寿命にしたがってフェードアウト
        var colorOverLife = _ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = grad;

        var renderer = _ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2f;
        renderer.velocityScale = 0.05f;
        renderer.material = sparkMaterial != null ? sparkMaterial : CreateDefaultSparkMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    private static Material _sharedDefault;
    private static Material CreateDefaultSparkMaterial()
    {
        if (_sharedDefault != null) return _sharedDefault;
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh == null) return null;
        _sharedDefault = new Material(sh) { name = "SparkBurst (default)" };
        _sharedDefault.SetColor("_BaseColor", Color.white);
        if (_sharedDefault.HasProperty("_Surface")) _sharedDefault.SetFloat("_Surface", 1); // Transparent
        if (_sharedDefault.HasProperty("_Blend")) _sharedDefault.SetFloat("_Blend", 1);   // Additive
        return _sharedDefault;
    }

    /// <summary>FlickerLight などから呼んで火花を散らす。</summary>
    public void Emit(int count)
    {
        if (_ps == null) return;
        _ps.Emit(Mathf.Max(0, count));
    }

    private void Update()
    {
        if (!autoEmit || _ps == null) return;
        if (Time.time >= _nextAutoEmit)
        {
            int c = Random.Range(particlesPerBurst.x, particlesPerBurst.y + 1);
            _ps.Emit(c);
            _nextAutoEmit = Time.time + Random.Range(autoEmitInterval.x, autoEmitInterval.y);
        }
    }
}
