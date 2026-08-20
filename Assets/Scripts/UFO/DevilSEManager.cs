using System.Collections;
using UnityEngine;

/// <summary>
/// Devilキャッチャー（実機・練習機）シーンで使われるSE(効果音)の再生窓口を1箇所にまとめたマネージャー。
///
/// 以前は UFOArmController / DevilItemGoal / UFOCameraController / TutorialCraneController /
/// TutorialItemGoal / TouchPanelOutlineController / TutorialStepController / TelevisionStaticController
/// など多数のスクリプトがそれぞれ個別に AudioSource を持ち、同じ「GetComponent→AddComponent」の
/// ボイラープレートを重複させていた。これらのクリップと再生処理をここへ集約することで、
/// 今後SEの音量調整・ON/OFF設定などを行う際に1箇所で完結するようにする。
///
/// シーンに1つだけ配置する（実機・練習機どちらの音もここに集約するため、Practice_Cranegame側には置かない）。
/// 各スクリプトからは DevilSEManager.Instance 経由で呼び出す。
///
/// なお、ボタン個別のクリック音（ButtonController）やトリガーゾーン個別の効果音（TriggerSoundPlayer）は、
/// 「同じスクリプトの複数配置ごとに異なるクリップを鳴らし分ける」という設計を維持したいため、
/// クリップ自体はここへ移さず、再生窓口（PlayOneShot）だけをここに一本化している。
/// </summary>
public class DevilSEManager : MonoBehaviour
{
    public static DevilSEManager Instance { get; private set; }

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
    [Tooltip("ルーレットアイテム（プレゼントボックス等）投入時の効果音。未設定の場合はコイン獲得音で代用します")]
    [SerializeField] private AudioClip realRouletteItemGetSound;
    [SerializeField, Range(0f, 10f)] private float realItemGetVolume = 1f;

    [Header("=== 実機：残り時間警告（10秒未満） ===")]
    [SerializeField] private AudioClip realLowTimeWarningLoopSound;
    [SerializeField, Range(0f, 10f)] private float realLowTimeWarningVolume = 1f;

    [Header("=== 実機：ルーレット ===")]
    [Tooltip("ルーレットが回転している間ループ再生する音")]
    [SerializeField] private AudioClip rouletteSpinLoopSound;
    [SerializeField, Range(0f, 10f)] private float rouletteSpinVolume = 1f;
    [Tooltip("ルーレットが回り終わって当選が確定した瞬間に鳴らす音（下のハズレスロット以外の通常当選時）")]
    [SerializeField] private AudioClip rouletteWinSound;
    [SerializeField, Range(0f, 10f)] private float rouletteWinVolume = 1f;
    [Tooltip("この番号のスロット（RouletteControllerのslots配列のElement番号と同じ、0始まり）が" +
             "当選した時はハズレ扱いとし、下のLoseSoundを鳴らす。デフォルトは1（Element1）")]
    [SerializeField] private int rouletteLoseSlotIndex = 1;
    [Tooltip("上のスロットが当選（＝ハズレ）した時の専用サウンド。未設定なら通常のrouletteWinSoundを使う")]
    [SerializeField] private AudioClip rouletteLoseSound;
    [Tooltip("大当たり（ジャックポット）の予兆演出（チェイスライト1周分の赤点灯）に合わせて鳴らす専用SE")]
    [SerializeField] private AudioClip jackpotOmenSound;
    [SerializeField, Range(0f, 10f)] private float jackpotOmenVolume = 1f;
    [Tooltip("大当たり（ジャックポット）が確定した結果で止まった瞬間の専用当選音。" +
             "未設定なら通常のrouletteWinSoundを使う")]
    [SerializeField] private AudioClip jackpotWinSound;
    [SerializeField, Range(0f, 10f)] private float jackpotWinVolume = 1f;

    [Header("=== 実機：ライト演出 ===")]
    [SerializeField] private AudioClip realLightFlickerSound;
    [SerializeField, Range(0f, 10f)] private float realLightFlickerVolume = 0.5f;
    [SerializeField] private AudioClip realLightOffSound;
    [SerializeField, Range(0f, 10f)] private float realLightOffVolume = 0.5f;

    [Header("=== 実機：環境音 ===")]
    [Tooltip("ライトが点灯している間（クレーンゲーム稼働中）ループ再生する環境音")]
    [SerializeField] private AudioClip ambientLoopSound;
    [SerializeField, Range(0f, 10f)] private float ambientVolume = 1f;

    [Header("=== 実機：引き出し演出 ===")]
    [Tooltip("セッション終了後、獲得したアイテムを見せる引き出しが開くタイミングで再生するSE")]
    [SerializeField] private AudioClip drawerOpenSound;
    [SerializeField, Range(0f, 10f)] private float drawerOpenVolume = 1f;
    [Tooltip("見せ終わった引き出しが閉まるタイミングで再生するSE")]
    [SerializeField] private AudioClip drawerCloseSound;
    [SerializeField, Range(0f, 10f)] private float drawerCloseVolume = 1f;

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
    [Tooltip("爪（フィンガー）が開く/閉じる瞬間のSE。開閉どちらも同じ音を共用する" +
             "（StartDescentCycle開始時の開き、掴む時の閉じ、手動開閉ボタンの開閉、いずれもここで再生）")]
    [SerializeField] private AudioClip clawToggleSound;
    [SerializeField, Range(0f, 10f)] private float clawToggleVolume = 1f;

    // ============================================================
    // 練習機 (Practice Machine)
    // ============================================================
    [Header("=== 練習機：BGM ===")]
    [Tooltip("レバー/ボタンを初めて操作した瞬間に再生を開始し、タイマーが0になったら止めるBGM")]
    [SerializeField] private AudioClip practiceBgmSound;
    [SerializeField, Range(0f, 1f)] private float practiceBgmVolume = 0.6f;

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
    [Header("=== 共通：コイン衝突音（CoinImpactSoundManagerが速度に応じた音量で再生） ===")]
    [Tooltip("コインが床に落ちた時の効果音（床は鉄板を想定）。複数登録すると毎回ランダムに1つ選ばれる" +
             "（同じ音の連打感を減らすため、バリエーションがあるならなるべく複数登録する）")]
    [SerializeField] private AudioClip[] floorImpactSounds;
    [SerializeField, Range(0f, 10f)] private float floorImpactVolume = 1f;
    [Tooltip("コイン同士がぶつかった時の効果音。複数登録すると毎回ランダムに1つ選ばれる")]
    [SerializeField] private AudioClip[] coinImpactSounds;
    [SerializeField, Range(0f, 10f)] private float coinImpactVolume = 1f;
    [Tooltip("時計・ルーレットアイテム（プレゼントボックス）が床に落ちた時専用の効果音。" +
             "複数登録すると毎回ランダムに1つ選ばれる")]
    [SerializeField] private AudioClip[] specialItemFloorImpactSounds;
    [SerializeField, Range(0f, 10f)] private float specialItemFloorImpactVolume = 1f;
    [Tooltip("時計・ルーレットアイテム（プレゼントボックス）がコイン等に衝突した時専用の効果音。" +
             "複数登録すると毎回ランダムに1つ選ばれる")]
    [SerializeField] private AudioClip[] specialItemImpactSounds;
    [SerializeField, Range(0f, 10f)] private float specialItemImpactVolume = 1f;
    [Tooltip("衝突音のピッチ（高低）をこの範囲でランダムに変化させ、単調な連打感を減らす")]
    [SerializeField] private Vector2 impactPitchRange = new Vector2(0.9f, 1.1f);

    [Header("=== 共通：タッチパネル（実機/練習機どちらのパネルでも使用） ===")]
    [SerializeField] private AudioClip touchSelectSound;
    [SerializeField, Range(0f, 10f)] private float touchSelectVolume = 1f;
    [SerializeField] private AudioClip touchSelect2Sound;
    [SerializeField, Range(0f, 10f)] private float touchSelect2Volume = 1f;
    [SerializeField] private AudioClip touchLockedSound;
    [SerializeField, Range(0f, 10f)] private float touchLockedVolume = 1f;
    [Tooltip("チュートリアル開始確認（Tutorial_CanvasのYes/No）で「いいえ」を押した時専用の拒否SE。" +
             "60/90等の通常ロック（touchLockedSound）とは別の音にしたい場合はここに設定する")]
    [SerializeField] private AudioClip touchNoLockedSound;
    [SerializeField, Range(0f, 10f)] private float touchNoLockedVolume = 1f;
    [Tooltip("タッチパネルの各項目にアウトラインが表示された瞬間（ホバー開始時）に鳴らすSE。" +
             "全項目で同じ音を使う前提で一元化している")]
    [SerializeField] private AudioClip touchHoverSound;
    [SerializeField, Range(0f, 10f)] private float touchHoverVolume = 1f;

    [Header("=== 共通：テレビ砂嵐 ===")]
    [Tooltip("カメラ切り替え時などに単発/ループで鳴らす砂嵐SE")]
    [SerializeField] private AudioClip televisionStaticSound;
    [SerializeField, Range(0f, 5f)] private float televisionStaticVolume = 1f;
    [Tooltip("Tutorial_Canvas表示中にループ再生する専用の砂嵐SE。未設定なら上のtelevisionStaticSoundを流用する")]
    [SerializeField] private AudioClip tutorialStaticLoopSound;
    [SerializeField, Range(0f, 5f)] private float tutorialStaticLoopVolume = 0.5f;
    [Tooltip("上のループ音を、クリップの最後まで待たずに何秒地点でループさせるか（秒）。" +
             "クリップ末尾に無音区間がある場合、そこを含めずにループさせたい時に使う")]
    [SerializeField] private float tutorialStaticLoopEndSeconds = 50f;

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
    private AudioSource _rouletteSpinLoopSource;
    private AudioSource _ambientLoopSource;
    private AudioSource _practiceBgmSource;
    private AudioSource _televisionStaticLoopSource;
    private AudioSource _tutorialStaticLoopSource;
    private Coroutine _tutorialStaticLoopMonitorRoutine;

    private void Awake()
    {
        // 実機・練習機のどちらの構成からも参照されうるため、既に別インスタンスが
        // Instanceを保持している場合は絶対に奪わない（他のDevil系シングルトンと同じガード方式）
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
    public void PlayRealRouletteItemGet() => PlayOneShot(realRouletteItemGetSound != null ? realRouletteItemGetSound : realCoinGetSound, realItemGetVolume);

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
    // 実機：ルーレット
    // ============================================================
    public void StartRouletteSpinLoop()
    {
        if (rouletteSpinLoopSound == null) return;
        EnsureLoopSource(ref _rouletteSpinLoopSource, "RouletteSpinLoopAudioSource");
        _rouletteSpinLoopSource.clip = rouletteSpinLoopSound;
        _rouletteSpinLoopSource.loop = true;
        _rouletteSpinLoopSource.volume = rouletteSpinVolume;
        _rouletteSpinLoopSource.Play();
    }

    public void StopRouletteSpinLoop()
    {
        if (_rouletteSpinLoopSource != null) _rouletteSpinLoopSource.Stop();
    }

    /// <summary>当選スロット番号を渡すと、それがrouletteLoseSlotIndex（デフォルトElement1＝ハズレ）と
    /// 一致する場合だけLoseSoundを鳴らす。isJackpotがtrueの場合は最優先でjackpotWinSoundを鳴らす。
    /// それ以外（番号を渡さない場合も含む）は通常の当選音を鳴らす</summary>
    public void PlayRouletteResult(int winningIndex = -1, bool isJackpot = false)
    {
        if (isJackpot && jackpotWinSound != null)
        {
            PlayOneShot(jackpotWinSound, jackpotWinVolume);
        }
        else if (winningIndex == rouletteLoseSlotIndex && rouletteLoseSound != null)
        {
            PlayOneShot(rouletteLoseSound, rouletteWinVolume);
        }
        else
        {
            PlayOneShot(rouletteWinSound, rouletteWinVolume);
        }
    }

    /// <summary>大当たり（ジャックポット）の予兆演出（チェイスライト1周分の赤点灯）に合わせて鳴らす</summary>
    public void PlayJackpotOmen() => PlayOneShot(jackpotOmenSound, jackpotOmenVolume);

    // ============================================================
    // 実機：ライト演出
    // ============================================================
    public void PlayRealLightFlicker() => PlayWithTailCut(realLightFlickerSound, realLightFlickerVolume, 0.4f);
    public void PlayRealLightOff() => PlayWithTailCut(realLightOffSound, realLightOffVolume, 0f);
    public void PlayDrawerOpen() => PlayOneShot(drawerOpenSound, drawerOpenVolume);
    public void PlayDrawerClose() => PlayOneShot(drawerCloseSound, drawerCloseVolume);

    // ============================================================
    // 実機：環境音
    // ============================================================
    public void StartAmbientLoop()
    {
        if (ambientLoopSound == null) return;
        EnsureLoopSource(ref _ambientLoopSource, "AmbientLoopAudioSource");
        _ambientLoopSource.clip = ambientLoopSound;
        _ambientLoopSource.loop = true;
        _ambientLoopSource.volume = ambientVolume;
        _ambientLoopSource.Play();
    }

    public void StopAmbientLoop()
    {
        if (_ambientLoopSource != null) _ambientLoopSource.Stop();
    }

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
    /// <summary>爪が開く/閉じる瞬間に鳴らす。開閉で同じ音を共用する</summary>
    public void PlayClawToggle() => PlayOneShot(clawToggleSound, clawToggleVolume);

    // ============================================================
    // 練習機：BGM
    // ============================================================
    /// <summary>レバー/ボタンを初めて操作した瞬間に呼ぶ。既に再生中の場合は何もしない</summary>
    public void StartPracticeBgm()
    {
        if (practiceBgmSound == null) return;
        EnsureLoopSource(ref _practiceBgmSource, "PracticeBgmAudioSource");
        if (_practiceBgmSource.isPlaying && _practiceBgmSource.clip == practiceBgmSound) return;
        _practiceBgmSource.clip = practiceBgmSound;
        _practiceBgmSource.loop = true;
        _practiceBgmSource.volume = practiceBgmVolume;
        _practiceBgmSource.Play();
    }

    /// <summary>タイマーが0になった瞬間、またはチュートリアル終了時に呼ぶ</summary>
    public void StopPracticeBgm()
    {
        if (_practiceBgmSource != null) _practiceBgmSource.Stop();
    }

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
    // 共通：コイン衝突音
    // ============================================================

    /// <summary>volumeFactor: 0〜1。CoinImpactSoundManagerが衝突速度から算出した音量係数</summary>
    public void PlayFloorImpact(float volumeFactor)
    {
        float pitch = Random.Range(impactPitchRange.x, impactPitchRange.y);
        PlayOneShotWithPitch(PickRandomClip(floorImpactSounds), floorImpactVolume * Mathf.Clamp01(volumeFactor), pitch);
    }

    /// <summary>volumeFactor: 0〜1。CoinImpactSoundManagerが衝突速度から算出した音量係数</summary>
    public void PlayCoinImpact(float volumeFactor)
    {
        float pitch = Random.Range(impactPitchRange.x, impactPitchRange.y);
        PlayOneShotWithPitch(PickRandomClip(coinImpactSounds), coinImpactVolume * Mathf.Clamp01(volumeFactor), pitch);
    }

    /// <summary>volumeFactor: 0〜1。時計・ルーレットアイテム（プレゼントボックス）が床に落ちた時専用の衝突音</summary>
    public void PlaySpecialItemFloorImpact(float volumeFactor)
    {
        float pitch = Random.Range(impactPitchRange.x, impactPitchRange.y);
        PlayOneShotWithPitch(PickRandomClip(specialItemFloorImpactSounds), specialItemFloorImpactVolume * Mathf.Clamp01(volumeFactor), pitch);
    }

    /// <summary>volumeFactor: 0〜1。時計・ルーレットアイテム（プレゼントボックス）がコイン等に衝突した時専用の衝突音</summary>
    public void PlaySpecialItemImpact(float volumeFactor)
    {
        float pitch = Random.Range(impactPitchRange.x, impactPitchRange.y);
        PlayOneShotWithPitch(PickRandomClip(specialItemImpactSounds), specialItemImpactVolume * Mathf.Clamp01(volumeFactor), pitch);
    }

    private static AudioClip PickRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        return clips[Random.Range(0, clips.Length)];
    }

    // ============================================================
    // 共通：タッチパネル
    // ============================================================
    public void PlayTouchSelect() => PlayOneShot(touchSelectSound, touchSelectVolume);
    public void PlayTouchSelect2() => PlayOneShot(touchSelect2Sound, touchSelect2Volume);
    public void PlayTouchLocked() => PlayOneShot(touchLockedSound, touchLockedVolume);
    public void PlayTouchNoLocked() => PlayOneShot(touchNoLockedSound, touchNoLockedVolume);
    public void PlayTouchHover() => PlayOneShot(touchHoverSound, touchHoverVolume);

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
            // loop=trueは万一下の監視が間に合わなかった場合の保険として残しておくが、
            // 実際のループはtutorialStaticLoopEndSeconds地点で手動で先頭へ戻すことで行う
            // （クリップ末尾の無音区間を含めてループさせないため）
            _tutorialStaticLoopSource.loop = true;
            _tutorialStaticLoopSource.volume = tutorialStaticLoopVolume;
            _tutorialStaticLoopSource.time = 0f;
            _tutorialStaticLoopSource.Play();

            if (_tutorialStaticLoopMonitorRoutine != null) StopCoroutine(_tutorialStaticLoopMonitorRoutine);
            _tutorialStaticLoopMonitorRoutine = StartCoroutine(MonitorTutorialStaticLoopRoutine());
        }
        else
        {
            _tutorialStaticLoopSource.Stop();
            if (_tutorialStaticLoopMonitorRoutine != null)
            {
                StopCoroutine(_tutorialStaticLoopMonitorRoutine);
                _tutorialStaticLoopMonitorRoutine = null;
            }
        }
    }

    /// <summary>tutorialStaticLoopEndSeconds地点に達するたびに先頭(0秒)へシークし直すことで、
    /// クリップ末尾の無音区間を含めずにループさせ続ける</summary>
    private IEnumerator MonitorTutorialStaticLoopRoutine()
    {
        while (true)
        {
            if (_tutorialStaticLoopSource != null && _tutorialStaticLoopSource.isPlaying &&
                _tutorialStaticLoopSource.time >= tutorialStaticLoopEndSeconds)
            {
                _tutorialStaticLoopSource.time = 0f;
            }
            yield return null;
        }
    }
}
