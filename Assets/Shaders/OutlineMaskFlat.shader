// アウトライン用のマスクシェーダ。ハイライト対象を白で塗りつぶすだけ。
// OutlineRendererFeature が一時 R8 RT を作って、登録済み Renderer をこのマテリアルで描画する。
// その後 OutlineEdgeDetect が R チャネルの境界を検出して輪郭線にする。
// ZTest Always: 奥行きを無視して常に塗る (Scene ビュー選択風に貫通させる)。
Shader "Hidden/OutlineMaskFlat"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "OutlineMask"
            ZWrite Off
            ZTest Always
            Cull Back
            Blend Off
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(1, 0, 0, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
