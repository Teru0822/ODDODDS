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

    [Header("Burn Settings")]
    [Range(1f, 10f)]
    public float burnDuration = 3.0f;

    [Header("Sound")]
    [Tooltip("燃焼中にループ再生するサウンドクリップ")]
    public AudioClip burnSound;
    [Range(0f, 1f)]
    public float burnVolume = 1f;
    [Tooltip("0=2D（常に一定音量）/ 1=3D（距離で減衰）。テストは0推奨")]
    [Range(0f, 1f)]
    public float spatialBlend = 0f;

    [Header("Light Flicker")]
    [Range(0f, 5f)]  public float minIntensity = 0.3f;
    [Range(0f, 10f)] public float maxIntensity  = 2.0f;
    [Range(1f, 30f)] public float flickerSpeed  = 12f;

    [Header("Events")]
    public UnityEvent onBurnComplete;

    private Material    _mat;
    private Material    _tmpMat;
    private Renderer    _tmpRenderer;
    private float       _progress;
    private bool        _burning;
    private float       _flickerSeed;
    private AudioSource _audio;

    void Awake()
    {
        _flickerSeed = Random.Range(0f, 100f);
        if (fireLight != null) fireLight.enabled = false;
    }

    void Start()
    {
        if (paperRenderer != null)
            _mat = paperRenderer.material;

        _audio              = gameObject.AddComponent<AudioSource>();
        _audio.clip         = burnSound;
        _audio.loop         = true;
        _audio.playOnAwake  = false;
        _audio.volume       = burnVolume;
        _audio.spatialBlend = spatialBlend;
    }

    void OnDestroy()
    {
        if (_mat    != null) Destroy(_mat);
        if (_tmpMat != null) Destroy(_tmpMat);
    }

    void Update()
    {
        if (!_burning) return;

        _progress = Mathf.Min(_progress + Time.deltaTime / burnDuration, 1.25f);
        _mat?.SetFloat("_BurnProgress", _progress);
        _tmpMat?.SetFloat("_BurnProgress", _progress);

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

        // タイプ完了後に呼ばれるので、ここで TMP マテリアルを差し替える
        // （Start() で設定するとタイプ中に TMP がメッシュ再生成して上書きされるため）
        SetupTMPBurnMaterial();
        _tmpMat?.SetMatrix("_PaperW2L", transform.worldToLocalMatrix);

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
        _tmpMat?.SetFloat("_BurnProgress", 0f);
        if (fireLight != null) fireLight.enabled = false;
        _audio.Stop();
    }

    void SetupTMPBurnMaterial()
    {
        var tmp = GetComponentInChildren<TextMeshPro>(true);
        if (tmp == null) return;

        var shader = Shader.Find("Custom/PaperBurnTMP");
        if (shader == null)
        {
            Debug.LogWarning("[RealisticPaperBurn] Custom/PaperBurnTMP シェーダーが見つかりません。", this);
            return;
        }

        // TMP の MeshRenderer を直接差し替える
        // fontMaterial 経由だとタイプ完了後でも TMP が内部で上書きするケースがある
        _tmpRenderer = tmp.GetComponent<MeshRenderer>();
        if (_tmpRenderer == null) return;

        _tmpMat        = new Material(_tmpRenderer.sharedMaterial);
        _tmpMat.shader = shader;

        if (_mat != null)
        {
            _tmpMat.SetFloat("_MacroScale", _mat.GetFloat("_MacroScale"));
            _tmpMat.SetFloat("_FineScale",  _mat.GetFloat("_FineScale"));
        }

        _tmpMat.SetFloat("_BurnProgress", 0f);
        _tmpRenderer.material = _tmpMat;
    }

    [ContextMenu("Test Burn")]
    void TestBurn() => StartBurning();

    [ContextMenu("Reset Burn")]
    void ResetBurn() => StopAndReset();
}
