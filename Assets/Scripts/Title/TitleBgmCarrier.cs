using System.Collections;
using UnityEngine;

/// <summary>
/// タイトルBGMをシーン遷移をまたいで鳴らし続けるための入れ物。
///
/// タイトルシーンのBGMはそのシーンのオブジェクトで鳴っているため、
/// シーンが破棄されると同時に途切れてしまう。
/// ローディング中も鳴らし続けたいので、遷移を始める直前にここへ引き渡す。
///
/// ロード中は SceneTransitionManager が AudioListener.pause で全体をミュートするため、
/// ignoreListenerPause を立てて、この音だけは止まらないようにしている。
///
/// ロードが終わったらフェードアウトして自分ごと消える。
/// </summary>
[DisallowMultipleComponent]
public class TitleBgmCarrier : MonoBehaviour
{
    /// <summary>現在ローディングをまたいで鳴っているBGM。無ければ null。</summary>
    public static TitleBgmCarrier Current { get; private set; }

    private AudioSource _source;
    private float _fadeOutDuration = 1.5f;
    private Coroutine _fadeRoutine;

    /// <summary>
    /// 鳴っているBGMを引き継いで、シーン遷移で消えない入れ物へ移す。
    /// 再生位置ごと引き継ぐので、聴感上は途切れない。
    /// </summary>
    /// <param name="source">タイトルシーンで鳴っている AudioSource</param>
    /// <param name="fadeOutDuration">ロード完了時にフェードアウトする時間</param>
    public static TitleBgmCarrier TakeOver(AudioSource source, float fadeOutDuration)
    {
        if (source == null || source.clip == null || !source.isPlaying) return null;

        // 二重に鳴らないよう、前回の引き継ぎが残っていれば先に始末する
        StopCurrentImmediate();

        var go = new GameObject("__TitleBgm (loading)");
        DontDestroyOnLoad(go);

        var carrier = go.AddComponent<TitleBgmCarrier>();
        carrier.Adopt(source, fadeOutDuration);
        return carrier;
    }

    private void Adopt(AudioSource source, float fadeOutDuration)
    {
        Current = this;
        _fadeOutDuration = fadeOutDuration;

        _source = gameObject.AddComponent<AudioSource>();
        _source.clip = source.clip;
        _source.loop = source.loop;
        _source.volume = source.volume;
        _source.pitch = source.pitch;
        _source.spatialBlend = source.spatialBlend;
        _source.outputAudioMixerGroup = source.outputAudioMixerGroup;
        _source.playOnAwake = false;

        // ロード中の AudioListener.pause に巻き込まれないようにする
        _source.ignoreListenerPause = true;

        _source.Play();

        // 再生位置を合わせてから元を止める（clip 設定後でないと反映されない）
        _source.timeSamples = Mathf.Clamp(source.timeSamples, 0, Mathf.Max(0, source.clip.samples - 1));
        source.Stop();
    }

    /// <summary>フェードアウトして自身を破棄する。ロードが終わったタイミングで呼ぶ。</summary>
    public void FadeOutAndDestroy(float duration = -1f)
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeOutRoutine(duration < 0f ? _fadeOutDuration : duration));
    }

    /// <summary>引き継いだBGMをフェードアウトさせて止める。参照を持たない場所から呼ぶ用。</summary>
    public static void StopCurrent(float duration = -1f)
    {
        if (Current != null) Current.FadeOutAndDestroy(duration);
    }

    /// <summary>フェードなしで即座に止める。タイトルへ戻ってBGMを鳴らし直す時など。</summary>
    public static void StopCurrentImmediate()
    {
        if (Current == null) return;

        TitleBgmCarrier carrier = Current;
        Current = null;
        Destroy(carrier.gameObject);
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        if (_source != null && duration > 0f)
        {
            float startVolume = _source.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _source.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
        }

        if (Current == this) Current = null;
        Destroy(gameObject);
    }
}
