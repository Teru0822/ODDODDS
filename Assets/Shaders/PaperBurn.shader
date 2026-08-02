Shader "Custom/PaperBurn"
{
    Properties
    {
        [Header(Paper Appearance)]
        _PaperColor  ("Paper Color (Light)",  Color) = (0.85, 0.78, 0.62, 1)
        _PaperColorB ("Paper Color (Dark)",   Color) = (0.65, 0.55, 0.38, 1)
        _MainTex     ("Paper Texture (optional)", 2D) = "white" {}
        _TexBlend    ("Texture Blend",  Range(0,1))  = 0

        [Header(Burn Effect)]
        _BurnProgress ("Burn Progress",  Range(0, 1.3)) = 0
        [HDR]
        _FireColor    ("Fire Color",    Color) = (3.0, 1.2, 0.1, 1)
        _FireWidth    ("Fire Width",   Range(0.005, 0.2)) = 0.05
        _CharColor    ("Char Color",    Color) = (0.05, 0.02, 0.01, 1)
        _CharWidth    ("Char Width",   Range(0.005, 0.2)) = 0.07

        [Header(Noise Settings)]
        _MacroScale  ("Macro Scale (spreading points)", Range(1,  20)) = 5
        _FineScale   ("Fine Scale  (edge detail)",      Range(10,200)) = 55

        [Header(Paper Warp)]
        _WarpAmount  ("Warp Amount", Range(0, 0.15)) = 0.04
        _WarpScale   ("Warp Scale",  Range(0.5, 5))  = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "AlphaTest"
        }

        Cull Off

        // ─── Forward Lit ────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _PaperColor, _PaperColorB;
                float  _TexBlend;
                float  _BurnProgress;
                half4  _FireColor;
                float  _FireWidth, _CharWidth;
                half4  _CharColor;
                float  _MacroScale, _FineScale;
                float  _WarpAmount, _WarpScale;
            CBUFFER_END

            // ── Noise ──────────────────────────────────────────────
            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }
            float Hash1(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // Voronoi (cellular) noise — each cell = one burn origin point
            float Voronoi(float2 uv, float scale)
            {
                float2 st = uv * scale;
                float2 i  = floor(st);
                float2 f  = frac(st);
                float  md = 8.0;
                UNITY_UNROLL
                for (int y = -1; y <= 1; y++)
                UNITY_UNROLL
                for (int x = -1; x <= 1; x++)
                {
                    float2 nb   = float2(x, y);
                    float2 diff = nb + Hash2(i + nb) - f;
                    md = min(md, dot(diff, diff));
                }
                return sqrt(md);
            }

            // Value noise — irregular jagged paper edge
            float VNoise(float2 uv, float scale)
            {
                float2 st = uv * scale;
                float2 i  = floor(st);
                float2 f  = frac(st);
                float2 u  = f * f * (3.0 - 2.0 * f);
                float a = Hash1(i);
                float b = Hash1(i + float2(1, 0));
                float c = Hash1(i + float2(0, 1));
                float d = Hash1(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }
            // ───────────────────────────────────────────────────────

            struct Attr {
                float4 posOS : POSITION;
                float3 nrmOS : NORMAL;
                float2 uv    : TEXCOORD0;
            };
            struct Vary {
                float4 posHCS : SV_POSITION;
                float3 nrmWS  : TEXCOORD0;
                float2 uv     : TEXCOORD1;
                float3 posWS  : TEXCOORD2;
                float  fog    : TEXCOORD3;
            };

            Vary Vert(Attr IN)
            {
                Vary O;
                // 2オクターブのノイズで有機的なぐにゃり感を作り、法線方向に頂点をずらす
                float warpN  = VNoise(IN.uv, _WarpScale * 2.5) * 0.55
                             + VNoise(IN.uv, _WarpScale)        * 0.45;
                float3 warpedPos = IN.posOS.xyz + IN.nrmOS * (warpN - 0.5) * _WarpAmount;

                VertexPositionInputs vpi = GetVertexPositionInputs(warpedPos);
                O.posHCS = vpi.positionCS;
                O.posWS  = vpi.positionWS;
                O.nrmWS  = TransformObjectToWorldNormal(IN.nrmOS);
                O.uv     = TRANSFORM_TEX(IN.uv, _MainTex);
                O.fog    = ComputeFogFactor(O.posHCS.z);
                return O;
            }

            half4 Frag(Vary IN) : SV_Target
            {
                float2 uv = IN.uv;

                // Burn mask: Voronoi dominates so multiple cell-centers ignite simultaneously
                // +0.15 offset ensures paper is fully intact at BurnProgress=0
                float mask = Voronoi(uv, _MacroScale) * 0.70
                           + VNoise(uv,  _FineScale)  * 0.30
                           + 0.15;

                // Discard burned pixels
                clip(mask - _BurnProgress);

                // Soft gradient fire glow (brightest at burn edge, fades outward over 2.5× FireWidth)
                float fireT    = saturate((mask - _BurnProgress) / max(_FireWidth * 2.5, 0.001));
                float fireMask = pow(1.0 - fireT, 1.8) * step(_BurnProgress, mask);

                // Soft gradient char (darkest just past fire edge, fades to paper)
                float charT    = saturate((mask - _BurnProgress - _FireWidth) / max(_CharWidth, 0.001));
                float charMask = pow(1.0 - charT, 0.7) * step(_BurnProgress + _FireWidth, mask);

                // Paper texture: multi-scale FBM (fine/medium/coarse) + horizontal fiber lines
                float g1    = VNoise(uv, 110.0) * 0.45;
                float g2    = VNoise(uv,  38.0) * 0.30;
                float g3    = VNoise(uv,  14.0) * 0.18;
                // Fiber: UV の Y を 0.04 倍に圧縮 → セルが横長になり紙の繊維方向の縞模様が出る
                float fiber = VNoise(float2(uv.x, uv.y * 0.04), 80.0) * 0.10;
                float grain = (g1 + g2 + g3 + fiber) * 0.50;
                half3 paperCol = lerp(_PaperColor.rgb, _PaperColorB.rgb, grain);
                half3 texCol   = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                half3 paper    = lerp(paperCol, texCol, _TexBlend);

                // Apply char scorching
                half3 base = lerp(paper, _CharColor.rgb, charMask);

                // Lambert + ambient
                // SampleSH はライトプローブがない場合 (0,0,0) を返すため
                // ライトマップを持たない動的オブジェクト向けに最低輝度フロアを設ける
                float3 nrmWS    = normalize(IN.nrmWS);
                float4 shadowUV = TransformWorldToShadowCoord(IN.posWS);
                Light  mainLit  = GetMainLight(shadowUV);
                float  NdotL    = saturate(dot(nrmWS, mainLit.direction));
                half3  ambient  = max(SampleSH(nrmWS), half3(0.35, 0.32, 0.28));
                half3  lighting = mainLit.color * NdotL + ambient;

                // Fire emission uses HDR color (high RGB → Bloom glows)
                half3 col = base * lighting + _FireColor.rgb * fireMask;
                col = MixFog(col, IN.fog);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // ─── Shadow Caster ──────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest  LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex   ShadVert
            #pragma fragment ShadFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // URP 17 (Unity 6) does not declare these in Shadows.hlsl — must be explicit
            float3 _LightDirection;
            float3 _LightPosition;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _PaperColor, _PaperColorB;
                float  _TexBlend;
                float  _BurnProgress;
                half4  _FireColor;
                float  _FireWidth, _CharWidth;
                half4  _CharColor;
                float  _MacroScale, _FineScale;
                float  _WarpAmount, _WarpScale;
            CBUFFER_END

            float2 SH2(float2 p){ p=float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3))); return frac(sin(p)*43758.5453); }
            float  SH1(float2 p){ return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }

            float SVor(float2 uv, float sc)
            {
                float2 st=uv*sc, i=floor(st), f=frac(st); float md=8.0;
                UNITY_UNROLL for(int y=-1;y<=1;y++)
                UNITY_UNROLL for(int x=-1;x<=1;x++){
                    float2 nb=float2(x,y), d=nb+SH2(i+nb)-f;
                    md=min(md,dot(d,d));
                }
                return sqrt(md);
            }
            float SVN(float2 uv, float sc)
            {
                float2 st=uv*sc, i=floor(st), f=frac(st); float2 u=f*f*(3.0-2.0*f);
                return lerp(lerp(SH1(i),SH1(i+float2(1,0)),u.x),
                            lerp(SH1(i+float2(0,1)),SH1(i+float2(1,1)),u.x),u.y);
            }

            struct SA { float4 pos:POSITION; float3 nrm:NORMAL; float2 uv:TEXCOORD0; };
            struct SV { float4 hcs:SV_POSITION; float2 uv:TEXCOORD0; };

            SV ShadVert(SA IN)
            {
                SV O;
                float warpN  = SVN(IN.uv, _WarpScale * 2.5) * 0.55
                             + SVN(IN.uv, _WarpScale)        * 0.45;
                float3 warpedOS = IN.pos.xyz + IN.nrm * (warpN - 0.5) * _WarpAmount;
                float3 posWS = TransformObjectToWorld(warpedOS);
                float3 nrmWS = TransformObjectToWorldNormal(IN.nrm);
                #ifdef _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 ld = normalize(_LightPosition - posWS);
                #else
                    float3 ld = _LightDirection;
                #endif
                float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, nrmWS, ld));
                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE * posCS.w);
                #else
                    posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE * posCS.w);
                #endif
                O.hcs = posCS;
                O.uv  = IN.uv;
                return O;
            }

            half ShadFrag(SV IN) : SV_Target
            {
                float mask = SVor(IN.uv, _MacroScale) * 0.70
                           + SVN(IN.uv,  _FineScale)  * 0.30
                           + 0.15;
                clip(mask - _BurnProgress);
                return 0;
            }
            ENDHLSL
        }
    }
}
