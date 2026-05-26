// 反転ハル (Inverted Hull) + ステンシルマスク方式のアウトラインシェーダ。
// InteractableOutlineMask が先にオブジェクト全パーツのシルエットをステンシル(bit 64)へ書き込み、
// このシェーダは「ステンシルが立っていない部分 (= オブジェクトの外側)」にだけ法線押し出しした後面を描く。
// これによりパーツ間の内側の縁取りが消え、オブジェクト全体の一番外の輪郭だけが残る。
//
// シェーダ名は "Hidden/InteractableOutline"。
Shader "Hidden/InteractableOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.35, 0.85, 1, 1)
        _OutlineWidth ("Outline Width", Float) = 0.005
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+20"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite Off
            ZTest LEqual

            Stencil
            {
                Ref 64
                ReadMask 64
                WriteMask 0
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 expandedOS = IN.positionOS.xyz + IN.normalOS * _OutlineWidth;
                OUT.positionHCS = TransformObjectToHClip(expandedOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
