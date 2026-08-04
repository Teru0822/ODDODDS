Shader "Custom/PaperBurnTMP"
{
    // TMP Distance Field シェーダーの最小実装 + 紙と同じバーンマスクで clip。
    // RealisticPaperBurn.cs がランタイムでこのシェーダーに差し替え、
    // _BurnProgress と _PaperW2L を毎フレーム更新する。

    Properties
    {
        [HideInInspector] _MainTex    ("Font Atlas",   2D)         = "white" {}
        _FaceColor  ("Face Color",   Color)         = (1,1,1,1)
        _FaceDilate ("Face Dilate",  Range(-1,1))   = 0

        // ── Burn (RealisticPaperBurn.cs から設定) ──
        _BurnProgress ("Burn Progress", Float) = 0
        _MacroScale   ("Macro Scale",   Float) = 5
        _FineScale    ("Fine Scale",    Float) = 55
        // _PaperW2L は Properties ブロックに書けないので CBUFFER 直宣言
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }

        Lighting Off
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4    _FaceColor;
            float     _FaceDilate;
            float     _BurnProgress;
            float     _MacroScale;
            float     _FineScale;
            float4x4  _PaperW2L;   // 紙の worldToLocalMatrix（C# から SetMatrix）

            struct a2v
            {
                float4 pos : POSITION;
                float2 uv  : TEXCOORD0;
                fixed4 col : COLOR;
            };
            struct v2f
            {
                float4 pos  : SV_POSITION;
                float2 uv   : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                fixed4 col  : COLOR;
            };

            // ── Noise（PaperBurn.shader と同一実装）────────────────────
            float2 _BH2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }
            float _BH1(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float _Vor(float2 uv, float sc)
            {
                float2 st = uv * sc, i = floor(st), f = frac(st);
                float md = 8.0;
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 d = float2(x, y) + _BH2(i + float2(x, y)) - f;
                    md = min(md, dot(d, d));
                }
                return sqrt(md);
            }
            float _VN(float2 uv, float sc)
            {
                float2 st = uv * sc, i = floor(st), f = frac(st);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(_BH1(i),              _BH1(i + float2(1,0)), u.x),
                            lerp(_BH1(i + float2(0,1)), _BH1(i + float2(1,1)), u.x), u.y);
            }
            // ────────────────────────────────────────────────────────────

            v2f vert(a2v v)
            {
                v2f o;
                o.pos  = UnityObjectToClipPos(v.pos);
                o.uv   = v.uv;
                o.wpos = mul(unity_ObjectToWorld, v.pos).xyz;
                o.col  = v.col * _FaceColor;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // ── TMP SDF でテキスト形状を決定 ──────────────────────────
                float sdf   = tex2D(_MainTex, i.uv).a;
                float edge  = 0.5 - _FaceDilate * 0.25;
                float alpha = smoothstep(edge - 0.02, edge + 0.02, sdf);
                clip(alpha - 0.01);

                // ── 紙のオブジェクト空間でバーンマスク（シェーダーと完全同期）
                // worldToLocalMatrix は scale を含む逆行列なので
                // 変換後の xyz が -0.5..+0.5 の object space 座標になる
                float4 lp  = mul(_PaperW2L, float4(i.wpos, 1.0));
                float2 puv = float2(lp.x + 0.5, lp.z + 0.5); // -0.5..+0.5 → 0..1
                float  msk = _Vor(puv, _MacroScale) * 0.70
                           + _VN(puv,  _FineScale)  * 0.30
                           + 0.15;
                clip(msk - _BurnProgress);

                return fixed4(i.col.rgb, i.col.a * alpha);
            }
            ENDCG
        }
    }
}
