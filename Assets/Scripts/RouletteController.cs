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
    // -----------------------------------------------------------------------
    // Inspector フィールド
    // -----------------------------------------------------------------------

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

        _isSpinning = true;
        SyncCurrentAngle();
        int winningIndex = SelectWinningSlot();
        float targetAngle = ComputeTargetAngle(winningIndex);
        StartCoroutine(SpinCoroutine(targetAngle, winningIndex));
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
        StartCoroutine(SpinCoroutine(targetAngle, slotIndex));
    }

    // -----------------------------------------------------------------------
    // Unity ライフサイクル
    // -----------------------------------------------------------------------

    private void Awake()
    {
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

        SyncCurrentAngle();
    }

    private void Update()
    {
        if (!enableDebugKey) return;
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard[debugSpinKey].wasPressedThisFrame)
            Spin();
    }

    // -----------------------------------------------------------------------
    // 内部ロジック
    // -----------------------------------------------------------------------

    /// <summary>
    /// 重み付きランダムで当選スロットのインデックスを返す。
    /// ItemSpawner.cs の累積和パターンを踏襲。
    /// </summary>
    private int SelectWinningSlot()
    {
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

        _isSpinning = false;

        string label = (slots != null && winningIndex < slots.Length && !string.IsNullOrEmpty(slots[winningIndex].label))
            ? slots[winningIndex].label
            : winningIndex.ToString();
        Debug.Log($"[RouletteController] スピン完了 → 当選スロット [{winningIndex}] {label}");

        OnSpinComplete.Invoke(winningIndex);
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
