using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ターン表示を 3D オブジェクト（counter プレハブ）で行う。
/// 数字を置く位置に空オブジェクトを 1 つ置いておくと、その下に桁数ぶんの
/// 数字オブジェクトを並べる。現在ターンが 1 なら「0」「0」「1」のように 0 埋めする。
///
/// 【セットアップ】
///   1. counter プレハブをシーンに置き、このコンポーネントを付ける
///   2. 数字を出したい場所に空オブジェクトを作り、Anchor に割り当てる
///        Current Turn … 現在のターン表示部分
///        Next Debt    … 次の取り立てまでの表示部分
///   3. Digit Prefabs に 0〜9 の数字オブジェクトを順番に入れる（要素 0 が「0」）
///
/// 桁の大きさ・間隔・向きは Anchor ごとに設定でき、Play 中の変更も即座に反映される。
/// </summary>
[DisallowMultipleComponent]
public class TurnCounter3D : MonoBehaviour
{
    /// <summary>数字を並べる 1 箇所分の設定。</summary>
    [Serializable]
    public class DigitField
    {
        [Tooltip("桁ごとの置き場所。上位桁から順に入れる。 " +
                 "3 桁なら [0]=百の位 [1]=十の位 [2]=1の位、 " +
                 "2 桁なら [0]=十の位 [1]=1の位")]
        public Transform[] digitAnchors;

        [Tooltip("数字オブジェクトの大きさ。桁ごとに変えたい場合は Anchor 側のスケールで調整する")]
        public Vector3 digitScale = Vector3.one;

        [Tooltip("数字オブジェクトの向き（Anchor から見たローカル角度）")]
        public Vector3 digitLocalEuler = Vector3.zero;

        /// <summary>実際に使える桁数。</summary>
        public int DigitCount => digitAnchors != null ? digitAnchors.Length : 0;

        [NonSerialized] public readonly List<GameObject> Spawned = new List<GameObject>();
        [NonSerialized] public int LastValue = int.MinValue;
    }

    [Header("数字オブジェクト")]
    [Tooltip("0〜9 の数字オブジェクト。要素 0 が「0」、要素 9 が「9」")]
    [SerializeField] private GameObject[] _digitPrefabs = new GameObject[10];

    [Header("表示する場所")]
    [Tooltip("現在のターン数（3 桁）。Digit Anchors に 百の位・十の位・1の位 の順で空オブジェクトを入れる")]
    [SerializeField] private DigitField _currentTurn = new DigitField();

    [Tooltip("次の取り立てまでのターン数（2 桁）。Digit Anchors に 十の位・1の位 の順で空オブジェクトを入れる")]
    [SerializeField] private DigitField _nextDebtTurn = new DigitField();

    [Header("Idle アニメーション")]
    [Tooltip("再生する Idle クリップ。ここに入れると AnimatorController 無しで再生・ループできる。 " +
             "Project ビューで counter.fbx を展開すると中に入っているクリップをドラッグする")]
    [SerializeField] private AnimationClip _idleClip;

    [Tooltip("Idle を回すオブジェクト（haguruma など）。空のままにすると子から全て自動収集する")]
    [SerializeField] private Animator[] _animators;

    [Tooltip("常時再生する Idle ステート名。空なら既定ステートをそのまま使う")]
    [SerializeField] private string _idleStateName = "Idle";

    [Tooltip("止まっていたら再生し直して、ループしないクリップでも回し続ける")]
    [SerializeField] private bool _keepIdlePlaying = true;

    [Header("カメラへの追従")]
    [Tooltip("カメラの子にして画面に貼り付ける。オフにするとワールド上の普通の 3D オブジェクトとして扱う")]
    [SerializeField] private bool _pinToCamera = true;

    [Tooltip("親にするカメラ。未指定なら Camera.main を使う")]
    [SerializeField] private Camera _camera;

    [Tooltip("カメラから見た位置。X-で左、Y+で上、Z+で奥（画面左上なら X- Y+）")]
    [SerializeField] private Vector3 _localPosition = new Vector3(-0.32f, 0.18f, 1f);

    [Tooltip("カメラから見た向き")]
    [SerializeField] private Vector3 _localEuler = new Vector3(0f, 180f, 0f);

    [Tooltip("カウンター全体の大きさ")]
    [SerializeField] private float _scale = 1f;

    [Header("退避（本・設定画面を開いた時）")]
    [Tooltip("Tab の本や Esc の設定画面を開いている間、画面左へどかす")]
    [SerializeField] private bool _stowWhileUIOpen = true;

    [Tooltip("退避先の位置（カメラから見たローカル座標）。通常位置より X をマイナスへ")]
    [SerializeField] private Vector3 _stowedLocalPosition = new Vector3(-0.75f, 0.18f, 1f);

    [Tooltip("退避時の向き")]
    [SerializeField] private Vector3 _stowedLocalEuler = new Vector3(0f, 180f, 0f);

    [Tooltip("どく／戻るのにかける時間(秒)")]
    [SerializeField] private float _stowDuration = 0.35f;

    [Tooltip("動きの緩急")]
    [SerializeField] private AnimationCurve _stowCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("専用ライト")]
    [Tooltip("このオブジェクトだけを照らすライトを使う。オフならシーンの通常のライティングを受ける")]
    [SerializeField] private bool _useDedicatedLight = true;

    [Tooltip("専用のポイントライト。未指定なら自動生成する")]
    [SerializeField] private Light _dedicatedLight;

    [Tooltip("Rendering Layer のビット番号。0 は既定レイヤーなので 1 以上で、他と重ならない番号にする")]
    [Range(1, 31)]
    [SerializeField] private int _lightingLayerBit = 1;

    [Tooltip("自動生成するライトの位置（カウンターから見たローカル座標）")]
    [SerializeField] private Vector3 _lightLocalPosition = new Vector3(0f, 0.25f, -0.25f);

    [SerializeField] private Color _lightColor = Color.white;
    [SerializeField] private float _lightIntensity = 3f;
    [SerializeField] private float _lightRange = 2f;

    [Header("表示タイミング")]
    [Tooltip("ゲーム画面の他の UI（所持コイン等）と同じタイミングで出す。" +
             "ローディング中・イントロツアー中は出さない")]
    [SerializeField] private bool _followGameUIVisibility = true;

    [Header("差し替え")]
    [Tooltip("置き換え対象の旧 2D ターン表示。指定すると起動時に非表示にする")]
    [SerializeField] private GameObject _legacyTurnUI;

    [Header("デバッグ")]
    [SerializeField] private bool _logEvents = false;

    // Play 中に Inspector をいじって調整できるよう、直前の見た目設定を覚えておく
    private readonly Dictionary<DigitField, string> _appliedLayout = new Dictionary<DigitField, string>();

    private Animation[] _legacyAnimations;

    // AnimatorController を用意しなくてもクリップを再生できるようにするための再生グラフ
    private PlayableGraph _graph;
    private AnimationClipPlayable _clipPlayable;
    private bool _graphReady;

    // Idle ステートを持たない Animator に Play("Idle") を投げるとエラーが出続けるので、事前に調べておく
    private bool[] _animatorHasIdle;
    private float _stowT;
    private bool _subscribed;
    private bool? _visibleApplied;
    private string _appliedLightSettings;
    private readonly HashSet<string> _warned = new HashSet<string>();

    private void Awake()
    {
        CollectAnimationTargets();
        SetupClipPlayback();

        if (_legacyTurnUI != null) _legacyTurnUI.SetActive(false);

        SetupDedicatedLight();
    }

    /// <summary>本や設定画面が開いている間はどいておく。</summary>
    private bool ShouldStow =>
        _stowWhileUIOpen && (BookOpenController.IsBookVisible || SettingUIManager.IsMenuOpen);

    /// <summary>
    /// このオブジェクトだけを照らすライトを用意する。
    /// URP の Rendering Layers を使い、カウンターと専用ライトだけを同じビットに置く。
    /// 他のライトは既定ビットのままなのでカウンターを照らさず、
    /// 専用ライトも既定ビットを照らさないので他のオブジェクトに影響しない。
    /// </summary>
    private void SetupDedicatedLight()
    {
        if (!_useDedicatedLight) return;

        if (_dedicatedLight == null)
        {
            var go = new GameObject("CounterLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = _lightLocalPosition;

            _dedicatedLight = go.AddComponent<Light>();
            _dedicatedLight.type = LightType.Point;
            _dedicatedLight.shadows = LightShadows.None;
        }

        ApplyLightSettings(true);
    }

    /// <summary>
    /// ライトの設定値を反映する。Play 中に Inspector を動かして詰められるよう毎フレーム呼び、
    /// 変わったフレームだけ実際に書き込む。
    /// </summary>
    private void ApplyLightSettings(bool force = false)
    {
        if (!_useDedicatedLight || _dedicatedLight == null) return;

        string signature = $"{_lightColor}|{_lightIntensity}|{_lightRange}|{_lightingLayerBit}|{_lightLocalPosition}";
        if (!force && signature == _appliedLightSettings) return;

        uint mask = 1u << _lightingLayerBit;

        _dedicatedLight.color = _lightColor;
        _dedicatedLight.intensity = _lightIntensity;
        _dedicatedLight.range = _lightRange;
        _dedicatedLight.transform.localPosition = _lightLocalPosition;

        // URP は「どのオブジェクトを照らすか」を Light ではなく
        // UniversalAdditionalLightData 側で持っている。
        // Light.renderingLayerMask は影用に同期される値でしかないため、
        // こちらを設定しないと照射レイヤーが既定のままになる
        var lightData = _dedicatedLight.GetComponent<UniversalAdditionalLightData>();
        if (lightData == null)
        {
            lightData = _dedicatedLight.gameObject.AddComponent<UniversalAdditionalLightData>();
        }
        lightData.renderingLayers = mask;

        // ビット番号を変えた時は、照らされる側も揃え直さないと真っ暗になる
        ApplyLightingLayer();

        _appliedLightSettings = signature;
    }

    /// <summary>カウンター配下の Renderer を専用ビットへ移す。数字を作り直すたびに呼ぶ。</summary>
    private void ApplyLightingLayer()
    {
        if (!_useDedicatedLight) return;

        uint mask = 1u << _lightingLayerBit;
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null) renderer.renderingLayerMask = mask;
        }
    }

    /// <summary>
    /// Idle を回す対象を集める。歯車それぞれに Animator が付いている構成なので、
    /// 1 つだけ拾うのではなく子を全て対象にする。
    /// </summary>
    private void CollectAnimationTargets()
    {
        if (_animators == null || _animators.Length == 0)
        {
            _animators = GetComponentsInChildren<Animator>(true);
        }

        _legacyAnimations = GetComponentsInChildren<Animation>(true);

        int hash = string.IsNullOrEmpty(_idleStateName) ? 0 : Animator.StringToHash(_idleStateName);
        _animatorHasIdle = new bool[_animators.Length];

        for (int i = 0; i < _animators.Length; i++)
        {
            var animator = _animators[i];
            _animatorHasIdle[i] = animator != null
                                  && animator.runtimeAnimatorController != null
                                  && hash != 0
                                  && animator.HasState(0, hash);
        }

        if (_animators.Length == 0 && _legacyAnimations.Length == 0 && _idleClip == null)
        {
            Debug.LogWarning("[TurnCounter3D] Animator も Animation も Idle Clip も見つかりません。" +
                             "Idle は再生されません。", this);
            return;
        }

        if (_logEvents)
        {
            int named = 0;
            foreach (bool has in _animatorHasIdle) if (has) named++;

            Debug.Log($"[TurnCounter3D] Idle 対象: Animator {_animators.Length} 個" +
                      $"（うち「{_idleStateName}」ステートあり {named} 個）, " +
                      $"Animation {_legacyAnimations.Length} 個", this);
        }
    }

    /// <summary>
    /// Idle クリップを AnimatorController 無しで直接再生する。
    /// counter.fbx は Generic インポートで Controller が自動生成されないため、
    /// クリップさえ指定すれば動くこの経路を用意しておく。
    /// </summary>
    private void SetupClipPlayback()
    {
        if (_idleClip == null) return;

        Animator target = null;
        if (_animators != null)
        {
            foreach (var animator in _animators)
            {
                if (animator != null) { target = animator; break; }
            }
        }

        // クリップのパスは FBX ルート基準なので、無ければルートに足す
        if (target == null) target = gameObject.AddComponent<Animator>();

        _graph = PlayableGraph.Create("TurnCounter3D Idle");
        _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        _clipPlayable = AnimationClipPlayable.Create(_graph, _idleClip);
        _clipPlayable.SetApplyFootIK(false);

        var output = AnimationPlayableOutput.Create(_graph, "Idle", target);
        output.SetSourcePlayable(_clipPlayable);

        _graph.Play();
        _graphReady = true;

        if (_logEvents)
        {
            Debug.Log($"[TurnCounter3D] Idle クリップ「{_idleClip.name}」を {target.name} で再生します" +
                      $"（長さ {_idleClip.length:0.##}秒 / Loop Time {_idleClip.isLooping}）", this);
        }
    }

    /// <summary>
    /// クリップの再生位置を自前で回す。
    /// インポート設定の Loop Time が入っていなくても確実にループさせるため。
    /// </summary>
    private void AdvanceClipPlayback()
    {
        if (!_graphReady || _idleClip == null) return;

        float length = _idleClip.length;
        if (length <= 0f) return;

        _clipPlayable.SetTime(Time.time % length);
    }

    private void OnDestroy()
    {
        if (_graph.IsValid()) _graph.Destroy();
    }

    /// <summary>カメラの子にして、指定のローカル位置へ置く。</summary>
    private void ApplyCameraPin()
    {
        if (!_pinToCamera) return;

        if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
        if (_camera == null) return;

        // カメラの子にしてはいけない。
        // タイトルからの遷移中は SceneTransitionManager が「持ち越したタイトルカメラ」を
        // Camera.main にしており、ロード完了時にそれを Destroy する。
        // 子になっているとカウンターごと道連れで消えるため、親子付けはせず
        // 毎フレーム カメラ基準のワールド座標を計算して置く（本と同じ方式）。
        if (transform.parent != null) transform.SetParent(null, true);

        // 退避の進み具合を更新する。設定画面で時間が止まっても動くよう unscaled で進める
        float target = ShouldStow ? 1f : 0f;
        _stowT = _stowDuration > 0f
            ? Mathf.MoveTowards(_stowT, target, Time.unscaledDeltaTime / _stowDuration)
            : target;

        float e = _stowCurve != null ? _stowCurve.Evaluate(_stowT) : _stowT;

        // Slerp なので原点まわりに弧を描いて左へ逃げる
        Vector3 localPos = Vector3.Slerp(_localPosition, _stowedLocalPosition, e);
        Quaternion localRot = Quaternion.Slerp(Quaternion.Euler(_localEuler),
                                               Quaternion.Euler(_stowedLocalEuler), e);

        Transform cam = _camera.transform;
        transform.SetPositionAndRotation(cam.TransformPoint(localPos), cam.rotation * localRot);
        transform.localScale = Vector3.one * _scale;
    }

    /// <summary>
    /// MoneyManager の購読を試みる。
    /// このプロジェクトは Multi-Scene 構成で、MoneyManager は MainScene ではなく
    /// 加法ロードされる Scene_Environment 側に居る。ビルドではサブシーンの
    /// ロードが非同期に後から走るため、Start の時点ではまだ存在しない。
    /// 見つかるまで毎フレーム試し続ける。
    /// </summary>
    private void TrySubscribeMoneyManager()
    {
        if (_subscribed) return;

        var money = MoneyManager.Instance;
        if (money == null)
        {
            WarnOnce("[TurnCounter3D] MoneyManager をまだ検出できません。" +
                     "サブシーンのロード待ちとみなして、見つかるまで待機します。");
            return;
        }

        money.OnCurrentTurnChange
            .Subscribe(value => SetValue(_currentTurn, value))
            .AddTo(this);

        money.OnNextDebtCollectionTurnChange
            .Subscribe(value => SetValue(_nextDebtTurn, value))
            .AddTo(this);

        _subscribed = true;

        if (_logEvents) Debug.Log("[TurnCounter3D] MoneyManager の購読を開始しました。", this);
    }

    private void OnEnable()
    {
        PlayIdle(true);
    }

    private void Update()
    {
        // 桁の大きさ・間隔・向きは調整しながら詰めたいので、変更を毎フレーム拾う
        ApplyLayoutIfChanged(_currentTurn);
        ApplyLayoutIfChanged(_nextDebtTurn);

        // サブシーンのロード完了を待って購読する（ビルドでは MainScene より後に来る）
        TrySubscribeMoneyManager();
        ApplyVisibility();

        ApplyLightSettings();

        if (_graphReady) AdvanceClipPlayback();
        else if (_keepIdlePlaying) PlayIdle(false);
    }

    private void LateUpdate()
    {
        // 位置・大きさを Play 中に調整できるよう毎フレーム反映する
        ApplyCameraPin();
    }

    /// <summary>指定の場所に値を表示する。桁数に合わせて 0 埋めする。</summary>
    private void SetValue(DigitField field, int value)
    {
        if (field == null) return;
        if (field.LastValue == value) return;

        field.LastValue = value;
        Rebuild(field, value);

        if (_logEvents) Debug.Log($"[TurnCounter3D] {FieldName(field)} = {value}（{field.DigitCount} 桁）", this);
    }

    private void Rebuild(DigitField field, int value)
    {
        ClearDigits(field);

        int count = field.DigitCount;
        if (count == 0)
        {
            WarnOnce("[TurnCounter3D] Digit Anchors が空です。桁ごとの置き場所を指定してください。");
            return;
        }

        int index = 0;
        foreach (int digit in ToDigits(value, count))
        {
            Transform anchor = field.digitAnchors[index];
            index++;

            // 使わない桁の Anchor を空欄にしておけば、その桁だけ非表示にできる
            if (anchor == null) continue;

            GameObject prefab = GetDigitPrefab(digit);
            if (prefab == null)
            {
                WarnOnce($"[TurnCounter3D] 数字「{digit}」のオブジェクトが未設定です。" +
                         "Digit Prefabs の要素を確認してください。");
                continue;
            }

            var instance = Instantiate(prefab, anchor);
            instance.name = $"Digit_{digit}";
            field.Spawned.Add(instance);
        }

        ApplyLayout(field);

        // 生成した数字にも専用ライトのビットを掛ける
        ApplyLightingLayer();

        // 生成し直した数字が、非表示中でも勝手に見えてしまわないようにする
        ApplyVisibility(true);
    }

    /// <summary>
    /// 値を桁ごとに分解する。桁数に足りない分は先頭を 0 で埋め、
    /// あふれた場合は下位の桁だけを残す（表示が崩れるより読める方を優先）。
    /// </summary>
    /// <summary>
    /// 他のゲーム UI と足並みを揃えて出し入れする。
    /// GameUIManager は イントロツアー終了（＝UFOキャッチャーのコインが降り終わった後）に
    /// 表示へ切り替わるので、そこへ追従させれば所持コイン表記などと同時に出る。
    /// ローディング中は GameUIManager がまだ居ない／非表示なので、カウンターも出ない。
    /// </summary>
    private void ApplyVisibility(bool force = false)
    {
        bool visible = true;
        if (_followGameUIVisibility)
        {
            var ui = GameUIManager.Instance;
            visible = ui != null && ui.IsGameUIVisible;
        }

        if (!force && _visibleApplied.HasValue && _visibleApplied.Value == visible) return;

        // SetActive で消すと Update が止まり、二度と復帰できなくなるので描画だけ止める
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null) renderer.enabled = visible;
        }

        if (_dedicatedLight != null) _dedicatedLight.enabled = visible;

        _visibleApplied = visible;

        if (_logEvents) Debug.Log($"[TurnCounter3D] 表示 = {visible}", this);
    }

    /// <summary>ログ用の呼び名。どちらの表示かが分かればよい。</summary>
    private string FieldName(DigitField field)
    {
        return field == _currentTurn ? "CurrentTurn" : "NextDebtTurn";
    }

    /// <summary>同じ警告でコンソールが埋まらないよう一度だけ出す。</summary>
    private void WarnOnce(string message)
    {
        if (!_warned.Add(message)) return;

        Debug.LogWarning(message, this);
    }

    private static IEnumerable<int> ToDigits(int value, int digitCount)
    {
        if (value < 0) value = 0;

        string text = value.ToString(new string('0', Mathf.Max(1, digitCount)));
        if (text.Length > digitCount) text = text.Substring(text.Length - digitCount);

        foreach (char c in text) yield return c - '0';
    }

    private GameObject GetDigitPrefab(int digit)
    {
        if (_digitPrefabs == null || digit < 0 || digit >= _digitPrefabs.Length) return null;

        return _digitPrefabs[digit];
    }

    /// <summary>大きさ・間隔・向きの設定が変わっていたら並べ直す。</summary>
    private void ApplyLayoutIfChanged(DigitField field)
    {
        if (field == null || field.Spawned.Count == 0) return;

        string signature = $"{field.digitScale}|{field.digitLocalEuler}";
        if (_appliedLayout.TryGetValue(field, out string applied) && applied == signature) return;

        ApplyLayout(field);
    }

    private void ApplyLayout(DigitField field)
    {
        for (int i = 0; i < field.Spawned.Count; i++)
        {
            var instance = field.Spawned[i];
            if (instance == null) continue;

            // 位置は Anchor そのものが決めるので、ここでは原点に揃えるだけ
            var t = instance.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.Euler(field.digitLocalEuler);
            t.localScale = field.digitScale;
        }

        _appliedLayout[field] = $"{field.digitScale}|{field.digitLocalEuler}";
    }

    private void ClearDigits(DigitField field)
    {
        foreach (var instance in field.Spawned)
        {
            if (instance != null) Destroy(instance);
        }

        field.Spawned.Clear();
        _appliedLayout.Remove(field);
    }

    /// <summary>
    /// Idle を回し続ける。歯車ごとに Animator がある構成なので全てに掛ける。
    /// ループ設定になっていないクリップでも、再生し切ったら掛け直して回し続ける。
    /// </summary>
    private void PlayIdle(bool forceRestart)
    {
        if (_animators != null)
        {
            for (int i = 0; i < _animators.Length; i++)
            {
                var animator = _animators[i];
                if (animator == null || !animator.isActiveAndEnabled) continue;

                if (animator.runtimeAnimatorController == null)
                {
                    WarnOnce($"[TurnCounter3D] '{animator.name}' に AnimatorController がありません。" +
                             "Idle Clip を割り当てるか、AnimatorController を作成してください。");
                    continue;
                }

                if (animator.speed <= 0f) animator.speed = 1f;

                // ステート名が無い/一致しない構成では既定ステートに任せる
                if (_animatorHasIdle == null || i >= _animatorHasIdle.Length || !_animatorHasIdle[i]) continue;

                var state = animator.GetCurrentAnimatorStateInfo(0);
                bool finished = !animator.IsInTransition(0) && state.normalizedTime >= 1f;

                if (forceRestart || !state.IsName(_idleStateName) || finished)
                {
                    animator.Play(_idleStateName, 0, 0f);
                }
            }
        }

        if (_legacyAnimations != null)
        {
            foreach (var animation in _legacyAnimations)
            {
                if (animation == null || !animation.isActiveAndEnabled) continue;
                if (animation.isPlaying && !forceRestart) continue;

                if (animation.clip != null) animation.wrapMode = WrapMode.Loop;
                animation.Play();
            }
        }
    }
}
