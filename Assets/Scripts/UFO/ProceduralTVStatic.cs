using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 動画ファイル（MP4）や VideoPlayer 不要で、本物のリアルなテレビ砂嵐（TV Static Noise）をリアルタイム自動生成・再生するコンポーネント。
/// 解像度・粒の細かさ（タイリング）・フィルタリングモードを自由に調整できます。
/// </summary>
public class ProceduralTVStatic : MonoBehaviour
{
    [Header("出力ターゲット設定")]
    [Tooltip("砂嵐を出力する UI RawImage（未設定の場合は自動取得します）")]
    [SerializeField] private RawImage targetRawImage;

    [Header("ノイズ解像度・フレームレート")]
    [Tooltip("砂嵐テクスチャの横幅（解像度）")]
    [SerializeField, Range(64, 1024)] private int textureWidth = 512;

    [Tooltip("砂嵐テクスチャの縦幅（解像度）")]
    [SerializeField, Range(64, 1024)] private int textureHeight = 512;

    [Tooltip("1秒間の砂嵐更新フレーム数 (FPS)")]
    [SerializeField, Range(10, 60)] private float frameRate = 30f;

    [Header("砂嵐の見た目・細かさ調整")]
    [Tooltip("砂嵐の粒の細かさ（タイリング数）。数字を大きくするほど粒が細かくなります（例: 2x2, 4x4）")]
    [SerializeField] private Vector2 uvTiling = new Vector2(2f, 2f);

    [Tooltip("テクスチャの補間モード（Bilinear: なめらかなテレビ砂嵐, Point: レトロドット風）")]
    [SerializeField] private FilterMode filterMode = FilterMode.Bilinear;

    [Tooltip("カラーノイズを使用するか（false の場合はモノクロ白黒の砂嵐）")]
    [SerializeField] private bool useColorNoise = false;

    [Tooltip("砂嵐の明るさ・強さ")]
    [SerializeField, Range(0.1f, 2f)] private float brightness = 1.0f;

    private Texture2D _noiseTexture;
    private Color32[] _colorBuffer;
    private float _timer;

    private void Awake()
    {
        InitNoiseTexture();
    }

    private void OnEnable()
    {
        InitNoiseTexture();
        GenerateNoiseFrame();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            InitNoiseTexture();
        }
    }
#endif

    private void InitNoiseTexture()
    {
        if (targetRawImage == null)
        {
            targetRawImage = GetComponent<RawImage>();
        }

        int totalPixels = textureWidth * textureHeight;
        if (_noiseTexture == null || _noiseTexture.width != textureWidth || _noiseTexture.height != textureHeight)
        {
            _noiseTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            _colorBuffer = new Color32[totalPixels];
        }

        _noiseTexture.filterMode = filterMode;
        _noiseTexture.wrapMode = TextureWrapMode.Repeat;

        if (targetRawImage != null)
        {
            targetRawImage.texture = _noiseTexture;
            // UVタイリングを設定して砂嵐の粒を細かく調整
            targetRawImage.uvRect = new Rect(0, 0, Mathf.Max(0.1f, uvTiling.x), Mathf.Max(0.1f, uvTiling.y));
        }
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        float interval = 1f / Mathf.Max(1f, frameRate);

        if (_timer >= interval)
        {
            _timer = 0f;
            GenerateNoiseFrame();
        }
    }

    private void GenerateNoiseFrame()
    {
        if (_colorBuffer == null || _noiseTexture == null) return;

        int len = _colorBuffer.Length;
        if (useColorNoise)
        {
            for (int i = 0; i < len; i++)
            {
                byte r = (byte)Mathf.Clamp(Random.Range(0, 256) * brightness, 0, 255);
                byte g = (byte)Mathf.Clamp(Random.Range(0, 256) * brightness, 0, 255);
                byte b = (byte)Mathf.Clamp(Random.Range(0, 256) * brightness, 0, 255);
                _colorBuffer[i] = new Color32(r, g, b, 255);
            }
        }
        else
        {
            for (int i = 0; i < len; i++)
            {
                byte val = (byte)Mathf.Clamp(Random.Range(0, 256) * brightness, 0, 255);
                _colorBuffer[i] = new Color32(val, val, val, 255);
            }
        }

        _noiseTexture.SetPixels32(_colorBuffer);
        _noiseTexture.Apply();

        if (targetRawImage != null && targetRawImage.texture != _noiseTexture)
        {
            targetRawImage.texture = _noiseTexture;
        }
    }
}
