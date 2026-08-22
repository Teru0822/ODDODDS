using UnityEngine;
using UnityEngine.UI;

namespace OddOdds.UI.Settings
{
    /// <summary>
    /// 既存の Image を覆わずに矩形の枠線を足す。
    ///
    /// 「白い矩形の上に一回り小さい黒を重ねる」方式は、子が必ず親より手前に描かれる都合上、
    /// 既に塗りを持っている UI には使えない（塗りを隠してしまう）。
    /// そこで上下左右 4 本の細い Image を子として置き、外周だけを描く。
    /// 中央は空くので、親の塗りや Color Tint、スクリプトによる色変更をそのまま活かせる。
    ///
    /// 位置やサイズを手で調整したい場合は <see cref="_autoLayout"/> をオフにする。
    /// オフの間は色と表示/非表示だけを管理し、RectTransform には触らない。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class UIRectBorder : MonoBehaviour
    {
        private const string EdgePrefix = "__Border_";
        private static readonly string[] EdgeNames = { "T", "B", "L", "R" };

        [Tooltip("線の太さ（px）")]
        [SerializeField] private float _thickness = 2f;

        [Tooltip("線の色")]
        [SerializeField] private Color _color = Color.white;

        [Tooltip("枠を内側に描く。オフだと外側にはみ出して描く")]
        [SerializeField] private bool _inside = true;

        [Tooltip("描く辺")]
        [SerializeField] private bool _top = true, _bottom = true, _left = true, _right = true;

        [Header("手動調整")]
        [Tooltip("オフにすると各辺の位置・サイズを自動で決めなくなり、手で自由に動かせるようになる。\n" +
                 "オンのままだと保存時に自動配置で上書きされる")]
        [SerializeField] private bool _autoLayout = true;

        [Tooltip("自動配置のときの内側への余白（左, 右, 下, 上）。少しだけ縮めたい時に使う")]
        [SerializeField] private Vector4 _padding = Vector4.zero;

        [Tooltip("辺の長さの調整。上下の辺は x、左右の辺は y の分だけ縮む")]
        [SerializeField] private Vector2 _lengthTrim = Vector2.zero;

        [SerializeField, HideInInspector] private Image[] _edges = new Image[4];

        public float Thickness
        {
            get => _thickness;
            set { _thickness = value; Apply(); }
        }

        public Color Color
        {
            get => _color;
            set { _color = value; Apply(); }
        }

        /// <summary>オフにすると各辺を手で自由に配置できる。</summary>
        public bool AutoLayout
        {
            get => _autoLayout;
            set { _autoLayout = value; Apply(); }
        }

        private void OnEnable() => Rebuild();

        private void OnValidate()
        {
            // OnValidate 中の生成/破棄は Unity が警告を出すため、ここでは既存の辺の更新だけ行う
            Apply();
        }

        /// <summary>辺の Image を作り直す。既にあれば作らない。</summary>
        public void Rebuild()
        {
            if (_edges == null || _edges.Length != 4) _edges = new Image[4];

            for (int i = 0; i < 4; i++)
            {
                if (_edges[i] != null) continue;

                var existing = transform.Find(EdgePrefix + EdgeNames[i]);
                if (existing != null)
                {
                    var found = existing.GetComponent<Image>();
                    if (found != null)
                    {
                        _edges[i] = found;
                        continue;
                    }
                }

                var go = new GameObject(EdgePrefix + EdgeNames[i], typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var image = go.GetComponent<Image>();
                image.sprite = null;
                image.raycastTarget = false;
                _edges[i] = image;

                // 新規に作った辺だけは、手動モードでも一度は形を整えておく
                LayoutEdge(i);
            }

            Apply();
        }

        /// <summary>色と表示辺を反映する。自動配置がオンなら位置・サイズも整える。</summary>
        public void Apply()
        {
            if (_edges == null) return;

            bool[] enabledEdges = { _top, _bottom, _left, _right };
            for (int i = 0; i < _edges.Length && i < 4; i++)
            {
                var edge = _edges[i];
                if (edge == null) continue;

                edge.color = _color;
                edge.gameObject.SetActive(enabledEdges[i]);

                if (_autoLayout) LayoutEdge(i);
            }
        }

        /// <summary>1 辺分の RectTransform を自動配置する。</summary>
        private void LayoutEdge(int i)
        {
            var edge = _edges != null && i < _edges.Length ? _edges[i] : null;
            if (edge == null) return;

            var rt = edge.rectTransform;
            float outward = _inside ? 0f : -_thickness;

            // _padding = (左, 右, 下, 上)
            float padL = _padding.x, padR = _padding.y, padB = _padding.z, padT = _padding.w;

            switch (i)
            {
                case 0: // 上
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot     = new Vector2(0.5f, 1f);
                    rt.sizeDelta = new Vector2(-(padL + padR) - _lengthTrim.x, _thickness);
                    rt.anchoredPosition = new Vector2((padL - padR) * 0.5f, -outward - padT);
                    break;
                case 1: // 下
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(1f, 0f);
                    rt.pivot     = new Vector2(0.5f, 0f);
                    rt.sizeDelta = new Vector2(-(padL + padR) - _lengthTrim.x, _thickness);
                    rt.anchoredPosition = new Vector2((padL - padR) * 0.5f, outward + padB);
                    break;
                case 2: // 左
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot     = new Vector2(0f, 0.5f);
                    rt.sizeDelta = new Vector2(_thickness, -(padT + padB) - _lengthTrim.y);
                    rt.anchoredPosition = new Vector2(outward + padL, (padB - padT) * 0.5f);
                    break;
                default: // 右
                    rt.anchorMin = new Vector2(1f, 0f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot     = new Vector2(1f, 0.5f);
                    rt.sizeDelta = new Vector2(_thickness, -(padT + padB) - _lengthTrim.y);
                    rt.anchoredPosition = new Vector2(-outward - padR, (padB - padT) * 0.5f);
                    break;
            }
        }
    }
}
