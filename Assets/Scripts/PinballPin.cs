using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ピンボールのピン (Enforce / Split) を表すコンポーネント。
/// 子オブジェクト pin_top の Renderer に発光色を設定し、玉が触れたら一度だけ効果を発動する。
/// 消費後は pin_top を黒色 (発光無し) にして、以降の衝突では効果なしになる。
///
/// 設置方法:
///   1. ピンの GameObject に本コンポーネントをアタッチ
///   2. type を Enforce か Split に設定
///   3. 子に "pin_top" GameObject (Renderer 持ち) があれば自動検出。
///      別名にする場合は pinTopRenderer を直接アサインするか pinTopChildName を変更
///   4. ピンの Collider のタグを「Enforce Pin」「Split Pin」と合わせるのが分かりやすい
///      (PinballBallController は本コンポーネントを直接見るのでタグ自体は必須ではない)
/// </summary>
public class PinballPin : MonoBehaviour
{
    public enum PinType { Split, Enforce }

    [Header("ピン種別")]
    [Tooltip("Split: 玉が分裂する従来のピン\nEnforce: 玉の発色レベルを 1 段階上げるピン")]
    [SerializeField] private PinType type = PinType.Split;

    [Header("発光対象")]
    [Tooltip("発光させる Renderer。未設定なら pinTopChildName で子を検索する")]
    [SerializeField] private Renderer pinTopRenderer;

    [Tooltip("pinTopRenderer 未設定時に検索する子オブジェクト名")]
    [SerializeField] private string pinTopChildName = "pin_top";

    [Header("発光色")]
    [Tooltip("Enforce Pin の初期発光色")]
    [SerializeField] private Color enforceEmissionColor = Color.red;

    [Tooltip("Split Pin の初期発光色")]
    [SerializeField] private Color splitEmissionColor = Color.blue;

    [Tooltip("消費後 (玉が触れた後) の発光色")]
    [SerializeField] private Color consumedEmissionColor = Color.black;

    [Tooltip("発光強度倍率")]
    [Min(0f)]
    [SerializeField] private float emissionIntensity = 2f;

    [Header("自転 (Y軸まわり)")]
    [Tooltip("Y軸まわりの自転速度 (deg/s)。0 で停止")]
    [SerializeField] private float yRotationSpeed = 90f;

    [Header("pin_wing パルス (XZ 拡縮)")]
    [Tooltip("拡縮させる pin_wing Transform。未設定なら pinWingChildName で子検索")]
    [SerializeField] private Transform pinWing;

    [Tooltip("pinWing 未設定時に検索する子オブジェクト名")]
    [SerializeField] private string pinWingChildName = "pin_wing";

    [Tooltip("1 周期の長さ (秒)。短いほど『ビクンビクン』が速い")]
    [SerializeField, Min(0.05f)] private float pulsePeriod = 0.6f;

    [Tooltip("XZ 拡縮の振幅。0.3 なら最大 1.3 倍")]
    [SerializeField, Min(0f)] private float pulseAmplitude = 0.3f;

    [Tooltip("周期に対する立ち上がりの長さ (0~1)。小さいほどパキッとビクン")]
    [SerializeField, Range(0.02f, 0.5f)] private float pulseRiseFraction = 0.12f;

    [Tooltip("周期内で減衰が終わる位置 (0~1)。これ以降は次パルスまで停止 (休止)")]
    [SerializeField, Range(0.2f, 1f)] private float pulseDecayEndFraction = 0.55f;

    [Tooltip("消費後 (黒くなった後) もアニメーションを続けるか")]
    [SerializeField] private bool keepAnimatingWhenConsumed = false;

    [Header("Exchange 換金で再有効化")]
    [Tooltip("Exchange で換金 (DispenseMoney) が完了した時に、このピンを再有効化 (消費前の状態に戻す)")]
    [SerializeField] private bool reactivateOnExchangeDispense = true;

    private bool _consumed = false;
    private ExchangeStation _exchangeStation;
    private Material _pinTopMaterialInstance;
    private Vector3 _pinWingInitialScale = Vector3.one;
    // XZ 一律拡縮の基準サイズ (初期 X と Z の平均)。これに pulse 倍率を掛けて X = Z にセット
    private float _pinWingBaseSizeXZ = 1f;
    private float _pulseTimer = 0f;

    public bool IsConsumed => _consumed;
    public PinType Type => type;

    void Awake()
    {
        if (pinTopRenderer == null && !string.IsNullOrEmpty(pinTopChildName))
        {
            var t = FindChildRecursive(transform, pinTopChildName);
            if (t != null) pinTopRenderer = t.GetComponent<Renderer>();
        }
        if (pinTopRenderer == null)
        {
            pinTopRenderer = GetComponentInChildren<Renderer>();
        }

        if (pinTopRenderer != null)
        {
            // material を読み取ると自動でインスタンス化される (sharedMaterial を汚染しない)
            _pinTopMaterialInstance = pinTopRenderer.material;
        }

        ApplyEmission(type == PinType.Enforce ? enforceEmissionColor : splitEmissionColor);

        // pin_wing を解決
        if (pinWing == null && !string.IsNullOrEmpty(pinWingChildName))
        {
            var w = FindChildRecursive(transform, pinWingChildName);
            if (w != null) pinWing = w;
        }
        if (pinWing != null)
        {
            // pin_wing の Transform が非単位回転 (Blender 由来等) だと、
            // localScale の X/Z 軸が斜めに傾いていて、X = Z で拡縮しても見た目が斜めに伸びる。
            // identity rotation のラッパー GameObject を間に挟んで、ラッパーを拡縮することで
            // Pin root のローカル XZ (= Y軸自転下でも水平な平面) で確実に均等拡張できる。
            GameObject wrapperGO = new GameObject(pinWing.name + "_ScaleWrapper");
            wrapperGO.transform.SetParent(pinWing.parent, false);
            wrapperGO.transform.localPosition = pinWing.localPosition;
            wrapperGO.transform.localRotation = Quaternion.identity;
            wrapperGO.transform.localScale = Vector3.one;

            // pin_wing 自体をラッパー配下に移す (ワールド姿勢維持)
            pinWing.SetParent(wrapperGO.transform, true);

            // 以降スケールするのはラッパー側
            pinWing = wrapperGO.transform;
            _pinWingInitialScale = Vector3.one;
            _pinWingBaseSizeXZ = 1f;
        }
    }

    void Start()
    {
        if (!reactivateOnExchangeDispense) return;
        _exchangeStation = FindAnyObjectByType<ExchangeStation>();
        if (_exchangeStation == null) return;
        if (_exchangeStation.onDispenseComplete == null)
        {
            _exchangeStation.onDispenseComplete = new UnityEvent();
        }
        _exchangeStation.onDispenseComplete.AddListener(Reactivate);
    }

    void OnDestroy()
    {
        if (_exchangeStation != null && _exchangeStation.onDispenseComplete != null)
        {
            _exchangeStation.onDispenseComplete.RemoveListener(Reactivate);
        }
    }

    /// <summary>消費状態を解除して発色を戻し、再びボールに効果を発動できるようにする。</summary>
    public void Reactivate()
    {
        if (!_consumed) return;
        _consumed = false;
        ApplyEmission(type == PinType.Enforce ? enforceEmissionColor : splitEmissionColor);
        _pulseTimer = 0f;
    }

    void Update()
    {
        if (_consumed && !keepAnimatingWhenConsumed) return;

        // Y 軸まわりの自転 (ローカル軸基準)
        if (!Mathf.Approximately(yRotationSpeed, 0f))
        {
            transform.Rotate(Vector3.up, yRotationSpeed * Time.deltaTime, Space.Self);
        }

        // pin_wing の XZ パルス拡縮 (Y は不変、X と Z は常に同値で 1:1)
        if (pinWing != null && pulseAmplitude > 0f)
        {
            _pulseTimer += Time.deltaTime;
            if (_pulseTimer >= pulsePeriod) _pulseTimer -= pulsePeriod;
            float t = _pulseTimer / pulsePeriod;
            float pulse = EvaluatePulse(t); // 0~1
            float currentSize = _pinWingBaseSizeXZ * (1f + pulse * pulseAmplitude);
            pinWing.localScale = new Vector3(
                currentSize,
                _pinWingInitialScale.y,
                currentSize
            );
        }
    }

    /// <summary>
    /// ビクン形状のパルスカーブ。
    ///   t < riseFraction         : 0 → 1 へ線形に立ち上がる
    ///   t < decayEndFraction     : 1 → 0 へ二次関数で減衰
    ///   それ以降                  : 0 で待機 (次のビクンまでの休止)
    /// </summary>
    float EvaluatePulse(float t)
    {
        float rise = Mathf.Max(0.001f, pulseRiseFraction);
        float decayEnd = Mathf.Max(rise + 0.001f, pulseDecayEndFraction);

        if (t < rise) return t / rise;
        if (t < decayEnd)
        {
            float u = (t - rise) / (decayEnd - rise);
            return 1f - u * u;
        }
        return 0f;
    }

    /// <summary>玉が触れた時に呼ぶ。一度だけ効果発動 → pin_top を黒に → 以降無効。</summary>
    public bool TryConsume()
    {
        if (_consumed) return false;
        _consumed = true;
        ApplyEmission(consumedEmissionColor);
        // パルス停止時はスケールを 1:1 ベースサイズに戻す (拡張途中で固まらないように)
        if (!keepAnimatingWhenConsumed && pinWing != null)
        {
            pinWing.localScale = new Vector3(_pinWingBaseSizeXZ, _pinWingInitialScale.y, _pinWingBaseSizeXZ);
        }
        return true;
    }

    void ApplyEmission(Color c)
    {
        if (_pinTopMaterialInstance == null) return;
        _pinTopMaterialInstance.EnableKeyword("_EMISSION");
        _pinTopMaterialInstance.SetColor("_EmissionColor", c * emissionIntensity);
    }

    static Transform FindChildRecursive(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c.name == name) return c;
            var found = FindChildRecursive(c, name);
            if (found != null) return found;
        }
        return null;
    }
}
