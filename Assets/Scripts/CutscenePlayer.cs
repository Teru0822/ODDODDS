using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// AnimationClip を「逆再生 (last → first) → 順再生 (first → last)」の順で 1 回だけ再生する。
/// AnimationClip.SampleAnimation で時間を直接サンプリングするため Animator/AnimatorController 不要。
/// Y キー (デフォルト) でトリガーされ、外部スクリプトからは <see cref="PlayCutscene"/> でも呼べる。
/// </summary>
public class CutscenePlayer : MonoBehaviour
{
    [Header("再生対象")]
    [Tooltip("Blender で Bake されたシェイプキーアニメ等の AnimationClip")]
    [SerializeField] private AnimationClip clip;

    [Tooltip("clip を適用する GameObject (SkinnedMeshRenderer 等を持つルート)")]
    [SerializeField] private GameObject animationTarget;

    [Header("トリガー")]
    [Tooltip("このキーを押すと再生開始 (テスト用)")]
    [SerializeField] private KeyCode triggerKey = KeyCode.Y;

    [Header("再生速度")]
    [Tooltip("逆再生・順再生それぞれの再生速度倍率 (1 = clip.length 秒で 1 区間)")]
    [SerializeField, Min(0.01f)] private float playSpeed = 1f;

    [Tooltip("逆再生と順再生の間で挟むウェイト秒数")]
    [SerializeField, Min(0f)] private float pauseBetween = 0f;

    private bool _isPlaying = false;

    /// <summary>現在再生中かどうか (外部から再トリガー抑止に使える)</summary>
    public bool IsPlaying => _isPlaying;

    void Reset()
    {
        if (animationTarget == null) animationTarget = gameObject;
    }

    void Update()
    {
        if (_isPlaying) return;
        if (IsTriggerPressed()) PlayCutscene();
    }

    /// <summary>
    /// 外部から呼び出して再生開始。UFOキャッチャー/ミニゲーム/ピンボール終了時はここを叩く。
    /// 既に再生中なら無視。
    /// </summary>
    public void PlayCutscene()
    {
        if (_isPlaying) return;
        if (clip == null || animationTarget == null)
        {
            Debug.LogWarning("[CutscenePlayer] clip または animationTarget が未設定です。");
            return;
        }
        StartCoroutine(PlayReverseThenForward());
    }

    bool IsTriggerPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            Key k = KeyCodeToKey(triggerKey);
            if (k != Key.None && Keyboard.current[k].wasPressedThisFrame) return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(triggerKey)) return true;
#endif
        return false;
    }

#if ENABLE_INPUT_SYSTEM
    static Key KeyCodeToKey(KeyCode code)
    {
        if (code >= KeyCode.A && code <= KeyCode.Z) return Key.A + (int)(code - KeyCode.A);
        if (code >= KeyCode.Alpha0 && code <= KeyCode.Alpha9) return Key.Digit0 + (int)(code - KeyCode.Alpha0);
        switch (code)
        {
            case KeyCode.Space: return Key.Space;
            case KeyCode.Return: return Key.Enter;
            case KeyCode.Escape: return Key.Escape;
            case KeyCode.Tab: return Key.Tab;
            default: return Key.None;
        }
    }
#endif

    IEnumerator PlayReverseThenForward()
    {
        _isPlaying = true;
        float length = clip.length;

        // フェーズ 1: 逆再生 (last → first)
        float elapsed = 0f;
        while (elapsed < length)
        {
            float t = Mathf.Clamp(length - elapsed, 0f, length);
            clip.SampleAnimation(animationTarget, t);
            elapsed += Time.deltaTime * playSpeed;
            yield return null;
        }
        clip.SampleAnimation(animationTarget, 0f);

        if (pauseBetween > 0f) yield return new WaitForSeconds(pauseBetween);

        // フェーズ 2: 順再生 (first → last)
        elapsed = 0f;
        while (elapsed < length)
        {
            float t = Mathf.Clamp(elapsed, 0f, length);
            clip.SampleAnimation(animationTarget, t);
            elapsed += Time.deltaTime * playSpeed;
            yield return null;
        }
        clip.SampleAnimation(animationTarget, length);

        _isPlaying = false;
    }
}
