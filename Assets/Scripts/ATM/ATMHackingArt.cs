using System.IO;
using UnityEngine;

namespace App.ATM
{
    /// <summary>
    /// ハッキング画面で使う画像を実行時に生成するユーティリティ。
    ///
    /// 画像アセットを追加しなくてもそのまま動くようにするのが狙い。
    /// StreamingAssets/ATM/images/ に同名の PNG を置けばそちらを優先して読み込むので、
    /// 後から本番用のイラストへ差し替えられる（例: devil.png）。
    /// </summary>
    public static class ATMHackingArt
    {
        /// <summary>StreamingAssets/ATM/images。ATMScreenRenderer と同じ置き場所。</summary>
        private static string ImagesDirectory => Path.Combine(Application.streamingAssetsPath, "ATM", "images");

        /// <summary>単色スプライト。実際の色は Image.color 側で指定する。</summary>
        public static Sprite CreateWhite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return ToSprite(tex);
        }

        /// <summary>
        /// 斜めストライプ(ハザード柄)のタイル用テクスチャ。
        /// RawImage の uvRect で繰り返して使う前提のため、size が period の倍数になるようにして継ぎ目を消している。
        /// </summary>
        public static Texture2D CreateDiagonalStripes(Color stripe, Color background, int size = 64, int period = 32)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // (x + y) の周期で 45 度のストライプになる
                    int phase = (x + y) % period;
                    pixels[y * size + x] = phase < period / 2 ? (Color32)stripe : (Color32)background;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>ブラウン管風の走査線。縦方向に繰り返して重ねる。</summary>
        public static Texture2D CreateScanlines(Color line, int lineHeight = 2, int gapHeight = 2)
        {
            int height = Mathf.Max(2, lineHeight + gapHeight);
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point
            };

            for (int y = 0; y < height; y++)
            {
                tex.SetPixel(0, y, y < lineHeight ? line : Color.clear);
            }
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 悪魔の頭のシルエット。差し替え用の devil.png があればそちらを読む。
        /// 形は「角 + 頭(上は楕円・下は顎へ向かう三角) - 目 - 口」の組み合わせで作る。
        /// </summary>
        public static Sprite CreateDevilSilhouette(Color color, int size = 192)
        {
            Sprite replacement = TryLoadImage("devil.png");
            if (replacement != null) return replacement;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 1ピクセルあたり2x2で判定して輪郭のギザつきを抑える
                    int hit = 0;
                    for (int sy = 0; sy < 2; sy++)
                    {
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float u = ((x + 0.25f + sx * 0.5f) / size) * 2f - 1f;
                            float v = ((y + 0.25f + sy * 0.5f) / size) * 2f - 1f;
                            if (IsInsideDevil(u, v)) hit++;
                        }
                    }

                    var c = color;
                    c.a *= hit / 4f;
                    pixels[y * size + x] = c;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return ToSprite(tex);
        }

        /// <summary>StreamingAssets/ATM/images から差し替え画像を読む。無ければ null。</summary>
        public static Sprite TryLoadImage(string fileName)
        {
            try
            {
                string path = Path.Combine(ImagesDirectory, fileName);
                if (!File.Exists(path)) return null;

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(File.ReadAllBytes(path))) return null;
                return ToSprite(tex);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ATMHackingArt] 画像の読み込みに失敗しました ({fileName}): {e.Message}");
                return null;
            }
        }

        private static Sprite ToSprite(Texture2D tex)
        {
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        // --- 悪魔シルエットの形状判定 (u,v は -1..1、v は上が正) ---

        private static bool IsInsideDevil(float u, float v)
        {
            // 角は頭からはみ出す位置に生えるので、頭の内外判定より先に見る
            if (IsInsideHorn(Mathf.Abs(u), v)) return true;

            if (!IsInsideHead(u, v)) return false;

            // 目と口はくり抜く
            if (IsInsideEye(u, v)) return false;
            if (IsInsideGrin(u, v)) return false;
            return true;
        }

        /// <summary>頭。上半分は楕円、下半分は顎へ向かって細くなる三角形。</summary>
        private static bool IsInsideHead(float u, float v)
        {
            const float rx = 0.55f;
            const float ry = 0.62f;
            const float jawTop = -0.10f;

            if (v >= jawTop)
            {
                return (u * u) / (rx * rx) + (v * v) / (ry * ry) <= 1f;
            }

            // 楕円の切り口と幅を揃えて段差が出ないようにする
            float halfWidth = rx * Mathf.Sqrt(Mathf.Max(0f, 1f - (jawTop * jawTop) / (ry * ry)));
            float t = Mathf.InverseLerp(jawTop, -0.92f, v);
            return Mathf.Abs(u) <= halfWidth * (1f - t);
        }

        /// <summary>角。ベジェ曲線に沿って太さが先細りする帯として判定する。u は絶対値を渡す。</summary>
        private static bool IsInsideHorn(float u, float v)
        {
            // 曲線までの距離計算は重いので、角が存在しうる範囲の外は先に弾く
            if (u < 0.17f || u > 1.05f || v < 0.28f || v > 1.2f) return false;

            var p0 = new Vector2(0.33f, 0.44f);
            var p1 = new Vector2(0.88f, 0.66f);
            var p2 = new Vector2(0.62f, 1.04f);
            const float baseRadius = 0.14f;
            const float tipRadius = 0.015f;

            var point = new Vector2(u, v);
            Vector2 previous = p0;
            const int segments = 24;

            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector2 current = QuadraticBezier(p0, p1, p2, t);
                float radius = Mathf.Lerp(baseRadius, tipRadius, t - 0.5f / segments);
                if (DistanceToSegment(point, previous, current) <= radius) return true;
                previous = current;
            }
            return false;
        }

        /// <summary>目。外側が上がるように傾けた楕円を左右に置く。</summary>
        private static bool IsInsideEye(float u, float v)
        {
            const float angle = 18f * Mathf.Deg2Rad;

            // dx に絶対値を使うことで左右が鏡像になる。外側ほど上がる向きに傾けて吊り目にする
            float dx = Mathf.Abs(u) - 0.235f;
            float dy = v - 0.10f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            float rx = dx * cos + dy * sin;
            float ry = -dx * sin + dy * cos;

            return (rx * rx) / (0.155f * 0.155f) + (ry * ry) / (0.085f * 0.085f) <= 1f;
        }

        /// <summary>口。同じ半径の円を上下にずらした差分（三日月）でニヤリとした形にする。</summary>
        private static bool IsInsideGrin(float u, float v)
        {
            const float radius = 0.42f;
            bool inLower = u * u + (v + 0.06f) * (v + 0.06f) <= radius * radius;
            bool inUpper = u * u + (v - 0.06f) * (v - 0.06f) <= radius * radius;
            return inLower && !inUpper;
        }

        private static Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float inv = 1f - t;
            return inv * inv * p0 + 2f * inv * t * p1 + t * t * p2;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq <= Mathf.Epsilon) return Vector2.Distance(point, a);

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
            return Vector2.Distance(point, a + ab * t);
        }
    }
}
