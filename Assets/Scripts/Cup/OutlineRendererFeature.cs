using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Scene ビュー選択風のポストプロセス輪郭ハイライト用 ScriptableRendererFeature (URP 17 / Render Graph)。
/// OutlineHighlightUtil.Active に登録された Renderer を以下の 2 パスで処理:
///   1) マスクパス: 一時 R8 RT に対象 Renderer をフラット白で描画 (奥行き無視)
///   2) エッジパス: マスクの境界を Sobel 風差分で検出し、_OutlineColor でカメラカラーに合成
/// 同じ (色, 太さ) の Renderer はまとめて 2 パスを実行。異なる色は色ごとに 2 パス。
///
/// ユーザー設定: Edit → Project Settings → Graphics → URP の有効な Renderer (PC_Renderer など)
///              を開き、Add Renderer Feature から OutlineRendererFeature を追加。
/// </summary>
public class OutlineRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("輪郭描画タイミング。透過オブジェクトの上に出すなら AfterRenderingTransparents")]
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Tooltip("マスク描画シェーダ (空なら Shader.Find で 'Hidden/OutlineMaskFlat' を取得)")]
        public Shader maskShader;

        [Tooltip("エッジ検出シェーダ (空なら Shader.Find で 'Hidden/OutlineEdgeDetect' を取得)")]
        public Shader edgeShader;
    }

    public Settings settings = new Settings();
    private OutlinePass _pass;

    public override void Create()
    {
        if (settings.maskShader == null) settings.maskShader = Shader.Find("Hidden/OutlineMaskFlat");
        if (settings.edgeShader == null) settings.edgeShader = Shader.Find("Hidden/OutlineEdgeDetect");
        if (settings.maskShader == null || settings.edgeShader == null)
        {
            Debug.LogWarning("[OutlineRendererFeature] 必要なシェーダが見つかりません (Hidden/OutlineMaskFlat, Hidden/OutlineEdgeDetect)。Assets/Shaders/ に存在するか Always Included Shaders に追加されているか確認してください。");
            return;
        }
        _pass = new OutlinePass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pass != null) renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
        _pass = null;
    }

    private class OutlinePass : ScriptableRenderPass
    {
        private readonly Material _maskMat;
        private readonly Material _edgeMat;
        private static readonly int s_OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int s_OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        public OutlinePass(Settings settings)
        {
            renderPassEvent = settings.renderPassEvent;
            _maskMat = CoreUtils.CreateEngineMaterial(settings.maskShader);
            _edgeMat = CoreUtils.CreateEngineMaterial(settings.edgeShader);
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_maskMat);
            CoreUtils.Destroy(_edgeMat);
        }

        private class MaskPassData
        {
            public List<Renderer> renderers;
            public Material maskMat;
        }

        private class EdgePassData
        {
            public TextureHandle mask;
            public Material edgeMat;
            public Color color;
            public float width;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var active = OutlineHighlightUtil.Active;
            if (active.Count == 0 || _maskMat == null || _edgeMat == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var camColor = resourceData.activeColorTexture;

            var groups = GroupByColor(active);
            int w = cameraData.cameraTargetDescriptor.width;
            int h = cameraData.cameraTargetDescriptor.height;

            foreach (var group in groups)
            {
                var maskDesc = new TextureDesc(w, h)
                {
                    format = GraphicsFormat.R8_UNorm,
                    clearBuffer = true,
                    clearColor = Color.clear,
                    name = "OutlineMask",
                    msaaSamples = MSAASamples.None
                };
                var maskTex = renderGraph.CreateTexture(maskDesc);

                // Pass A: マスク描画
                using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>("OutlineMask", out var data))
                {
                    data.renderers = group.renderers;
                    data.maskMat = _maskMat;
                    builder.SetRenderAttachment(maskTex, 0);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((MaskPassData d, RasterGraphContext ctx) =>
                    {
                        foreach (var r in d.renderers)
                        {
                            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                            var mats = r.sharedMaterials;
                            int n = mats != null ? mats.Length : 0;
                            for (int sm = 0; sm < n; sm++)
                            {
                                ctx.cmd.DrawRenderer(r, d.maskMat, sm, 0);
                            }
                        }
                    });
                }

                // Pass B: エッジ検出してカメラカラーに合成
                using (var builder = renderGraph.AddRasterRenderPass<EdgePassData>("OutlineEdge", out var data))
                {
                    data.mask = maskTex;
                    data.edgeMat = _edgeMat;
                    data.color = group.color;
                    data.width = group.width;
                    builder.UseTexture(maskTex);
                    builder.SetRenderAttachment(camColor, 0);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((EdgePassData d, RasterGraphContext ctx) =>
                    {
                        d.edgeMat.SetColor(s_OutlineColorId, d.color);
                        d.edgeMat.SetFloat(s_OutlineWidthId, d.width);
                        Blitter.BlitTexture(ctx.cmd, d.mask, new Vector4(1f, 1f, 0f, 0f), d.edgeMat, 0);
                    });
                }
            }
        }

        private struct ColorGroup
        {
            public Color color;
            public float width;
            public List<Renderer> renderers;
        }

        private List<ColorGroup> GroupByColor(IReadOnlyList<OutlineHighlightUtil.OutlineRequest> active)
        {
            var groups = new List<ColorGroup>();
            for (int i = 0; i < active.Count; i++)
            {
                var req = active[i];
                if (req.renderer == null) continue;
                int found = -1;
                for (int g = 0; g < groups.Count; g++)
                {
                    if (groups[g].color == req.color && Mathf.Approximately(groups[g].width, req.width))
                    {
                        found = g;
                        break;
                    }
                }
                if (found >= 0)
                {
                    groups[found].renderers.Add(req.renderer);
                }
                else
                {
                    groups.Add(new ColorGroup
                    {
                        color = req.color,
                        width = req.width,
                        renderers = new List<Renderer> { req.renderer }
                    });
                }
            }
            return groups;
        }
    }
}
