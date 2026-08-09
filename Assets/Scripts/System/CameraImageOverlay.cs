using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// カメラの映像の上に透過PNGを重ねて表示する。ストアページ用のスクリーンショットで、
/// ロゴ・枠・構図ガイドなどを合成した状態のまま撮影するためのコンポーネント。
///
/// 使い方: 撮影用カメラにこのコンポーネントを付け、Image に透過PNGを割り当てるだけ。
/// [ExecuteAlways] なので Play を押さなくても Game ビューに反映される。
///
/// 表示用のCanvasは実行時に自動生成され、HideFlags.DontSave を付けているため
/// シーンファイルには保存されない（＝撮影シーンのYAMLが汚れない）。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraImageOverlay : MonoBehaviour
{
    /// <summary>画像を画面にどう収めるか。</summary>
    public enum FitMode
    {
        [Tooltip("縦横比を保ったまま画面内に収める（余白が出る）")]
        Contain,

        [Tooltip("縦横比を保ったまま画面を覆う（はみ出しは切れる）")]
        Cover,

        [Tooltip("縦横比を無視して画面いっぱいに引き伸ばす")]
        Stretch,

        [Tooltip("画像本来のピクセルサイズで中央に置く")]
        Native,
    }

    [Header("重ねる画像")]
    [Tooltip("表示する透過PNG。アルファはそのまま反映されます")]
    [SerializeField] private Texture _image;

    [Tooltip("画面への収め方。まずここで大まかに決めてから Scale で微調整します")]
    [SerializeField] private FitMode _fit = FitMode.Contain;

    [Tooltip("Fit で決まったサイズに掛ける倍率。1で等倍、0.5で半分、2で倍。縦横比は保たれます")]
    [Min(0.01f)]
    [SerializeField] private float _scale = 1f;

    [Tooltip("縦横を個別に微調整したい時の倍率。通常は 1,1 のままで構いません")]
    [SerializeField] private Vector2 _scaleXY = Vector2.one;

    [Header("見え方")]
    [Tooltip("表示のオン/オフ。撮り比べる時にここで切り替えます")]
    [SerializeField] private bool _show = true;

    [Range(0f, 1f)]
    [Tooltip("不透明度。ガイドとして薄く重ねたい時に下げます")]
    [SerializeField] private float _opacity = 1f;

    [Tooltip("乗算する色。白のままなら元の色で表示されます")]
    [SerializeField] private Color _tint = Color.white;

    [Tooltip("中央からのズラし量(ピクセル)")]
    [SerializeField] private Vector2 _offset = Vector2.zero;

    [Tooltip("表示順。他のUIより手前に出したい場合は大きくします")]
    [SerializeField] private int _sortingOrder = 30000;

    private const string OverlayObjectName = "__CameraImageOverlay (auto)";

    private Camera _camera;
    private Canvas _canvas;
    private RectTransform _canvasRect;
    private RawImage _rawImage;
    private RectTransform _imageRect;

    private void OnEnable()
    {
        _camera = GetComponent<Camera>();
        Rebuild();
    }

    private void OnDisable()
    {
        DestroyOverlay();
    }

    private void OnDestroy()
    {
        DestroyOverlay();
    }

    private void Update()
    {
        Apply();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Inspector をいじった瞬間に Game ビューへ反映させる
        if (isActiveAndEnabled) UnityEditor.EditorApplication.delayCall += SafeApply;
    }

    private void SafeApply()
    {
        if (this == null) return;
        Apply();
    }
#endif

    /// <summary>表示用のCanvasを作り直す。</summary>
    private void Rebuild()
    {
        DestroyOverlay();
        if (_camera == null) _camera = GetComponent<Camera>();

        var go = new GameObject(OverlayObjectName, typeof(RectTransform), typeof(Canvas));
        // シーンに保存させない。撮影シーンのYAMLを汚さず、二重生成も起きない
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(transform, false);

        _canvas = go.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceCamera;
        _canvas.worldCamera = _camera;
        _canvas.sortingOrder = _sortingOrder;
        _canvasRect = (RectTransform)go.transform;

        var imageGo = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        imageGo.hideFlags = HideFlags.DontSave;
        imageGo.transform.SetParent(go.transform, false);

        _rawImage = imageGo.GetComponent<RawImage>();
        _rawImage.raycastTarget = false;
        _imageRect = (RectTransform)imageGo.transform;
        _imageRect.anchorMin = _imageRect.anchorMax = _imageRect.pivot = new Vector2(0.5f, 0.5f);

        Apply();
    }

    /// <summary>Inspector の設定を表示へ反映する。</summary>
    private void Apply()
    {
        if (_canvas == null || _rawImage == null)
        {
            if (isActiveAndEnabled) Rebuild();
            return;
        }

        bool visible = _show && _image != null;
        if (_rawImage.enabled != visible) _rawImage.enabled = visible;
        if (!visible) return;

        // Canvas がカメラの手前に来るようにする（近すぎるとクリップされる）
        _canvas.worldCamera = _camera;
        _canvas.sortingOrder = _sortingOrder;
        _canvas.planeDistance = Mathf.Max(_camera.nearClipPlane + 0.01f, 0.1f);

        _rawImage.texture = _image;
        Color c = _tint;
        c.a = _tint.a * _opacity;
        _rawImage.color = c;

        _imageRect.sizeDelta = CalculateSize();
        _imageRect.anchoredPosition = _offset;
    }

    /// <summary>フィット方法と倍率に応じた表示サイズを求める。</summary>
    private Vector2 CalculateSize()
    {
        Vector2 baseSize = CalculateFittedSize();

        // Fit で決めた基準サイズに、全体倍率と縦横個別倍率を掛ける
        return new Vector2(
            baseSize.x * _scale * _scaleXY.x,
            baseSize.y * _scale * _scaleXY.y);
    }

    /// <summary>倍率をかける前の、フィット方法だけで決まるサイズ。</summary>
    private Vector2 CalculateFittedSize()
    {
        Vector2 canvasSize = _canvasRect.rect.size;
        Vector2 imageSize = new Vector2(_image.width, _image.height);

        if (imageSize.x <= 0f || imageSize.y <= 0f) return canvasSize;

        switch (_fit)
        {
            case FitMode.Stretch:
                return canvasSize;

            case FitMode.Contain:
            {
                float scale = Mathf.Min(canvasSize.x / imageSize.x, canvasSize.y / imageSize.y);
                return imageSize * scale;
            }

            case FitMode.Cover:
            {
                float scale = Mathf.Max(canvasSize.x / imageSize.x, canvasSize.y / imageSize.y);
                return imageSize * scale;
            }

            default: // Native
                return imageSize;
        }
    }

    private void DestroyOverlay()
    {
        // 自動生成物が残らないよう、既存の同名オブジェクトもまとめて消す
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null || child.name != OverlayObjectName) continue;

            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        _canvas = null;
        _canvasRect = null;
        _rawImage = null;
        _imageRect = null;
    }
}
