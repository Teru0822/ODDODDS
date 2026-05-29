// マスクテクスチャの境界 (オブジェクトの外周シルエット) だけを _OutlineColor で塗る
// フルスクリーンエッジ検出シェーダ。Blitter.BlitTexture から呼ばれる。
// 周囲 4 サンプルを取り、マスク値が neighborhood で差があれば「エッジ」として描画する。
// 結果は Camera Color に合成 (SrcAlpha OneMinusSrcAlpha)。
Shader "Hidden/OutlineEdgeDetect"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.2, 0.5, 1, 1)
        _OutlineWidth ("Outline Width (px)", Float) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "OutlineEdge"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _OutlineColor;
            float _OutlineWidth;

            half SampleMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).r;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float2 px = _BlitTexture_TexelSize.xy * max(1.0, _OutlineWidth);

                half center = SampleMask(uv);
                half l = SampleMask(uv + float2(-px.x, 0));
                half r = SampleMask(uv + float2( px.x, 0));
                half d = SampleMask(uv + float2(0, -px.y));
                half u = SampleMask(uv + float2(0,  px.y));

                // 隣接ピクセルとの差。中央が外側 (0) で隣接に内側 (1) があるとき、
                // または中央が内側で隣接に外側があるとき = 境界
                half maxN = max(max(l, r), max(d, u));
                half minN = min(min(l, r), min(d, u));
                half edge = max(maxN - center, center - minN);

                half alpha = step(0.5, edge) * _OutlineColor.a;
                return half4(_OutlineColor.rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
