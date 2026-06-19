// リアルなマグマ（溶岩）マテリアル用 URP シェーダ。テクスチャ不要・手続き的。
// 暗い岩殻(crust)の隙間に、ゆっくり流れる溶岩の亀裂(glowing veins)を描く。
// 亀裂は深紅→橙→黄白へのグラデーションで HDR 発光し、Bloom で輝く。
// ドメインワーピングした fbm ノイズを _Time でスクロールさせて「流れる」質感を出す。
Shader "Custom/Magma"
{
    Properties
    {
        [Header(Colors)]
        _CrustColor ("Crust Color (岩殻)",        Color) = (0.03, 0.02, 0.015, 1)
        _LavaCool   ("Lava 1 (深紅)",             Color) = (0.7, 0.06, 0.0, 1)
        _LavaMid    ("Lava 2 (橙)",               Color) = (1.0, 0.35, 0.02, 1)
        _LavaHot    ("Lava 3 (黄白/最高温)",      Color) = (1.0, 0.9, 0.5, 1)

        [Header(Pattern)]
        _Scale       ("Noise Scale",              Float) = 3.0
        _Warp        ("Flow Warp (流れの歪み)",   Range(0,2)) = 1.0
        _FlowSpeed   ("Flow Speed",               Float) = 0.08
        _CrustAmount ("Crust Amount (殻の割合)",  Range(0,1)) = 0.55
        _CrackSharp  ("Crack Sharpness",          Range(0.5,6)) = 2.2

        [Header(Glow)]
        _CrustBright ("Crust Brightness",         Range(0,1)) = 0.18
        [HDR] _Emission ("Lava Emission (HDR)",   Float) = 5.0

        [Header(Detail)]
        _DetailScale ("Detail Scale",             Float) = 9.0
        _DetailAmount("Detail Amount",            Range(0,1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _CrustColor;
                float4 _LavaCool;
                float4 _LavaMid;
                float4 _LavaHot;
                float  _Scale;
                float  _Warp;
                float  _FlowSpeed;
                float  _CrustAmount;
                float  _CrackSharp;
                float  _CrustBright;
                float  _Emission;
                float  _DetailScale;
                float  _DetailAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            // --- 手続き的ノイズ ------------------------------------------------
            float2 hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453) * 2.0 - 1.0;
            }

            float gnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = dot(hash2(i + float2(0, 0)), f - float2(0, 0));
                float b = dot(hash2(i + float2(1, 0)), f - float2(1, 0));
                float c = dot(hash2(i + float2(0, 1)), f - float2(0, 1));
                float d = dot(hash2(i + float2(1, 1)), f - float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) * 0.5 + 0.5;
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                [unroll]
                for (int i = 0; i < 5; i++)
                {
                    v += amp * gnoise(p);
                    p *= 2.03;
                    amp *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y * _FlowSpeed;
                float2 uv = IN.uv * _Scale;

                // ドメインワーピングで「流れる」溶岩のうねりを作る
                float2 q = float2(fbm(uv + float2(0.0, t)),
                                  fbm(uv + float2(5.2, -t) + 1.3));
                float2 r = float2(fbm(uv + _Warp * q + float2(1.7, 9.2) + t * 0.5),
                                  fbm(uv + _Warp * q + float2(8.3, 2.8) - t * 0.5));
                float n = fbm(uv + _Warp * r);

                // 細部ノイズで岩肌のざらつきを足す
                n += (_DetailAmount * 0.35) * (fbm(uv * (_DetailScale / max(_Scale, 0.001))) - 0.5);
                n = saturate(n);

                // 殻(高n)↔溶岩(低n)。heat=1 が最も熱い亀裂
                float heat = 1.0 - smoothstep(_CrustAmount - 0.18, _CrustAmount + 0.18, n);
                heat = pow(saturate(heat), _CrackSharp);

                // 温度カラーランプ
                float3 col = _CrustColor.rgb;
                col = lerp(col, _LavaCool.rgb, smoothstep(0.02, 0.45, heat));
                col = lerp(col, _LavaMid.rgb,  smoothstep(0.45, 0.78, heat));
                col = lerp(col, _LavaHot.rgb,  smoothstep(0.78, 1.0,  heat));

                // 殻は暗く、溶岩は HDR で強く光らせる（Bloom が拾う）
                float glow = _CrustBright + heat * _Emission;
                float3 outRGB = col * glow;

                return half4(outRGB, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
