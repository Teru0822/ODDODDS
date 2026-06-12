using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 「古びた・腐敗・廃墟」系のホラー画面ポストエフェクト用 ScriptableRendererFeature (URP 17 / Render Graph)。
/// カメラカラーを一時 RT にコピーしつつ Hidden/HorrorDecay シェーダを掛け、結果をカメラカラーに差し戻す
/// (フルスクリーン 1 パス)。退色・腐食グレード・黴染み・色収差・グレイン・ヴィネット・照明ちらつきを適用。
///
/// 設定手順: Edit → Project Settings → Graphics → 有効な Renderer (PC_Renderer / Mobile_Renderer) を開き、
///          Add Renderer Feature から HorrorDecayRendererFeature を追加。Inspector で強度を調整できる。
/// 一時的に切りたいときは Renderer Feature のチェックを外すか、settings.intensity を 0 に。
/// </summary>
public class HorrorDecayRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("適用タイミング。標準のポストプロセス後に乗せるなら AfterRenderingPostProcessing")]
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

        [Tooltip("効果シェーダ (空なら Shader.Find で 'Hidden/HorrorDecay' を取得)")]
        public Shader shader;

        [Tooltip("全効果の最終ブレンド量。0 で素通し、1 でフル適用")]
        [Range(0f, 1f)] public float intensity = 1f;

        [Header("Color / 腐食")]
        [Range(0f, 1f)]   public float desaturation = 0.55f;
        public Color rotColor = new Color(0.32f, 0.30f, 0.18f, 1f);
        [Range(0f, 1f)]   public float rotStrength = 0.45f;
        [Range(0.5f, 2f)] public float contrast = 1.18f;
        [Range(0f, 0.2f)] public float blackLift = 0.03f;

        [Header("Mold / 黴染み")]
        public Color moldColor = new Color(0.05f, 0.06f, 0.03f, 1f);
        [Range(0f, 1f)]  public float moldStrength = 0.55f;
        [Range(1f, 12f)] public float moldScale = 4.5f;
        [Range(0f, 1f)]  public float moldEdgeCreep = 0.6f;

        [Header("Vignette / Lens")]
        [Range(0f, 2f)]   public float vignette = 1.1f;
        [Range(0.5f, 6f)] public float vignettePower = 2.4f;
        [Range(0f, 3f)]   public float chromaticAberration = 0.7f;
        [Range(0f, 2f)]   public float warp = 0.35f;

        [Header("Grain / Flicker")]
        [Range(0f, 1f)]  public float filmGrain = 0.18f;
        [Range(0f, 1f)]  public float lightFlicker = 0.12f;
        [Range(0f, 30f)] public float flickerSpeed = 9.0f;
    }

    public Settings settings = new Settings();
    private HorrorDecayPass _pass;

    public override void Create()
    {
        if (settings.shader == null) settings.shader = Shader.Find("Hidden/HorrorDecay");
        if (settings.shader == null)
        {
            Debug.LogWarning("[HorrorDecayRendererFeature] シェーダ 'Hidden/HorrorDecay' が見つかりません。" +
                             "Assets/Shaders/HorrorDecay.shader が存在するか、必要なら Always Included Shaders に追加してください。");
            return;
        }
        _pass = new HorrorDecayPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pass == null || settings.intensity <= 0f) return;
        // ゲームカメラのみ (シーンビュー/プレビューには掛けない)
        var cam = renderingData.cameraData.cameraType;
        if (cam != CameraType.Game && cam != CameraType.VR) return;
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
        _pass = null;
    }

    private class HorrorDecayPass : ScriptableRenderPass
    {
        private readonly Settings _s;
        private readonly Material _mat;

        private static readonly int s_Desaturation   = Shader.PropertyToID("_Desaturation");
        private static readonly int s_RotColor        = Shader.PropertyToID("_RotColor");
        private static readonly int s_RotStrength      = Shader.PropertyToID("_RotStrength");
        private static readonly int s_Contrast        = Shader.PropertyToID("_Contrast");
        private static readonly int s_Lift            = Shader.PropertyToID("_Lift");
        private static readonly int s_MoldColor       = Shader.PropertyToID("_MoldColor");
        private static readonly int s_MoldStrength    = Shader.PropertyToID("_MoldStrength");
        private static readonly int s_MoldScale       = Shader.PropertyToID("_MoldScale");
        private static readonly int s_MoldCreep       = Shader.PropertyToID("_MoldCreep");
        private static readonly int s_Vignette        = Shader.PropertyToID("_Vignette");
        private static readonly int s_VignettePower   = Shader.PropertyToID("_VignettePower");
        private static readonly int s_ChromaticAberr  = Shader.PropertyToID("_ChromaticAberr");
        private static readonly int s_Warp            = Shader.PropertyToID("_WarpAmount");
        private static readonly int s_Grain           = Shader.PropertyToID("_GrainAmount");
        private static readonly int s_Flicker         = Shader.PropertyToID("_FlickerAmount");
        private static readonly int s_FlickerSpeed    = Shader.PropertyToID("_FlickerSpeed");

        public HorrorDecayPass(Settings settings)
        {
            _s = settings;
            renderPassEvent = settings.renderPassEvent;
            _mat = CoreUtils.CreateEngineMaterial(settings.shader);
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_mat);
        }

        private void ApplySettings()
        {
            // intensity は各効果の量に乗せて「素通し ↔ フル適用」を 1 ノブで制御
            float k = Mathf.Clamp01(_s.intensity);
            _mat.SetFloat(s_Desaturation, _s.desaturation * k);
            _mat.SetColor(s_RotColor, _s.rotColor);
            _mat.SetFloat(s_RotStrength, _s.rotStrength * k);
            _mat.SetFloat(s_Contrast, Mathf.Lerp(1f, _s.contrast, k));
            _mat.SetFloat(s_Lift, _s.blackLift * k);
            _mat.SetColor(s_MoldColor, _s.moldColor);
            _mat.SetFloat(s_MoldStrength, _s.moldStrength * k);
            _mat.SetFloat(s_MoldScale, _s.moldScale);
            _mat.SetFloat(s_MoldCreep, _s.moldEdgeCreep);
            _mat.SetFloat(s_Vignette, _s.vignette * k);
            _mat.SetFloat(s_VignettePower, _s.vignettePower);
            _mat.SetFloat(s_ChromaticAberr, _s.chromaticAberration * k);
            _mat.SetFloat(s_Warp, _s.warp * k);
            _mat.SetFloat(s_Grain, _s.filmGrain * k);
            _mat.SetFloat(s_Flicker, _s.lightFlicker * k);
            _mat.SetFloat(s_FlickerSpeed, _s.flickerSpeed);
        }

        private class PassData
        {
            public TextureHandle source;
            public Material mat;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_mat == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            var source = resourceData.activeColorTexture;

            // 出力先となる一時 RT (source と同フォーマット)
            var desc = cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.depthBufferBits = 0;
            var destination = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, desc, "HorrorDecayTarget", false, FilterMode.Bilinear);

            ApplySettings();

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Horror Decay", out var data))
            {
                data.source = source;
                data.mat = _mat;
                builder.UseTexture(source);
                builder.SetRenderAttachment(destination, 0);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), d.mat, 0);
                });
            }

            // 加工後を以降のパスのカメラカラーとして差し戻す
            resourceData.cameraColor = destination;
        }
    }
}
