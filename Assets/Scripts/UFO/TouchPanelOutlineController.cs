using System.Collections;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// television の画面 (Play_Canvas) に重ねた TouchPanel > Play (30 / 60 / 90 / Quit の当たり判定用オブジェクト) へ
/// マウスレイキャストを行い、対応する Play_Canvas 側パネルの Outline 表示 / 非表示を切り替えます。
/// TouchPanel > Play の各子オブジェクトは Play_Canvas 上の UI と位置を揃えるための透明な当たり判定用オブジェクトで、
/// 実際に見せたい輪郭は Play_Canvas 側の Panel_30 / Panel_60 / Panel_90 / Quit&gt;Button にすでにアタッチされた
/// UnityEngine.UI.Outline を使う。
/// UFOキャッチャー再生中（画面が固定される間）のみ判定を行い、それ以外は常に非表示にする。
/// さらに 30 / 60 / 90 は左クリックで「選択」でき、選択音 → 砂嵐演出 → Play_Canvas2 への切り替えを行う。
/// ラウンド1のみ、TouchPanel > Tutorial (Yes / No) も同じ仕組みで判定する。Tutorial_Canvas の Yes/No は
/// Play_Canvas の Panel_30 等と同じ「透明な当たり判定 + Outline表示 + ホバー/選択SE」のパターンを踏襲しており、
/// Yes を選ぶと Practice_Cranegame 側のチュートリアル(TutorialCraneController)を開始し、
/// No を選ぶといつも通り Play_Canvas から開始する。
/// このスクリプトは TouchPanel オブジェクトにアタッチして使用する。
/// </summary>
public class TouchPanelOutlineController : MonoBehaviour
{
    [System.Serializable]
    private class TouchTarget
    {
        [Tooltip("TouchPanel 配下の当たり判定用オブジェクト。空欄なら touchAreaName から自動取得")]
        public Transform touchArea;

        [Tooltip("touchArea が未設定の場合に TouchPanel から検索するパス（例: 'Play/30'）")]
        public string touchAreaName;

        [Tooltip("表示を切り替える Outline コンポーネント。空欄なら playCanvasPath から自動取得")]
        public Outline outline;

        [Tooltip("outline が未設定の場合に Play_Canvas 直下から検索するパス（例: 'Panel_30' や 'Quit/Button'）")]
        public string playCanvasPath;

        [Tooltip("選択された時に Play_Canvas2 側のテキストへ反映する内容（30/60/90 のみ使用）")]
        [TextArea]
        public string resultText;

        [Tooltip("選択された時のプレイ時間（秒）。30/60/90 のみ使用し、Play_Canvas2 の Play クリック時に使われる")]
        public float durationSeconds;

        [Tooltip("選択された時の消費 Devil Coins。30/60/90 のみ使用し、Play_Canvas2 の Play クリック時に MoneyManager から引かれる")]
        public float durationCost;

        [Tooltip("true の場合、outline を Play_Canvas ではなく Play_Canvas2 側から検索する（Play2 グループ用）")]
        public bool useCanvas2;

        [Tooltip("true の場合、outline を Play_Canvas ではなく Tutorial_Canvas 側から検索する（Tutorial グループ用）")]
        public bool useTutorialCanvas;

        [Tooltip("true の場合ロック中として扱う。ホバー時のアウトラインが赤くなり、クリックしても選択できず拒否演出が出る")]
        public bool isLocked;

        [Tooltip("ロック中に表示するロックアイコン（例: Rock_60 / Rock_90）。空欄なら lockIndicatorPath から自動取得")]
        public GameObject lockIndicator;

        [Tooltip("lockIndicator が未設定の場合に Play_Canvas 直下から検索する名前（例: 'Rock_60'）")]
        public string lockIndicatorPath;

        [System.NonSerialized] public Color normalOutlineColor;
        [System.NonSerialized] public bool wasHovered;
    }

    [Header("マウスレイ発信元カメラ")]
    [Tooltip("null の場合 UFOCameraController の現在のアクティブカメラ（UFOプレイ中は frontCamera）を使用")]
    [SerializeField] private Camera rayCamera;

    [Tooltip("Raycast の最大距離 (m)")]
    [SerializeField] private float maxDistance = 50f;

    [Header("判定対象 (Play)")]
    [SerializeField] private TouchTarget touch30 = new TouchTarget { touchAreaName = "Play/30", playCanvasPath = "Panel_30", resultText = "              30秒\n1500 Devil Coins", durationSeconds = 30f, durationCost = 1500f };
    [SerializeField] private TouchTarget touch60 = new TouchTarget { touchAreaName = "Play/60", playCanvasPath = "Panel_60", resultText = "              60秒\n2500 Devil Coins", durationSeconds = 60f, durationCost = 2500f, isLocked = true, lockIndicatorPath = "Rock_60" };
    [SerializeField] private TouchTarget touch90 = new TouchTarget { touchAreaName = "Play/90", playCanvasPath = "Panel_90", resultText = "              90秒\n4000 Devil Coins", durationSeconds = 90f, durationCost = 4000f, isLocked = true, lockIndicatorPath = "Rock_90" };
    [SerializeField] private TouchTarget touchQuit = new TouchTarget { touchAreaName = "Play/Quit", playCanvasPath = "Quit/Button" };

    [Header("ロック中の選択 (拒否演出)")]
    [Tooltip("ロック中の 60 / 90 をホバーした時のアウトライン色")]
    [SerializeField] private Color lockedOutlineColor = new Color(1f, 0f, 0f, 0.5f);

    [Tooltip("拒否演出（UIが揺れる）の振幅")]
    [SerializeField] private float lockedShakeStrength = 12f;

    [Tooltip("拒否演出（UIが揺れる）の継続時間（秒）")]
    [SerializeField] private float lockedShakeDuration = 0.3f;

    [Header("判定対象 (Play2)")]
    [SerializeField] private TouchTarget touch2Play = new TouchTarget { touchAreaName = "Play2/Play", playCanvasPath = "Play", useCanvas2 = true };
    [SerializeField] private TouchTarget touch2Back = new TouchTarget { touchAreaName = "Play2/Back", playCanvasPath = "Back", useCanvas2 = true };

    [Header("判定対象 (Tutorial)")]
    [SerializeField] private TouchTarget touchYes = new TouchTarget { touchAreaName = "Tutorial/Yes", playCanvasPath = "Yes", useTutorialCanvas = true };
    [SerializeField] private TouchTarget touchNo = new TouchTarget { touchAreaName = "Tutorial/No", playCanvasPath = "No", useTutorialCanvas = true };

    [Header("判定対象 (Play_tutorial)")]
    [Tooltip("Play と同じ構造で複製した、チュートリアル用の Play_Canvas 判定グループ。表示される Canvas 自体は Play_Canvas を使い回す")]
    [SerializeField] private TouchTarget touchQuitTutorial = new TouchTarget { touchAreaName = "Play_tutorial/Quit", playCanvasPath = "Quit/Button" };

    [Tooltip("チュートリアルでは 30 のみプレイ可能。60/90 はラウンド数やタイプライターでの解禁に関わらず常にロックのままにする")]
    [SerializeField] private TouchTarget touch30Tutorial = new TouchTarget { touchAreaName = "Play_tutorial/30", playCanvasPath = "Panel_30", resultText = "              30秒\n1500 Devil Coins", durationSeconds = 30f, durationCost = 1500f };
    [SerializeField] private TouchTarget touch60Tutorial = new TouchTarget { touchAreaName = "Play_tutorial/60", playCanvasPath = "Panel_60", durationSeconds = 60f, durationCost = 2500f, isLocked = true, lockIndicatorPath = "Rock_60" };
    [SerializeField] private TouchTarget touch90Tutorial = new TouchTarget { touchAreaName = "Play_tutorial/90", playCanvasPath = "Panel_90", durationSeconds = 90f, durationCost = 4000f, isLocked = true, lockIndicatorPath = "Rock_90" };

    [Header("判定対象 (Play2_tutorial)")]
    [Tooltip("Play2 と同じ構造で複製した、チュートリアル用の Play_Canvas2 判定グループ。表示される Canvas 自体は Play_Canvas2 を使い回す")]
    [SerializeField] private TouchTarget touch2PlayTutorial = new TouchTarget { touchAreaName = "Play2_tutorial/Play", playCanvasPath = "Play", useCanvas2 = true };
    [SerializeField] private TouchTarget touch2BackTutorial = new TouchTarget { touchAreaName = "Play2_tutorial/Back", playCanvasPath = "Back", useCanvas2 = true };

    [Header("チュートリアル連携")]
    [Tooltip("Yes 選択時にチュートリアルを開始する Practice_Cranegame 側のコントローラー")]
    [SerializeField] private TutorialCraneController tutorialCrane;

    [Tooltip("Play_tutorial の Play_Canvas が表示された瞬間に操作説明のステップ演出を開始するコントローラー（任意）")]
    [SerializeField] private TutorialStepController tutorialStepController;

    [Header("選択 (クリック) → Play_Canvas2 遷移")]
    [Tooltip("選択結果（秒数 / Devil Coins）を表示する Play_Canvas2 側のテキスト。未設定なら playCanvas2TextPath から自動取得")]
    [SerializeField] private TextMeshProUGUI play2ResultText;

    [Tooltip("play2ResultText が未設定の場合に Play_Canvas2 直下から検索する名前")]
    [SerializeField] private string playCanvas2TextPath = "Text (TMP)";

    private enum PanelGroup { Tutorial, Play, Play2, PlayTutorial, Play2Tutorial }

    private TouchTarget[] _targets;
    private TouchTarget[] _selectableTargets;
    private TouchTarget[] _selectableTutorialTargets;
    private TelevisionStaticController _tvController;
    private Transform _tutorialGroup;
    private Transform _playGroup;
    private Transform _play2Group;
    private Transform _playTutorialGroup;
    private Transform _play2TutorialGroup;
    private bool _selectionTriggered;
    private float _selectedDurationSeconds = 30f;
    private float _selectedCost;
    private float _selectedTutorialDurationSeconds = 30f;
    private float _selectedTutorialCost;
    private bool _tutorialPending;
    private bool _hasStartedPlaySession;
    // チュートリアルを最後まで見終えた（Quitで途中終了ではなく）ことがあれば、
    // ラウンド1中に機体を何度クリックし直してもTutorial_Canvas（Yes/No）を出さない
    private bool _hasCompletedTutorialOnce;

    private void Awake()
    {
        _targets = new[]
        {
            touch30, touch60, touch90, touchQuit, touch2Play, touch2Back, touchYes, touchNo,
            touchQuitTutorial, touch30Tutorial, touch60Tutorial, touch90Tutorial,
            touch2PlayTutorial, touch2BackTutorial
        };
        _selectableTargets = new[] { touch30, touch60, touch90 };
        _selectableTutorialTargets = new[] { touch30Tutorial, touch60Tutorial, touch90Tutorial };

        _tvController = FindAnyObjectByType<TelevisionStaticController>();
        Canvas playCanvas = _tvController != null ? _tvController.PlayCanvas : null;
        Canvas playCanvas2 = _tvController != null ? _tvController.PlayCanvas2 : null;
        Canvas tutorialCanvas = _tvController != null ? _tvController.TutorialCanvas : null;

        if (play2ResultText == null && playCanvas2 != null && !string.IsNullOrEmpty(playCanvas2TextPath))
        {
            Transform textTarget = playCanvas2.transform.Find(playCanvas2TextPath);
            if (textTarget != null)
            {
                play2ResultText = textTarget.GetComponent<TextMeshProUGUI>();
            }
        }

        foreach (var t in _targets)
        {
            if (t.touchArea == null && !string.IsNullOrEmpty(t.touchAreaName))
            {
                t.touchArea = transform.Find(t.touchAreaName);
            }
            EnsureCollider(t.touchArea);

            Canvas searchCanvas = t.useTutorialCanvas ? tutorialCanvas : (t.useCanvas2 ? playCanvas2 : playCanvas);
            if (t.outline == null && searchCanvas != null && !string.IsNullOrEmpty(t.playCanvasPath))
            {
                Transform target = searchCanvas.transform.Find(t.playCanvasPath);
                if (target != null)
                {
                    t.outline = target.GetComponent<Outline>();
                }
            }

            // 開始時は非表示にしておく（色は通常時の色として記憶しておき、ロック解除時に復元する）
            if (t.outline != null)
            {
                t.normalOutlineColor = t.outline.effectColor;
                t.outline.enabled = false;
            }

            if (t.lockIndicator == null && playCanvas != null && !string.IsNullOrEmpty(t.lockIndicatorPath))
            {
                Transform li = playCanvas.transform.Find(t.lockIndicatorPath);
                if (li != null)
                {
                    t.lockIndicator = li.gameObject;
                }
            }
            if (t.lockIndicator != null)
            {
                t.lockIndicator.SetActive(t.isLocked);
            }
        }

        // TouchPanel > Play / Play2 / Tutorial / Play_tutorial / Play2_tutorial の当たり判定グループ。最初は Play のみ有効にする
        // （ラウンド1でまだプレイしていない場合は、UFOモードに入るたびに HandleUfoModeEntered が Tutorial に切り替える）
        _tutorialGroup = transform.Find("Tutorial");
        _playGroup = transform.Find("Play");
        _play2Group = transform.Find("Play2");
        _playTutorialGroup = transform.Find("Play_tutorial");
        _play2TutorialGroup = transform.Find("Play2_tutorial");
        SetActiveGroup(PanelGroup.Play);

        if (tutorialCrane == null)
        {
            tutorialCrane = FindAnyObjectByType<TutorialCraneController>();
        }
        if (tutorialStepController == null)
        {
            tutorialStepController = FindAnyObjectByType<TutorialStepController>();
        }
    }

    private void Start()
    {
        // MoneyManager は加法ロードされる別サブシーン側にいるため、MultiSceneLoader の非同期ロードが
        // 終わるまで Instance が null の可能性がある。Instance が現れるまで毎フレーム待ってから購読する。
        Observable.EveryUpdate()
            .Select(_ => MoneyManager.Instance)
            .Where(mm => mm != null)
            .First()
            .Subscribe(mm =>
            {
                // 現在のターン数、および以降のターン変化の両方をチェックする（Skip(1)はしない）
                // ラウンド2（MoneyManager のターン2）に到達したら、60秒を自動解禁する（90秒解禁と同じ UnlockDuration を使用）。
                mm.OnCurrentTurnChange
                    .Subscribe(turn =>
                    {
                        if (turn >= 2)
                        {
                            UnlockDuration(60f);
                        }
                    })
                    .AddTo(this);
            })
            .AddTo(this);

        // tutorialCrane / tutorialStepController も加法ロードされる別サブシーン側にいるため、
        // MultiSceneLoader の非同期ロードが終わるまで Awake() 時点では見つからない可能性がある。
        // 見つかるまで毎フレーム待ってから、イベント購読（OnEnable が Awake 直後で間に合わなかった分）と
        // 参照解決を行う。既に Awake/OnEnable で解決済み（サブシーンをエディタで開いたまま再生した場合）なら
        // 何もしない。
        Observable.EveryUpdate()
            .Select(_ => tutorialCrane != null ? tutorialCrane : FindAnyObjectByType<TutorialCraneController>())
            .Where(tc => tc != null)
            .First()
            .Subscribe(tc =>
            {
                if (tutorialCrane == null)
                {
                    tutorialCrane = tc;
                    tutorialCrane.OnTutorialEntered += HandleTutorialEntered;
                    tutorialCrane.OnTutorialFinished += HandleTutorialFinished;
                    tutorialCrane.OnTutorialCompleted += HandleTutorialCompleted;
                }
                if (tutorialStepController == null)
                {
                    tutorialStepController = FindAnyObjectByType<TutorialStepController>();
                }
            })
            .AddTo(this);
    }

    private void OnEnable()
    {
        UFOCameraController.OnUfoModeChanged += HandleUfoModeEntered;
        if (tutorialCrane != null)
        {
            tutorialCrane.OnTutorialEntered += HandleTutorialEntered;
            tutorialCrane.OnTutorialFinished += HandleTutorialFinished;
            tutorialCrane.OnTutorialCompleted += HandleTutorialCompleted;
        }
    }

    /// <summary>
    /// UFOモードに入った（machine をクリックしてカメラが到着した）瞬間に呼ばれる。
    /// ラウンド1でまだ実際にプレイしていない間は、Play_Canvas を経由せずその場で直接
    /// Tutorial_Canvas に切り替える（砂嵐演出なし）ことで、機体を再度クリックするたびに
    /// 「プレイするまでは毎回チュートリアルの Yes/No から始まる」ようにする。
    /// </summary>
    private void HandleUfoModeEntered(bool active)
    {
        if (!active) return;
        if (_hasStartedPlaySession) return;
        if (_hasCompletedTutorialOnce) return;
        if (MoneyManager.Instance == null || MoneyManager.Instance.CurrentTurnCount > 1) return;

        _tutorialPending = true;
        SetActiveGroup(PanelGroup.Tutorial);

        if (_tvController != null)
        {
            _tvController.ShowTutorialCanvas();
        }
    }


    /// <summary>
    /// TouchPanel の当たり判定用オブジェクトに Collider がなければ、SpriteRenderer のサイズに合わせて自動付与する。
    /// </summary>
    private static void EnsureCollider(Transform area)
    {
        if (area == null) return;
        if (area.GetComponent<Collider>() != null) return;

        BoxCollider box = area.gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;

        SpriteRenderer sr = area.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            Bounds b = sr.sprite.bounds;
            box.center = b.center;
            box.size = new Vector3(b.size.x, b.size.y, Mathf.Max(b.size.z, 0.05f));
        }
    }

    private void Update()
    {
        if (!UFOCameraController.IsPlayingUfo)
        {
            HideAll();
            _selectionTriggered = false; // 次回プレイ開始時にまた選択できるようにリセット
            // チュートリアルの Yes/No 未回答（ラウンド1）の間は Tutorial 側の当たり判定を維持する
            SetActiveGroup(_tutorialPending ? PanelGroup.Tutorial : PanelGroup.Play);
            return;
        }

        // チュートリアルのステップ演出側がブロックを指定している間は、television操作を受け付けない
        // （当たり判定はPhysics.Raycast直判定でUI EventSystemを経由しないため、ここで自前にスキップする）
        if (tutorialStepController != null && tutorialStepController.IsBlockingInteraction)
        {
            HideAll();
            return;
        }

        Camera cam = rayCamera != null ? rayCamera
            : (UFOCameraController.Instance != null ? UFOCameraController.Instance.GetActiveCamera() : Camera.main);

        if (cam == null || Mouse.current == null)
        {
            HideAll();
            return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(mousePos);

        // isTrigger の当たり判定用 Collider を使うため、プロジェクト設定に関わらず必ずヒットさせる
        Transform hitArea = null;
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, ~0, QueryTriggerInteraction.Collide))
        {
            hitArea = hit.collider.transform;
        }

        // Play_Canvas が表示されている間のみ 30/60/90 の選択を受け付ける（Play_Canvas2 遷移後は再トリガーしない）
        bool canSelect = !_selectionTriggered && _tvController != null
            && _tvController.PlayCanvas != null && _tvController.PlayCanvas.gameObject.activeSelf;

        // Play_Canvas2 が表示されている間のみ Play2 の Play / Back の選択を受け付ける。
        // Play_Canvas2 はPlay2_tutorialとも共有（使い回し）しているため、Canvas表示だけでなく
        // 実機側の当たり判定グループ（_play2Group）が有効かどうかも見て、チュートリアル中は
        // 実機側のEnterキー処理（HandlePlay2PlayClicked）が誤って発火しないようにする
        bool canSelectPlay2 = _tvController != null
            && _tvController.PlayCanvas2 != null && _tvController.PlayCanvas2.gameObject.activeSelf
            && _play2Group != null && _play2Group.gameObject.activeSelf;

        // Tutorial_Canvas が表示されている間のみ Yes / No の選択を受け付ける（ラウンド1、未回答の間だけ）
        bool canSelectTutorial = _tutorialPending && _tvController != null
            && _tvController.TutorialCanvas != null && _tvController.TutorialCanvas.gameObject.activeSelf;

        // Play_tutorial グループが有効な間のみ 30/60/90 の選択を受け付ける
        bool canSelectPlayTutorial = _playTutorialGroup != null && _playTutorialGroup.gameObject.activeSelf;

        // Play2_tutorial グループが有効な間のみ Play / Back の選択を受け付ける
        bool canSelectPlay2Tutorial = _play2TutorialGroup != null && _play2TutorialGroup.gameObject.activeSelf;

        bool leftClicked = Mouse.current.leftButton.wasPressedThisFrame;

        foreach (var t in _targets)
        {
            // touchQuit (Play) と touchQuitTutorial (Play_tutorial) のように、
            // 同じ Play_Canvas 上の Outline を複数の TouchTarget が共有しているケースがある。
            // 自分の当たり判定グループが非表示（無効）の間は、他グループが今まさに点けた
            // 共有 Outline を誤って消してしまわないよう、判定・書き込みごとスキップする。
            bool areaActive = t.touchArea != null && t.touchArea.gameObject.activeInHierarchy;
            bool isHovered = areaActive && hitArea == t.touchArea;

            if (t.outline != null && areaActive)
            {
                t.outline.enabled = isHovered;
                if (isHovered)
                {
                    t.outline.effectColor = t.isLocked ? lockedOutlineColor : t.normalOutlineColor;
                }
            }

            // ホバー開始の瞬間（未ホバー → ホバー）にだけ SE を鳴らす
            if (isHovered && !t.wasHovered)
            {
                UFOSEManager.Instance?.PlayTouchHover();
            }
            t.wasHovered = isHovered;
        }

        if (leftClicked)
        {
            if (canSelect)
            {
                foreach (var t in _selectableTargets)
                {
                    if (t.touchArea != null && hitArea == t.touchArea)
                    {
                        if (t.isLocked)
                        {
                            HandleLockedClicked(t);
                        }
                        else
                        {
                            HandleSelected(t);
                        }
                        break;
                    }
                }
            }

            if (canSelectPlayTutorial)
            {
                foreach (var t in _selectableTutorialTargets)
                {
                    if (t.touchArea != null && hitArea == t.touchArea)
                    {
                        if (t.isLocked)
                        {
                            HandleLockedClicked(t);
                        }
                        else
                        {
                            HandleTutorialDurationSelected(t);
                        }
                        break;
                    }
                }
            }

            if (touchQuit.touchArea != null && hitArea == touchQuit.touchArea)
            {
                HandleQuitClicked();
            }

            if (touchQuitTutorial.touchArea != null && hitArea == touchQuitTutorial.touchArea)
            {
                HandlePlayTutorialQuitClicked();
            }

            if (canSelectPlay2)
            {
                if (touch2Back.touchArea != null && hitArea == touch2Back.touchArea)
                {
                    HandlePlay2BackClicked();
                }
                else if (touch2Play.touchArea != null && hitArea == touch2Play.touchArea)
                {
                    HandlePlay2PlayClicked();
                }
            }

            if (canSelectPlay2Tutorial)
            {
                if (touch2BackTutorial.touchArea != null && hitArea == touch2BackTutorial.touchArea)
                {
                    HandlePlay2TutorialBackClicked();
                }
                else if (touch2PlayTutorial.touchArea != null && hitArea == touch2PlayTutorial.touchArea)
                {
                    HandlePlay2TutorialPlayClicked();
                }
            }

            if (canSelectTutorial)
            {
                if (touchYes.touchArea != null && hitArea == touchYes.touchArea)
                {
                    HandleTutorialYesClicked();
                }
                else if (touchNo.touchArea != null && hitArea == touchNo.touchArea)
                {
                    if (touchNo.isLocked)
                    {
                        HandleLockedClicked(touchNo);
                    }
                    else
                    {
                        HandleTutorialNoClicked();
                    }
                }
            }
        }

        // Play_Canvas2 表示中は、Enter キーでもマウスクリックと同じ Play を実行できるようにする
        if (canSelectPlay2 && Keyboard.current != null &&
            (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
        {
            HandlePlay2PlayClicked();
        }

        // Play2_tutorial 表示中も同様に Enter キーで Play を実行できるようにする
        if (canSelectPlay2Tutorial && Keyboard.current != null &&
            (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
        {
            HandlePlay2TutorialPlayClicked();
        }
    }

    /// <summary>
    /// Play_Canvas 上の Quit が左クリックされた時の処理。UFOキャッチャーモードを終了する。
    /// </summary>
    private void HandleQuitClicked()
    {
        HideAll();

        if (UFOCameraController.Instance != null)
        {
            UFOCameraController.Instance.RequestExitUfoMode();
        }
    }

    /// <summary>
    /// 30 / 60 / 90 のいずれかが左クリックで選択された時の処理。
    /// Play_Canvas2 側のテキストを更新し、選択 SE を鳴らし、アウトラインを消し、
    /// 当たり判定グループを Play → Play2 に切り替えたうえで、砂嵐演出を挟んで Play_Canvas2 へ切り替える。
    /// </summary>
    private void HandleSelected(TouchTarget target)
    {
        _selectionTriggered = true;
        _selectedDurationSeconds = target.durationSeconds;
        _selectedCost = target.durationCost;

        if (play2ResultText != null)
        {
            play2ResultText.text = target.resultText;
        }

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.ShowMoneyCountPreview(target.durationCost);
        }

        UFOSEManager.Instance?.PlayTouchSelect();
        HideAll();
        SetActiveGroup(PanelGroup.Play2);

        if (_tvController != null)
        {
            _tvController.PlayStaticThenShowCanvas(_tvController.PlayCanvas2);
        }
    }

    /// <summary>
    /// Play_tutorial の 30（60/90 は常にロックのため実際にはここへは来ない）が左クリックされた時の処理。
    /// 実機の HandleSelected と同じく、Play_Canvas2 側のテキスト更新・MoneyCount プレビュー・選択 SE を行い、
    /// 判定グループを Play_tutorial → Play2_tutorial に切り替えたうえで、砂嵐演出を挟んで Play_Canvas2（使い回し）へ切り替える。
    /// </summary>
    private void HandleTutorialDurationSelected(TouchTarget target)
    {
        // Play_tutorialの30が選ばれた瞬間。チュートリアルステップ側が「操作待ち」の場合のみ
        // NotifyStepActionPerformed 側で進行を判断する
        if (target == touch30Tutorial && tutorialStepController != null)
        {
            tutorialStepController.NotifyStepActionPerformed();
        }

        _selectedTutorialDurationSeconds = target.durationSeconds;
        _selectedTutorialCost = target.durationCost;

        if (play2ResultText != null)
        {
            play2ResultText.text = target.resultText;
        }

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.ShowMoneyCountPreview(target.durationCost);
        }

        UFOSEManager.Instance?.PlayTouchSelect();
        HideAll();
        SetActiveGroup(PanelGroup.Play2Tutorial);

        if (_tvController != null)
        {
            _tvController.PlayStaticThenShowCanvas(_tvController.PlayCanvas2);
        }
    }

    /// <summary>
    /// Tutorial_Canvas の Yes が左クリックされた時の処理。
    /// Tutorial_Canvas を閉じ、TutorialCraneController のローディング演出（+ 瞬間移動）を開始する。
    /// 移動が完了すると HandleTutorialEntered が呼ばれ、そこで Play_Canvas（Play_tutorial 判定）を表示する。
    /// </summary>
    private void HandleTutorialYesClicked()
    {
        UFOSEManager.Instance?.PlayTouchSelect();
        HideAll();

        if (_tvController != null && _tvController.TutorialCanvas != null)
        {
            _tvController.TutorialCanvas.gameObject.SetActive(false);
        }

        if (tutorialCrane != null)
        {
            tutorialCrane.EnterTutorial();
        }
    }

    /// <summary>
    /// TutorialCraneController のローディング演出（カメラ・television の瞬間移動）が完了した瞬間に呼ばれる。
    /// 移動が終わった状態の television に、砂嵐演出なしで Play_Canvas（Play_tutorial 判定）を表示する。
    /// </summary>
    private void HandleTutorialEntered()
    {
        SetActiveGroup(PanelGroup.PlayTutorial);

        // touch60/90Tutorial は実機の touch60/90 と同じ Rock_60/90 アイコンを共有しているため、
        // 実機側が解禁済み（Rock非表示）でも、チュートリアルでは常にロック中の見た目に戻す
        RefreshLockIndicators(_selectableTutorialTargets);

        if (_tvController != null)
        {
            _tvController.ShowPlayCanvas();
        }

        if (tutorialStepController != null)
        {
            tutorialStepController.StartTutorialSteps();
        }
    }

    /// <summary>
    /// Play_tutorial の Quit が左クリックされた時の処理。
    /// 実機の Quit（UFOモード自体を終了）とは異なり、TutorialCraneController.ExitTutorial() でカメラ・television を
    /// 元の位置へ戻す（すらーぷ）。戻り終わったら HandleTutorialFinished が Tutorial_Canvas の Yes/No を表示する。
    /// </summary>
    private void HandlePlayTutorialQuitClicked()
    {
        UFOSEManager.Instance?.PlayTouchSelect();
        HideAll();

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.ClearMoneyCountPreview();
        }

        if (tutorialStepController != null)
        {
            tutorialStepController.StopTutorialSteps();
        }

        if (tutorialCrane != null)
        {
            tutorialCrane.ExitTutorial();
        }
    }

    /// <summary>
    /// TutorialCraneController の終了処理（カメラ・television が元の位置へ戻り終わった瞬間）に呼ばれる。
    /// Play_tutorial の Quit で途中終了した場合、television のチュートリアル Yes/No 画面を表示し直す。
    /// </summary>
    private void HandleTutorialFinished()
    {
        _tutorialPending = true;
        SetActiveGroup(PanelGroup.Tutorial);

        if (_tvController != null)
        {
            _tvController.ShowTutorialCanvas();
        }
    }

    /// <summary>
    /// チュートリアルのステップ演出を最後まで見終えて終了した場合に呼ばれる。
    /// Tutorial_Canvas（Yes/No）には戻さず、いつも通り実機の Play_Canvas を直接表示する
    /// （Tutorial_Canvas の No を選んだ時と同じ処理）。
    /// </summary>
    private void HandleTutorialCompleted()
    {
        _tutorialPending = false;
        _hasCompletedTutorialOnce = true;

        HideAll();
        SetActiveGroup(PanelGroup.Play);
        RefreshLockIndicators(_selectableTargets);

        if (_tvController != null)
        {
            _tvController.PlayStaticThenShowCanvas(_tvController.PlayCanvas);
        }
    }

    /// <summary>
    /// Tutorial_Canvas の No が左クリックされた時の処理。
    /// 選択 SE を鳴らし、通常通り Play_Canvas を表示する。
    /// </summary>
    private void HandleTutorialNoClicked()
    {
        _tutorialPending = false;

        UFOSEManager.Instance?.PlayTouchSelect();
        HideAll();
        SetActiveGroup(PanelGroup.Play);
        RefreshLockIndicators(_selectableTargets);

        if (_tvController != null)
        {
            _tvController.PlayStaticThenShowCanvas(_tvController.PlayCanvas);
        }
    }

    /// <summary>
    /// ロック中（60/90）の項目がクリックされた時の処理。選択はさせず、拒否 SE と揺れ演出のみ行う。
    /// </summary>
    private void HandleLockedClicked(TouchTarget target)
    {
        if (target == touchNo)
        {
            UFOSEManager.Instance?.PlayTouchNoLocked();
        }
        else
        {
            UFOSEManager.Instance?.PlayTouchLocked();
        }

        if (target.outline != null)
        {
            RectTransform rt = target.outline.GetComponent<RectTransform>();
            StartCoroutine(ShakeUI(rt));
        }
    }

    private IEnumerator ShakeUI(RectTransform rt)
    {
        if (rt == null) yield break;

        Vector2 originalPos = rt.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < lockedShakeDuration)
        {
            elapsed += Time.deltaTime;
            float damper = 1f - Mathf.Clamp01(elapsed / lockedShakeDuration);
            float offsetX = Random.Range(-1f, 1f) * lockedShakeStrength * damper;
            rt.anchoredPosition = originalPos + new Vector2(offsetX, 0f);
            yield return null;
        }
        rt.anchoredPosition = originalPos;
    }

    /// <summary>
    /// 指定した秒数（30/60/90）のロックを解除する。解除条件は別途実装予定のため、
    /// 現時点では外部（今後追加する解禁ロジック）からこのメソッドを呼び出すことで手動解除できるようにしておく。
    /// </summary>
    public void UnlockDuration(float durationSeconds)
    {
        foreach (var t in _selectableTargets)
        {
            if (!Mathf.Approximately(t.durationSeconds, durationSeconds)) continue;

            t.isLocked = false;
            if (t.lockIndicator != null) t.lockIndicator.SetActive(false);
            if (t.outline != null) t.outline.effectColor = t.normalOutlineColor;
        }
    }

    /// <summary>
    /// 指定したターゲット群の isLocked 状態を、対応するロックアイコン（Rock_60/90 等）へ再適用する。
    /// touch60/90（実機）と touch60/90Tutorial は同じアイコンを共有しているため、
    /// Play ⇔ Play_tutorial のグループ切り替え時に、切り替え先の isLocked を見た目にも反映させ直す必要がある。
    /// </summary>
    private static void RefreshLockIndicators(TouchTarget[] targets)
    {
        if (targets == null) return;
        foreach (var t in targets)
        {
            if (t.lockIndicator != null) t.lockIndicator.SetActive(t.isLocked);
        }
    }

    /// <summary>
    /// Play_Canvas2 の Back が左クリックされた時の処理。Esc/F キーで戻る場合と同じ処理を行う。
    /// </summary>
    private void HandlePlay2BackClicked()
    {
        UFOSEManager.Instance?.PlayTouchSelect2();
        ReturnToPlay1();
    }

    /// <summary>
    /// Play_Canvas2 の Play が左クリックされた時の処理。選択済みの秒数・Devil Coins で UFOキャッチャーの
    /// プレイセッションを開始する。所持金が足りない場合は開始せず拒否演出のみ行う。
    /// </summary>
    private void HandlePlay2PlayClicked()
    {
        if (UFOCameraController.Instance == null) return;

        if (MoneyManager.Instance != null && MoneyManager.Instance.CurrentMoney < _selectedCost)
        {
            HandlePlay2PlayRejected();
            return;
        }

        bool started = UFOCameraController.Instance.StartPlaySessionFromTelevision(_selectedDurationSeconds, _selectedCost);
        if (started)
        {
            _hasStartedPlaySession = true;

            UFOSEManager.Instance?.PlayTouchSelect2();
            HideAll();

            // プレイ開始によりメニュー操作は終了するため、Play / Play2 どちらの当たり判定も無効化する
            if (_playGroup != null) _playGroup.gameObject.SetActive(false);
            if (_play2Group != null) _play2Group.gameObject.SetActive(false);
        }
        else
        {
            HandlePlay2PlayRejected();
        }
    }

    /// <summary>
    /// Devil Coins 不足などでプレイを開始できなかった時の拒否演出（ロック中クリック時と同じ SE + 揺れを流用）。
    /// </summary>
    private void HandlePlay2PlayRejected()
    {
        UFOSEManager.Instance?.PlayTouchLocked();

        if (touch2Play.outline != null)
        {
            RectTransform rt = touch2Play.outline.GetComponent<RectTransform>();
            StartCoroutine(ShakeUI(rt));
        }
    }

    /// <summary>
    /// Play2_tutorial の Back が左クリックされた時の処理。実機の Play2 Back と同様。
    /// </summary>
    private void HandlePlay2TutorialBackClicked()
    {
        UFOSEManager.Instance?.PlayTouchSelect2();
        ReturnToPlayTutorial();
    }

    /// <summary>
    /// Play2_tutorial の Play が左クリックされた時の処理。
    /// 実機と異なり Devil Coins の決済は行わない（チュートリアルは無料）。TutorialCraneController は
    /// 「はい」を押した時点で練習機の場所へ移動済みだが、レバー/ボタン操作はまだ解禁していないため、
    /// ここで BeginTutorialPlay() を呼んで初めて操作可能にし、Play_Canvas2（使い回し）のメニュー表示を閉じる。
    /// </summary>
    private void HandlePlay2TutorialPlayClicked()
    {
        // チュートリアルステップ側が「操作待ち」の場合のみ NotifyStepActionPerformed 側で進行を判断する
        if (tutorialStepController != null)
        {
            tutorialStepController.NotifyStepActionPerformed();
        }

        UFOSEManager.Instance?.PlayTouchSelect2();
        HideAll();

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.ClearMoneyCountPreview();
        }

        // メニュー操作は終了するため、Play_tutorial / Play2_tutorial どちらの当たり判定も無効化する
        if (_playTutorialGroup != null) _playTutorialGroup.gameObject.SetActive(false);
        if (_play2TutorialGroup != null) _play2TutorialGroup.gameObject.SetActive(false);

        if (_tvController != null && _tvController.PlayCanvas2 != null)
        {
            _tvController.PlayCanvas2.gameObject.SetActive(false);
        }

        if (tutorialCrane != null)
        {
            tutorialCrane.BeginTutorialPlay();
        }
    }

    /// <summary>
    /// 砂嵐演出を挟んで Play_Canvas2 から Play_Canvas へ戻る。
    /// Esc/F キー（UFOCameraController.HandleUfoInput）と Play2 の Back クリックの両方から呼ばれる。
    /// </summary>
    public void ReturnToPlay1()
    {
        if (_tvController == null) return;

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.ClearMoneyCountPreview();
        }

        HideAll();
        SetActiveGroup(PanelGroup.Play);
        RefreshLockIndicators(_selectableTargets);
        _selectionTriggered = false;
        _tvController.PlayStaticThenShowCanvas(_tvController.PlayCanvas);
    }

    /// <summary>
    /// 砂嵐演出を挟んで Play_Canvas2（使い回し）から Play_Canvas（Play_tutorial 判定）へ戻る。
    /// Play2_tutorial の Back クリックから呼ばれる。
    /// </summary>
    private void ReturnToPlayTutorial()
    {
        if (_tvController == null) return;

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.ClearMoneyCountPreview();
        }

        HideAll();
        SetActiveGroup(PanelGroup.PlayTutorial);
        RefreshLockIndicators(_selectableTutorialTargets);

        _tvController.PlayStaticThenShowCanvas(_tvController.PlayCanvas);
    }

    /// <summary>
    /// TouchPanel 配下の Tutorial / Play / Play2 / Play_tutorial / Play2_tutorial 当たり判定グループの有効・無効を切り替える。
    /// </summary>
    private void SetActiveGroup(PanelGroup group)
    {
        if (_tutorialGroup != null) _tutorialGroup.gameObject.SetActive(group == PanelGroup.Tutorial);
        if (_playGroup != null) _playGroup.gameObject.SetActive(group == PanelGroup.Play);
        if (_play2Group != null) _play2Group.gameObject.SetActive(group == PanelGroup.Play2);
        if (_playTutorialGroup != null) _playTutorialGroup.gameObject.SetActive(group == PanelGroup.PlayTutorial);
        if (_play2TutorialGroup != null) _play2TutorialGroup.gameObject.SetActive(group == PanelGroup.Play2Tutorial);
    }

    private void OnDisable()
    {
        UFOCameraController.OnUfoModeChanged -= HandleUfoModeEntered;
        if (tutorialCrane != null)
        {
            tutorialCrane.OnTutorialEntered -= HandleTutorialEntered;
            tutorialCrane.OnTutorialFinished -= HandleTutorialFinished;
            tutorialCrane.OnTutorialCompleted -= HandleTutorialCompleted;
        }
        HideAll();
    }

    private void HideAll()
    {
        if (_targets == null) return;
        foreach (var t in _targets)
        {
            if (t.outline != null && t.outline.enabled)
            {
                t.outline.enabled = false;
            }
            t.wasHovered = false;
        }
    }
}
