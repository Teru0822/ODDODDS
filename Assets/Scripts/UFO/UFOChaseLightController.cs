using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// コイン投入・プレイ開始中、左右のライト（通常ライト・パトランプが混在していても可）を
/// 指定した順番で1つずつ流れるように点灯させる「チェイスライト」演出。
///
/// UFOChaseLightUnitが各prefab（bottom_lump_left等、10種類）に1つずつ自己申告で付いている前提で、
/// このコントローラーは実行時に FindObjectsByType で全インスタンスを自動収集し、
/// 「同じPositionのユニットはまとめて1グループとして同時に点灯」させる。
/// 同じprefabが何セットシーンに置かれていても、手動でLightをドラッグして配線する必要はない。
///
/// 優先度は以下の順（上ほど優先）:
/// 1. 残り時間が10秒を切ったら → パトランプには一切触らず PatoLampController 側の警告点灯・
///    回転に完全に任せる。通常ライトはブラックダイヤ等の獲得演出中でなければ赤で点滅させる
/// 2. ブラックダイヤ・時計・ルーレット(プレゼントボックス)獲得演出中（UFOItemGoal.IsFlashing）
///    → 通常ライトのチェイス/点滅を消灯して待機（既存の獲得演出に任せる）
/// 3. 上記以外（通常プレイ中）→ 指定順のチェイス演出（パトランプ含む）
/// </summary>
public class UFOChaseLightController : MonoBehaviour
{
    private const bool Lamp = true;
    private const bool Normal = false;

    // 左: 1(bottom_lump_left) → 7(PatoLamp_bottom_left) → 3(middle_lump_left) → 9(PatoLamp_top_left) → 5(top_lump_left)
    private static readonly (UFOChaseLightUnit.Position pos, bool isPatrolLamp)[] LeftOrder =
    {
        (UFOChaseLightUnit.Position.BottomLumpLeft,     Normal),
        (UFOChaseLightUnit.Position.PatoLampBottomLeft, Lamp),
        (UFOChaseLightUnit.Position.MiddleLumpLeft,     Normal),
        (UFOChaseLightUnit.Position.PatoLampTopLeft,    Lamp),
        (UFOChaseLightUnit.Position.TopLumpLeft,        Normal),
    };

    // 右: 6(top_lump_right) → 10(PatoLamp_top_right) → 4(middle_lump_right) → 8(PatoLamp_bottom_right) → 2(bottom_lump_right)
    private static readonly (UFOChaseLightUnit.Position pos, bool isPatrolLamp)[] RightOrder =
    {
        (UFOChaseLightUnit.Position.TopLumpRight,        Normal),
        (UFOChaseLightUnit.Position.PatoLampTopRight,    Lamp),
        (UFOChaseLightUnit.Position.MiddleLumpRight,     Normal),
        (UFOChaseLightUnit.Position.PatoLampBottomRight, Lamp),
        (UFOChaseLightUnit.Position.BottomLumpRight,     Normal),
    };

    [Header("チェイス演出")]
    [Tooltip("次の1つに切り替わるまでの間隔（秒）")]
    [SerializeField, Min(0.01f)] private float stepInterval = 0.15f;

    [Tooltip("同時に点灯させたままにする個数。1なら1つだけが動く定番のチェイス演出、" +
             "2以上にすると尾を引くように見える")]
    [SerializeField, Min(1)] private int litCount = 1;

    [Header("残り時間警告（パトランプと同じ10秒しきい値）")]
    [Tooltip("残りこの秒数を切ったら、通常ライトを赤の点滅に切り替え、パトランプ部分はPatoLampControllerに譲る")]
    [SerializeField] private float lowTimeThresholdSeconds = 10f;

    [Tooltip("赤点滅の間隔（秒）")]
    [SerializeField, Min(0.01f)] private float redBlinkInterval = 0.25f;

    [Tooltip("残り時間警告で使う色（赤がデフォルト）")]
    [ColorUsage(true, true)]
    [SerializeField] private Color warningColor = Color.red;

    [Header("ルーレット演出")]
    [Tooltip("ルーレットが回転している最中、水色のランプが次の1つに切り替わるまでの間隔（秒）。小さいほど速く流れる")]
    [SerializeField, Min(0.01f)] private float rouletteChaseStepInterval = 0.15f;

    [Tooltip("ルーレットが当たってから配当（降雨）が終わるまでの、全体点滅の間隔（秒）")]
    [SerializeField, Min(0.01f)] private float rouletteBlinkInterval = 0.2f;

    [Header("コイン獲得演出")]
    [Tooltip("銅貨・銀貨・金貨を獲得した瞬間、通常ライト全体を今の色のまま一瞬点灯させておく時間（秒）")]
    [SerializeField, Min(0.05f)] private float coinCatchFlashDuration = 0.3f;

    // UFOItemGoal.HandleItemDrop側から、銅貨/銀貨/金貨を獲得した瞬間に呼ばれる（インスタンス跨ぎでアクセスするためstatic）
    private static float s_coinCatchFlashTimer = 0f;
    private static float s_coinCatchFlashDuration = 0.3f;

    /// <summary>
    /// 銅貨・銀貨・金貨のいずれかを獲得した瞬間に呼び出す。通常ライト全体を今の色のまま
    /// 一瞬点灯させる（UFOItemGoal.HandleItemDropから呼ばれる）
    /// </summary>
    public static void TriggerCoinCatchFlash()
    {
        s_coinCatchFlashTimer = s_coinCatchFlashDuration;
    }

    // Position -> そのPositionを自己申告している全ユニットのLight/Rendererをまとめたもの
    private Dictionary<UFOChaseLightUnit.Position, List<Light>> _lightsByPosition;
    private Dictionary<UFOChaseLightUnit.Position, List<Renderer>> _renderersByPosition;

    // 優先度（上ほど優先）: Deferred > RouletteWon > RouletteSpinning > CoinInsertFlash > WarningBlink/Chase > Off
    private enum RegularLightMode { Off, CoinInsertFlash, Chase, WarningBlink, RouletteSpinning, RouletteWon, Deferred }
    private RegularLightMode _currentMode = RegularLightMode.Off;
    private Coroutine _routine;

    private readonly Dictionary<Light, Color> _originalColors = new Dictionary<Light, Color>();

    // マテリアルのEmission色のキャッシュ（_EmissionColorプロパティを持つマテリアルのみ対象）
    private readonly Dictionary<Renderer, Color> _originalEmissionColors = new Dictionary<Renderer, Color>();

    private void Start()
    {
        RefreshUnits();
        s_coinCatchFlashDuration = coinCatchFlashDuration;

        // プレハブ側のLightはデフォルトで有効(点灯済み)になっているため、何もしないと
        // _currentModeの初期値(Off)と最初のUpdate()の判定結果(Off)が一致してしまい、
        // 切り替え処理が一度も走らないまま「最初から点いている」状態になってしまう。
        // コイン投入時に確実に「消灯→点灯」の変化として見えるよう、起動時に一度明示的に消灯しておく
        SetLightsForOrder(LeftOrder, normalOnly: false, on: false);
        SetLightsForOrder(RightOrder, normalOnly: false, on: false);
    }

    /// <summary>
    /// シーン上の全UFOChaseLightUnitを検索し、Position別にLightをグループ化し直す。
    /// ラウンドが変わってアイテムが再生成される等、シーン構成が変わったタイミングで呼び直せる
    /// </summary>
    public void RefreshUnits()
    {
        _lightsByPosition = new Dictionary<UFOChaseLightUnit.Position, List<Light>>();
        _renderersByPosition = new Dictionary<UFOChaseLightUnit.Position, List<Renderer>>();

        var units = FindObjectsByType<UFOChaseLightUnit>(FindObjectsSortMode.None);
        foreach (var unit in units)
        {
            if (unit == null) continue;

            if (!_lightsByPosition.TryGetValue(unit.Kind, out var lightList))
            {
                lightList = new List<Light>();
                _lightsByPosition[unit.Kind] = lightList;
            }
            foreach (var light in unit.Lights)
            {
                if (light != null) lightList.Add(light);
            }

            if (!_renderersByPosition.TryGetValue(unit.Kind, out var rendererList))
            {
                rendererList = new List<Renderer>();
                _renderersByPosition[unit.Kind] = rendererList;
            }
            foreach (var renderer in unit.Renderers)
            {
                if (renderer != null) rendererList.Add(renderer);
            }
        }
    }

    private void Update()
    {
        if (_lightsByPosition == null) RefreshUnits();

        if (s_coinCatchFlashTimer > 0f)
        {
            s_coinCatchFlashTimer -= Time.deltaTime;
        }

        bool lowTime = IsLowTime();
        RegularLightMode desiredMode = DetermineRegularLightMode(lowTime);

        if (desiredMode != _currentMode)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            switch (desiredMode)
            {
                case RegularLightMode.CoinInsertFlash:
                    // 2つの状況で使う共通の見た目（通常ライトだけを今の色のまま点ける）:
                    // ①コイン投入直後〜レバー/ボタン操作までの間 ②銅貨・銀貨・金貨を獲得した瞬間の一瞬フラッシュ
                    // パトランプはPatoLampControllerに委ねるため触らない方針だが、Chase中の「パトランプの番」で
                    // enabled=trueのまま切り替わってくると、RestoreAllColors()で色だけ本来の色(赤)に戻り、
                    // 点いたまま赤く見えてしまう。それを防ぐため、ここで明示的に消灯しておく
                    RestoreAllColors();
                    SetLightsForOrder(LeftOrder, normalOnly: true, on: true);
                    SetLightsForOrder(RightOrder, normalOnly: true, on: true);
                    SetLightsForOrder(LeftOrder, normalOnly: false, on: false, patrolOnly: true);
                    SetLightsForOrder(RightOrder, normalOnly: false, on: false, patrolOnly: true);
                    break;
                case RegularLightMode.Chase:
                    RestoreAllColors();
                    _routine = StartCoroutine(ChaseRoutine());
                    break;
                case RegularLightMode.WarningBlink:
                    // Chase中に引き継いだ色やルーレットの水色が、PatoLampController管理下の
                    // パトランプに残ったままにならないよう、一旦全て元の色（パトランプなら本来の赤等）に戻す
                    RestoreAllColors();
                    SetLightsForOrder(LeftOrder, normalOnly: true, on: false);
                    SetLightsForOrder(RightOrder, normalOnly: true, on: false);
                    _routine = StartCoroutine(WarningBlinkRoutine());
                    break;
                case RegularLightMode.RouletteSpinning:
                    RestoreAllColors();
                    _routine = StartCoroutine(RouletteChaseRoutine());
                    break;
                case RegularLightMode.RouletteWon:
                    RestoreAllColors();
                    SetLightsForOrder(LeftOrder, normalOnly: false, on: false);
                    SetLightsForOrder(RightOrder, normalOnly: false, on: false);
                    _routine = StartCoroutine(RouletteBlinkRoutine());
                    break;
                case RegularLightMode.Deferred:
                    // ブラックダイヤ・時計等の獲得演出中（UFOItemGoal.FlashLampsCoroutineが同じ
                    // Light/Rendererを直接操作している）。ここでは一切手を出さず完全に委ねる。
                    // 中途半端にRestoreAllColors()やenabled切り替えを行うと、演出側が点けた直後の色を
                    // 上書きして消してしまうため、意図的に何もしない
                    break;
                case RegularLightMode.Off:
                default:
                    RestoreAllColors();
                    SetLightsForOrder(LeftOrder, normalOnly: true, on: false);
                    SetLightsForOrder(RightOrder, normalOnly: true, on: false);
                    // lowTimeでなければパトランプも消灯する（プレイ終了/演出待機時のリセット）。
                    // lowTime中は絶対に触らずPatoLampControllerに委ねる
                    if (!lowTime)
                    {
                        SetLightsForOrder(LeftOrder, normalOnly: false, on: false, patrolOnly: true);
                        SetLightsForOrder(RightOrder, normalOnly: false, on: false, patrolOnly: true);
                    }
                    break;
            }

            _currentMode = desiredMode;
        }
    }

    private bool IsLowTime()
    {
        var ctrl = UFOCameraController.Instance;
        return ctrl != null && UFOCameraController.IsPlaySessionActive &&
               ctrl.RemainingTime > 0f && ctrl.RemainingTime <= lowTimeThresholdSeconds;
    }

    private RegularLightMode DetermineRegularLightMode(bool lowTime)
    {
        if (!UFOCameraController.IsPlaySessionActive) return RegularLightMode.Off;

        // ブラックダイヤ・時計等の獲得演出中は、UFOItemGoal.FlashLampsCoroutineに完全に委ねる
        // （こちらのRestoreAllColors()やenabled切り替えで、演出側が点けた色を消してしまわないように）
        if (UFOItemGoal.IsFlashing) return RegularLightMode.Deferred;

        // ルーレット関連の演出は、低残り時間の警告よりも優先して見せる
        if (UFOItemGoal.IsRouletteRewardPending) return RegularLightMode.RouletteWon;
        if (RouletteController.Instance != null && RouletteController.Instance.IsSpinning) return RegularLightMode.RouletteSpinning;

        // レバー/ボタンを実際に操作してタイマーが始まるまでは、チェイスは開始せず、
        // コイン投入時に点いた通常ライトをそのまま点けたままにしておく。
        // また、銅貨・銀貨・金貨を獲得した瞬間も、同じ「通常ライト全体を今の色で点灯」演出を一瞬流用する
        if (!UFOCameraController.IsTimerStarted || s_coinCatchFlashTimer > 0f) return RegularLightMode.CoinInsertFlash;

        return lowTime ? RegularLightMode.WarningBlink : RegularLightMode.Chase;
    }

    private IEnumerator ChaseRoutine()
    {
        int leftIndex = 0;
        int rightIndex = 0;

        while (true)
        {
            ApplyChaseStep(LeftOrder, leftIndex);
            ApplyChaseStep(RightOrder, rightIndex);

            leftIndex = (leftIndex + 1) % LeftOrder.Length;
            rightIndex = (rightIndex + 1) % RightOrder.Length;

            yield return new WaitForSeconds(stepInterval);
        }
    }

    /// <summary>
    /// order内の、currentIndexからlitCount個分（ループ含む）が「今の番」。今の番だけ点灯し、
    /// それ以外（パトランプ含む）は消灯する（このコルーチンはlowTimeでない間しか動かない）。
    /// overrideColorがnullなら通常ライトはUnityで設定済みの元の色のまま点灯する。
    /// パトランプの番になったときは、直前に光った通常ライトと同じ色を引き継いで点灯する
    /// （overrideColorが指定されている場合＝ルーレットのチェイス演出時は、全て指定色で統一する）
    /// </summary>
    private void ApplyChaseStep((UFOChaseLightUnit.Position pos, bool isPatrolLamp)[] order, int currentIndex, Color? overrideColor = null)
    {
        for (int i = 0; i < order.Length; i++)
        {
            bool isCurrent = false;
            for (int offset = 0; offset < litCount; offset++)
            {
                if (i == (currentIndex + offset) % order.Length)
                {
                    isCurrent = true;
                    break;
                }
            }

            if (overrideColor.HasValue)
            {
                SetPositionOnOffWithColor(order[i].pos, isCurrent, overrideColor.Value);
            }
            else if (isCurrent && order[i].isPatrolLamp)
            {
                Color inheritedColor = GetPrecedingNormalColor(order, i);
                SetPositionOnOffWithColor(order[i].pos, true, inheritedColor);
            }
            else
            {
                SetPositionOnOff(order[i].pos, isCurrent);
            }
        }
    }

    /// <summary>
    /// order内でindexから遡って最初に見つかる「通常ライト」の、現在の（Unity設定済みの）色を返す。
    /// パトランプが光る番になったときに引き継ぐ色を決めるために使う
    /// </summary>
    private Color GetPrecedingNormalColor((UFOChaseLightUnit.Position pos, bool isPatrolLamp)[] order, int index)
    {
        for (int back = 1; back <= order.Length; back++)
        {
            int idx = (index - back + order.Length) % order.Length;
            if (!order[idx].isPatrolLamp)
            {
                return GetNativeLightColor(order[idx].pos);
            }
        }
        return Color.white;
    }

    private Color GetNativeLightColor(UFOChaseLightUnit.Position pos)
    {
        if (_lightsByPosition != null && _lightsByPosition.TryGetValue(pos, out var lights))
        {
            foreach (var light in lights)
            {
                if (light != null) return light.color;
            }
        }
        return Color.white;
    }

    /// <summary>
    /// 残り10秒を切った間、通常ライトだけを赤で同時点滅させる。パトランプは一切触らない
    /// （PatoLampController側の警告点灯・回転に完全に任せる）。
    /// </summary>
    private IEnumerator WarningBlinkRoutine()
    {
        bool on = false;
        while (true)
        {
            on = !on;
            SetLightsForOrder(LeftOrder, normalOnly: true, on: on, overrideColor: warningColor);
            SetLightsForOrder(RightOrder, normalOnly: true, on: on, overrideColor: warningColor);
            yield return new WaitForSeconds(redBlinkInterval);
        }
    }

    /// <summary>
    /// ルーレットが回転している最中、指定順（パトランプ含む）で1つずつ水色を流れるように点灯させる。
    /// 通常のChaseRoutineと同じ並び方だが、色をUFOItemGoal.RouletteChaseColorで上書きする
    /// </summary>
    private IEnumerator RouletteChaseRoutine()
    {
        int leftIndex = 0;
        int rightIndex = 0;

        while (true)
        {
            Color color = UFOItemGoal.RouletteChaseColor;
            ApplyChaseStep(LeftOrder, leftIndex, color);
            ApplyChaseStep(RightOrder, rightIndex, color);

            leftIndex = (leftIndex + 1) % LeftOrder.Length;
            rightIndex = (rightIndex + 1) % RightOrder.Length;

            yield return new WaitForSeconds(rouletteChaseStepInterval);
        }
    }

    /// <summary>
    /// ルーレットが当たってから配当（降雨）が終わるまで、全ライト（パトランプ含む）を
    /// 水色で同時点滅させる
    /// </summary>
    private IEnumerator RouletteBlinkRoutine()
    {
        bool on = false;
        while (true)
        {
            on = !on;
            Color color = UFOItemGoal.RouletteChaseColor;
            SetLightsForOrder(LeftOrder, normalOnly: false, on: on, overrideColor: color);
            SetLightsForOrder(RightOrder, normalOnly: false, on: on, overrideColor: color);
            yield return new WaitForSeconds(rouletteBlinkInterval);
        }
    }

    private void SetLightsForOrder(
        (UFOChaseLightUnit.Position pos, bool isPatrolLamp)[] order,
        bool normalOnly, bool on, bool patrolOnly = false, Color? overrideColor = null)
    {
        foreach (var (pos, isPatrolLamp) in order)
        {
            if (normalOnly && isPatrolLamp) continue;
            if (patrolOnly && !isPatrolLamp) continue;

            if (_lightsByPosition.TryGetValue(pos, out var lights))
            {
                foreach (var light in lights)
                {
                    if (light == null) continue;
                    if (overrideColor.HasValue)
                    {
                        CacheOriginalColor(light);
                        light.color = overrideColor.Value;
                    }
                    light.enabled = on;
                }
            }

            if (overrideColor.HasValue)
            {
                SetPositionEmission(pos, on ? overrideColor.Value : (Color?)null);
            }
        }
    }

    /// <summary>
    /// 今の番なら点灯（Unityで設定済みの元の色のまま）、そうでなければ消灯する。
    /// Lightは色を一切上書きしない（enabledの切り替えのみ）。Materialは、点灯中は元のEmission、
    /// 消灯中は黒にすることでLightと見た目を連動させる
    /// </summary>
    private void SetPositionOnOff(UFOChaseLightUnit.Position pos, bool on)
    {
        if (_lightsByPosition != null && _lightsByPosition.TryGetValue(pos, out var lights))
        {
            foreach (var light in lights)
            {
                if (light != null) light.enabled = on;
            }
        }

        SetPositionEmission(pos, on ? (Color?)null : Color.black);
    }

    /// <summary>
    /// ルーレットのチェイス演出用。今の番なら指定色（水色）で点灯、そうでなければ消灯する
    /// </summary>
    private void SetPositionOnOffWithColor(UFOChaseLightUnit.Position pos, bool on, Color color)
    {
        if (_lightsByPosition != null && _lightsByPosition.TryGetValue(pos, out var lights))
        {
            foreach (var light in lights)
            {
                if (light == null) continue;
                if (on)
                {
                    CacheOriginalColor(light);
                    light.color = color;
                }
                light.enabled = on;
            }
        }

        SetPositionEmission(pos, on ? color : (Color?)null);
    }

    /// <summary>
    /// このPositionに属するRendererのマテリアルEmissionを色を変える（nullなら元の色に戻す）。
    /// UFOItemGoal.FlashLampsCoroutineと同じ、MaterialPropertyBlock経由での書き換え方式
    /// （マテリアルアセット自体は書き換えない）
    /// </summary>
    private void SetPositionEmission(UFOChaseLightUnit.Position pos, Color? color)
    {
        if (_renderersByPosition == null || !_renderersByPosition.TryGetValue(pos, out var renderers)) return;

        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            if (renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty("_EmissionColor")) continue;

            if (!_originalEmissionColors.ContainsKey(renderer))
            {
                _originalEmissionColors[renderer] = renderer.sharedMaterial.GetColor("_EmissionColor");
            }

            Color target = color ?? _originalEmissionColors[renderer];

            var mpb = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", target);
            renderer.SetPropertyBlock(mpb);
        }
    }

    private void CacheOriginalColor(Light light)
    {
        if (!_originalColors.ContainsKey(light))
        {
            _originalColors[light] = light.color;
        }
    }

    private void RestoreAllColors()
    {
        foreach (var kv in _originalColors)
        {
            if (kv.Key != null) kv.Key.color = kv.Value;
        }

        if (_renderersByPosition != null)
        {
            foreach (var pos in _renderersByPosition.Keys)
            {
                SetPositionEmission(pos, null);
            }
        }
    }
}
