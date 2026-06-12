// 「古びた・腐敗・廃墟」系のホラー画面ポストエフェクト (URP 17 / Render Graph)。
// HorrorDecayRendererFeature から Blitter.BlitTexture 経由で呼ばれるフルスクリーンシェーダ。
// カメラカラー (_BlitTexture) を 1 枚受け取り、以下を順に適用して出力する:
//   退色 → 腐食カラーグレード → コントラスト潰し → 周辺の黴(かび)染み →
//   端の色収差 → 微細な歪み(呼吸) → フィルムグレイン → ヴィネット → 照明のちらつき
// テクスチャ不要 (ノイズは手続き的に生成)。各効果は Material プロパティで増減・無効化できる。
Shader "Hidden/HorrorDecay"
{
    Properties
    {
        _Desaturation     ("Desaturation",        Range(0,1)) = 0.55
        _RotColor         ("Rot Tint Color",      Color)      = (0.32, 0.30, 0.18, 1)
        _RotStrength      ("Rot Tint Strength",   Range(0,1)) = 0.45
        _Contrast         ("Contrast",            Range(0.5,2)) = 1.18
        _Lift             ("Black Lift (汚れ)",   Range(0,0.2)) = 0.03

        _MoldColor        ("Mold Color",          Color)      = (0.05, 0.06, 0.03, 1)
        _MoldStrength     ("Mold Strength",       Range(0,1)) = 0.55
        _MoldScale        ("Mold Scale",          Range(1,12)) = 4.5
        _MoldCreep        ("Mold Edge Creep",     Range(0,1)) = 0.6

        _Vignette         ("Vignette Strength",   Range(0,2)) = 1.1
        _VignettePower    ("Vignette Power",      Range(0.5,6)) = 2.4

        _ChromaticAberr   ("Chromatic Aberration",Range(0,3)) = 0.7
        _WarpAmount       ("Warp (呼吸)",         Range(0,2)) = 0.35
        _GrainAmount      ("Film Grain",          Range(0,1)) = 0.18

        _FlickerAmount    ("Light Flicker",       Range(0,1)) = 0.12
        _FlickerSpeed     ("Flicker Speed",       Range(0,30)) = 9.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "HorrorDecay"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float  _Desaturation;
            float4 _RotColor;
            float  _RotStrength;
            float  _Contrast;
            float  _Lift;

            float4 _MoldColor;
            float  _MoldStrength;
            float  _MoldScale;
            float  _MoldCreep;

            float  _Vignette;
            float  _VignettePower;

            float  _ChromaticAberr;
            float  _WarpAmount;
            float  _GrainAmount;

            float  _FlickerAmount;
            float  _FlickerSpeed;

            // --- 手続き的ノイズ -----------------------------------------------
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    v += amp * valueNoise(p);
                    p *= 2.02;
                    amp *= 0.5;
                }
                return v;
            }

            float3 SampleSrc(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float  t  = _Time.y;

                float2 toCenter = uv - 0.5;
                float  dist     = length(toCenter);

                // --- 微細な歪み (壁が息づくような揺らぎ) --------------------------
                float2 warp = float2(sin(uv.y * 11.0 + t * 0.7),
                                     cos(uv.x * 9.0  + t * 0.5)) * _WarpAmount * 0.0035;
                float2 suv  = uv + warp;

                // --- 端ほど強い色収差 -------------------------------------------
                float2 caOff = toCenter * dist * _ChromaticAberr * 0.012;
                float3 col;
                col.r = SampleSrc(suv + caOff).r;
                col.g = SampleSrc(suv).g;
                col.b = SampleSrc(suv - caOff).b;

                // --- 退色 -------------------------------------------------------
                float lum = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(col, lum.xxx, _Desaturation);

                // --- 腐食カラーグレード (緑〜セピアに沈める) ----------------------
                float3 rot = lum * _RotColor.rgb * 3.0; // 輝度を腐食色で着色
                col = lerp(col, rot, _RotStrength);

                // --- コントラスト潰し + 黒の浮き (埃っぽさ) -----------------------
                col = (col - 0.5) * _Contrast + 0.5;
                col = col * (1.0 - _Lift) + _Lift;

                // --- 黴(かび)染み: 画面端から這い寄る ----------------------------
                float n     = fbm(uv * _MoldScale + t * 0.02);
                float edge  = lerp(1.0, smoothstep(0.15, 0.55, dist), _MoldCreep);
                float mold  = smoothstep(0.52, 0.92, n) * edge;
                col = lerp(col, _MoldColor.rgb, saturate(mold) * _MoldStrength);

                // --- フィルムグレイン (時間でちらつく粒子) ------------------------
                float grain = hash21(uv * _ScreenParams.xy + frac(t) * 91.7) - 0.5;
                col += grain * _GrainAmount;

                // --- ヴィネット (四隅が腐って沈む) -------------------------------
                float vig = pow(saturate(1.0 - dist * _Vignette), _VignettePower);
                col *= vig;

                // --- 照明のちらつき (壊れかけの蛍光灯) ---------------------------
                float fl = (valueNoise(float2(t * _FlickerSpeed, 0.0)) - 0.5);
                fl += 0.5 * (valueNoise(float2(t * _FlickerSpeed * 2.3, 7.0)) - 0.5);
                col *= 1.0 + fl * _FlickerAmount;

                return half4(max(col, 0.0), 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
