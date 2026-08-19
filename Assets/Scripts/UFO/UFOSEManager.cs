using System.Collections;
using UnityEngine;

/// <summary>
/// UFOキャッチャー（実機・練習機）シーンで使われるSE(効果音)の再生窓口を1箇所にまとめたマネージャー。
///
/// 以前は UFOArmController / UFOItemGoal / UFOCameraController / TutorialCraneController /
/// TutorialItemGoal / TouchPanelOutlineController / TutorialStepController / TelevisionStaticController
/// など多数のスクリプトがそれぞれ個別に AudioSource を持ち、同じ「GetComponent→AddComponent」の
/// ボイラープレートを重複させていた。これらのクリップと再生処理をここへ集約することで、
/// 今後SEの音量調整・ON/OFF設定などを行う際に1箇所で完結するようにする。
///
/// シーンに1つだけ配置する（実機・練習機どちらの音もここに集約するため、Practice_Cranegame側には置かない）。
/// 各スクリプトからは UFOSEManager.Instance 経由で呼び出す。
///
/// なお、ボタン個別のクリック音（ButtonController）やトリガーゾーン個別の効果音（TriggerSoundPlayer）は、
/// 「同じスクリプトの複数配置ごとに異なるクリップを鳴らし分ける」という設計を維持したいため、
/// クリップ自体はここへ移さず、再生窓口（PlayOneShot）だけをここに一本化している。
/// </summary>
public class UFOSEManager : MonoBehaviour
{
    public static UFOSEManager Instance { get; private set; }

    // ============================================================
    // 実機 (Real Machine)
    // ============================================================
    [Header("=== 実機：コイン投入・アイテム獲得 ===")]
    [SerializeField] private AudioClip realCoinInsertSound;
    [SerializeField, Range(0f, 10f)] private float realCoinInsertVolume = 1f;
    [SerializeField] private AudioClip realCoinGetSound;
    [Tooltip("未設定の場合はコイン獲得音で代用します")]
    [SerializeField] private AudioClip realWatchGetSound;
    [Tooltip("未設定の場合はコイン獲得音で代用します")]
    [SerializeField] private AudioClip realBlackDiamondGetSound;
    [SerializeField, Range(0f, 10f)] private float realItemGetVolume = 1f;

    [Header("=== 実機：残り時間警告（10秒未満） ===")]
    [SerializeField] private AudioClip realLowTimeWarningLoopSound;
    [SerializeField, Range(0f, 10f)] private float realLowTimeWarningVolume = 1f;

    [Header("=== 実機：ライト演出 ===")]
    [SerializeField] private AudioClip realLightFlickerSound;
    [SerializeField, Range(0f, 10f)] private float realLightFlickerVolume = 0.5f;
    [SerializeField] private AudioClip realLightOffSound;
    [SerializeField, Range(0f, 10f)] private float realLightOffVolume = 0.5f;

    [Header("=== 実機：アーム ===")]
    [SerializeField] private AudioClip armDescentRustleSound;
    [SerializeField] private AudioClip armMoveLoopSound;
    [SerializeField, Range(0f, 10f)] private float armMoveVolume = 1f;
    [SerializeField] private AudioClip armMoveLoopSound2;
    [SerializeField, Range(0f, 10f)] private float armMoveVolume2 = 1f;
    [SerializeField] private AudioClip armMoveStartSound;
    [SerializeField, Range(0f, 10f)] private float armMoveStartVolume = 1f;
    [SerializeField] private AudioClip armMoveStopSound;
    [SerializeField, Range(0f, 10f)] private float armMoveStopVolume = 1f;

    // ============================================================
    // 練習機 (Practice Machine)
    // ============================================================
    [Header("=== 練習機：コイン投入・アイテム獲得 ===")]
    [SerializeField] private AudioClip practiceCoinInsertSound;
    [SerializeField, Range(0f, 10f)] private float practiceCoinInsertVolume = 1f;
    [SerializeField] private AudioClip practiceCoinGetSound;
    [Tooltip("未設定の場合はコイン獲得音で代用します")]
    [SerializeField] private AudioClip practiceWatchGetSound;
    [Tooltip("未設定の場合はコイン獲得音で代用します")]
    [SerializeField] private AudioClip practiceBlackDiamondGetSound;
    [SerializeField, Range(0f, 10f)] private float practiceItemGetVolume = 1f;

    [Header("=== 練習機：残り時間警告（10秒未満） ===")]
    [SerializeField] private AudioClip practiceLowTimeWarningLoopSound;
    [SerializeField, Range(0f, 10f)] private float practiceLowTimeWarningVolume = 1f;

    [Header("=== 練習機：チュートリアルUI ===")]
    [SerializeField] private AudioClip tutorialFrameSnapSound;
    [SerializeField, Range(0f, 1f)] private float tutorialFrameSnapVolume = 1f;
    [SerializeField] private AudioClip tutorialMessageTypingSound;
    [SerializeField, Range(0f, 1f)] private float tutorialMessageTypingVolume = 1f;

    // ============================================================
    // 共通 (Shared)
    // ============================================================
    [Header("=== 共通：タッチパネル（実機/練習機どちらのパネルでも使用） ===")]
    [SerializeField] private AudioClip touchSelectSound;
    [SerializeField, Range(0f, 10f)] private float touchSelectVolume = 1f;
    [SerializeField] private AudioClip touchSelect2Sound;
    [SerializeField, Range(0f, 10f)] private float touchSelect2Volume = 1f;
    [SerializeField] private AudioClip touchLockedSound;
    [SerializeField, Range(0f, 10f)] private float touchLockedVolume = 1f;

    [Header("=== 共通：テレビ砂嵐 ===")]
    [Tooltip("カメラ切り替え時などに単発/ループで鳴らす砂嵐SE")]
    [SerializeField] private AudioClip televisionStaticSound;
    [SerializeField, Range(0f, 5f)] private float televisionStaticVolume = 1f;
    [Tooltip("Tutorial_Canvas表示中にループ再生する専用の砂嵐SE。未設定なら上のtelevisionStaticSoundを流用する")]
    [SerializeField] private AudioClip tutorialStaticLoopSound;
    [SerializeField, Range(0f, 5f)] private float tutorialStaticLoopVolume = 0.5f;

    // ============================================================
    // 内部再生チャンネル
    // ============================================================
    [Header("=== 内部設定 ===")]
    [Tooltip("重複再生される単発SE(PlayOneShot系)をまとめて鳴らす共有AudioSource。" +
             "PlayOneShotは呼び出し時点の音量・ピッチをそれぞれ独立して記録するため、" +
             "同時に複数の音が重なっても互いに影響しない。未設定なら自動でAddComponentする")]
    [SerializeField] private AudioSource oneShotSource;

    private AudioSource _warningLoopSource;
    // 実機・練習機は同時にプレイされないため警告音のAudioSourceは共有しているが、
    // 「今どちらが鳴らしているか」を覚えておかないと、待機側（プレイしていない方）が毎フレーム
    // 「自分は鳴らしていないはずだから止める」と誤って相手の警告音を止めてしまう。
    // このフラグでどちらが現在の再生元かを判定し、無関係な側からのStop要求を無視する。
    private bool? _warningLoopOwnerIsPractice;
    private AudioSource _armMoveLoopSource;
    private AudioSource _armMoveLoopSource2;
    private AudioSource _televisionStaticLoopSource;
    private AudioSource _tutorialStaticLoopSource;

    private void Awake()
    {
        // 実機・練習機のどちらの構成からも参照されうるため、既に別インスタンスが
        // Instanceを保持している場合は絶対に奪わない（他のUFO系シングルトンと同じガード方式）
        if (Instance == null)
        {
            Instance = this;
        }

        if (oneShotSource == null)
        {
            oneShotSource = GetComponent<AudioSource>();
            if (oneShotSource == null)
            {
                oneShotSource = gameObject.AddComponent<AudioSource>();
            }
        }
        oneShotSource.playOnAwake = false;
        oneShotSource.spatialBlend = 0f;
    }

    // ============================================================
    // 汎用再生API（TriggerSoundPlayer / ButtonController など、
    // クリップを自前で持つスクリプト用の共有再生窓口）
    // ============================================================

    /// <summary>任意のクリップを共有の単発再生チャンネルで鳴らす（多重再生対応）</summary>
    public void PlayOneShot(AudioClip clip, float volume = 1f)
    {
        if (clip == null || oneShotSource == null) return;
        oneShotSource.PlayOneShot(clip, volume);
    }

    /// <summary>ピッチを指定して単発再生する（タイプ音のランダムピッチ等）。
    /// PlayOneShotは呼び出し時点のAudioSource.pitchを個別に記録して再生するため、
    /// 共有ソースであっても他の再生中の音のピッチには影響しない</summary>
    public void PlayOneShotWithPitch(AudioClip clip, float volume, float pitch)
    {
        if (clip == null || oneShotSource == null) return;
        float originalPitch = oneShotSource.pitch;
        oneShotSource.pitch = pitch;
        oneShotSource.PlayOneShot(clip, volume);
        oneShotSource.pitch = originalPitch;
    }

    /// <summary>クリップの末尾を指定秒数カットして再生する（照明の「カチッ」音等、余韻を残したくない場合用）。
    /// 専用の一時AudioSourceを生成し、指定時間後に自動的に停止・破棄する</summary>
    public void PlayWithTailCut(AudioClip clip, float volume, float cutDuration)
    {
        if (clip == null) return;

        GameObject tempAudioObj = new GameObject("TempTailCutAudioSource");
        tempAudioObj.transform.SetParent(transform, false);
        AudioSource tempSource = tempAudioObj.AddComponent<AudioSource>();
        tempSource.clip = clip;
        tempSource.volume = volume;
        tempSource.spatialBlend = 0f;
        tempSource.playOnAwake = false;
        tempSource.loop = false;
        tempSource.Play();

        float playDuration = Mathf.Max(0f, clip.length - cutDuration);
        StartCoroutine(StopAndDestroyAudioCoroutine(tempAudioObj, tempSource, playDuration));
    }

    private IEnumerator StopAndDestroyAudioCoroutine(GameObject audioObj, AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (source != null) source.Stop();
        if (audioObj != null) Destroy(audioObj);
    }

    private AudioSource EnsureLoopSource(ref AudioSource slot, string objectName)
    {
        if (slot == null)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            slot = go.AddComponent<AudioSource>();
            slot.playOnAwake = false;
            slot.spatialBlend = 0f;
        }
        return slot;
    }

    // ============================================================
    // 実機：コイン投入・アイテム獲得
    // ============================================================
    public void PlayRealCoinInsert() => PlayOneShot(realCoinInsertSound, realCoinInsertVolume);

    /// <summary>実機/練習機のどちらのコイン投入音を鳴らすかをまとめて切り替えるための窓口。
    /// TriggerSoundPlayer（コイン投入口）のisPracticeInstanceフラグから呼ばれる</summary>
    public void PlayCoinInsert(bool isPractice)
    {
        if (isPractice) PlayPracticeCoinInsert();
        else PlayRealCoinInsert();
    }
    public void PlayRealCoinGet() => PlayOneShot(realCoinGetSound, realItemGetVolume);
    public void PlayRealWatchGet() => PlayOneShot(realWatchGetSound != null ? realWatchGetSound : realCoinGetSound, realItemGetVolume);
    public void PlayRealBlackDiamondGet() => PlayOneShot(realBlackDiamondGetSound != null ? realBlackDiamondGetSound : realCoinGetSound, realItemGetVolume);

    // ============================================================
    // 実機/練習機 共通：残り時間警告（実機・練習機は同時に鳴らないため1チャンネルを共有する）
    // ============================================================

    /// <summary>isPracticeで指定した側の警告音が、今まさに鳴っているか。
    /// 待機している側（プレイしていない方）が誤って相手の警告音を止めてしまわないよう、
    /// 単に「何か鳴っているか」ではなく「自分（isPractice）が鳴らしたものが鳴っているか」を判定する</summary>
    public bool IsLowTimeWarningActive(bool isPractice) =>
        _warningLoopSource != null && _warningLoopSource.isPlaying && _warningLoopOwnerIsPractice == isPractice;

    public void StartLowTimeWarning(bool isPractice)
    {
        AudioClip clip = isPractice ? practiceLowTimeWarningLoopSound : realLowTimeWarningLoopSound;
        float volume = isPractice ? practiceLowTimeWarningVolume : realLowTimeWarningVolume;
        if (clip == null) return;

        EnsureLoopSource(ref _warningLoopSource, "WarningLoopAudioSource");
        _warningLoopSource.clip = clip;
        _warningLoopSource.loop = true;
        _warningLoopSource.volume = volume;
        _warningLoopSource.Play();
        _warningLoopOwnerIsPractice = isPractice;
    }

    /// <summary>isPractice側からの停止要求。今鳴っているのが自分（isPractice）の警告音でない場合は
    /// 何もしない（実機・練習機どちらか一方が待機中でも、相手の警告音を誤って止めないようにするため）</summary>
    public void StopLowTimeWarning(bool isPractice)
    {
        if (_warningLoopSource == null) return;
        if (_warningLoopOwnerIsPractice != isPractice) return;

        _warningLoopSource.Stop();
        _warningLoopSource.clip = null;
        _warningLoopSource.loop = false;
        _warningLoopOwnerIsPractice = null;
    }

    // ============================================================
    // 実機：ライト演出
    // ============================================================
    public void PlayRealLightFlicker() => PlayWithTailCut(realLightFlickerSound, realLightFlickerVolume, 0.4f);
    public void PlayRealLightOff() => PlayWithTailCut(realLightOffSound, realLightOffVolume, 0f);

    // ============================================================
    // 実機：アーム
    // ============================================================

    /// <summary>アームがコインの山に衝突した際のがさがさ音を再生する。
    /// pitch・重ね再生回数(repeatCount)は呼び出し側（UFOArmController）が
    /// 衝突検知したコイン枚数から算出した値をそのまま渡す</summary>
    public void PlayArmDescentRustle(float volume, float pitch, int repeatCount)
    {
        if (armDescentRustleSound == null) return;
        for (int i = 0; i < Mathf.Max(1, repeatCount); i++)
        {
            PlayOneShotWithPitch(armDescentRustleSound, volume, pitch);
        }
    }

    public void StartArmMoveLoop()
    {
        if (armMoveLoopSound != null)
        {
            EnsureLoopSource(ref _armMoveLoopSource, "ArmMoveLoopAudioSource");
            _armMoveLoopSource.clip = armMoveLoopSound;
            _armMoveLoopSource.loop = true;
            _armMoveLoopSource.volume = armMoveVolume;
            _armMoveLoopSource.Play();
        }

        if (armMoveLoopSound2 != null)
        {
            EnsureLoopSource(ref _armMoveLoopSource2, "ArmMoveLoopAudioSource2");
            _armMoveLoopSource2.clip = armMoveLoopSound2;
            _armMoveLoopSource2.loop = true;
            _armMoveLoopSource2.volume = armMoveVolume2;
            _armMoveLoopSource2.Play();
        }
    }

    public void StopArmMoveLoop()
    {
        if (_armMoveLoopSource != null) _armMoveLoopSource.Stop();
        if (_armMoveLoopSource2 != null) _armMoveLoopSource2.Stop();
    }

    public void PlayArmMoveStart() => PlayOneShot(armMoveStartSound, armMoveStartVolume);
    public void PlayArmMoveStop() => PlayOneShot(armMoveStopSound, armMoveStopVolume);

    // ============================================================
    // 練習機：コイン投入・アイテム獲得
    // ============================================================
    public void PlayPracticeCoinInsert() => PlayOneShot(practiceCoinInsertSound, practiceCoinInsertVolume);
    public void PlayPracticeCoinGet() => PlayOneShot(practiceCoinGetSound, practiceItemGetVolume);
    public void PlayPracticeWatchGet() => PlayOneShot(practiceWatchGetSound != null ? practiceWatchGetSound : practiceCoinGetSound, practiceItemGetVolume);
    public void PlayPracticeBlackDiamondGet() => PlayOneShot(practiceBlackDiamondGetSound != null ? practiceBlackDiamondGetSound : practiceCoinGetSound, practiceItemGetVolume);

    // ============================================================
    // 練習機：チュートリアルUI
    // ============================================================
    public void PlayTutorialFrameSnap() => PlayOneShot(tutorialFrameSnapSound, tutorialFrameSnapVolume);
    public void PlayTutorialMessageTyping(float pitch) => PlayOneShotWithPitch(tutorialMessageTypingSound, tutorialMessageTypingVolume, pitch);

    // ============================================================
    // 共通：タッチパネル
    // ============================================================
    public void PlayTouchSelect() => PlayOneShot(touchSelectSound, touchSelectVolume);
    public void PlayTouchSelect2() => PlayOneShot(touchSelect2Sound, touchSelect2Volume);
    public void PlayTouchLocked() => PlayOneShot(touchLockedSound, touchLockedVolume);

    // ============================================================
    // 共通：テレビ砂嵐
    // ============================================================
    public void PlayTelevisionStaticOneShot() => PlayOneShot(televisionStaticSound, televisionStaticVolume);

    public void StartTelevisionStaticLoop()
    {
        if (televisionStaticSound == null) return;
        EnsureLoopSource(ref _televisionStaticLoopSource, "TelevisionStaticLoopAudioSource");
        _televisionStaticLoopSource.clip = televisionStaticSound;
        _televisionStaticLoopSource.loop = true;
        _televisionStaticLoopSource.volume = televisionStaticVolume;
        _televisionStaticLoopSource.Play();
    }

    public void StopTelevisionStaticLoop()
    {
        if (_televisionStaticLoopSource != null) _televisionStaticLoopSource.Stop();
    }

    /// <summary>Tutorial_Canvas表示中だけループ再生する砂嵐SEの再生・停止を切り替える。
    /// 専用クリップ未設定時はtelevisionStaticSoundを代用する</summary>
    public void SetTutorialStaticLoopActive(bool active)
    {
        AudioClip clip = tutorialStaticLoopSound != null ? tutorialStaticLoopSound : televisionStaticSound;
        if (clip == null) return;

        EnsureLoopSource(ref _tutorialStaticLoopSource, "TutorialStaticLoopAudioSource");

        if (active)
        {
            _tutorialStaticLoopSource.clip = clip;
            _tutorialStaticLoopSource.loop = true;
            _tutorialStaticLoopSource.volume = tutorialStaticLoopVolume;
            _tutorialStaticLoopSource.Play();
        }
        else
        {
            _tutorialStaticLoopSource.Stop();
        }
    }
}
