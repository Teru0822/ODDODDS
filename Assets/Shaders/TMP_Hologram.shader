// TextMeshProUGUI 用ホログラムシェーダー（URP）
// SDF テキストレンダリング + スキャンライン・リムグロー・フリッカー・グリッチを統合。
// 頂点カラー（timerText.color）をそのまま発光色として使用するため、
// TimerDisplay の warningFaceColor は黒ではなく赤に設定すること。
Shader "Custom/TMP_Hologram"
{
    Properties
    {
        // === TMP 必須プロパティ（Inspector では非表示）===
        [HideInInspector] _MainTex          ("Font Atlas",          2D)           = "white" {}
        [HideInInspector] _FaceDilate       ("Face Dilate",         Range(-1,1))  = 0
        [HideInInspector] _GradientScale    ("Gradient Scale",      Float)        = 5.0
        [HideInInspector] _ScaleRatioA      ("Scale Ratio A",       Float)        = 1.0
        [HideInInspector] _Sharpness        ("Sharpness",           Range(-1,1))  = 0
        [HideInInspector] _VertexOffsetX    ("Vertex OffsetX",      Float)        = 0
        [HideInInspector] _VertexOffsetY    ("Vertex OffsetY",      Float)        = 0
        [HideInInspector] _StencilComp      ("Stencil Comparison",  Float)        = 8
        [HideInInspector] _Stencil          ("Stencil ID",          Float)        = 0
        [HideInInspector] _StencilOp        ("Stencil Operation",   Float)        = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask",  Float)        = 255
        [HideInInspector] _StencilReadMask  ("Stencil Read Mask",   Float)        = 255
        [HideInInspector] _ColorMask        ("Color Mask",          Float)        = 15
        [HideInInspector] _ClipRect         ("Clip Rect",           Vector)       = (-32767,-32767,32767,32767)
        [HideInInspector] _MaskSoftnessX    ("Mask Softness X",     Float)        = 0
        [HideInInspector] _MaskSoftnessY    ("Mask Softness Y",     Float)        = 0

        // === ホログラム全般 ===
        [Header(Hologram)]
        _HologramAlpha      ("Hologram Alpha",       Range(0,1))   = 0.85
        _EmissionIntensity  ("Emission Intensity",   Range(0,5))   = 2.0

        // === スキャンライン ===
        [Header(Scanline)]
        _ScanlineSpeed      ("Scanline Speed",       Float)        = 1.5
        _ScanlineFrequency  ("Scanline Frequency",   Float)        = 30.0
        _ScanlineContrast   ("Scanline Contrast",    Range(0,1))   = 0.25

        // === リムグロー（文字の縁を光らせる）===
        [Header(Rim Glow)]
        _RimIntensity       ("Rim Intensity",        Range(0,5))   = 2.5
        _RimWidth           ("Rim Width",            Range(0,0.5)) = 0.15

        // === フリッカー ===
        [Header(Flicker)]
        _FlickerSpeed       ("Flicker Speed",        Float)        = 8.0
        _FlickerIntensity   ("Flicker Intensity",    Range(0,1))   = 0.08

        // === グリッチ ===
        [Header(Glitch)]
        _GlitchSpeed        ("Glitch Speed",         Float)        = 3.0
        _GlitchIntensity    ("Glitch Intensity",     Range(0,0.1)) = 0.02
        _GlitchProbability  ("Glitch Probability",   Range(0,1))   = 0.05
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
            "PreviewType"     = "Plane"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "TMP_HOLOGRAM"
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ----------------------------------------------------------------
            // 入出力
            // ----------------------------------------------------------------
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv0        : TEXCOORD0;  // フォントアトラス UV
                float2 uv1        : TEXCOORD1;  // TMP スケール（未使用だが渡す）
                float4 color      : COLOR;      // timerText.color が入る頂点カラー
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv0         : TEXCOORD0;
                float4 color       : COLOR;
                float2 worldXY     : TEXCOORD1;  // Rect Mask 2D 用ワールド XY
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ----------------------------------------------------------------
            // テクスチャ・定数バッファ
            // ----------------------------------------------------------------
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _ClipRect;
                float  _FaceDilate;
                float  _Sharpness;
                float  _VertexOffsetX;
                float  _VertexOffsetY;
                float  _MaskSoftnessX;
                float  _MaskSoftnessY;

                float  _HologramAlpha;
                float  _EmissionIntensity;

                float  _ScanlineSpeed;
                float  _ScanlineFrequency;
                float  _ScanlineContrast;

                float  _RimIntensity;
                float  _RimWidth;

                float  _FlickerSpeed;
                float  _FlickerIntensity;

                float  _GlitchSpeed;
                float  _GlitchIntensity;
                float  _GlitchProbability;
            CBUFFER_END

            // ----------------------------------------------------------------
            // ユーティリティ
            // ----------------------------------------------------------------
            float hash11(float x)
            {
                return frac(sin(x * 127.1) * 43758.5453);
            }
            float hash21(float2 p)
            {
                p  = frac(p * float2(443.9, 441.4));
                p += dot(p, p.yx + 19.19);
                return frac(p.x * p.y);
            }

            // ----------------------------------------------------------------
            // 頂点シェーダー
            // ----------------------------------------------------------------
            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float4 pos  = IN.positionOS;
                pos.x      += _VertexOffsetX;
                pos.y      += _VertexOffsetY;

                OUT.positionHCS = TransformObjectToHClip(pos.xyz);
                OUT.uv0         = IN.uv0;
                OUT.color       = IN.color;
                OUT.worldXY     = mul(unity_ObjectToWorld, pos).xy;
                return OUT;
            }

            // ----------------------------------------------------------------
            // フラグメントシェーダー
            // ----------------------------------------------------------------
            half4 Frag(Varyings IN) : SV_Target
            {
                // ============================================================
                // 1. SDF アルファ計算（ddx/ddy でフォントサイズに自動追従）
                // ============================================================
                float sdf = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv0).a;

                float edgeWidth = length(float2(ddx(sdf), ddy(sdf)));
                edgeWidth = clamp(edgeWidth, 0.001, 0.5);

                float faceEdge  = 0.5 - _FaceDilate * 0.5;
                float textAlpha = smoothstep(faceEdge - edgeWidth,
                                             faceEdge + edgeWidth, sdf);
                textAlpha *= IN.color.a;

                if (textAlpha < 0.001) discard;

                // ============================================================
                // 2. ベースカラー（timerText.color = TimerDisplay.cs が設定）
                // ============================================================
                float3 baseColor = IN.color.rgb;

                // ============================================================
                // 3. スキャンライン（UV Y 軸の水平ライン）
                // ============================================================
                float scanY    = IN.uv0.y * _ScanlineFrequency - _Time.y * _ScanlineSpeed;
                float scanWave = 0.5 + 0.5 * sin(scanY * 6.28318);
                float scanMult = 1.0 - _ScanlineContrast * (1.0 - scanWave);

                // ============================================================
                // 4. リムグロー（文字の縁 = SDF が faceEdge に近い領域）
                // ============================================================
                float rimDist = abs(sdf - faceEdge);
                float rimGlow = saturate(1.0 - rimDist / max(_RimWidth, 0.001));
                rimGlow = rimGlow * rimGlow * _RimIntensity;

                // ============================================================
                // 5. フリッカー（時間ベースのランダム輝度変動）
                // ============================================================
                float flickerT = floor(_Time.y * _FlickerSpeed);
                float flicker  = 1.0 - hash11(flickerT) * _FlickerIntensity;

                // ============================================================
                // 6. グリッチ（水平バンドが確率的に横ずれ）
                // ============================================================
                float glitchT    = floor(_Time.y * _GlitchSpeed);
                float glitchBand = floor(IN.uv0.y * 12.0);
                float glitchRand = hash21(float2(glitchBand, glitchT));
                // _GlitchProbability より大きい値のみグリッチ発生
                float glitchOn   = step(1.0 - _GlitchProbability, glitchRand);
                float glitchDelta = (hash21(float2(glitchT + 3.7, glitchBand)) - 0.5)
                                    * 2.0 * _GlitchIntensity * glitchOn;

                float sdfG = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                               float2(IN.uv0.x + glitchDelta, IN.uv0.y)).a;
                float glitchAlpha = smoothstep(faceEdge - edgeWidth,
                                               faceEdge + edgeWidth, sdfG)
                                  * abs(glitchDelta) * 30.0;

                // ============================================================
                // 7. 最終合成
                // ============================================================
                float3 finalColor  = baseColor * _EmissionIntensity * scanMult;
                finalColor        += baseColor * rimGlow;
                finalColor        += baseColor * glitchAlpha;
                finalColor        *= flicker;

                float finalAlpha   = textAlpha * _HologramAlpha * flicker;
                finalAlpha         = saturate(finalAlpha);

                // ============================================================
                // 8. UI クリッピング（Rect Mask 2D がある場合のみ有効）
                // ============================================================
                #ifdef UNITY_UI_CLIP_RECT
                    float2 softness = float2(_MaskSoftnessX, _MaskSoftnessY) * 2.0 + 0.001;
                    float2 m = saturate((_ClipRect.zw - _ClipRect.xy
                               - abs(IN.worldXY * 2.0 - (_ClipRect.xy + _ClipRect.zw)))
                               / softness);
                    finalAlpha *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                    clip(finalAlpha - 0.001);
                #endif

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }

    Fallback "TextMeshPro/Mobile/Distance Field"
}
