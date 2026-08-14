using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// チュートリアル中に、画面を暗転させつつ操作対象（UI要素 or 3Dオブジェクト）だけを枠で囲んでハイライトし、
/// 説明文を表示していくステップ演出。ゲーム崩壊スターレール等のチュートリアル演出と同じ考え方で、
/// 専用シェーダーは使わず、暗転パネルを上下左右4枚に分割して対象の周りだけ切り抜く方式で実現する。
///
/// 使い方:
/// 1. steps に表示したいステップを順番に登録する（対象 + 説明文 + 進行方式）。
/// 2. 外部（TouchPanelOutlineController.HandleTutorialEntered 等）から StartTutorialSteps() を呼ぶ。
/// 3. 「実際の操作待ち」のステップでは、対応する操作を検知した側から NotifyStepActionPerformed() を呼ぶ。
/// </summary>
public class TutorialStepController : MonoBehaviour
{
    /// <summary>このステップ中にクレーンの入力（レバー/カメラQE/ボタン）をどう制限するか</summary>
    private enum CraneInputMode
    {
        [InspectorName("制限なし（全て操作可）")] AllAllowed,
        [InspectorName("全て禁止")] AllBlocked,
        [InspectorName("レバーのみ操作可")] LeverOnly,
        [InspectorName("カメラ切り替え(Q/E)のみ操作可")] CameraOnly,
        [InspectorName("Start Descentボタンのみ操作可")] StartDescentOnly,
        [InspectorName("Toggle Clawボタンのみ操作可")] ToggleClawOnly,
        [InspectorName("キー「3」（television収納/復元）のみ操作可")] Key3Only,
    }

    [System.Serializable]
    private class TutorialStep
    {
        [Tooltip("ハイライトするUI要素。UIが対象の場合はこちらを設定する。" +
                 "targetUIIsMoneyCount=ONの場合はこちらは無視される")]
        public RectTransform targetUI;

        [Tooltip("ON: targetUIをInspectorで直接設定する代わりに、GameUIManagerの通常のMoneyCount表示" +
                 "（_moneyText）を実行時に自動取得する。Inspectorでドラッグしようとするとmismatchになる場合はこちらを使う")]
        public bool targetUIIsMoneyCount = false;

        [Tooltip("2つ目のUI要素（任意）。設定すると targetUI とこの要素の両方を含む最小の矩形を、" +
                 "1つの枠でまとめてハイライトする（例: MoneyCountと差し引かれるMoneyCountを同時に囲む）。" +
                 "targetUI2IsMoneyCountPreview=ONの場合はこちらは無視される")]
        public RectTransform targetUI2;

        [Tooltip("ON: targetUI2をInspectorで直接設定する代わりに、GameUIManagerの「差し引き後MoneyCount」表示" +
                 "（playerInfoPanel内のmoneyText_info）を実行時に自動取得する。Play_Canvas2表示中しか実体が" +
                 "現れずInspectorで直接ドラッグできない場合に使う")]
        public bool targetUI2IsMoneyCountPreview = false;

        [Tooltip("ハイライトする3Dオブジェクト（レバーなど）。UI以外が対象の場合はこちらを設定する。" +
                 "Rendererが見つかればその範囲を、見つからなければ位置を中心にした固定サイズを使う")]
        public Transform targetWorld;

        [Tooltip("説明文")]
        [TextArea]
        public string message;

        [Tooltip("枠を対象より広げる余白（スクリーンピクセル）")]
        public float padding = 20f;

        [Tooltip("自動計算されたハイライト範囲の左下側の微調整（referenceResolutionでの基準ピクセル。" +
                 "実行時の解像度に応じて自動スケーリングされる）。" +
                 "正の値で右/上へ縮める、負の値で左/下へ広げる。大きさ・ずれの調整用")]
        public Vector2 rectAdjustMin = Vector2.zero;

        [Tooltip("自動計算されたハイライト範囲の右上側の微調整（referenceResolutionでの基準ピクセル。" +
                 "実行時の解像度に応じて自動スケーリングされる）。" +
                 "正の値で右/上へ広げる、負の値で左/下へ縮める。大きさ・ずれの調整用")]
        public Vector2 rectAdjustMax = Vector2.zero;

        [Tooltip("3Dオブジェクトが対象で、かつ Renderer が見つからない場合に使う固定サイズ（スクリーンピクセル、x=幅・y=高さ）")]
        public Vector2 fallbackWorldTargetSize = new Vector2(120f, 120f);

        [Tooltip("ON: このステップは対応する側から NotifyStepActionPerformed() を呼ぶまで進まない（スペースキーでは進めない）。" +
                 "OFF: スペースキーを押すと次のステップへ進む")]
        public bool waitForExternalAction = false;

        [Tooltip("ON: targetUI/targetWorldを設定していても穴を開けず、画面全体を暗転させる（枠線も非表示）。" +
                 "説明文だけ見せたいステップ用。hideDimmingMask=ONの場合はこちらは無視される")]
        public bool fullDarkBackground = false;

        [Tooltip("ON: 暗転パネルを一切表示しない（画面全体がそのまま見える）。fullDarkBackgroundより優先される。" +
                 "プレイ操作中で視界を遮りたくないステップ用")]
        public bool hideDimmingMask = false;

        [Tooltip("このステップの説明文背景boxの幅（px）。文章量に応じて高さだけ自動調整される")]
        public float messageBoxMaxWidth = 600f;

        [Tooltip("ON: 説明文の位置を自動配置（対象の上/下、または画面中央）にせず、" +
                 "messageBoxAnchor/messageBoxPivotで指定した位置に固定する")]
        public bool overrideMessageBoxPosition = false;

        [Tooltip("overrideMessageBoxPosition=ON時の表示位置。画面を(0,0)=左下〜(1,1)=右上とした正規化座標")]
        public Vector2 messageBoxAnchor = new Vector2(0.5f, 0.5f);

        [Tooltip("overrideMessageBoxPosition=ON時のPivot（boxのどの点をmessageBoxAnchorに合わせるか）")]
        public Vector2 messageBoxPivot = new Vector2(0.5f, 0.5f);

        [Tooltip("OFF: このステップ表示中はオーバーレイのUIレイキャストを無効化し、下の要素へのクリックを通す。" +
                 "ON（既定）: 通常通りオーバーレイがクリックを受ける")]
        public bool blockRaycasts = true;

        [Tooltip("ON: レバー/ボタン操作でタイマーが動き出し、残り時間がtimerThresholdSeconds以下になった瞬間に" +
                 "タイマーをその秒数で自動的に一時停止し、次のステップへ進む（waitForExternalActionと併用可）")]
        public bool advanceWhenTimerReaches = false;

        [Tooltip("advanceWhenTimerReaches=ON時の閾値（残り秒数）")]
        public float timerThresholdSeconds = 25f;

        [Tooltip("このステップ表示中、レバー/カメラ切り替え(Q/E)/Start Descentボタン/Toggle Clawボタンを" +
                 "どう制限するか。「全て禁止」以外を選ぶとキー「3」（television収納/復元）も禁止される")]
        public CraneInputMode craneInputMode = CraneInputMode.AllAllowed;

        [Tooltip("ON: レバー/ボタン操作が検知された瞬間（タイマーが動き出した瞬間）に説明文を非表示にする")]
        public bool hideMessageWhenOperated = false;

        [Tooltip("ON: Start Descentボタンによる下降→上昇の一連動作が完了した瞬間に自動で次のステップへ進む")]
        public bool advanceWhenDescentCycleCompletes = false;

        [Tooltip("ON: Toggle Clawボタンによる爪の開閉動作が完了した瞬間に自動で次のステップへ進む")]
        public bool advanceWhenClawToggleCompletes = false;

        [Tooltip("ON: モニターのサブカメラがLeftになった瞬間に自動で次のステップへ進む")]
        public bool advanceWhenCameraIsLeft = false;

        [Tooltip("ON: モニターのサブカメラがRightになった瞬間に自動で次のステップへ進む")]
        public bool advanceWhenCameraIsRight = false;

        [Tooltip("ON: 落とし口にアイテムが入った瞬間に自動で次のステップへ進む")]
        public bool advanceWhenItemDropped = false;

        [Tooltip("ON: このステップに入った瞬間、タイマーをresetTimerToSecondsにリセットする" +
                 "（_timerStartedもリセットされ、次にレバー/ボタンを操作した時から再カウントダウンが始まる）")]
        public bool resetTimerOnEnter = false;

        [Tooltip("resetTimerOnEnter=ON時にセットする秒数")]
        public float resetTimerToSeconds = 30f;

        [Tooltip("ON: タイマーが0になった瞬間に自動で次のステップへ進む")]
        public bool advanceWhenTimerExpires = false;
    }

    [Header("ステップ")]
    [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();

    [Tooltip("rectAdjustMin/Maxを調整した時の基準解像度（今Editorで確認しながら値を決めた時の解像度を入れる）。" +
             "実行時の実解像度がこれと異なる場合、比率に応じて自動スケーリングする")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    [Header("オーバーレイ Canvas")]
    [Tooltip("このコントローラーが表示・非表示を切り替える、演出全体を乗せた Canvas")]
    [SerializeField] private Canvas overlayCanvas;

    [Tooltip("オーバーレイのレイキャストON/OFF切り替えに使うCanvasGroup。未設定ならoverlayCanvasから自動取得/自動追加する")]
    [SerializeField] private CanvasGroup overlayCanvasGroup;

    [Header("暗転パネル（上下左右の4枚。対象の周りだけ切り抜く）")]
    [SerializeField] private RectTransform topPanel;
    [SerializeField] private RectTransform bottomPanel;
    [SerializeField] private RectTransform leftPanel;
    [SerializeField] private RectTransform rightPanel;

    [Tooltip("Left/Rightパネルを上下に何pxだけ重ねてTop/Bottomパネルとの継ぎ目の隙間を消すか")]
    [SerializeField] private float seamOverlapPixels = 2f;

    [Tooltip("画面外周に接する辺を何pxだけ外側にはみ出させて、解像度の丸め誤差等によるフチの隙間を消すか")]
    [SerializeField] private float edgeOverscanPixels = 4f;

    [Tooltip("fullDarkBackground=ONのステップで使う、完全に暗い時のパネル色（通常は不透明な黒）。" +
             "通常の穴あき演出時は各パネルの元の色に戻す")]
    [SerializeField] private Color fullDarkBackgroundColor = new Color(0f, 0f, 0f, 1f);

    [Header("枠線（対象の周囲に表示する縁取り。Outline付きImage等）")]
    [SerializeField] private RectTransform frameBorder;

    [Header("枠線の二重取り（少しずらして重ねる、影のような装飾用。任意）")]
    [Tooltip("frameBorder の後ろに少しずらして重ねる、影のような二重枠用RectTransform。未設定なら二重取りなし")]
    [SerializeField] private RectTransform frameBorderShadow;

    [Tooltip("影用の枠をどれだけずらすか（スクリーンピクセル）。左下にずらすならX・Yとも負の値")]
    [SerializeField] private Vector2 frameShadowOffset = new Vector2(-6f, -6f);

    [Tooltip("影用の枠のImageコンポーネント。未設定なら frameBorderShadow から自動取得する。" +
             "中のFrameが暗い(透明)時に影だけ残って浮いて見えないよう、枠線のパルスと同期して明滅させる")]
    [SerializeField] private Image frameBorderShadowImage;

    [Header("枠線のスナップ演出（大→小で対象に吸い付く）")]
    [Tooltip("開始時、対象の矩形の何倍の大きさから縮み始めるか")]
    [SerializeField] private float frameStartScale = 2.2f;

    [Tooltip("枠が対象に吸い付くまでの所要時間（秒）")]
    [SerializeField, Min(0.01f)] private float frameSnapDuration = 0.4f;

    [Tooltip("縮む時のイージング（0→1）")]
    [SerializeField] private AnimationCurve frameSnapCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("SE 再生用の AudioSource。未設定なら自身に AddComponent して使う")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("枠線が縮みきって対象に吸い付いた瞬間に鳴らすSE。未設定なら無音")]
    [SerializeField] private AudioClip frameSnapCompleteSound;

    [SerializeField, Range(0f, 1f)] private float frameSnapCompleteVolume = 1f;

    [Tooltip("SEを鳴らすタイミング（縮み演出の進行度 0〜1）。1だと完全に縮みきった瞬間、" +
             "0.85等にすると縮みきる少し手前で鳴る")]
    [SerializeField, Range(0f, 1f)] private float frameSnapSoundTriggerProgress = 0.85f;

    [Header("枠線の発光風パルス演出（Emissionの代わり）")]
    [Tooltip("枠線のImageコンポーネント。未設定なら frameBorder から自動取得する")]
    [SerializeField] private Image frameBorderImage;

    [Tooltip("ON: 枠線を明滅させて光っているように見せる")]
    [SerializeField] private bool pulseFrameBorder = true;

    [Tooltip("明滅時の明るい側の色（Colorのアルファも含めて指定。暗い側は元の色を使う）")]
    [SerializeField] private Color pulseBrightColor = new Color(1f, 0.95f, 0.6f, 1f);

    [Tooltip("暗い⇔明るいを1往復するのにかかる時間（秒）")]
    [SerializeField, Min(0.05f)] private float pulseDuration = 0.5f;

    [Tooltip("枠の裏に重ねる加算合成のグロー（光暈）用Image。UI/Additiveシェーダーを設定したMaterialを使う。" +
             "未設定でも動作するが、その場合はグロー無し（明滅のみ）になる")]
    [SerializeField] private Image glowImage;

    [Tooltip("グローの明滅で使うアルファの下限（0〜1）。RGBはInspectorで設定した色のまま、アルファだけ上下させる")]
    [SerializeField, Range(0f, 1f)] private float glowPulseMinAlpha = 0f;

    [Tooltip("グローの明滅で使うアルファの上限（0〜1）。控えめにしたいなら低い値のままでOK（例: 10/255 ≒ 0.04）")]
    [SerializeField, Range(0f, 1f)] private float glowPulseMaxAlpha = 10f / 255f;

    [Tooltip("グローの拡大縮小の下限倍率（現在のスケールに対する倍率）")]
    [SerializeField] private float glowPulseMinScale = 1.2f;

    [Tooltip("グローの拡大縮小の上限倍率（現在のスケールに対する倍率）")]
    [SerializeField] private float glowPulseMaxScale = 1.9f;

    [Tooltip("Glow自体の1サイクル（拡大→静かに最小へ戻す→待機）にかかる時間（秒）。" +
             "枠(Pulse Duration)より短くすると、枠が1往復する間にGlowが複数回パルスするようになる（テンポアップ）")]
    [SerializeField, Min(0.05f)] private float glowPulseDuration = 0.25f;

    [Header("説明テキスト")]
    [SerializeField] private TMP_Text messageText;
    [Tooltip("説明テキストの背景・親。対象の上下どちらに置くかを自動調整する")]
    [SerializeField] private RectTransform messageBox;
    [Tooltip("対象の枠とテキスト欄の間の余白（スクリーンピクセル）")]
    [SerializeField] private float messageBoxGap = 24f;

    [Tooltip("説明文の背景box（MessageBox自体、またはその子）の内側余白（左右・上下）。" +
             "messageBoxの背景ImageはInspectorで設定しておく。messageTextはmessageBox内に" +
             "アンカーストレッチ（0,0〜1,1）で配置しておくと、この余白と実際の折り返し幅が一致する")]
    [SerializeField] private Vector2 messageBoxPadding = new Vector2(32f, 20f);

    [Tooltip("1文字あたりの表示間隔（秒）。導入ツアーのテロップ（IntroTourTelop）と同じ「カタカタ」演出")]
    [SerializeField] private float messageCharacterInterval = 0.06f;

    [Tooltip("1文字ごとに鳴らす効果音。未設定なら無音")]
    [SerializeField] private AudioClip messageTypingSound;

    [Tooltip("文字送り音のピッチの最小値")]
    [SerializeField] private float messageTypingPitchMin = 0.6f;

    [Tooltip("文字送り音のピッチの最大値")]
    [SerializeField] private float messageTypingPitchMax = 0.8f;

    [Tooltip("文字送り音が連続しすぎないための最低間隔（秒）")]
    [SerializeField] private float minMessageTypingInterval = 0.05f;

    [Range(0f, 1f)]
    [Tooltip("文字送り音の音量")]
    [SerializeField] private float messageTypingVolume = 0.7f;

    [Tooltip("「Press Space」等の操作ガイド。文字を表示しきった後に表示する（IntroTourTelopの_advanceHintと同じ）。" +
             "画面左下など好きな位置にあらかじめ配置しておく。waitForExternalAction=ONのステップでは表示しない")]
    [SerializeField] private GameObject advanceHint;

    [Tooltip("文字を表示しきってから操作ガイドを出すまでの待ち時間（秒）")]
    [SerializeField] private float advanceHintDelay = 0.3f;

    [Header("カメラ")]
    [Tooltip("3Dオブジェクトのスクリーン座標変換に使うカメラ。未設定の場合は tutorialCrane.GetActiveCamera()、" +
             "それも無ければ Camera.main を使用する")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("targetCamera が未設定の時、プレイヤーの実カメラ取得に使う参照。" +
             "playerCamera は別シーン（加算ロード）のため Inspector で直接ドラッグできないことがあるので、こちら経由で取得する")]
    [SerializeField] private TutorialCraneController tutorialCrane;

    /// <summary>チュートリアルの全ステップが終了した時に呼ばれる</summary>
    public event System.Action OnAllStepsCompleted;

    /// <summary>現在、オーバーレイが操作をブロックしたい状態か（blockRaycasts=ONのステップ表示中）。
    /// television側のタッチ判定はPhysics.RaycastでUI EventSystemを経由しないため、
    /// 呼び出し側（TouchPanelOutlineController等）がこれを見て自分で判定をスキップする必要がある</summary>
    public bool IsBlockingInteraction =>
        _isActive && overlayCanvasGroup != null && overlayCanvasGroup.blocksRaycasts;

    private int _currentStepIndex = -1;
    private bool _isActive;
    private Coroutine _frameSnapCoroutine;
    private Tween _framePulseTween;
    private Tween _glowPulseTween;
    private Color _frameBorderBaseColor;
    private bool _hasCapturedFrameBorderBaseColor;
    private Color _glowBaseColor;
    private Vector3 _glowBaseScale;
    private bool _hasCapturedGlowBaseColor;
    private Color _frameBorderShadowBaseColor;
    private bool _hasCapturedFrameBorderShadowBaseColor;
    private Image[] _maskPanelImages;
    private Color[] _maskPanelBaseColors;
    private bool _hasCapturedMaskPanelBaseColors;
    private bool _maskPanelsForcedDark;
    private int _stepShownFrame = -1;
    private Coroutine _messageTypeCoroutine;
    private float _lastTypingSoundTime;

    private void Awake()
    {
        if (overlayCanvas != null)
        {
            overlayCanvas.gameObject.SetActive(false);
            if (overlayCanvasGroup == null)
            {
                overlayCanvasGroup = overlayCanvas.GetComponent<CanvasGroup>();
                if (overlayCanvasGroup == null)
                {
                    overlayCanvasGroup = overlayCanvas.gameObject.AddComponent<CanvasGroup>();
                }
            }
        }
        if (frameBorderImage == null && frameBorder != null)
        {
            frameBorderImage = frameBorder.GetComponent<Image>();
        }
        if (frameBorderShadowImage == null && frameBorderShadow != null)
        {
            frameBorderShadowImage = frameBorderShadow.GetComponent<Image>();
        }
        if (tutorialCrane == null)
        {
            tutorialCrane = FindAnyObjectByType<TutorialCraneController>();
        }
        EnsureAudioSource();

        // Glowはframe Borderの子として配置されている前提。中央固定サイズのアンカーのままだと
        // frameBorderが長方形・大きいサイズになってもGlowが追従せず一定の大きさのままになってしまうため、
        // 親（frameBorder）いっぱいに引き伸ばすアンカーへ矯正する。これでGlowの基準サイズが常に
        // frameBorderの現在の縦横比・大きさに一致し、StartFramePulse側のスケールパルス(1.2〜1.9倍)が
        // その上から比率を保ったまま乗算されるようになる
        if (glowImage != null && frameBorder != null && glowImage.rectTransform.parent == frameBorder)
        {
            glowImage.rectTransform.anchorMin = Vector2.zero;
            glowImage.rectTransform.anchorMax = Vector2.one;
            glowImage.rectTransform.offsetMin = Vector2.zero;
            glowImage.rectTransform.offsetMax = Vector2.zero;
        }
    }

    private void OnEnable()
    {
        Debug.Log($"[TutorialDebug] OnEnable: tutorialCrane={(tutorialCrane != null ? "OK" : "NULL")}");
        if (tutorialCrane != null)
        {
            tutorialCrane.OnButtonPressed += HandleButtonPressed;
            tutorialCrane.OnItemGoalDropped += HandleItemGoalDropped;
            tutorialCrane.OnTimerExpired += HandleTimerExpired;
            tutorialCrane.OnDescentCycleCompleted += HandleDescentCycleCompleted;
            tutorialCrane.OnClawToggleCompleted += HandleClawToggleCompleted;
        }
    }

    private void OnDisable()
    {
        if (tutorialCrane != null)
        {
            tutorialCrane.OnButtonPressed -= HandleButtonPressed;
            tutorialCrane.OnItemGoalDropped -= HandleItemGoalDropped;
            tutorialCrane.OnTimerExpired -= HandleTimerExpired;
            tutorialCrane.OnDescentCycleCompleted -= HandleDescentCycleCompleted;
            tutorialCrane.OnClawToggleCompleted -= HandleClawToggleCompleted;
        }
    }

    /// <summary>Start Descentの下降→上昇の一連動作が本当に完了した瞬間に呼ばれる（イベント駆動、ポーリングなし）</summary>
    private void HandleDescentCycleCompleted()
    {
        Debug.Log($"[TutorialDebug] HandleDescentCycleCompleted fired: _isActive={_isActive}, step={_currentStepIndex}");
        if (!_isActive || _currentStepIndex < 0 || _currentStepIndex >= steps.Count) return;

        TutorialStep step = steps[_currentStepIndex];
        Debug.Log($"[TutorialDebug] advanceWhenDescentCycleCompletes={step.advanceWhenDescentCycleCompletes}");
        if (step.advanceWhenDescentCycleCompletes)
        {
            AdvanceStep();
        }
    }

    /// <summary>Toggle Clawの手動開閉が完了した瞬間に呼ばれる（イベント駆動、ポーリングなし）</summary>
    private void HandleClawToggleCompleted()
    {
        if (!_isActive || _currentStepIndex < 0 || _currentStepIndex >= steps.Count) return;

        TutorialStep step = steps[_currentStepIndex];
        if (step.advanceWhenClawToggleCompletes)
        {
            AdvanceStep();
        }
    }

    /// <summary>タイマーが0になった瞬間に呼ばれる。現在のステップが該当設定を持っていれば次へ進む</summary>
    private void HandleTimerExpired()
    {
        if (!_isActive || _currentStepIndex < 0 || _currentStepIndex >= steps.Count) return;

        TutorialStep step = steps[_currentStepIndex];
        if (step.advanceWhenTimerExpires)
        {
            AdvanceStep();
        }
    }

    /// <summary>落とし口にアイテムが入った瞬間に呼ばれる。現在のステップが該当設定を持っていれば次へ進む</summary>
    private void HandleItemGoalDropped(UFOItemType itemType)
    {
        if (!_isActive || _currentStepIndex < 0 || _currentStepIndex >= steps.Count) return;

        TutorialStep step = steps[_currentStepIndex];
        if (step.advanceWhenItemDropped)
        {
            AdvanceStep();
        }
    }

    /// <summary>
    /// ボタンが押された同じフレームで即座に呼ばれる。現在のステップが該当ボタンの
    /// 「完了検知で次へ進む」設定を持っている場合、連打防止のためこの場で即座にロックする
    /// （Update()側のポーリングだと1フレーム遅れて連打を許してしまうため）。
    /// </summary>
    private void HandleButtonPressed(ButtonController.ButtonType buttonType)
    {
        Debug.Log($"[TutorialDebug] HandleButtonPressed fired: buttonType={buttonType}, step={_currentStepIndex}");
        if (!_isActive || _currentStepIndex < 0 || _currentStepIndex >= steps.Count || tutorialCrane == null) return;

        TutorialStep step = steps[_currentStepIndex];

        // どちらか一方が押された瞬間に両方を同時にロックする。StartDescent/ToggleClawは
        // UFOArmController側で同じIsInputLocked（IsBusy || 手動開閉コルーチン中）を共有しているため、
        // 押された方だけをロックすると、連打でもう片方が重ねて呼ばれてアーム側の状態が壊れることがある
        if (step.advanceWhenDescentCycleCompletes && buttonType == ButtonController.ButtonType.StartDescent)
        {
            tutorialCrane.SetButtonTypeAllowed(ButtonController.ButtonType.StartDescent, false);
            tutorialCrane.SetButtonTypeAllowed(ButtonController.ButtonType.ToggleClaw, false);
        }
        else if (step.advanceWhenClawToggleCompletes && buttonType == ButtonController.ButtonType.ToggleClaw)
        {
            tutorialCrane.SetButtonTypeAllowed(ButtonController.ButtonType.ToggleClaw, false);
            tutorialCrane.SetButtonTypeAllowed(ButtonController.ButtonType.StartDescent, false);
        }
    }

    private void EnsureAudioSource()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, volume);
    }

    /// <summary>
    /// 先頭のステップからチュートリアル演出を開始する。
    /// </summary>
    public void StartTutorialSteps()
    {
        if (steps == null || steps.Count == 0) return;

        _isActive = true;
        if (overlayCanvas != null) overlayCanvas.gameObject.SetActive(true);
        ShowStep(0);
    }

    /// <summary>
    /// 演出を即座に終了する（Quit等でチュートリアル自体を中断する時に呼ぶ）。
    /// </summary>
    public void StopTutorialSteps()
    {
        _isActive = false;
        _currentStepIndex = -1;
        if (_frameSnapCoroutine != null)
        {
            StopCoroutine(_frameSnapCoroutine);
            _frameSnapCoroutine = null;
        }
        if (_messageTypeCoroutine != null)
        {
            StopCoroutine(_messageTypeCoroutine);
            _messageTypeCoroutine = null;
            if (audioSource != null) audioSource.pitch = 1f;
        }
        SetAdvanceHintActive(false);
        SetMaskPanelsFullyDark(false);
        if (overlayCanvasGroup != null) overlayCanvasGroup.blocksRaycasts = true;
        if (tutorialCrane != null)
        {
            tutorialCrane.SetButtonInputAllowed(true);
            tutorialCrane.SetLeverInputAllowed(true);
            tutorialCrane.SetCameraSwitchAllowed(true);
            tutorialCrane.SetKey3InputAllowed(true);
        }
        if (messageBox != null) messageBox.gameObject.SetActive(true);
        StopFramePulse();
        if (overlayCanvas != null) overlayCanvas.gameObject.SetActive(false);
    }

    /// <summary>
    /// 「実際の操作待ち」ステップ用。対応する操作を検知した側（レバー入力など）から呼ぶことで次のステップへ進む。
    /// 現在のステップが waitForExternalAction=false の場合や、演出が非アクティブな場合は何もしない。
    /// </summary>
    public void NotifyStepActionPerformed()
    {
        if (!_isActive || _currentStepIndex < 0 || _currentStepIndex >= steps.Count) return;
        if (!steps[_currentStepIndex].waitForExternalAction) return;

        AdvanceStep();
    }

    private void Update()
    {
        if (!_isActive || _currentStepIndex < 0 || _currentStepIndex >= steps.Count) return;

        TutorialStep step = steps[_currentStepIndex];

        // frameBorder は AnimateFrameSnap コルーチンが専任で更新する（大→小のスナップ演出のため）。
        // マスクと説明文欄は演出不要なので、ここで毎フレーム対象に追従させる。
        bool hasTargetRect = TryGetTargetScreenRectForDisplay(step, out Rect screenRect);
        if (step.hideDimmingMask)
        {
            HideMaskPanels();
        }
        else
        {
            SetMaskPanelsFullyDark(step.fullDarkBackground);
            if (hasTargetRect)
            {
                UpdateMaskPanels(screenRect);
            }
            else
            {
                // ハイライト対象が無い、または fullDarkBackground 指定のステップは、
                // 穴を開けず画面全体を暗転させる
                CoverFullScreenMask();
            }
        }

        if (step.overrideMessageBoxPosition)
        {
            ApplyMessageBoxPositionOverride(step);
        }
        else if (hasTargetRect)
        {
            UpdateMessageBox(screenRect);
        }
        else
        {
            CenterMessageBox();
        }

        // レバー/ボタン操作が検知された（タイマーが動き出した）瞬間に説明文を隠す。
        // ステップ自体は継続し、タイマー閾値の監視は引き続き行う
        if (step.hideMessageWhenOperated && messageBox != null && tutorialCrane != null)
        {
            bool shouldShow = !tutorialCrane.IsTimerStarted;
            if (messageBox.gameObject.activeSelf != shouldShow)
            {
                messageBox.gameObject.SetActive(shouldShow);
            }
        }

        // レバー/ボタン操作でタイマーが動き出し、指定した残り秒数まで減った瞬間に
        // タイマーをそこで一時停止し、自動的に次のステップへ進む
        if (step.advanceWhenTimerReaches && tutorialCrane != null &&
            tutorialCrane.IsTimerStarted && tutorialCrane.RemainingTime <= step.timerThresholdSeconds)
        {
            tutorialCrane.PauseTimerAt(step.timerThresholdSeconds);
            AdvanceStep();
            return;
        }

        // ※ Start Descent/Toggle Clawの完了検知はポーリングではなく、HandleDescentCycleCompleted/
        // HandleClawToggleCompleted（UFOArmControllerのイベント駆動）で行っている

        // モニターのサブカメラがLeft/Rightになった瞬間に進む
        if (step.advanceWhenCameraIsLeft && tutorialCrane != null &&
            tutorialCrane.CurrentSubCameraState == UFOCameraController.UfoSubCameraState.Left)
        {
            AdvanceStep();
            return;
        }

        if (step.advanceWhenCameraIsRight && tutorialCrane != null &&
            tutorialCrane.CurrentSubCameraState == UFOCameraController.UfoSubCameraState.Right)
        {
            AdvanceStep();
            return;
        }

        // ステップ表示のきっかけになったクリック（例:「はい」ボタン）自体を、
        // このステップへの「進む」入力として二重に拾ってしまわないよう、表示した同じフレームは無視する
        bool sameFrameAsShown = Time.frameCount == _stepShownFrame;
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Debug.Log($"[TutorialDebug] Space pressed: step={_currentStepIndex}, waitForExternalAction={step.waitForExternalAction}, " +
                      $"craneInputMode={step.craneInputMode}, willAdvance={!step.waitForExternalAction && !sameFrameAsShown}");
        }
        if (!step.waitForExternalAction && !sameFrameAsShown &&
            Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            AdvanceStep();
        }
    }

    private void ShowStep(int index)
    {
        _currentStepIndex = index;
        _stepShownFrame = Time.frameCount;
        TutorialStep step = steps[index];

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.blocksRaycasts = step.blockRaycasts;
        }

        if (tutorialCrane != null)
        {
            // 前のステップまでの連打等でToggleClawの手動開閉コルーチンが残留し、IsInputLockedが
            // 解除されないまま固まっていることがあるため、新しいステップに入るたびに必ず解除しておく
            tutorialCrane.ForceReleaseArmLock();

            CraneInputMode mode = step.craneInputMode;
            bool allAllowed = mode == CraneInputMode.AllAllowed;

            bool startDescentAllowed = allAllowed || mode == CraneInputMode.StartDescentOnly;
            bool toggleClawAllowed = allAllowed || mode == CraneInputMode.ToggleClawOnly;

            tutorialCrane.SetLeverInputAllowed(allAllowed || mode == CraneInputMode.LeverOnly);
            tutorialCrane.SetCameraSwitchAllowed(allAllowed || mode == CraneInputMode.CameraOnly);
            tutorialCrane.SetButtonTypeAllowed(ButtonController.ButtonType.StartDescent, startDescentAllowed);
            tutorialCrane.SetButtonTypeAllowed(ButtonController.ButtonType.ToggleClaw, toggleClawAllowed);
            tutorialCrane.SetKey3InputAllowed(allAllowed || mode == CraneInputMode.Key3Only);

            Debug.Log($"[TutorialDebug] ShowStep({index}): craneInputMode={mode}, waitForExternalAction={step.waitForExternalAction}, " +
                      $"startDescentAllowed={startDescentAllowed}, toggleClawAllowed={toggleClawAllowed}, " +
                      $"advanceWhenDescentCycleCompletes={step.advanceWhenDescentCycleCompletes}");

            if (step.resetTimerOnEnter)
            {
                tutorialCrane.ResetTimer(step.resetTimerToSeconds);
            }
        }

        if (messageBox != null)
        {
            messageBox.gameObject.SetActive(true);
        }

        if (_messageTypeCoroutine != null)
        {
            StopCoroutine(_messageTypeCoroutine);
            _messageTypeCoroutine = null;
        }
        // 文字を打ち始める前に、完成後の文章量に合わせて背景boxを先に確定サイズへ整える
        // （タイプ中にbox自体がガタガタ広がらないように）
        ResizeMessageBoxToFitText(step);
        SetAdvanceHintActive(false);
        if (messageText != null)
        {
            _messageTypeCoroutine = StartCoroutine(TypeMessage(step));
        }

        // 表示直後に一度、正しい位置へ即座に合わせておく（Updateを待たない）
        bool hasTargetRect = TryGetTargetScreenRectForDisplay(step, out Rect screenRect);
        if (step.hideDimmingMask)
        {
            HideMaskPanels();
        }
        else
        {
            SetMaskPanelsFullyDark(step.fullDarkBackground);
            if (hasTargetRect)
            {
                UpdateMaskPanels(screenRect);
            }
            else
            {
                CoverFullScreenMask();
            }
        }

        if (step.overrideMessageBoxPosition)
        {
            ApplyMessageBoxPositionOverride(step);
        }
        else if (hasTargetRect)
        {
            UpdateMessageBox(screenRect);
        }
        else
        {
            CenterMessageBox();
        }

        if (_frameSnapCoroutine != null)
        {
            StopCoroutine(_frameSnapCoroutine);
        }
        _frameSnapCoroutine = StartCoroutine(AnimateFrameSnap(step));

        StartFramePulse();
    }

    /// <summary>
    /// 枠線を、対象より一回り大きい状態からイージングで縮めて吸い付かせる演出。
    /// 所要時間が経過した後も、対象が動く場合に備えてライブで追従を続ける。
    /// </summary>
    private IEnumerator AnimateFrameSnap(TutorialStep step)
    {
        if (frameBorder == null) yield break;
        if (!TryGetTargetScreenRectForDisplay(step, out Rect targetRect))
        {
            // ハイライト対象が無い、または fullDarkBackground 指定のステップでは枠線を表示しない
            frameBorder.gameObject.SetActive(false);
            if (frameBorderShadow != null) frameBorderShadow.gameObject.SetActive(false);
            yield break;
        }
        frameBorder.gameObject.SetActive(true);
        if (frameBorderShadow != null) frameBorderShadow.gameObject.SetActive(true);

        Rect startRect = ScaleRectFromCenter(targetRect, frameStartScale);

        bool soundPlayed = false;
        float elapsed = 0f;
        while (elapsed < frameSnapDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / frameSnapDuration);
            float eased = frameSnapCurve.Evaluate(t);

            // 対象が動く場合に備えて、目標矩形自体は毎フレーム再取得する
            if (TryGetTargetScreenRect(step, out Rect liveTarget))
            {
                targetRect = liveTarget;
            }

            UpdateFrameBorder(LerpRect(startRect, targetRect, eased));

            // 縮みきる少し手前（frameSnapSoundTriggerProgress）でSEを鳴らす
            if (!soundPlayed && t >= frameSnapSoundTriggerProgress)
            {
                soundPlayed = true;
                PlayOneShot(frameSnapCompleteSound, frameSnapCompleteVolume);
            }

            yield return null;
        }

        if (!soundPlayed)
        {
            PlayOneShot(frameSnapCompleteSound, frameSnapCompleteVolume);
        }

        // スナップ完了後は、対象が動く場合に備えてそのままライブ追従を続ける
        while (true)
        {
            if (TryGetTargetScreenRect(step, out Rect liveTarget))
            {
                UpdateFrameBorder(liveTarget);
            }
            yield return null;
        }
    }

    /// <summary>
    /// 説明文を1文字ずつ表示する。導入ツアーのテロップ（IntroTourTelop.TypeLine）と同じ考え方で、
    /// 文字ごとにピッチをランダムに変えた効果音を鳴らして「カタカタ」感を出す。
    /// </summary>
    private IEnumerator TypeMessage(TutorialStep step)
    {
        messageText.text = "";
        string message = step.message;

        if (!string.IsNullOrEmpty(message))
        {
            for (int i = 0; i < message.Length; i++)
            {
                messageText.text += message[i];
                PlayTypingSound(message[i]);
                yield return new WaitForSeconds(messageCharacterInterval);
            }
        }

        if (audioSource != null) audioSource.pitch = 1f;

        // waitForExternalAction=ONのステップは操作待ちなので「Press Space」は出さない
        if (!step.waitForExternalAction)
        {
            if (advanceHintDelay > 0f) yield return new WaitForSeconds(advanceHintDelay);
            SetAdvanceHintActive(true);
        }
    }

    private void SetAdvanceHintActive(bool active)
    {
        if (advanceHint != null && advanceHint.activeSelf != active)
        {
            advanceHint.SetActive(active);
        }
    }

    private void PlayTypingSound(char c)
    {
        if (messageTypingSound == null || audioSource == null) return;
        if (c == ' ' || c == '\n' || c == '　') return;
        if (Time.time - _lastTypingSoundTime < minMessageTypingInterval) return;

        audioSource.pitch = Random.Range(messageTypingPitchMin, messageTypingPitchMax);
        audioSource.PlayOneShot(messageTypingSound, messageTypingVolume);
        _lastTypingSoundTime = Time.time;
    }

    private static Rect ScaleRectFromCenter(Rect rect, float scale)
    {
        Vector2 center = rect.center;
        Vector2 size = rect.size * scale;
        return new Rect(center - size * 0.5f, size);
    }

    private static Rect LerpRect(Rect a, Rect b, float t)
    {
        return new Rect(
            Mathf.Lerp(a.x, b.x, t),
            Mathf.Lerp(a.y, b.y, t),
            Mathf.Lerp(a.width, b.width, t),
            Mathf.Lerp(a.height, b.height, t));
    }

    private void AdvanceStep()
    {
        int next = _currentStepIndex + 1;
        if (next >= steps.Count)
        {
            EndTutorial();
            return;
        }
        ShowStep(next);
    }

    private void EndTutorial()
    {
        _isActive = false;
        _currentStepIndex = -1;
        if (_frameSnapCoroutine != null)
        {
            StopCoroutine(_frameSnapCoroutine);
            _frameSnapCoroutine = null;
        }
        if (_messageTypeCoroutine != null)
        {
            StopCoroutine(_messageTypeCoroutine);
            _messageTypeCoroutine = null;
            if (audioSource != null) audioSource.pitch = 1f;
        }
        SetAdvanceHintActive(false);
        SetMaskPanelsFullyDark(false);
        if (overlayCanvasGroup != null) overlayCanvasGroup.blocksRaycasts = true;
        if (tutorialCrane != null)
        {
            tutorialCrane.SetButtonInputAllowed(true);
            tutorialCrane.SetLeverInputAllowed(true);
            tutorialCrane.SetCameraSwitchAllowed(true);
            tutorialCrane.SetKey3InputAllowed(true);
        }
        if (messageBox != null) messageBox.gameObject.SetActive(true);
        StopFramePulse();
        if (overlayCanvas != null) overlayCanvas.gameObject.SetActive(false);
        // ステップを最後まで見終えたら、Tutorial_Canvas(Yes/No)には戻さず、ローディングを挟んで
        // いつも通りのPlay_Canvasへ直接戻す（タイマー切れ・Quitとは異なる終了経路）
        if (tutorialCrane != null) tutorialCrane.CompleteTutorial();
        OnAllStepsCompleted?.Invoke();
    }

    private void StartFramePulse()
    {
        StopFramePulse();

        if (pulseFrameBorder && frameBorderImage != null)
        {
            if (!_hasCapturedFrameBorderBaseColor)
            {
                _frameBorderBaseColor = frameBorderImage.color;
                _hasCapturedFrameBorderBaseColor = true;
            }
            frameBorderImage.color = _frameBorderBaseColor;

            Sequence frameSequence = DOTween.Sequence();
            frameSequence.Join(frameBorderImage.DOColor(pulseBrightColor, pulseDuration).SetEase(Ease.InOutSine));

            // 影の枠も、枠線本体と全く同じ設定（同じ目標色・同じpulseDuration・同じEase）で
            // 同じSequenceにJoinする。別々の色域(消える/消えない)にすると動きが合って見えないため、
            // 「暗い⇔明るい」の同じ二色間パルスをそのまま影にも適用して完全に一致させる
            if (frameBorderShadowImage != null)
            {
                if (!_hasCapturedFrameBorderShadowBaseColor)
                {
                    _frameBorderShadowBaseColor = frameBorderShadowImage.color;
                    _hasCapturedFrameBorderShadowBaseColor = true;
                }
                frameBorderShadowImage.color = _frameBorderShadowBaseColor;

                frameSequence.Join(frameBorderShadowImage.DOColor(pulseBrightColor, pulseDuration).SetEase(Ease.InOutSine));
            }

            frameSequence.SetLoops(-1, LoopType.Yoyo);
            _framePulseTween = frameSequence;
        }

        // Glow（加算合成）は frameBorder の子として配置している前提のため、位置は自動追従する。
        // 「最小(1.2倍・透明) → 最大(1.9倍・アルファ最大)」を拡大とアルファを完全に同期させて一緒に動かし、
        // 最大に達したら静かに最小へ戻し、縮んでいた分と同じ時間だけ何もせず待ってからまた拡大する。
        if (pulseFrameBorder && glowImage != null)
        {
            if (!_hasCapturedGlowBaseColor)
            {
                _glowBaseColor = glowImage.color;
                _glowBaseScale = glowImage.rectTransform.localScale;
                _hasCapturedGlowBaseColor = true;
            }

            Color minColor = _glowBaseColor;
            minColor.a = glowPulseMinAlpha;
            Color maxColor = _glowBaseColor;
            maxColor.a = glowPulseMaxAlpha;
            Vector3 minScale = _glowBaseScale * glowPulseMinScale;
            Vector3 maxScale = _glowBaseScale * glowPulseMaxScale;

            glowImage.color = minColor;
            glowImage.rectTransform.localScale = minScale;

            Sequence glowSequence = DOTween.Sequence();
            // 拡大(1.2→1.9)とアルファ(最小→最大)を同時に進める。枠より短い glowPulseDuration を使うことで、
            // 枠が1往復する間にGlowが複数回パルスし、テンポアップして見える
            glowSequence.Append(glowImage.rectTransform.DOScale(maxScale, glowPulseDuration).SetEase(Ease.InOutSine));
            glowSequence.Join(glowImage.DOColor(maxColor, glowPulseDuration).SetEase(Ease.InOutSine));
            // 最大に達したら、アニメーションなしで静かに最小(透明)へ戻す
            glowSequence.AppendCallback(() =>
            {
                glowImage.rectTransform.localScale = minScale;
                glowImage.color = minColor;
            });
            // 縮んでいた分と同じ時間だけ何もせず待つ
            glowSequence.AppendInterval(glowPulseDuration);
            glowSequence.SetLoops(-1);
            _glowPulseTween = glowSequence;
        }
    }

    private void StopFramePulse()
    {
        if (_framePulseTween != null)
        {
            _framePulseTween.Kill();
            _framePulseTween = null;
        }
        if (frameBorderImage != null && _hasCapturedFrameBorderBaseColor)
        {
            frameBorderImage.color = _frameBorderBaseColor;
        }
        if (frameBorderShadowImage != null && _hasCapturedFrameBorderShadowBaseColor)
        {
            frameBorderShadowImage.color = _frameBorderShadowBaseColor;
        }

        if (_glowPulseTween != null)
        {
            _glowPulseTween.Kill();
            _glowPulseTween = null;
        }
        if (glowImage != null && _hasCapturedGlowBaseColor)
        {
            glowImage.rectTransform.localScale = _glowBaseScale;
            glowImage.color = _glowBaseColor;
        }
    }

    private Camera GetCamera()
    {
        if (targetCamera != null) return targetCamera;
        if (tutorialCrane != null)
        {
            Camera cam = tutorialCrane.GetActiveCamera();
            if (cam != null) return cam;
        }
        return Camera.main;
    }

    /// <summary>
    /// ステップの対象（UI or 3Dオブジェクト）を、余白込みのスクリーン座標矩形として取得する。
    /// </summary>
    private bool TryGetTargetScreenRect(TutorialStep step, out Rect screenRect)
    {
        screenRect = default;

        RectTransform targetUI = step.targetUIIsMoneyCount
            ? (GameUIManager.Instance != null ? GameUIManager.Instance.MoneyTextRect : null)
            : step.targetUI;

        if (targetUI != null)
        {
            Rect rect = GetUIScreenRect(targetUI);

            RectTransform targetUI2 = step.targetUI2IsMoneyCountPreview
                ? (GameUIManager.Instance != null ? GameUIManager.Instance.MoneyTextInfoRect : null)
                : step.targetUI2;

            if (targetUI2 != null)
            {
                rect = UnionRect(rect, GetUIScreenRect(targetUI2));
            }
            screenRect = ApplyRectAdjust(ExpandRect(rect, step.padding), step);
            return true;
        }

        if (step.targetWorld != null)
        {
            screenRect = ApplyRectAdjust(ExpandRect(GetWorldScreenRect(step.targetWorld, step.fallbackWorldTargetSize), step.padding), step);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 計算されたハイライト矩形に、ステップごとの手動微調整（rectAdjustMin/Max）を適用する。
    /// xMin/yMinは正で右/上へ縮める（負で左/下へ広げる）、xMax/yMaxは正で右/上へ広げる（負で左/下へ縮める）。
    /// </summary>
    private Rect ApplyRectAdjust(Rect rect, TutorialStep step)
    {
        // rectAdjustMin/MaxはreferenceResolutionを基準にしたピクセル値。
        // 実際の解像度がreferenceResolutionと異なる場合は比率でスケーリングし、
        // 解像度を変えても見た目のズレ量が変わらないようにする
        float scaleX = referenceResolution.x > 0f ? Mathf.Max(1f, Screen.width) / referenceResolution.x : 1f;
        float scaleY = referenceResolution.y > 0f ? Mathf.Max(1f, Screen.height) / referenceResolution.y : 1f;

        Vector2 adjustMin = new Vector2(step.rectAdjustMin.x * scaleX, step.rectAdjustMin.y * scaleY);
        Vector2 adjustMax = new Vector2(step.rectAdjustMax.x * scaleX, step.rectAdjustMax.y * scaleY);

        return new Rect(
            rect.xMin + adjustMin.x,
            rect.yMin + adjustMin.y,
            rect.width - adjustMin.x + adjustMax.x,
            rect.height - adjustMin.y + adjustMax.y);
    }

    /// <summary>2つの矩形の両方を含む最小の矩形を返す。</summary>
    private static Rect UnionRect(Rect a, Rect b)
    {
        float xMin = Mathf.Min(a.xMin, b.xMin);
        float yMin = Mathf.Min(a.yMin, b.yMin);
        float xMax = Mathf.Max(a.xMax, b.xMax);
        float yMax = Mathf.Max(a.yMax, b.yMax);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    /// <summary>
    /// マスク・枠線・説明文の位置決めで使う「穴を開けるかどうか」の判定。
    /// fullDarkBackground=ON のステップは、targetUI/targetWorldが設定されていても
    /// 穴を開けず画面全体を暗転させたいので、常に false を返す。
    /// </summary>
    private bool TryGetTargetScreenRectForDisplay(TutorialStep step, out Rect screenRect)
    {
        if (step.fullDarkBackground)
        {
            screenRect = default;
            return false;
        }
        return TryGetTargetScreenRect(step, out screenRect);
    }

    private Rect GetUIScreenRect(RectTransform rt)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners); // 0:左下 1:左上 2:右上 3:右下

        Canvas canvas = rt.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

        return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
    }

    private Rect GetWorldScreenRect(Transform target, Vector2 fallbackSize)
    {
        Camera cam = GetCamera();
        if (cam == null)
        {
            return new Rect(0f, 0f, 0f, 0f);
        }

        Renderer targetRenderer = target.GetComponentInChildren<Renderer>();
        if (targetRenderer == null)
        {
            Vector3 screenPoint = cam.WorldToScreenPoint(target.position);
            Vector2 half = fallbackSize * 0.5f;
            return new Rect(screenPoint.x - half.x, screenPoint.y - half.y, fallbackSize.x, fallbackSize.y);
        }

        Bounds b = targetRenderer.bounds;
        Vector3 c = b.center;
        Vector3 e = b.extents;

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c + new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z);

            Vector3 sp = cam.WorldToScreenPoint(corner);
            if (sp.x < minX) minX = sp.x;
            if (sp.x > maxX) maxX = sp.x;
            if (sp.y < minY) minY = sp.y;
            if (sp.y > maxY) maxY = sp.y;
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private static Rect ExpandRect(Rect rect, float padding)
    {
        return new Rect(rect.xMin - padding, rect.yMin - padding, rect.width + padding * 2f, rect.height + padding * 2f);
    }

    /// <summary>
    /// ハイライト対象が無いステップ用。穴を開けず、画面全体を暗転パネルで覆う。
    /// </summary>
    private void CoverFullScreenMask()
    {
        UpdateMaskPanels(new Rect(0f, 0f, 0f, 0f));
    }

    /// <summary>
    /// hideDimmingMask=ON のステップ用。暗転パネル4枚を全てゼロサイズにして、画面が何も遮られずそのまま見える状態にする。
    /// </summary>
    private void HideMaskPanels()
    {
        SetMaskPanelRect(topPanel, 0f, 0f, 0f, 0f);
        SetMaskPanelRect(bottomPanel, 0f, 0f, 0f, 0f);
        SetMaskPanelRect(leftPanel, 0f, 0f, 0f, 0f);
        SetMaskPanelRect(rightPanel, 0f, 0f, 0f, 0f);
    }

    /// <summary>
    /// fullDarkBackground=ON のステップ用に、暗転パネル4枚の色を完全に暗い色（fullDarkBackgroundColor）へ
    /// 上書きする。OFFの時は各パネル本来の色に戻す。
    /// </summary>
    private void SetMaskPanelsFullyDark(bool fullyDark)
    {
        if (fullyDark == _maskPanelsForcedDark) return;
        _maskPanelsForcedDark = fullyDark;

        if (!_hasCapturedMaskPanelBaseColors)
        {
            _maskPanelImages = new[]
            {
                topPanel != null ? topPanel.GetComponent<Image>() : null,
                bottomPanel != null ? bottomPanel.GetComponent<Image>() : null,
                leftPanel != null ? leftPanel.GetComponent<Image>() : null,
                rightPanel != null ? rightPanel.GetComponent<Image>() : null,
            };
            _maskPanelBaseColors = new Color[_maskPanelImages.Length];
            for (int i = 0; i < _maskPanelImages.Length; i++)
            {
                if (_maskPanelImages[i] != null) _maskPanelBaseColors[i] = _maskPanelImages[i].color;
            }
            _hasCapturedMaskPanelBaseColors = true;
        }

        for (int i = 0; i < _maskPanelImages.Length; i++)
        {
            if (_maskPanelImages[i] == null) continue;
            _maskPanelImages[i].color = fullyDark ? fullDarkBackgroundColor : _maskPanelBaseColors[i];
        }
    }

    /// <summary>
    /// ハイライト対象が無いステップ用。説明文の吹き出しを画面中央に置く。
    /// </summary>
    private void CenterMessageBox()
    {
        if (messageBox == null) return;
        messageBox.anchorMin = new Vector2(0.5f, 0.5f);
        messageBox.anchorMax = new Vector2(0.5f, 0.5f);
        messageBox.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>
    /// overrideMessageBoxPosition=ON のステップ用。自動配置をせず、指定した位置に固定する。
    /// </summary>
    private void ApplyMessageBoxPositionOverride(TutorialStep step)
    {
        if (messageBox == null) return;
        messageBox.anchorMin = step.messageBoxAnchor;
        messageBox.anchorMax = step.messageBoxAnchor;
        messageBox.pivot = step.messageBoxPivot;
    }

    /// <summary>
    /// 背景box（messageBox）を、実際に表示する文章の長さに合わせたサイズへリサイズする。
    /// messageBoxMaxWidth 内で折り返した時の縦横サイズをTMPの計算に任せ、そこにpaddingを足す。
    /// </summary>
    private void ResizeMessageBoxToFitText(TutorialStep step)
    {
        if (messageBox == null || messageText == null) return;

        float maxWidth = step.messageBoxMaxWidth;
        float innerWidth = Mathf.Max(1f, maxWidth - messageBoxPadding.x * 2f);

        // messageTextの実際の描画幅を、これから折り返し計算に使う幅と必ず一致させる
        // （Inspectorでの手動アンカー設定に頼らず、コード側で強制する）
        RectTransform textRt = messageText.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(messageBoxPadding.x, messageBoxPadding.y);
        textRt.offsetMax = new Vector2(-messageBoxPadding.x, -messageBoxPadding.y);
        messageText.enableWordWrapping = true;

        // 横幅はこのステップのmessageBoxMaxWidthに固定し、文章量に応じて高さだけ自動調整する
        Vector2 preferred = messageText.GetPreferredValues(step.message ?? string.Empty, innerWidth, 0f);
        float boxHeight = preferred.y + messageBoxPadding.y * 2f;

        messageBox.sizeDelta = new Vector2(maxWidth, boxHeight);
    }

    /// <summary>
    /// 上下左右4枚の暗転パネルを、正規化した(0〜1)アンカー座標で配置する。
    /// アンカーはスクリーン解像度・Canvasのスケールに関わらず常に画面全体を0〜1で表すため、
    /// CanvasScalerの設定を気にせずそのまま使える。
    /// </summary>
    private void UpdateMaskPanels(Rect holeScreenRect)
    {
        float w = Mathf.Max(1f, Screen.width);
        float h = Mathf.Max(1f, Screen.height);

        float left = Mathf.Clamp01(holeScreenRect.xMin / w);
        float right = Mathf.Clamp01(holeScreenRect.xMax / w);
        float bottom = Mathf.Clamp01(holeScreenRect.yMin / h);
        float top = Mathf.Clamp01(holeScreenRect.yMax / h);

        // Top/Bottomパネルとぴったり同じ座標で接すると、ピクセルの丸め誤差で継ぎ目に
        // 1px前後の隙間が見えてしまうことがあるため、Left/Rightパネルを上下に少しだけ
        // はみ出させて重ねる（同じ色同士の重なりなので見た目には影響しない）。
        float overlapY = seamOverlapPixels / h;
        float overlappedBottom = Mathf.Clamp01(bottom - overlapY);
        float overlappedTop = Mathf.Clamp01(top + overlapY);

        SetMaskPanelRect(topPanel, 0f, top, 1f, 1f);
        SetMaskPanelRect(bottomPanel, 0f, 0f, 1f, bottom);
        SetMaskPanelRect(leftPanel, 0f, overlappedBottom, left, overlappedTop);
        SetMaskPanelRect(rightPanel, right, overlappedBottom, 1f, overlappedTop);
    }

    /// <summary>
    /// 暗転パネルをアンカーで配置する。画面外周（アンカー0 または 1）に接する辺だけ
    /// edgeOverscanPixels 分だけ外側にはみ出させ、対象の穴に接する内側の辺はそのままにする。
    /// </summary>
    private void SetMaskPanelRect(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);

        float offsetMinX = Mathf.Approximately(xMin, 0f) ? -edgeOverscanPixels : 0f;
        float offsetMinY = Mathf.Approximately(yMin, 0f) ? -edgeOverscanPixels : 0f;
        float offsetMaxX = Mathf.Approximately(xMax, 1f) ? edgeOverscanPixels : 0f;
        float offsetMaxY = Mathf.Approximately(yMax, 1f) ? edgeOverscanPixels : 0f;

        rt.offsetMin = new Vector2(offsetMinX, offsetMinY);
        rt.offsetMax = new Vector2(offsetMaxX, offsetMaxY);
    }

    private void UpdateFrameBorder(Rect holeScreenRect)
    {
        if (frameBorder == null) return;

        float w = Mathf.Max(1f, Screen.width);
        float h = Mathf.Max(1f, Screen.height);

        frameBorder.anchorMin = new Vector2(holeScreenRect.xMin / w, holeScreenRect.yMin / h);
        frameBorder.anchorMax = new Vector2(holeScreenRect.xMax / w, holeScreenRect.yMax / h);
        frameBorder.offsetMin = Vector2.zero;
        frameBorder.offsetMax = Vector2.zero;

        // 影のような二重取り。同じ矩形を frameShadowOffset 分だけずらして重ねるだけ（サイズは同じ）
        if (frameBorderShadow != null)
        {
            Rect shifted = holeScreenRect;
            shifted.x += frameShadowOffset.x;
            shifted.y += frameShadowOffset.y;

            frameBorderShadow.anchorMin = new Vector2(shifted.xMin / w, shifted.yMin / h);
            frameBorderShadow.anchorMax = new Vector2(shifted.xMax / w, shifted.yMax / h);
            frameBorderShadow.offsetMin = Vector2.zero;
            frameBorderShadow.offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// 説明テキストの吹き出しを対象の枠のすぐ下（画面下半分に入り切らない場合は上）に配置する。
    /// </summary>
    private void UpdateMessageBox(Rect holeScreenRect)
    {
        if (messageBox == null) return;

        float w = Mathf.Max(1f, Screen.width);
        float h = Mathf.Max(1f, Screen.height);

        bool placeBelow = holeScreenRect.yMin > h * 0.35f; // 対象が画面上寄りなら下に、下寄りなら上に出す

        float anchorX = Mathf.Clamp01(holeScreenRect.center.x / w);
        float anchorY = placeBelow
            ? Mathf.Clamp01((holeScreenRect.yMin - messageBoxGap) / h)
            : Mathf.Clamp01((holeScreenRect.yMax + messageBoxGap) / h);

        messageBox.anchorMin = new Vector2(anchorX, anchorY);
        messageBox.anchorMax = new Vector2(anchorX, anchorY);
        messageBox.pivot = new Vector2(0.5f, placeBelow ? 1f : 0f);
    }
}
