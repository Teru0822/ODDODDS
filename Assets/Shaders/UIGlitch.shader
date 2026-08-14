Shader "UI/Glitch"
{
    // Tutorial_Canvas等の上に重ねて使う「ハッキングされたような」画面ノイズシェーダー。
    // GrabPassで背後を取得して歪ませる方式は、Screen Space - Camera + RenderTexture構成の
    // TV画面キャンバスとの相性が悪く映らないことがあるため、背後には一切触れず、
    // 色付きノイズ・走査線・ブロック状の光を単純に上から重ねる方式にしている。
    // _GlitchIntensityが0の間は完全に透明で、スクリプト側からバースト的に0→1→0と動かして使う。
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Noise Tint", Color) = (0.7, 1, 0.85, 1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        [Header(Glitch)]
        _GlitchIntensity ("Glitch Intensity (0=off, 1=max)", Range(0,1)) = 0
        _ScanlineDensity ("Scanline Density", Float) = 300
        _BlockSize ("Block Cell Size (UV, 0-1)", Range(0.005, 0.3)) = 0.05
        _NoiseSeed ("Noise Seed (drive from script)", Float) = 0
        _MaxAlpha ("Max Overlay Alpha", Range(0,1)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float4 _ClipRect;

            float _GlitchIntensity;
            float _ScanlineDensity;
            float _BlockSize;
            float _NoiseSeed;
            float _MaxAlpha;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // RGBずれっぽさを出すため、チャンネルごとに少しずらしたノイズをサンプルする
                float rNoise = hash(uv * 260.0 + float2(_NoiseSeed, 0.0));
                float gNoise = hash(uv * 260.0 + float2(0.0, _NoiseSeed));
                float bNoise = hash(uv * 260.0 + float2(_NoiseSeed, _NoiseSeed));

                // ブロック状にランダムな明るさを出す（データモッシュ風のブロックノイズ）
                float2 blockCoord = floor(uv / max(_BlockSize, 0.001));
                float blockRand = hash(blockCoord + _NoiseSeed);
                float blockActive = step(0.85, blockRand);

                // 横方向の走査線
                float scanLine = step(0.5, frac(uv.y * _ScanlineDensity + _NoiseSeed * 0.37));

                float pattern = saturate(blockActive * 0.9 + scanLine * 0.12 + 0.04);

                fixed3 col = fixed3(rNoise, gNoise, bNoise) * _Color.rgb;
                fixed4 outColor = fixed4(col, pattern * _MaxAlpha * _GlitchIntensity * IN.color.a);

                #ifdef UNITY_UI_CLIP_RECT
                outColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(outColor.a - 0.001);
                #endif

                return outColor;
            }
            ENDCG
        }
    }
}
