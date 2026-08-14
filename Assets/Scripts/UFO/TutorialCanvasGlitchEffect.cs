using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tutorial_Canvas全体に「ハッキングされたような」画面ノイズ（色付きノイズ・スキャンライン・
/// ブロック状の光）を常時表示し続ける。
/// UI/Glitchシェーダーを使ったマテリアルを設定したImage（Tutorial_Canvasの最前面に、画面全体を
/// 覆うサイズ・アンカーストレッチで配置したもの）にアタッチして使う。
/// </summary>
[RequireComponent(typeof(Image))]
public class TutorialCanvasGlitchEffect : MonoBehaviour
{
    [Tooltip("常時表示するグリッチ強度")]
    [SerializeField, Range(0f, 1f)] private float intensity = 0.5f;

    [Tooltip("ノイズパターンが切り替わる速さ（大きいほど激しくチラつく）")]
    [SerializeField] private float noiseFlickerSpeed = 20f;

    private Image _image;
    private Material _materialInstance;

    private static readonly int GlitchIntensityId = Shader.PropertyToID("_GlitchIntensity");
    private static readonly int NoiseSeedId = Shader.PropertyToID("_NoiseSeed");

    private void Awake()
    {
        _image = GetComponent<Image>();
        // 他のImageとマテリアルが共有され、意図せず互いのグリッチ状態に影響しないようインスタンス化する
        _materialInstance = new Material(_image.material);
        _image.material = _materialInstance;
    }

    private void OnEnable()
    {
        _materialInstance.SetFloat(GlitchIntensityId, intensity);
    }

    private void OnDisable()
    {
        _materialInstance.SetFloat(GlitchIntensityId, 0f);
    }

    private void Update()
    {
        _materialInstance.SetFloat(GlitchIntensityId, intensity);
        _materialInstance.SetFloat(NoiseSeedId, Time.time * noiseFlickerSpeed);
    }
}
