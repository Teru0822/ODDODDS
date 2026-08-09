using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 起動直後に表示するスタジオロゴの演出。
///
/// 流れ:
///   1. 画面を単色で覆った状態から始まる（タイトルシーンは裏で動いているが見えない）
///   2. SEと同時にロゴがフェードイン
///   3. しばらく表示したあとロゴがフェードアウト
///   4. 覆っていた単色がゆっくり晴れて、タイトルシーンが現れる
///
/// タイトルシーン(3D_Title_Sample)の空オブジェクトに付けて使う。
/// 表示用のCanvasは実行時に自動生成し、HideFlags.DontSave を付けているため
/// シーンファイルには保存されない。
/// </summary>
[DisallowMultipleComponent]
public class StudioLogoIntro : MonoBehaviour
{
    [Header("ロゴ")]
    [Tooltip("表示するロゴ画像（透過PNG推奨）")]
    [SerializeField] private Texture _logo;

    [Range(0.05f, 1f)]
    [Tooltip("ロゴの横幅を画面幅の何割にするか")]
    [SerializeField] private float _logoWidthRatio = 0.45f;

    [Header("覆う色")]
    [Tooltip("演出中に画面を覆う色。通常は黒")]
    [SerializeField] private Color _backgroundColor = Color.black;

    [Header("効果音")]
    [Tooltip("ロゴのフェードイン開始と同時に鳴らすSE")]
    [SerializeField] private AudioClip _startupSound;

    [Range(0f, 1f)]
    [SerializeField] private float _volume = 1f;

    [Header("タイミング(秒)")]
    [Tooltip("起動してからロゴが出はじめるまでの間")]
    [SerializeField] private float _initialDelay = 0.4f;

    [Tooltip("ロゴがフェードインする時間")]
    [SerializeField] private float _logoFadeIn = 1.2f;

    [Tooltip("ロゴを表示したままにする時間")]
    [SerializeField] private float _logoHold = 1.5f;

    [Tooltip("ロゴがフェードアウトする時間")]
    [SerializeField] private float _logoFadeOut = 1.0f;

    [Tooltip("ロゴが消えてからタイトルが出はじめるまでの間")]
    [SerializeField] private float _gapBeforeTitle = 0.4f;

    [Tooltip("タイトルシーンがフェードインする時間。ゆっくり出したいので長めが既定")]
    [SerializeField] private float _titleFadeIn = 2.0f;

    [Header("演出後に有効化するオブジェクト")]
    [Tooltip("タイトルが現れてから動かしたいもの（BGMのAudioSourceなど）。演出中は非アクティブにしておく")]
    [SerializeField] private GameObject[] _activateAfterIntro;

    [Header("スキップ")]
    [Tooltip("クリックまたは何かキーを押すと演出を飛ばせるようにする")]
    [SerializeField] private bool _allowSkip = true;

    [Header("再生条件")]
    [Tooltip("アプリ起動につき1回だけ再生する。ESCでのタイトル再読み込み時に再生されるのを防ぐ")]
    [SerializeField] private bool _playOnlyOncePerLaunch = true;

    [Header("デバッグ")]
    [SerializeField] private bool _logEvents = false;

    private const string CanvasObjectName = "__StudioLogoIntro (auto)";

    // アプリ起動から一度でも再生したか。シーンを読み直しても false に戻らないよう static
    private static bool _hasPlayedThisLaunch;

    private Canvas _canvas;
    private RawImage _background;
    private RawImage _logoImage;
    private RectTransform _logoRect;
    private AudioSource _audioSource;
    private bool _skipRequested;

    private void Awake()
    {
        if (_playOnlyOncePerLaunch && _hasPlayedThisLaunch)
        {
            // 2回目以降は演出せず、伏せてあるオブジェクトだけ起こして終わる
            ActivateAfterIntroObjects();
            enabled = false;
            return;
        }

        _hasPlayedThisLaunch = true;
        BuildOverlay();
    }

    private void Start()
    {
        if (!enabled) return;
        StartCoroutine(PlayIntro());
    }

    private void Update()
    {
        if (!_allowSkip || _skipRequested) return;

        // このプロジェクトは新Input System専用設定のため、旧 Input クラスは使えない
        bool pressed = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                    || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

        if (pressed)
        {
            _skipRequested = true;
            if (_logEvents) Debug.Log("[StudioLogoIntro] 入力によりロゴ演出をスキップします", this);
        }
    }

    /// <summary>画面を覆うCanvasとロゴを組み立てる。</summary>
    private void BuildOverlay()
    {
        var canvasGo = new GameObject(CanvasObjectName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.hideFlags = HideFlags.DontSave;
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // モヤ(TransitionCanvas)よりさらに手前に出す
        _canvas.sortingOrder = 32000;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _background = CreateImage(canvasGo.transform, "Background");
        var bgRect = (RectTransform)_background.transform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        _background.color = _backgroundColor;

        _logoImage = CreateImage(canvasGo.transform, "Logo");
        _logoRect = (RectTransform)_logoImage.transform;
        _logoRect.anchorMin = _logoRect.anchorMax = _logoRect.pivot = new Vector2(0.5f, 0.5f);
        _logoImage.texture = _logo;
        _logoImage.color = new Color(1f, 1f, 1f, 0f);
        _logoImage.enabled = _logo != null;

        LayoutLogo();

        _audioSource = gameObject.GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;
    }

    private static RawImage CreateImage(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<RawImage>();
        image.raycastTarget = false;
        return image;
    }

    /// <summary>ロゴを画面幅比で配置する。縦横比は保つ。</summary>
    private void LayoutLogo()
    {
        if (_logo == null || _logoRect == null) return;

        // CanvasScaler の参照解像度基準で計算する
        float referenceWidth = 1920f;
        float width = referenceWidth * _logoWidthRatio;
        float aspect = _logo.height / (float)Mathf.Max(1, _logo.width);
        _logoRect.sizeDelta = new Vector2(width, width * aspect);
    }

    private IEnumerator PlayIntro()
    {
        if (_logEvents) Debug.Log("[StudioLogoIntro] ロゴ演出を開始します", this);

        // 演出中に動いてほしくないものは伏せておく
        DeactivateAfterIntroObjects();

        yield return WaitOrSkip(_initialDelay);

        // SEはロゴのフェードイン開始と同時に鳴らす
        if (_startupSound != null && _audioSource != null && !_skipRequested)
        {
            _audioSource.PlayOneShot(_startupSound, _volume);
        }

        yield return FadeLogo(0f, 1f, _logoFadeIn);
        yield return WaitOrSkip(_logoHold);
        yield return FadeLogo(1f, 0f, _logoFadeOut);

        yield return WaitOrSkip(_gapBeforeTitle);

        // 覆っていた色を晴らして、タイトルシーンを見せる
        yield return FadeBackground(_backgroundColor.a, 0f, _titleFadeIn);

        ActivateAfterIntroObjects();
        DestroyOverlay();

        if (_logEvents) Debug.Log("[StudioLogoIntro] ロゴ演出を終了しました", this);
    }

    private IEnumerator FadeLogo(float from, float to, float duration)
    {
        if (_logoImage == null || _logo == null) yield break;

        // スキップされたら即座に終端の状態にする
        if (_skipRequested || duration <= 0f)
        {
            SetLogoAlpha(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (_skipRequested) break;
            elapsed += Time.unscaledDeltaTime;
            SetLogoAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        SetLogoAlpha(to);
    }

    private IEnumerator FadeBackground(float from, float to, float duration)
    {
        if (_background == null) yield break;

        // ここはスキップされても、いきなり切り替わらないよう短縮だけする
        float actual = _skipRequested ? Mathf.Min(duration, 0.35f) : duration;
        if (actual <= 0f)
        {
            SetBackgroundAlpha(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < actual)
        {
            elapsed += Time.unscaledDeltaTime;
            SetBackgroundAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / actual)));
            yield return null;
        }
        SetBackgroundAlpha(to);
    }

    private IEnumerator WaitOrSkip(float seconds)
    {
        if (seconds <= 0f) yield break;

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (_skipRequested) yield break;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void SetLogoAlpha(float a)
    {
        if (_logoImage == null) return;
        Color c = _logoImage.color;
        c.a = a;
        _logoImage.color = c;
    }

    private void SetBackgroundAlpha(float a)
    {
        if (_background == null) return;
        Color c = _backgroundColor;
        c.a = a;
        _background.color = c;
    }

    private void DeactivateAfterIntroObjects()
    {
        if (_activateAfterIntro == null) return;
        foreach (var go in _activateAfterIntro)
        {
            if (go != null) go.SetActive(false);
        }
    }

    private void ActivateAfterIntroObjects()
    {
        if (_activateAfterIntro == null) return;
        foreach (var go in _activateAfterIntro)
        {
            if (go != null) go.SetActive(true);
        }
    }

    private void DestroyOverlay()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name == CanvasObjectName) Destroy(child.gameObject);
        }

        _canvas = null;
        _background = null;
        _logoImage = null;
        _logoRect = null;
    }
}
