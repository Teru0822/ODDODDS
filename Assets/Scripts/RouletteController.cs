using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 5分割（各72°）の円柱リールをルーレットのように回転させ、
/// 重み付き確率で当選スロットを決定し、真ん中にスナップ停止させるコントローラー。
///
/// 使い方:
///   1. Roulette prefab に本スクリプトをアタッチ（Roulette ルートへ）
///   2. Inspector で reelTransform（Reel GO）と slots（Spin1〜5）を設定
///   3. 外部から Spin() を呼ぶとスピン開始
///   4. 完了時に OnSpinComplete(int winningIndex) が発火する
/// </summary>
public class RouletteController : MonoBehaviour
{
    public static RouletteController Instance { get; private set; }

    // -----------------------------------------------------------------------
    // Inspector フィールド
    // -----------------------------------------------------------------------

    [Header("解放状態（タイプライター id16「クレーンゲームにルーレット機能を追加する」で解放）")]
    [Tooltip("Devil_Eye本体の見た目（3Dモデル）。未解放時は非表示にし、解放されたらアクティブ化する。" +
             "未設定の場合、親階層から「Devil_Eye」という名前の子オブジェクトを自動検索する")]
    [SerializeField] private GameObject devilEyeVisual;

    [SerializeField] private bool isUnlocked = false;

    [Header("デバッグ設定")]
    [Tooltip("ON: id16スキルを取得していなくても、常にDevil_Eyeを表示しルーレットアイテムも降らせます（テスト用）")]
    [SerializeField] private bool debugForceUnlock = false;

    /// <summary>ルーレット機能が解放されているか（本解放 or デバッグ強制のいずれか）</summary>
    public bool IsRouletteUnlocked => isUnlocked || debugForceUnlock;

    [Header("スロット設定")]
    [Tooltip("5スロット分のエントリー。slotTransform に Spin1〜Spin5 を、weight に相対確率を設定する")]
    [SerializeField] private SlotEntry[] slots;

    [Header("リール")]
    [Tooltip("回転させる空の GameObject（Reel）。SpinPoll / RouletteMain は含めないこと")]
    [SerializeField] private Transform reelTransform;

    [Header("スピン設定")]
    [Tooltip("停止前に回す最小フル回転数")]
    [SerializeField, Min(1f)] private float minSpins = 5f;

    [Tooltip("停止前に回す最大フル回転数")]
    [SerializeField, Min(1f)] private float maxSpins = 8f;

    [Tooltip("スピン開始〜停止までの所要秒数")]
    [SerializeField, Min(0.1f)] private float spinDuration = 1.5f;

    [Tooltip("減速曲線。横軸が時間(0→1)、縦軸が進行度(0→1)。\n" +
             "スロット感を出すには開始タンジェントを大きくして最初から全速にすること")]
    [SerializeField] private AnimationCurve decelerationCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 3f),   // 開始：即フルスピード（outTangent大）
        new Keyframe(1f, 1f, 0f, 0f)    // 終了：ピタッと停止（inTangent=0）
    );

    [Header("角度設定")]
    [Tooltip("カメラ正面（当たり判定の基準）となる Y 軸回転角度（度）。" +
             "通常は 0 のまま Reel の初期 Transform を Editor で調整すること")]
    [SerializeField] private float centerAngle = 0f;

    [Tooltip("スロット間の角度間隔。5分割なら 360/5 = 72")]
    [SerializeField] private float anglePerSlot = 72f;

    [Header("演出ライト設定")]
    [Tooltip("ルーレット停止時に点滅させる演出用ライトオブジェクト（空のGameObjectなど）")]
    [SerializeField] private GameObject winLightObject;

    [Tooltip("ライトを点滅させる全体の時間（秒）")]
    [SerializeField] private float lightDuration = 3f;

    [Tooltip("点滅の間隔（秒）。0.5秒に設定すると、0.5秒点灯・0.5秒消灯の1秒周期になります")]
    [SerializeField] private float blinkInterval = 0.5f;

    [Header("Devil Eye 演出アニメーション設定")]
    [Tooltip("スピン実行時に Devil Eye の移動・ワープ・3秒保持アニメーションを再生するか")]
    [SerializeField] private bool useSequenceAnimation = true;

    [Tooltip("イントロ/アウトロ移動の各区間のイージング")]
    [SerializeField] private AnimationCurve sequenceEaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Coroutine _lightCoroutine;

    [Header("イベント")]
    [Tooltip("スピン完了時に発火。引数は当選スロットのインデックス（0〜slots.Length-1）")]
    public UnityEvent<int> OnSpinComplete;

    [Header("デバッグ")]
    [Tooltip("このキーを押すとテストスピンを実行する")]
    [SerializeField] private Key debugSpinKey = Key.Space;

    [Tooltip("デバッグキー入力を有効にするか（本番では false にすること）")]
    [SerializeField] private bool enableDebugKey = true;

    // -----------------------------------------------------------------------
    // 内部状態
    // -----------------------------------------------------------------------

    private bool _isSpinning = false;

    /// <summary>
    /// 累積回転角度（Unity の eulerAngles 正規化を避けるためフロートで持つ）。
    /// 例: 4 周後は 1440.0 など、360 に丸めない。
    /// </summary>
    private float _currentReelAngle = 0f;

    // -----------------------------------------------------------------------
    // パブリック API
    // -----------------------------------------------------------------------

    /// <summary>スピン中かどうか</summary>
    public bool IsSpinning => _isSpinning;

    /// <summary>
    /// ローグライクスキル「クレーンゲームにルーレット機能を追加する」(id16) 取得時に呼ばれる。
    /// Devil_Eyeを表示状態にする。
    /// </summary>
    public void Unlock()
    {
        if (isUnlocked) return;

        // Roulette本体（このGameObject自身）がDevil_Eyeと一緒に非アクティブな状態からスタートする構成のため、
        // Awake() がまだ実行されておらず Instance 登録も済んでいないことがある。
        // 解放時にまず自分自身をアクティブ化することで、Awake() を確実に走らせる。
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        isUnlocked = true;
        ApplyUnlockVisibility();
        Debug.Log("[RouletteController] ルーレット機能（Devil_Eye）が解放されました。");
    }

    /// <summary>現在の解放状態（本解放 or デバッグ強制）に応じて Devil_Eye の表示/非表示を切り替える</summary>
    private void ApplyUnlockVisibility()
    {
        if (devilEyeVisual != null)
        {
            devilEyeVisual.SetActive(IsRouletteUnlocked);
        }
    }

    /// <summary>
    /// スピンを開始する。スピン中に呼ばれた場合は無視する。
    /// 当選スロットは weight による重み付きランダムで決定する。
    /// </summary>
    public void Spin()
    {
        if (_isSpinning)
        {
            Debug.Log("[RouletteController] スピン中のため新しいスピンを無視しました。");
            return;
        }

        if (!ValidateReferences()) return;

        if (useSequenceAnimation)
        {
            StartCoroutine(ExecuteEyeSequenceAndSpinRoutine());
        }
        else
        {
            _isSpinning = true;
            SyncCurrentAngle();
            int winningIndex = SelectWinningSlot();
            float targetAngle = ComputeTargetAngle(winningIndex);
            StartCoroutine(SpinStandaloneRoutine(targetAngle, winningIndex));
        }
    }

    /// <summary>
    /// Devil_Eyeの演出シーケーションを使わない単独スピン用のラッパー。
    /// SpinCoroutine() 自体はもう _isSpinning を false に戻さないため、ここで完了を待ってから戻す。
    /// </summary>
    private IEnumerator SpinStandaloneRoutine(float targetAngle, int winningIndex)
    {
        yield return StartCoroutine(SpinCoroutine(targetAngle, winningIndex));
        _isSpinning = false;
    }

    /// <summary>
    /// デバッグ用：指定インデックスのスロットを強制的に当選としてスピンする。
    /// Inspector の Context Menu からも呼び出せる。
    /// </summary>
    [ContextMenu("Test Spin (ランダム)")]
    public void DebugSpin() => Spin();

    /// <summary>デバッグ用：インデックス 0 で強制スピン</summary>
    [ContextMenu("Test Spin (強制 index 0)")]
    public void DebugSpinForce0() => SpinForceResult(0);

    /// <summary>
    /// 指定インデックスのスロットを当選として強制スピンする（デバッグ・演出用）。
    /// </summary>
    public void SpinForceResult(int slotIndex)
    {
        if (_isSpinning)
        {
            Debug.Log("[RouletteController] スピン中のため強制スピンを無視しました。");
            return;
        }

        if (!ValidateReferences()) return;

        slotIndex = Mathf.Clamp(slotIndex, 0, slots.Length - 1);
        _isSpinning = true;
        SyncCurrentAngle();
        float targetAngle = ComputeTargetAngle(slotIndex);
        StartCoroutine(SpinStandaloneRoutine(targetAngle, slotIndex));
    }

    // -----------------------------------------------------------------------
    // Unity ライフサイクル
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("[RouletteController] 既に別のインスタンスが存在するため、こちらは Instance に登録しません。");
        }

        // Devil_Eyeの見た目が未設定の場合、親階層から「Devil_Eye」という名前の子オブジェクトを自動検索する
        if (devilEyeVisual == null && transform.parent != null)
        {
            var found = transform.parent.Find("Devil_Eye");
            if (found != null) devilEyeVisual = found.gameObject;
        }

        ApplyUnlockVisibility();

        if (reelTransform == null)
            Debug.LogWarning("[RouletteController] reelTransform が未設定です。Inspector で Reel を割り当ててください。");

        if (slots == null || slots.Length == 0)
            Debug.LogWarning("[RouletteController] slots が空です。Spin1〜Spin5 を Inspector で設定してください。");
        else if (slots.Length != 5)
            Debug.LogWarning($"[RouletteController] slots の数が5ではありません ({slots.Length})。anglePerSlot と一致しているか確認してください。");

        if (minSpins > maxSpins)
        {
            Debug.LogWarning($"[RouletteController] minSpins ({minSpins}) > maxSpins ({maxSpins})。値を入れ替えます。");
            (minSpins, maxSpins) = (maxSpins, minSpins);
        }

        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].slotTransform != null)
                {
                    float angle = ((slots[i].slotTransform.localEulerAngles.z % 360f) + 360f) % 360f;
                    Debug.Log($"[RouletteController] Slot {i} ({slots[i].label}): Transform = {slots[i].slotTransform.name}, Local Z Angle = {angle:F1}°");
                }
                else
                {
                    Debug.LogWarning($"[RouletteController] Slot {i} の Transform が未設定です。");
                }
            }
        }

        if (winLightObject != null)
        {
            winLightObject.SetActive(false);
        }

        SyncCurrentAngle();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Play中にdebugForceUnlockのチェックを付け外しした際、即座にDevil_Eyeの表示へ反映する
        if (Application.isPlaying)
        {
            ApplyUnlockVisibility();
        }
    }
#endif

    private void Update()
    {
        if (!enableDebugKey) return;
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        bool isKeyPressed = keyboard.kKey.wasPressedThisFrame || (debugSpinKey != Key.None && keyboard[debugSpinKey].wasPressedThisFrame);
        if (isKeyPressed)
        {
            var goal = UFOItemGoal.Instance != null ? UFOItemGoal.Instance : FindAnyObjectByType<UFOItemGoal>();
            if (goal != null)
            {
                goal.TriggerRouletteItemGoalEffect();
            }
            else
            {
                Spin();
            }
        }
    }

    // -----------------------------------------------------------------------
    // 内部ロジック
    // -----------------------------------------------------------------------

    // SetForcedNextResult() で設定された、次回1回分だけ強制する当選インデックス（消費後は自動的に解除）
    private int? _forcedNextWinningIndex = null;

    /// <summary>
    /// 次に呼ばれるSpin()の結果を強制的に指定インデックスにする（1回限り、消費後は自動的に解除される）。
    /// SpinForceResult()と違い、通常のSpin()と同じアニメーション経路（useSequenceAnimation時はDevil_Eyeの
    /// イントロ〜アウトロ演出込み）をそのまま使う。保証当選演出などで使用する。
    /// </summary>
    public void SetForcedNextResult(int slotIndex)
    {
        _forcedNextWinningIndex = Mathf.Clamp(slotIndex, 0, slots.Length - 1);
    }

    /// <summary>
    /// 重み付きランダムで当選スロットのインデックスを返す。SetForcedNextResult()で予約されていれば、
    /// それを最優先で返し予約を消費する。
    /// ItemSpawner.cs の累積和パターンを踏襲。
    /// </summary>
    private int SelectWinningSlot()
    {
        if (_forcedNextWinningIndex.HasValue)
        {
            int forced = _forcedNextWinningIndex.Value;
            _forcedNextWinningIndex = null;
            return forced;
        }

        float totalWeight = 0f;
        for (int i = 0; i < slots.Length; i++)
            totalWeight += slots[i].weight;

        if (totalWeight <= 0f)
        {
            Debug.LogWarning("[RouletteController] 全スロットの weight が 0 です。フォールバックとしてスロット 0 を選択します。");
            return 0;
        }

        float rand = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < slots.Length; i++)
        {
            cumulative += slots[i].weight;
            if (rand < cumulative)
                return i;
        }

        // 浮動小数点の誤差で末尾に到達した場合のフォールバック
        return slots.Length - 1;
    }

    /// <summary>
    /// winningIndex のスロットが centerAngle の位置（正面）に来るように
    /// リールを正方向（+Z）に回した場合の目標累積角度を計算する。
    ///
    /// _currentReelAngle は SyncCurrentAngle() で [0,360) に揃えてから呼ぶこと。
    /// delta は「現在位置から目標位置まで正方向に回る最小角度」を求め、
    /// その上にフル回転を積む。これにより何周目のスピンでも正しく停止する。
    /// </summary>
    private float ComputeTargetAngle(int winningIndex)
    {
        // 当選スロットが正面（centerAngle）に来るときのリール角度（絶対、0-360）
        // 逆回転方向のズレを解消するため、-winningIndex を基準にして計算します
        float slotBaseAngle = -winningIndex * anglePerSlot;
        float targetEffective = ((centerAngle - slotBaseAngle) % 360f + 360f) % 360f;

        // 現在位置から目標まで正方向に回る最小角度
        float delta = ((targetEffective - _currentReelAngle) % 360f + 360f) % 360f;

        // delta が極小（ほぼ同じ面）のときは最低1周追加して停止がわかるようにする
        if (delta < 1f) delta += 360f;

        // フル回転を上乗せ
        float spins = Random.Range(minSpins, maxSpins);
        float fullRevolutions = Mathf.Floor(spins) * 360f;

        return _currentReelAngle + fullRevolutions + delta;
    }

    /// <summary>
    /// スピン前にリールの実際の Z 角度（0-360）を _currentReelAngle に同期する。
    /// Editor での位置調整や前回スピン後の誤差を吸収する。
    /// </summary>
    private void SyncCurrentAngle()
    {
        if (reelTransform != null)
            _currentReelAngle = ((reelTransform.localEulerAngles.z % 360f) + 360f) % 360f;
    }

    /// <summary>
    /// PinballSessionController.MoveCamera() と同パターンの補間コルーチン。
    /// decelerationCurve で減速しながら targetAngle まで回転し、最後にスナップ。
    /// </summary>
    private IEnumerator SpinCoroutine(float targetAngle, int winningIndex)
    {
        float startAngle = _currentReelAngle;
        Debug.Log($"[RouletteController] スピン開始: 開始Z角度={startAngle:F1}°, 目標Z角度={targetAngle:F1}°, 当選={winningIndex}");
        float safeDuration = Mathf.Max(0.01f, spinDuration);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / safeDuration;
            float e = decelerationCurve.Evaluate(Mathf.Clamp01(t));
            float angle = Mathf.Lerp(startAngle, targetAngle, e);
            reelTransform.localEulerAngles = new Vector3(0f, 0f, angle);
            yield return null;
        }

        // 浮動小数点誤差を消すハードスナップ
        reelTransform.localEulerAngles = new Vector3(0f, 0f, targetAngle);
        _currentReelAngle = targetAngle;

        // _isSpinning は「絵柄の回転」だけでなく「Devil_Eyeの一連の演出全体」が完全に終わったことを表す
        // フラグとして外部（待ち行列処理等）から参照されるため、ここでは false にしない。
        // ExecuteEyeSequenceAndSpinRoutine() の finally、または非シーケンス経路のラッパーで false にする。

        string label = (slots != null && winningIndex < slots.Length && !string.IsNullOrEmpty(slots[winningIndex].label))
            ? slots[winningIndex].label
            : winningIndex.ToString();
        Debug.Log($"[RouletteController] スピン完了 → 当選スロット [{winningIndex}] {label}");

        StartLightShow();

        OnSpinComplete.Invoke(winningIndex);
    }

    /// <summary>
    /// Devil_Eye の移動・ワープ・3秒保持付きルーレット演出コルーチン。
    /// 1. イントロ (計1秒): Y=4.708上昇 → Vector3(-2.06100011, 4.21000004, 4.01000023) ワープ → Y=3.37400007 下降
    /// 2. スピン実行
    /// 3. スピン終了後 3.0 秒間保持
    /// 4. アウトロ (計1秒): Y=4.21000004上昇 → 初期X,Z(Y=4.708)ワープ → 初期Y下降
    /// </summary>
    private IEnumerator ExecuteEyeSequenceAndSpinRoutine()
    {
        _isSpinning = true;

        RouletteFloater floater = GetComponent<RouletteFloater>();
        if (floater == null) floater = GetComponentInParent<RouletteFloater>();
        if (floater == null) floater = GetComponentInChildren<RouletteFloater>();

        if (floater != null)
        {
            floater.IsSequenceActive = true;
        }

        try
        {
            // 初期位置（ローカル）・回転の保持
            Vector3 startLocalBasePos = floater != null ? floater.BaseLocalPosition : transform.localPosition;
            Quaternion startLocalRot = transform.localRotation;

            // ワープ先のローカル位置および回転
            Vector3 targetWarpPos = new Vector3(-2.06100011f, 5.0f, 4.01000023f);
            Quaternion targetWarpRot = new Quaternion(0.0478489846f, -0.858475208f, 0.0284163076f, -0.50982672f);

            // イントロ1秒間の各ステップの所要時間（上昇 0.33秒 -> ワープ 0.34秒 -> 下降 0.33秒）
            float durA = 0.33f;
            float durB = 0.34f;
            float durC = 0.33f;

            // -------------------------------------------------------------------
            // Phase 1: イントロ演出（合計1.0秒）
            // -------------------------------------------------------------------
            // 1a. Y 座標を 5.0 まで上昇
            Vector3 pos1aStart = startLocalBasePos;
            Vector3 pos1aEnd = new Vector3(startLocalBasePos.x, 5.0f, startLocalBasePos.z);
            float t = 0f;
            while (t < durA)
            {
                t += Time.deltaTime;
                float e = sequenceEaseCurve != null ? sequenceEaseCurve.Evaluate(Mathf.Clamp01(t / durA)) : Mathf.Clamp01(t / durA);
                Vector3 curPos = Vector3.Lerp(pos1aStart, pos1aEnd, e);
                if (floater != null) floater.BaseLocalPosition = curPos;
                else transform.localPosition = curPos;
                yield return null;
            }

            // 1b. ワープ位置 Vector3(-2.06100011, 5.0, 4.01000023) および Rotation 設定
            Vector3 pos1bStart = pos1aEnd;
            Vector3 pos1bEnd = targetWarpPos;
            t = 0f;
            while (t < durB)
            {
                t += Time.deltaTime;
                float e = sequenceEaseCurve != null ? sequenceEaseCurve.Evaluate(Mathf.Clamp01(t / durB)) : Mathf.Clamp01(t / durB);
                Vector3 curPos = Vector3.Lerp(pos1bStart, pos1bEnd, e);
                Quaternion curRot = Quaternion.Slerp(startLocalRot, targetWarpRot, e);
                if (floater != null) floater.BaseLocalPosition = curPos;
                else transform.localPosition = curPos;
                transform.localRotation = curRot;
                yield return null;
            }
            transform.localRotation = targetWarpRot;

            // 1c. Y 座標を 3.37400007 まで下降
            Vector3 pos1cStart = targetWarpPos;
            Vector3 pos1cEnd = new Vector3(targetWarpPos.x, 3.37400007f, targetWarpPos.z);
            t = 0f;
            while (t < durC)
            {
                t += Time.deltaTime;
                float e = sequenceEaseCurve != null ? sequenceEaseCurve.Evaluate(Mathf.Clamp01(t / durC)) : Mathf.Clamp01(t / durC);
                Vector3 curPos = Vector3.Lerp(pos1cStart, pos1cEnd, e);
                if (floater != null) floater.BaseLocalPosition = curPos;
                else transform.localPosition = curPos;
                yield return null;
            }
            if (floater != null) floater.BaseLocalPosition = pos1cEnd;
            else transform.localPosition = pos1cEnd;

            // -------------------------------------------------------------------
            // Phase 2: ルーレット回転
            // -------------------------------------------------------------------
            SyncCurrentAngle();
            int winningIndex = SelectWinningSlot();
            float targetAngle = ComputeTargetAngle(winningIndex);

            // SpinCoroutine を実行し完了を待機
            yield return StartCoroutine(SpinCoroutine(targetAngle, winningIndex));

            // -------------------------------------------------------------------
            // Phase 3: 回転終了後 3.0 秒間滞留
            // -------------------------------------------------------------------
            yield return new WaitForSeconds(3.0f);

            // -------------------------------------------------------------------
            // Phase 4: アウトロ演出（合計1.0秒）
            // -------------------------------------------------------------------
            // 4a. Y 座標を 5.0 まで上昇
            Vector3 currentBase = floater != null ? floater.BaseLocalPosition : transform.localPosition;
            Vector3 pos4aEnd = new Vector3(currentBase.x, 5.0f, currentBase.z);
            t = 0f;
            while (t < durA)
            {
                t += Time.deltaTime;
                float e = sequenceEaseCurve != null ? sequenceEaseCurve.Evaluate(Mathf.Clamp01(t / durA)) : Mathf.Clamp01(t / durA);
                Vector3 curPos = Vector3.Lerp(currentBase, pos4aEnd, e);
                if (floater != null) floater.BaseLocalPosition = curPos;
                else transform.localPosition = curPos;
                yield return null;
            }

            // 4b. 最初にいた場所（Y=5.0）へワープ・回転復帰
            Vector3 pos4bStart = pos4aEnd;
            Vector3 pos4bEnd = new Vector3(startLocalBasePos.x, 5.0f, startLocalBasePos.z);
            t = 0f;
            while (t < durB)
            {
                t += Time.deltaTime;
                float e = sequenceEaseCurve != null ? sequenceEaseCurve.Evaluate(Mathf.Clamp01(t / durB)) : Mathf.Clamp01(t / durB);
                Vector3 curPos = Vector3.Lerp(pos4bStart, pos4bEnd, e);
                Quaternion curRot = Quaternion.Slerp(targetWarpRot, startLocalRot, e);
                if (floater != null) floater.BaseLocalPosition = curPos;
                else transform.localPosition = curPos;
                transform.localRotation = curRot;
                yield return null;
            }
            transform.localRotation = startLocalRot;


            // 4c. もともといた初期 Y 座標まで下降
            Vector3 pos4cStart = pos4bEnd;
            Vector3 pos4cEnd = startLocalBasePos;
            t = 0f;
            while (t < durC)
            {
                t += Time.deltaTime;
                float e = sequenceEaseCurve != null ? sequenceEaseCurve.Evaluate(Mathf.Clamp01(t / durC)) : Mathf.Clamp01(t / durC);
                Vector3 curPos = Vector3.Lerp(pos4cStart, pos4cEnd, e);
                if (floater != null) floater.BaseLocalPosition = curPos;
                else transform.localPosition = curPos;
                yield return null;
            }
            if (floater != null) floater.BaseLocalPosition = startLocalBasePos;
            else transform.localPosition = startLocalBasePos;
        }
        finally
        {
            if (floater != null)
            {
                floater.IsSequenceActive = false;
            }
            _isSpinning = false;
        }
    }

    /// <summary>必須参照を検証し、問題があれば警告して false を返す。</summary>
    private bool ValidateReferences()
    {
        if (reelTransform == null)
        {
            Debug.LogWarning("[RouletteController] reelTransform が未設定のためスピンをスキップします。");
            return false;
        }

        if (slots == null || slots.Length == 0)
        {
            Debug.LogWarning("[RouletteController] slots が空のためスピンをスキップします。");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 指定インデックスのスロットエントリーを取得する。範囲外の場合は null を返す。
    /// </summary>
    public SlotEntry GetSlotEntry(int index)
    {
        if (slots != null && index >= 0 && index < slots.Length)
        {
            return slots[index];
        }
        return null;
    }

    private void StartLightShow()
    {
        if (winLightObject == null) return;
        if (_lightCoroutine != null)
        {
            StopCoroutine(_lightCoroutine);
        }
        _lightCoroutine = StartCoroutine(LightShowRoutine());
    }

    private IEnumerator LightShowRoutine()
    {
        float elapsed = 0f;
        bool isActive = false;
        float interval = Mathf.Max(0.01f, blinkInterval);

        // lightDuration の時間中、blinkInterval 間隔でオンオフを繰り返す
        while (elapsed < lightDuration)
        {
            isActive = !isActive;
            winLightObject.SetActive(isActive);

            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        // 最後に必ず非アクティブ（消灯）にする
        winLightObject.SetActive(false);
        _lightCoroutine = null;
    }
}

// ---------------------------------------------------------------------------
// SlotEntry — スロット1枠分の設定
// ---------------------------------------------------------------------------

/// <summary>ルーレットの1スロット分の設定。Inspector で5つ並べて使う。</summary>
[System.Serializable]
public class SlotEntry
{
    [Tooltip("このスロットの Transform（Spin1〜Spin5）")]
    public Transform slotTransform;

    [Tooltip("当選の相対確率。0 なら絶対に選ばれない。他スロットとの比率で確率が決まる")]
    [Min(0f)]
    public float weight = 1f;

    [Tooltip("デバッグログ・Inspector 識別用のラベル（空欄でも動作する）")]
    public string label = "";

    [Tooltip("このスロットが当選した際に排出するオブジェクトのプレハブ")]
    public GameObject rewardPrefab;
}
