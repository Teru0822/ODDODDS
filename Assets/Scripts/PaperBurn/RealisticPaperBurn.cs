using TMPro;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class RealisticPaperBurn : MonoBehaviour
{
    [Header("References")]
    [Tooltip("燃えるメッシュの Renderer")]
    public Renderer paperRenderer;

    [Tooltip("燃焼中に周囲を照らすライト（省略可）")]
    public Light fireLight;

    [Tooltip("燃焼と同時に消えるテキスト。未設定の場合は子から自動検索")]
    public TextMeshPro burnText;

    [Header("Burn Settings")]
    [Range(1f, 10f)]
    public float burnDuration = 3.0f;

    [Header("Sound")]
    [Tooltip("燃焼中にループ再生するサウンドクリップ")]
    public AudioClip burnSound;
    [Range(0f, 1f)]
    public float burnVolume = 1f;

    [Header("Light Flicker")]
    [Range(0f, 5f)]  public float minIntensity = 0.3f;
    [Range(0f, 10f)] public float maxIntensity  = 2.0f;
    [Range(1f, 30f)] public float flickerSpeed  = 12f;

    [Header("Events")]
    public UnityEvent onBurnComplete;

    private Material    _mat;
    private float       _progress;
    private bool        _burning;
    private float       _flickerSeed;
    private AudioSource _audio;
    private Color       _textStartColor;

    void Awake()
    {
        _flickerSeed = Random.Range(0f, 100f);
        if (fireLight != null) fireLight.enabled = false;
    }

    void Start()
    {
        if (paperRenderer != null)
            _mat = paperRenderer.material;

        // AudioSource を自動生成（Loop 再生・PlayOnAwake なし）
        _audio             = gameObject.AddComponent<AudioSource>();
        _audio.clip        = burnSound;
        _audio.loop        = true;
        _audio.playOnAwake = false;
        _audio.volume      = burnVolume;
        _audio.spatialBlend = 1f;

        // テキストが未設定なら子から自動検索
        if (burnText == null)
            burnText = GetComponentInChildren<TextMeshPro>();
        if (burnText != null)
            _textStartColor = burnText.color;
    }

    void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }

    void Update()
    {
        if (!_burning) return;

        _progress = Mathf.Min(_progress + Time.deltaTime / burnDuration, 1.25f);
        _mat?.SetFloat("_BurnProgress", _progress);

        // テキストを燃焼に合わせてフェードアウト（progress 0→1 でアルファ 1→0）
        if (burnText != null)
        {
            Color c = _textStartColor;
            c.a = Mathf.Lerp(1f, 0f, Mathf.Clamp01(_progress));
            burnText.color = c;
        }

        // ライトの揺らめき
        if (fireLight != null)
        {
            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, _flickerSeed);
            fireLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
        }

        if (_progress >= 1.25f)
        {
            _burning = false;
            if (fireLight != null) fireLight.enabled = false;
            _audio.Stop();
            onBurnComplete?.Invoke();
        }
    }

    public void StartBurning()
    {
        if (_burning) return;
        _burning  = true;
        _progress = 0f;

        if (fireLight != null) fireLight.enabled = true;
        if (burnSound != null) _audio.Play();
    }

    public void StopAndReset()
    {
        _burning  = false;
        _progress = 0f;
        _mat?.SetFloat("_BurnProgress", 0f);
        if (fireLight != null) fireLight.enabled = false;
        _audio.Stop();

        // テキストを元の色に戻す
        if (burnText != null) burnText.color = _textStartColor;
    }

    [ContextMenu("Test Burn")]
    void TestBurn() => StartBurning();

    [ContextMenu("Reset Burn")]
    void ResetBurn() => StopAndReset();
}
