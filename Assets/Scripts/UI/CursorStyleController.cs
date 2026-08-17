using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// マウスカーソルの見た目を状況に応じて差し替える。
///   通常                        … 矢印
///   押せる UI ボタンの上        … パー（開いた手）
///   パーの状態から左クリック中  … グー（握った手）
///
/// カーソル画像は Inspector のスロットに差し込む。未設定のスロットは矢印へフォールバックし、
/// 矢印も未設定なら OS 標準カーソルのままになる（＝画像を入れるまで見た目は変わらない）。
///
/// 【重要】カーソル用テクスチャのインポート設定
///   画像を選択して Inspector で以下にすること。ここを外すと SetCursor が効かない。
///     Texture Type       = Cursor
///     Read/Write Enabled = ON
///     Compression        = None
///   元画像は大きめ（128px 等）でも構わない。表示サイズは sizeMode で調整する。
///
/// 【重要】大きさを変えたいときは cursorMode = ForceSoftware にすること
///   Auto はハードウェアカーソルで、OS がシステムのカーソルサイズ（Windows は通常 32px）に
///   合わせて描画するため、いくらテクスチャを拡大しても見た目の大きさが変わらない。
///

/// 【置き場所】
///   EventSystem と同じく常駐しているオブジェクトに 1 つだけ付ける（MainScene 推奨）。
///   シーンをまたいで使うなら persistAcrossScenes を ON にし、ルート階層のオブジェクトに付けること。
/// </summary>
[DisallowMultipleComponent]
public class CursorStyleController : MonoBehaviour
{
    public enum CursorStyle { Arrow, OpenHand, Fist }

    public enum SizeMode { Original, FixedPixels, Multiplier }

    [Header("カーソル画像")]
    [Tooltip("通常時の矢印カーソル。未設定なら OS 標準カーソルのままになる")]
    public Texture2D arrowTexture;

    [Tooltip("矢印のクリック位置 (px)。画像の左上が (0,0)。矢印は先端を指すので通常は (0,0)")]
    public Vector2 arrowHotspot = Vector2.zero;

    [Tooltip("押せるボタンの上で出す「パー」カーソル")]
    public Texture2D openHandTexture;

    [Tooltip("パーのクリック位置 (px)。手のひら中央あたりを指定すると自然")]
    public Vector2 openHandHotspot = new Vector2(16f, 16f);

    [Tooltip("クリック中に出す「グー」カーソル")]
    public Texture2D fistTexture;

    [Tooltip("グーのクリック位置 (px)。パーとずらすとクリック位置が動いて見えるので揃えるのが無難")]
    public Vector2 fistHotspot = new Vector2(16f, 16f);

    [Header("大きさ")]
    [Tooltip("大きさの決め方。\n" +
             "Original    … 元画像のまま\n" +
             "FixedPixels … 長辺を指定 px にそろえる (元画像の解像度に左右されないのでおすすめ)\n" +
             "Multiplier  … 元画像の何倍か で指定")]
    public SizeMode sizeMode = SizeMode.FixedPixels;

    [Tooltip("FixedPixels 用。カーソルの長辺の大きさ (px)。縦横比は保たれる。\n" +
             "一般的なカーソルは 32、大きめにしたいなら 48〜64 あたり")]
    [Range(8, 128)]
    public int cursorPixelSize = 32;

    [Tooltip("Multiplier 用。元画像の何倍にするか。1 = 元画像のまま")]
    [Range(0.05f, 4f)]
    public float cursorScale = 1f;

    [Tooltip("拡大縮小時になめらかに補間する。\nドット絵のカーソルをくっきり出したい場合は OFF (ニアレストネイバー)")]
    public bool smoothScaling = true;

    [Header("判定設定")]
    [Tooltip("Button や Toggle 以外に、クリックを受け取る UI (IPointerClickHandler) もパーの対象に含める")]
    public bool includeClickHandlers = false;

    [Tooltip("ボタンの上で押し始めたら、指を離すまでボタンの外へ動かしてもグーのままにする")]
    public bool keepFistUntilRelease = true;

    [Header("動作設定")]
    [Tooltip("ForceSoftware = Unity が自分で描画する。指定した大きさがそのまま反映される。\n" +
             "Auto = OS のハードウェアカーソル。動きは軽いが、OS がシステムのカーソルサイズ" +
             "(Windows は通常 32px) に合わせて描くため【大きさの設定が効かない】。\n" +
             "大きさを変えたいなら ForceSoftware にすること")]
    public CursorMode cursorMode = CursorMode.ForceSoftware;

    [Tooltip("シーン遷移後も残す。ON にする場合はルート階層のオブジェクトに付けること")]
    public bool persistAcrossScenes = false;

    [Header("デバッグ")]
    [Tooltip("起動時に設定内容を Console に出力する")]
    public bool logOnStart = true;

    private static CursorStyleController _instance;

    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(8);
    private PointerEventData _pointerData;

    private CursorStyle _currentStyle = CursorStyle.Arrow;
    private bool _styleApplied = false;
    private bool _pressStartedOnButton = false;
    private bool _wasCursorVisible = false;

    // 拡大率を適用した複製。等倍や拡大できない場合は null のままで、元画像をそのまま使う
    private Texture2D _scaledArrow;
    private Texture2D _scaledOpenHand;
    private Texture2D _scaledFist;

    // 作り直しが必要かの判定用に、前回生成したときの設定を覚えておく
    private SizeMode _builtSizeMode = (SizeMode)(-1);
    private int _builtPixelSize = -1;
    private float _builtScale = float.NaN;
    private bool _builtSmooth;
    private CursorMode _builtCursorMode = (CursorMode)(-1);
    private bool _warnedSizeIgnored = false;
    private Texture2D _builtArrowSource;
    private Texture2D _builtOpenHandSource;
    private Texture2D _builtFistSource;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            // タイトルから持ち越した常駐版と、遷移先シーンに置かれた分が重なるケース。
            // 先に居るものを優先し、後から来た方は黙って引き下がる
            Debug.Log($"[CursorStyleController] 既に '{DescribeLocation(_instance)}' が動作中のため、" +
                      $"'{DescribeLocation(this)}' は無効化しました。", this);
            enabled = false;
            return;
        }

        _instance = this;
        if (persistAcrossScenes) DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        DestroyScaledTextures();
        if (_instance == this) _instance = null;
    }

    private void Start()
    {
        ValidateTextures();
        RebuildScaledTextures();
        ApplyStyle(CursorStyle.Arrow, force: true);

        if (logOnStart)
        {
            Debug.Log($"[CursorStyleController] 初期化完了: " +
                      $"矢印={NameOf(arrowTexture)}, パー={NameOf(openHandTexture)}, グー={NameOf(fistTexture)}, " +
                      $"sizeMode={sizeMode}, 実サイズ={DescribeSize(arrowTexture, _scaledArrow)}, " +
                      $"smoothScaling={smoothScaling}, " +
                      $"cursorMode={cursorMode}, keepFistUntilRelease={keepFistUntilRelease}", this);
        }
    }

    private void Update()
    {
        // Inspector で拡大率や画像が変更されたら作り直す。Play 中の調整をその場で反映させるため、
        // カーソルが隠れている時でも先に処理しておく
        if (NeedsRebuild())
        {
            RebuildScaledTextures();
            ApplyStyle(_currentStyle, force: true);
        }

        // カーソルが隠れている間 (FPS 操作中など) は判定しない。
        // 再表示された時に正しい絵で出したいので、押下状態だけリセットしておく
        if (!Cursor.visible)
        {
            _pressStartedOnButton = false;
            _wasCursorVisible = false;
            return;
        }

        // 隠れていた状態から戻った直後は、他システムが書き換えている可能性があるので必ず貼り直す
        bool justBecameVisible = !_wasCursorVisible;
        _wasCursorVisible = true;

        Mouse mouse = Mouse.current;
        bool overButton = IsPointerOverClickableUI(mouse);
        bool pressed = mouse != null && mouse.leftButton.isPressed;

        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            _pressStartedOnButton = overButton;
        }
        if (!pressed)
        {
            _pressStartedOnButton = false;
        }

        // パーの上で押している間だけグーにする
        bool fistCondition = keepFistUntilRelease ? _pressStartedOnButton : overButton;

        CursorStyle style;
        if (pressed && fistCondition) style = CursorStyle.Fist;
        else if (overButton) style = CursorStyle.OpenHand;
        else style = CursorStyle.Arrow;

        ApplyStyle(style, justBecameVisible);
    }

    /// <summary>
    /// マウス位置に「押せる」UI があるか。EventSystem のレイキャストで判定する
    /// </summary>
    private bool IsPointerOverClickableUI(Mouse mouse)
    {
        EventSystem es = EventSystem.current;
        if (es == null || mouse == null) return false;

        if (_pointerData == null) _pointerData = new PointerEventData(es);
        _pointerData.Reset();
        _pointerData.position = mouse.position.ReadValue();

        _raycastResults.Clear();
        es.RaycastAll(_pointerData, _raycastResults);

        for (int i = 0; i < _raycastResults.Count; i++)
        {
            GameObject go = _raycastResults[i].gameObject;
            if (go == null) continue;

            // Button / Toggle / Slider など。無効化中や CanvasGroup で操作不能なものは押せない扱い
            Selectable selectable = go.GetComponentInParent<Selectable>();
            if (selectable != null && selectable.isActiveAndEnabled && selectable.IsInteractable())
            {
                return true;
            }

            if (includeClickHandlers && ExecuteEvents.GetEventHandler<IPointerClickHandler>(go) != null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// カーソル画像を切り替える。同じ状態が続く間は SetCursor を呼ばない
    /// </summary>
    private void ApplyStyle(CursorStyle style, bool force = false)
    {
        if (!force && _styleApplied && _currentStyle == style) return;

        ResolveCursor(style, out Texture2D texture, out Vector2 hotspot);

        // texture が null の場合、Unity の仕様で OS 標準カーソルに戻る
        Cursor.SetCursor(texture, texture != null ? hotspot : Vector2.zero, cursorMode);

        _currentStyle = style;
        _styleApplied = true;
    }

    /// <summary>
    /// 状態に対応する画像とホットスポットを解決する。未設定のスロットは矢印へフォールバックする
    /// </summary>
    private void ResolveCursor(CursorStyle style, out Texture2D texture, out Vector2 hotspot)
    {
        switch (style)
        {
            case CursorStyle.OpenHand:
                if (openHandTexture != null)
                {
                    texture = _scaledOpenHand != null ? _scaledOpenHand : openHandTexture;
                    hotspot = ScaleHotspot(openHandHotspot, openHandTexture, texture);
                    return;
                }
                break;

            case CursorStyle.Fist:
                if (fistTexture != null)
                {
                    texture = _scaledFist != null ? _scaledFist : fistTexture;
                    hotspot = ScaleHotspot(fistHotspot, fistTexture, texture);
                    return;
                }
                break;
        }

        texture = _scaledArrow != null ? _scaledArrow : arrowTexture;
        hotspot = ScaleHotspot(arrowHotspot, arrowTexture, texture);
    }

    /// <summary>
    /// 拡大した分だけホットスポットもずらす。指定した「クリック位置」が画像上の同じ場所を指し続けるようにする
    /// </summary>
    private static Vector2 ScaleHotspot(Vector2 hotspot, Texture2D source, Texture2D used)
    {
        if (source == null || used == null || source == used) return hotspot;

        return new Vector2(hotspot.x * used.width / source.width,
                           hotspot.y * used.height / source.height);
    }

    /// <summary>
    /// Inspector 側で拡大率・補間方法・元画像のいずれかが変わったか
    /// </summary>
    private bool NeedsRebuild()
    {
        return _builtSizeMode != sizeMode
            || _builtPixelSize != cursorPixelSize
            || !Mathf.Approximately(_builtScale, cursorScale)
            || _builtSmooth != smoothScaling
            || _builtCursorMode != cursorMode
            || _builtArrowSource != arrowTexture
            || _builtOpenHandSource != openHandTexture
            || _builtFistSource != fistTexture;
    }

    private void RebuildScaledTextures()
    {
        DestroyScaledTextures();

        _scaledArrow = CreateScaled(arrowTexture, "矢印");
        _scaledOpenHand = CreateScaled(openHandTexture, "パー");
        _scaledFist = CreateScaled(fistTexture, "グー");

        _builtSizeMode = sizeMode;
        _builtPixelSize = cursorPixelSize;
        _builtScale = cursorScale;
        _builtSmooth = smoothScaling;
        _builtCursorMode = cursorMode;
        _builtArrowSource = arrowTexture;
        _builtOpenHandSource = openHandTexture;
        _builtFistSource = fistTexture;

        WarnIfSizeIgnored();
    }

    private void DestroyScaledTextures()
    {
        if (_scaledArrow != null) Destroy(_scaledArrow);
        if (_scaledOpenHand != null) Destroy(_scaledOpenHand);
        if (_scaledFist != null) Destroy(_scaledFist);

        _scaledArrow = null;
        _scaledOpenHand = null;
        _scaledFist = null;
    }

    /// <summary>
    /// 拡大率を適用した複製を作る。等倍・元画像なし・読み取り不可の場合は null を返し、呼び出し側は元画像を使う
    /// </summary>
    /// <summary>
    /// sizeMode に応じて生成後のサイズを決める。FixedPixels では長辺を合わせ、縦横比は保つ
    /// </summary>
    private void ResolveTargetSize(Texture2D source, out int width, out int height)
    {
        switch (sizeMode)
        {
            case SizeMode.FixedPixels:
                int longestSide = Mathf.Max(source.width, source.height);
                float ratio = longestSide > 0 ? (float)cursorPixelSize / longestSide : 1f;
                width = Mathf.Max(1, Mathf.RoundToInt(source.width * ratio));
                height = Mathf.Max(1, Mathf.RoundToInt(source.height * ratio));
                return;

            case SizeMode.Multiplier:
                width = Mathf.Max(1, Mathf.RoundToInt(source.width * cursorScale));
                height = Mathf.Max(1, Mathf.RoundToInt(source.height * cursorScale));
                return;

            default:
                width = source.width;
                height = source.height;
                return;
        }
    }

    private Texture2D CreateScaled(Texture2D source, string label)
    {
        if (source == null) return null;

        ResolveTargetSize(source, out int width, out int height);

        // 元画像と同じ大きさなら複製しない（無駄な劣化とメモリを避ける）
        if (width == source.width && height == source.height) return null;

        if (!source.isReadable)
        {
            Debug.LogWarning($"[CursorStyleController] {label}カーソル '{source.name}' は Read/Write が無効なため拡大縮小できません。" +
                             "インポート設定で Read/Write Enabled = ON にしてください。元の大きさのまま表示します。", this);
            return null;
        }

        var scaled = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = $"{source.name}_{width}x{height}",
            filterMode = smoothScaling ? FilterMode.Bilinear : FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            // シーンに保存させず、Play 終了時に確実に破棄させる
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f) / height;
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width;

                pixels[y * width + x] = smoothScaling
                    ? source.GetPixelBilinear(u, v)
                    : source.GetPixel(Mathf.Min(source.width - 1, (int)(u * source.width)),
                                      Mathf.Min(source.height - 1, (int)(v * source.height)));
            }
        }

        scaled.SetPixels(pixels);
        // 第 2 引数 false: SetCursor が CPU 側で読むので readable のまま残す
        scaled.Apply(false, false);

        return scaled;
    }

    /// <summary>
    /// 大きさを指定しているのに Auto (ハードウェアカーソル) のままだと OS 側のサイズで描かれてしまう。
    /// 「スライダーを動かしても変わらない」で悩まないよう、一度だけ知らせる
    /// </summary>
    private void WarnIfSizeIgnored()
    {
        bool sizeIgnored = cursorMode == CursorMode.Auto && sizeMode != SizeMode.Original;

        if (sizeIgnored && !_warnedSizeIgnored)
        {
            Debug.LogWarning("[CursorStyleController] cursorMode = Auto では OS がシステムのカーソルサイズで描画するため、" +
                             "大きさの設定が画面に反映されません。cursorMode を ForceSoftware にしてください。", this);
            _warnedSizeIgnored = true;
        }
        else if (!sizeIgnored)
        {
            _warnedSizeIgnored = false;
        }
    }

    private void ValidateTextures()
    {
        WarnIfUnsuitable(arrowTexture, "矢印");
        WarnIfUnsuitable(openHandTexture, "パー");
        WarnIfUnsuitable(fistTexture, "グー");
    }

    /// <summary>
    /// カーソルに使えないインポート設定を起動時に知らせる。
    /// 圧縮されたままだと SetCursor しても見た目が変わらず、原因に気付きにくいため
    /// </summary>
    private void WarnIfUnsuitable(Texture2D texture, string label)
    {
        if (texture == null) return;

        if (!texture.isReadable)
        {
            Debug.LogWarning($"[CursorStyleController] {label}カーソル '{texture.name}' は Read/Write が無効です。" +
                             "インポート設定で Texture Type = Cursor, Read/Write Enabled = ON にしてください。", this);
        }

        if (texture.format != TextureFormat.RGBA32 && texture.format != TextureFormat.ARGB32)
        {
            Debug.LogWarning($"[CursorStyleController] {label}カーソル '{texture.name}' のフォーマットが {texture.format} です。" +
                             "圧縮されているとカーソルに反映されないことがあります。Compression = None を推奨します。", this);
        }
    }

    /// <summary>
    /// 重複時にどちらが生き残ったか分かるよう、シーン名込みで表す。
    /// タイトルとゲーム側で同じ名前を付けていても区別できるようにする
    /// </summary>
    private static string DescribeLocation(CursorStyleController controller)
    {
        if (controller == null) return "(なし)";

        string sceneName = controller.gameObject.scene.name;
        return string.IsNullOrEmpty(sceneName)
            ? controller.name
            : $"{sceneName}/{controller.name}";
    }

    private static string NameOf(Texture2D texture)
    {
        return texture != null ? texture.name : "(未設定)";
    }

    /// <summary>実際に画面へ出るカーソルの大きさを、元画像からの変化がわかる形で文字列にする</summary>
    private static string DescribeSize(Texture2D source, Texture2D scaled)
    {
        if (source == null) return "(画像未設定)";
        if (scaled == null) return $"{source.width}x{source.height}(元のまま)";

        return $"{source.width}x{source.height} → {scaled.width}x{scaled.height}";
    }
}
