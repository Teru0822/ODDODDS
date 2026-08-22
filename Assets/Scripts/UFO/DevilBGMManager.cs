using System.Collections;
using UnityEngine;

/// <summary>
/// Devilキャッチャー（実機）専用のBGM再生マネージャー。
/// プレイ中に初めてレバー/ボタンが操作された瞬間（UFOCameraController.OnControlInputUsed）にBGMを再生し、
/// プレイセッションが終了した瞬間（OnPlaySessionActiveChanged(false)）に停止する。
///
/// 残り時間が10秒を切ると（OnLowTimeThresholdChanged）、あらかじめ用意した「テンポだけ上げて
/// 書き出した別音源（fastBgmClip）」へ、再生位置を同期させながらクロスフェードで切り替える。
/// AudioSource.pitchで速度を上げるとピッチも一緒に上がってしまうため、ピッチはそのままに
/// テンポだけ上げたい場合はこの「別音源への切り替え」方式が必要になる。
///
/// 練習機（Practice_Cranegame）側のBGMもここで管理する（TutorialCraneControllerから呼ばれる）。
/// 実機用の通常/高速切り替えとは独立した専用AudioSourceで再生するため、互いに干渉しない。
/// </summary>
public class DevilBGMManager : MonoBehaviour
{
    public static DevilBGMManager Instance { get; private set; }

    [Header("BGM設定（実機用）")]
    [Tooltip("プレイ中に初めてレバー/ボタンを操作した瞬間に再生する通常速度のBGM")]
    [SerializeField] private AudioClip bgmClip;

    [Tooltip("残り時間10秒未満の間に再生する、テンポだけを上げて書き出した別音源。" +
             "同じ曲をDAW等でピッチを変えずにテンポだけ変更して書き出したものを想定。" +
             "未設定の場合、10秒未満になっても速度は変化しない")]
    [SerializeField] private AudioClip fastBgmClip;

    [Range(0f, 1f)]
    [Tooltip("BGMの音量")]
    [SerializeField] private float bgmVolume = 0.6f;

    [Tooltip("再生開始・終了時のフェードイン・フェードアウトの所要時間（秒）。0で即座に切り替わる")]
    [SerializeField] private float fadeDuration = 1.0f;

    [Tooltip("通常速度⇔高速版の切り替え（クロスフェード）にかける時間（秒）。0で即座に切り替わる")]
    [SerializeField] private float speedCrossfadeDuration = 0.5f;

    [Tooltip("通常速度BGM再生用のAudioSource。未設定なら自動でAddComponentする")]
    [SerializeField] private AudioSource normalSource;

    [Tooltip("高速版BGM再生用のAudioSource。未設定なら自動でAddComponentする")]
    [SerializeField] private AudioSource fastSource;

    [Header("BGM設定（練習機用）")]
    [Tooltip("レバー/ボタンを初めて操作した瞬間に再生を開始し、タイマー0またはチュートリアル終了時に止めるBGM")]
    [SerializeField] private AudioClip practiceBgmClip;

    [Tooltip("残り時間10秒未満の間に再生する、テンポだけを上げて書き出した別音源。" +
             "未設定の場合、10秒未満になっても速度は変化しない")]
    [SerializeField] private AudioClip practiceFastBgmClip;

    [Range(0f, 1f)]
    [Tooltip("練習機BGMの音量")]
    [SerializeField] private float practiceBgmVolume = 0.6f;

    [Tooltip("練習機BGM再生用のAudioSource。未設定なら自動でAddComponentする")]
    [SerializeField] private AudioSource practiceSource;

    [Tooltip("練習機・高速版BGM再生用のAudioSource。未設定なら自動でAddComponentする")]
    [SerializeField] private AudioSource practiceFastSource;

    private Coroutine _normalFadeCoroutine;
    private Coroutine _fastFadeCoroutine;
    private Coroutine _practiceFadeCoroutine;
    private Coroutine _practiceFastFadeCoroutine;
    private Coroutine _crossfadeCoroutine;
    private Coroutine _practiceCrossfadeCoroutine;
    private bool _isFastActive;
    private bool _isPracticeFastActive;

    private void Awake()
    {
        // 将来的に他インスタンスが増えても既存のInstanceを奪わない（他のDevil系シングルトンと同じガード方式）
        if (Instance == null)
        {
            Instance = this;
        }

        normalSource = EnsureSource(normalSource, "NormalBgmSource");
        fastSource = EnsureSource(fastSource, "FastBgmSource");
        practiceSource = EnsureSource(practiceSource, "PracticeBgmSource");
        practiceFastSource = EnsureSource(practiceFastSource, "PracticeFastBgmSource");

        // AudioClipは実際にPlay()される瞬間に初めてデコードされる（Load Typeが
        // Decompress On Load 等の場合）ため、何もしないと「レバーを動かした瞬間」や
        // 「10秒未満になった瞬間」に同期的なデコード負荷でフレームがカクつく。
        // シーン開始時にバックグラウンドで先読みしておくことでこれを防ぐ。
        if (bgmClip != null) bgmClip.LoadAudioData();
        if (fastBgmClip != null) fastBgmClip.LoadAudioData();
        if (practiceBgmClip != null) practiceBgmClip.LoadAudioData();
        if (practiceFastBgmClip != null) practiceFastBgmClip.LoadAudioData();
    }

    private AudioSource EnsureSource(AudioSource source, string fallbackName)
    {
        if (source != null) return ConfigureSource(source);

        // 通常/高速の2系統を同時に鳴らし分けるため、専用の子GameObjectとして生成する
        GameObject go = new GameObject(fallbackName);
        go.transform.SetParent(transform, false);
        return ConfigureSource(go.AddComponent<AudioSource>());
    }

    private AudioSource ConfigureSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.pitch = 1f;
        source.volume = 0f;
        return source;
    }

    private void OnEnable()
    {
        UFOCameraController.OnControlInputUsed += HandleControlInputUsed;
        UFOCameraController.OnPlaySessionActiveChanged += HandlePlaySessionActiveChanged;
        // OnLowTimeWarningChangedではなくOnLowTimeThresholdChangedを使う。前者はルーレット回転中・
        // アイテム獲得演出中はfalseになってしまう（警告音を鳴らさないための仕様）ため、そちらに
        // 連動させると「10秒未満でルーレット/ブラックダイヤを入れると通常テンポに戻ってしまう」バグになる。
        // BGMのテンポは演出の有無に関わらず、純粋に残り時間だけで判定したい
        UFOCameraController.OnLowTimeThresholdChanged += HandleLowTimeWarningChanged;
    }

    private void OnDisable()
    {
        UFOCameraController.OnControlInputUsed -= HandleControlInputUsed;
        UFOCameraController.OnPlaySessionActiveChanged -= HandlePlaySessionActiveChanged;
        UFOCameraController.OnLowTimeThresholdChanged -= HandleLowTimeWarningChanged;
    }

    private void HandleControlInputUsed()
    {
        if (bgmClip == null) return;

        _isFastActive = false;
        normalSource.clip = bgmClip;
        normalSource.time = 0f;
        normalSource.volume = 0f;
        normalSource.Play();
        fastSource.Stop();

        StartFade(normalSource, bgmVolume);
    }

    private void HandlePlaySessionActiveChanged(bool isActive)
    {
        if (isActive) return;

        // 次回セッションに備えて、通常/高速どちらもフェードアウトして止めておく
        StartFade(normalSource, 0f, stopOnComplete: true);
        StartFade(fastSource, 0f, stopOnComplete: true);
        if (_crossfadeCoroutine != null) StopCoroutine(_crossfadeCoroutine);
        _isFastActive = false;
    }

    private void HandleLowTimeWarningChanged(bool isLowTime)
    {
        if (fastBgmClip == null) return;
        if (isLowTime == _isFastActive) return;
        if (!normalSource.isPlaying && !fastSource.isPlaying) return; // BGM再生前は何もしない

        AudioSource from = _isFastActive ? fastSource : normalSource;
        AudioSource to = _isFastActive ? normalSource : fastSource;
        AudioClip toClip = _isFastActive ? bgmClip : fastBgmClip;

        // 曲の長さの比から、テンポがどれだけ変わっているかを実測する
        // （fastBgmClipが正確に1.5倍で書き出されているとは限らないため、決め打ちにしない）
        float speedRatio = toClip.length > 0f && from.clip != null
            ? from.clip.length / toClip.length
            : 1f;

        // 現在の再生位置を、切り替え先の音源での「同じ曲の場所」に変換して同期させる
        float convertedTime = _isFastActive ? from.time * speedRatio : from.time / speedRatio;
        to.clip = toClip;
        to.time = toClip.length > 0f ? Mathf.Repeat(convertedTime, toClip.length) : 0f;
        to.volume = 0f;
        to.Play();

        _isFastActive = !_isFastActive;

        if (_crossfadeCoroutine != null) StopCoroutine(_crossfadeCoroutine);
        _crossfadeCoroutine = StartCoroutine(CrossfadeRoutine(from, to, bgmVolume));
    }

    private IEnumerator CrossfadeRoutine(AudioSource from, AudioSource to, float targetVolume)
    {
        float startFromVolume = from.volume;
        float elapsed = 0f;

        if (speedCrossfadeDuration <= 0f)
        {
            from.volume = 0f;
            to.volume = targetVolume;
        }
        else
        {
            while (elapsed < speedCrossfadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / speedCrossfadeDuration;
                from.volume = Mathf.Lerp(startFromVolume, 0f, t);
                to.volume = Mathf.Lerp(0f, targetVolume, t);
                yield return null;
            }
            from.volume = 0f;
            to.volume = targetVolume;
        }

        from.Stop();
    }

    private void StartFade(AudioSource source, float targetVolume, bool stopOnComplete = false)
    {
        if (source == null) return;

        bool isNormal = source == normalSource;
        Coroutine existing = isNormal ? _normalFadeCoroutine : _fastFadeCoroutine;
        if (existing != null) StopCoroutine(existing);

        Coroutine started = StartCoroutine(FadeVolumeRoutine(source, targetVolume, stopOnComplete));
        if (isNormal) _normalFadeCoroutine = started;
        else _fastFadeCoroutine = started;
    }

    private IEnumerator FadeVolumeRoutine(AudioSource source, float targetVolume, bool stopOnComplete)
    {
        float startVolume = source.volume;
        float elapsed = 0f;

        if (fadeDuration <= 0f)
        {
            source.volume = targetVolume;
        }
        else
        {
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / fadeDuration);
                yield return null;
            }
            source.volume = targetVolume;
        }

        if (stopOnComplete)
        {
            source.Stop();
        }
    }

    // ============================================================
    // 練習機（Practice Machine）
    // ============================================================

    /// <summary>レバー/ボタンを初めて操作した瞬間に呼ぶ。既に再生中の場合は何もしない</summary>
    public void StartPracticeBgm()
    {
        if (practiceBgmClip == null) return;
        if (practiceSource.isPlaying && practiceSource.clip == practiceBgmClip) return;

        _isPracticeFastActive = false;
        practiceSource.clip = practiceBgmClip;
        practiceSource.time = 0f;
        practiceSource.volume = 0f;
        practiceSource.Play();
        practiceFastSource.Stop();

        StartPracticeFade(practiceSource, practiceBgmVolume);
    }

    /// <summary>タイマーが0になった瞬間、またはチュートリアル終了時に呼ぶ</summary>
    public void StopPracticeBgm()
    {
        StartPracticeFade(practiceSource, 0f, stopOnComplete: true);
        StartPracticeFade(practiceFastSource, 0f, stopOnComplete: true);
        if (_practiceCrossfadeCoroutine != null) StopCoroutine(_practiceCrossfadeCoroutine);
        _isPracticeFastActive = false;
    }

    /// <summary>
    /// 練習機側で、残り時間が10秒未満に切り替わった/戻った時に呼ぶ（TutorialCraneControllerから）。
    /// 実機のHandleLowTimeWarningChangedと同じ考え方で、練習機用の別音源へクロスフェードで切り替える。
    /// </summary>
    public void SetPracticeLowTime(bool isLowTime)
    {
        if (practiceFastBgmClip == null) return;
        if (isLowTime == _isPracticeFastActive) return;
        if (!practiceSource.isPlaying && !practiceFastSource.isPlaying) return; // BGM再生前は何もしない

        AudioSource from = _isPracticeFastActive ? practiceFastSource : practiceSource;
        AudioSource to = _isPracticeFastActive ? practiceSource : practiceFastSource;
        AudioClip toClip = _isPracticeFastActive ? practiceBgmClip : practiceFastBgmClip;

        float speedRatio = toClip.length > 0f && from.clip != null
            ? from.clip.length / toClip.length
            : 1f;

        float convertedTime = _isPracticeFastActive ? from.time * speedRatio : from.time / speedRatio;
        to.clip = toClip;
        to.time = toClip.length > 0f ? Mathf.Repeat(convertedTime, toClip.length) : 0f;
        to.volume = 0f;
        to.Play();

        _isPracticeFastActive = !_isPracticeFastActive;

        if (_practiceCrossfadeCoroutine != null) StopCoroutine(_practiceCrossfadeCoroutine);
        _practiceCrossfadeCoroutine = StartCoroutine(CrossfadeRoutine(from, to, practiceBgmVolume));
    }

    private void StartPracticeFade(AudioSource source, float targetVolume, bool stopOnComplete = false)
    {
        if (source == null) return;

        bool isNormal = source == practiceSource;
        Coroutine existing = isNormal ? _practiceFadeCoroutine : _practiceFastFadeCoroutine;
        if (existing != null) StopCoroutine(existing);

        Coroutine started = StartCoroutine(FadeVolumeRoutine(source, targetVolume, stopOnComplete));
        if (isNormal) _practiceFadeCoroutine = started;
        else _practiceFastFadeCoroutine = started;
    }
}
