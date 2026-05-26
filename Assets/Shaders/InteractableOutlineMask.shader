// アウトライン用のステンシルマスクシェーダ。
// オブジェクトの全パーツ (元メッシュ) のシルエットをステンシルバッファ (bit 64) に書き込むだけ。色は描かない。
// InteractableOutline より早い Queue で描画され、「オブジェクトが占める領域」を確定させる。
// その後 InteractableOutline がステンシルの立っていない外側にだけ輪郭を描くため、
// 内側のパーツ境界の縁取りが抑制され、外周シルエットだけが残る。
//
// シェーダ名は "Hidden/InteractableOutlineMask"。
Shader "Hidden/InteractableOutlineMask"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+10"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "OutlineMask"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            ColorMask 0

            Stencil
            {
                Ref 64
                ReadMask 64
                WriteMask 64
                Comp Always
                Pass Replace
            }

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
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
