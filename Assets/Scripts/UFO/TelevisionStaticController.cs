using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;

/// <summary>
/// Q/E によるサブカメラ切り替え（Left/Right/Back）を提供する側が実装するインターフェース。
/// 実機の UFOCameraController の代わりに、チュートリアル(TutorialCraneController)など別のカメラ群を
/// TelevisionStaticController の Canvas 切り替えに差し込むための、ICraneControlSource と同じ考え方の拡張点。
/// </summary>
public interface ISubCameraSource
{
    Camera GetSubCamera(UFOCameraController.UfoSubCameraState state);
}

/// <summary>
/// television オブジェクトや UI 用にアタッチして使用するスクリプト。
/// サブカメラ（Left, Right, Back）を Q/E キーで切り替えた際に、対応する Canvas の表示切替と 0.5 秒間の砂嵐演出を実行します。
/// </summary>
public class TelevisionStaticController : MonoBehaviour
{
    [Header("カメラ別 Canvas 設定")]
    [Tooltip("Devil_Camera_Left に対応する Canvas")]
    [SerializeField] private Canvas canvasLeft;

    [Tooltip("Devil_Camera_Right に対応する Canvas")]
    [SerializeField] private Canvas canvasRight;

    [Tooltip("Devil_Camera_Back に対応する Canvas")]
    [SerializeField] private Canvas canvasBack;

    [Tooltip("初期表示用の Play Canvas（Devilキャッチャー遷移時に最初に表示される）")]
    [SerializeField] private Canvas playCanvas;

    [Tooltip("Play Canvas から 30/60/90 を選択した際に、砂嵐を挟んで切り替わる Canvas")]
    [SerializeField] private Canvas playCanvas2;

    [Tooltip("チュートリアルの Yes/No を尋ねる Canvas（ラウンド1のみ表示）")]
    [SerializeField] private Canvas tutorialCanvas;

    [Tooltip("Play Canvas のコンテンツスケール（カメラ内に収める調整用。小さくすると全体が縮小される）")]
    [SerializeField, Range(0.01f, 2.0f)] private float playCanvasScale = 0.5f;

    [Tooltip("Play Canvas2 のコンテンツスケール（カメラ内に収める調整用。小さくすると全体が縮小される）")]
    [SerializeField, Range(0.01f, 2.0f)] private float playCanvas2Scale = 0.5f;

    [Tooltip("Tutorial Canvas のコンテンツスケール（カメラ内に収める調整用。小さくすると全体が縮小される）")]
    [SerializeField, Range(0.01f, 2.0f)] private float tutorialCanvasScale = 0.5f;

    [Header("砂嵐（TV Static）演出設定")]
    [Tooltip("砂嵐を配置した Noise Canvas")]
    [SerializeField] private Canvas noiseCanvas;

    [Tooltip("Noise Canvas 専用のカメラ（NoiseCamera）")]
    [SerializeField] private Camera noiseCamera;

    [Tooltip("動画ファイル不要のリアルタイム砂嵐自動生成機能（動画トラブル時の決定版）")]
    [SerializeField] private ProceduralTVStatic proceduralStatic;

    [Tooltip("砂嵐動画を再生する VideoPlayer")]
    [SerializeField] private VideoPlayer staticVideoPlayer;

    [Tooltip("砂嵐用 RawImage（VideoPlayer の表示用）")]
    [SerializeField] private RawImage staticRawImage;

    [Tooltip("砂嵐全体を表示制御する GameObject / Panel（NoiseCanvasが未設定の場合に使用）")]
    [SerializeField] private GameObject staticNoisePanel;

    [Tooltip("カメラ切り替え時の砂嵐表示時間（秒）")]
    [SerializeField, Min(0.05f)] private float staticDuration = 0.5f;

    [Tooltip("白画面（デコード遅延）を防ぐため、動画をバックグラウンドで常にループ再生しておくか")]
    [SerializeField] private bool keepVideoPlayingInBackground = true;

    [Tooltip("動画冒頭のデコード遅延を避けるため、何秒目から再生を始めるか（ループ時も毎回この位置へシークし直す）")]
    [SerializeField, Min(0f)] private double videoStartOffsetSeconds = 1.0;

    [Header("砂嵐 SE")]
    [Tooltip("砂嵐の表示時間（staticDuration）に合わせてループ再生するか。false の場合は開始時に一度だけ再生する。" +
             "実際のクリップ・音量はDevilSEManagerで一元管理")]
    [SerializeField] private bool loopStaticSound = true;

    private bool _wasTutorialCanvasActive;

    private Coroutine _staticCoroutine;

    /// <summary>
    /// カメラ切り替え（Q/Eキー等）によるCanvas切り替えが有効かどうか。
    /// 初期状態では false（Play_Canvas のみ表示）。外部から EnableCameraSwitching() で有効化する。
    /// </summary>
    private bool _cameraSwitchingEnabled = false;

    /// <summary>
    /// 設定されている間、Q/E のサブカメラ切り替えはこちら（チュートリアル等）のカメラ群を使用する。
    /// null の場合は今まで通り実機の UFOCameraController.Instance を使用する。
    /// </summary>
    private ISubCameraSource _subCameraSourceOverride;

    /// <summary>
    /// Q/E サブカメラ切り替えの参照先を差し替える。TutorialCraneController が、
    /// 練習機の場所へ移動した時に自分自身を設定し、実機側へ戻した時に null を設定する。
    /// </summary>
    public void SetSubCameraSourceOverride(ISubCameraSource source)
    {
        _subCameraSourceOverride = source;
    }

    private void OnEnable()
    {
        UFOCameraController.OnSubCameraChanged += HandleSubCameraChanged;
    }

    private void OnDisable()
    {
        UFOCameraController.OnSubCameraChanged -= HandleSubCameraChanged;

        if (staticVideoPlayer != null)
        {
            staticVideoPlayer.loopPointReached -= OnStaticVideoLoopPointReached;
        }
    }

    private void Start()
    {
        // 動画の事前読み込み・バックグラウンドループ準備（白画面遅延の防止）
        if (staticVideoPlayer != null)
        {
            staticVideoPlayer.isLooping = true;
            staticVideoPlayer.loopPointReached += OnStaticVideoLoopPointReached;
            if (keepVideoPlayingInBackground)
            {
                PlayStaticVideoFromOffset();
            }
            else
            {
                staticVideoPlayer.Prepare();
            }
        }

        // 初期カメラに対応する worldCamera の自動割り当て
        SyncCanvasWorldCameras(UFOCameraController.UfoSubCameraState.Back);

        // 初期状態では砂嵐を非表示にする
        SetStaticActive(false);

        // 初期状態: Play_Canvas のみ表示し、カメラ用 Canvas はすべて非表示
        _cameraSwitchingEnabled = false;
        ShowPlayCanvas();
    }

    private void Update()
    {
        // Tutorial_Canvasの表示/非表示は複数箇所（ShowTutorialCanvas/UpdateCanvasVisibility/
        // ShowPlayCanvas等）から切り替えられるため、呼び出し箇所ごとにループ音を仕込むのではなく、
        // 毎フレーム状態を監視して変化した瞬間にループ再生の開始・停止を行う
        bool isTutorialCanvasActive = tutorialCanvas != null && tutorialCanvas.gameObject.activeSelf;
        if (isTutorialCanvasActive != _wasTutorialCanvasActive)
        {
            _wasTutorialCanvasActive = isTutorialCanvasActive;
            SetTutorialStaticLoopActive(isTutorialCanvasActive);
        }
    }

    /// <summary>
    /// Tutorial_Canvas表示中だけループ再生する砂嵐SEの再生・停止を切り替える。
    /// </summary>
    private void SetTutorialStaticLoopActive(bool active)
    {
        DevilSEManager.Instance?.SetTutorialStaticLoopActive(active);
    }

    /// <summary>
    /// サブカメラ切り替え通知を受け取り、指定 Canvas の表示と 0.5 秒の砂嵐演出を実行します。
    /// </summary>
    public void HandleSubCameraChanged(UFOCameraController.UfoSubCameraState newState)
    {
        if (this == null || gameObject == null || !gameObject.activeInHierarchy) return;

        // カメラ切り替えが無効な間はイベントを無視する
        if (!_cameraSwitchingEnabled) return;

        if (_staticCoroutine != null)
        {
            StopCoroutine(_staticCoroutine);
        }

        _staticCoroutine = StartCoroutine(PlayStaticRoutine(newState));
    }

    private IEnumerator PlayStaticRoutine(UFOCameraController.UfoSubCameraState newState)
    {
        // 1. 各 Canvas の Screen Space - Camera 用 worldCamera を同期
        SyncCanvasWorldCameras(newState);

        // 2. 砂嵐パネルおよび NoiseCamera / NoiseCanvas を表示・再生開始
        SetStaticActive(true);

        if (staticVideoPlayer != null && !keepVideoPlayingInBackground)
        {
            PlayStaticVideoFromOffset();
        }

        // 3. 指定時間（デフォルト 0.5 秒）待機
        yield return new WaitForSeconds(staticDuration);

        // 4. 対応する Canvas の表示を切り替え
        UpdateCanvasVisibility(newState);

        // 5. 砂嵐パネルおよび NoiseCamera を非表示
        SetStaticActive(false);

        if (staticVideoPlayer != null && !keepVideoPlayingInBackground)
        {
            staticVideoPlayer.Pause();
        }

        _staticCoroutine = null;
    }

    /// <summary>
    /// 各カメラに対応する Canvas の表示を切り替えます。
    /// カメラ用 Canvas が表示されると Play_Canvas は自動的に非表示になります。
    /// </summary>
    public void UpdateCanvasVisibility(UFOCameraController.UfoSubCameraState state)
    {
        if (canvasLeft != null)  canvasLeft.gameObject.SetActive(state == UFOCameraController.UfoSubCameraState.Left);
        if (canvasRight != null) canvasRight.gameObject.SetActive(state == UFOCameraController.UfoSubCameraState.Right);
        if (canvasBack != null)  canvasBack.gameObject.SetActive(state == UFOCameraController.UfoSubCameraState.Back);

        // カメラ用 Canvas に切り替えたら Play_Canvas / Play_Canvas2 / Tutorial_Canvas を非表示にする
        if (playCanvas != null) playCanvas.gameObject.SetActive(false);
        if (playCanvas2 != null) playCanvas2.gameObject.SetActive(false);
        if (tutorialCanvas != null) tutorialCanvas.gameObject.SetActive(false);

        SyncTvCamerasEnabled();
    }

    /// <summary>
    /// 砂嵐演出を挟まずに Tutorial_Canvas を直接表示します。
    /// ラウンド1でまだプレイしていない状態で Devilモードに入った瞬間（カメラ到着と同フレーム）に
    /// TouchPanelOutlineController から呼ばれ、Play_Canvas を経由せず最初から Tutorial_Canvas を見せるために使用する。
    /// </summary>
    public void ShowTutorialCanvas()
    {
        if (canvasLeft != null)  canvasLeft.gameObject.SetActive(false);
        if (canvasRight != null) canvasRight.gameObject.SetActive(false);
        if (canvasBack != null)  canvasBack.gameObject.SetActive(false);
        if (playCanvas != null)  playCanvas.gameObject.SetActive(false);
        if (playCanvas2 != null) playCanvas2.gameObject.SetActive(false);
        if (tutorialCanvas != null)
        {
            tutorialCanvas.gameObject.SetActive(true);
            ScaleCanvasContent(tutorialCanvas, tutorialCanvasScale);
        }

        SyncTvCamerasEnabled();
    }

    /// <summary>
    /// Play_Canvas のみを表示し、カメラ用 Canvas をすべて非表示にします。
    /// 初期状態および Play_Canvas に戻す際に使用します。
    /// </summary>
    public void ShowPlayCanvas()
    {
        if (canvasLeft != null)  canvasLeft.gameObject.SetActive(false);
        if (canvasRight != null) canvasRight.gameObject.SetActive(false);
        if (canvasBack != null)  canvasBack.gameObject.SetActive(false);
        if (playCanvas2 != null) playCanvas2.gameObject.SetActive(false);
        if (tutorialCanvas != null) tutorialCanvas.gameObject.SetActive(false);
        if (playCanvas != null)
        {
            playCanvas.gameObject.SetActive(true);
            ScaleCanvasContent(playCanvas, playCanvasScale);
        }

        SyncTvCamerasEnabled();
    }

    /// <summary>
    /// 砂嵐演出を挟んで、現在表示中の Canvas から targetCanvas（Play_Canvas2 等）へ切り替えます。
    /// Play_Canvas 上で 30/60/90 が選択された際に TouchPanelOutlineController から呼び出されます。
    /// </summary>
    public void PlayStaticThenShowCanvas(Canvas targetCanvas)
    {
        if (this == null || gameObject == null || !gameObject.activeInHierarchy) return;

        if (_canvasSwitchCoroutine != null)
        {
            StopCoroutine(_canvasSwitchCoroutine);
        }
        _canvasSwitchCoroutine = StartCoroutine(PlayStaticThenShowCanvasRoutine(targetCanvas));
    }

    private Coroutine _canvasSwitchCoroutine;

    private IEnumerator PlayStaticThenShowCanvasRoutine(Canvas targetCanvas)
    {
        SetStaticActive(true);

        if (staticVideoPlayer != null && !keepVideoPlayingInBackground)
        {
            PlayStaticVideoFromOffset();
        }

        yield return new WaitForSeconds(staticDuration);

        if (canvasLeft != null)  canvasLeft.gameObject.SetActive(false);
        if (canvasRight != null) canvasRight.gameObject.SetActive(false);
        if (canvasBack != null)  canvasBack.gameObject.SetActive(false);
        if (playCanvas != null)  playCanvas.gameObject.SetActive(false);
        if (playCanvas2 != null) playCanvas2.gameObject.SetActive(false);
        if (tutorialCanvas != null) tutorialCanvas.gameObject.SetActive(false);
        if (targetCanvas != null)
        {
            targetCanvas.gameObject.SetActive(true);
            ScaleCanvasContent(targetCanvas, GetScaleForCanvas(targetCanvas));
        }

        SyncTvCamerasEnabled();

        SetStaticActive(false);

        if (staticVideoPlayer != null && !keepVideoPlayingInBackground)
        {
            staticVideoPlayer.Pause();
        }

        _canvasSwitchCoroutine = null;
    }

    /// <summary>
    /// Play_Canvas / canvasLeft / canvasRight / canvasBack / tutorialCanvas の worldCamera（PlayCamera, 左右背面カメラ,
    /// TutorialCamera）は全て同じ RenderTexture へ描画するため、対応する Canvas が非表示の間は必ず無効化しておかないと
    /// 描画が競合してしまう（例: UFOCameraController.SetSubCameraState はカメラ切り替えが未解禁でも
    /// backCamera.enabled を直接 true にするため、対応する canvasBack が表示されるまでは無効化し続ける必要がある）。
    /// tutorialCanvas 用の TutorialCamera をここで無効化し忘れると、Tutorial_Canvas から Play_Canvas へ戻った後も
    /// TutorialCamera が同じ RenderTexture へ描画し続け、PlayCamera の描画結果を上書きしてしまい
    /// 「Play_Canvas に戻っても映らない」状態になる。
    /// Play_Canvas2 は Screen Space - Overlay（専用カメラなし）で TV 画面の上に重ねて表示する仕組みのため、
    /// Play_Canvas2 表示中もその背景として PlayCamera は有効なままにしておく
    /// （そうしないと RenderTexture に描画するカメラが1つも無くなり、直前の砂嵐フレームで画面が固まってしまう）。
    /// Canvas の表示切り替えを行うメソッドの最後で必ず呼び出す。
    /// </summary>
    private void SyncTvCamerasEnabled()
    {
        bool playCameraNeeded = (playCanvas != null && playCanvas.gameObject.activeSelf)
            || (playCanvas2 != null && playCanvas2.gameObject.activeSelf);
        SetCanvasCameraEnabled(playCanvas, playCameraNeeded);

        SyncCanvasCameraEnabled(canvasLeft);
        SyncCanvasCameraEnabled(canvasRight);
        SyncCanvasCameraEnabled(canvasBack);
        SyncCanvasCameraEnabled(tutorialCanvas);
    }

    private static void SyncCanvasCameraEnabled(Canvas canvas)
    {
        if (canvas == null) return;
        SetCanvasCameraEnabled(canvas, canvas.gameObject.activeSelf);
    }

    private static void SetCanvasCameraEnabled(Canvas canvas, bool active)
    {
        if (canvas == null) return;
        Camera cam = canvas.worldCamera;
        if (cam == null) return;
        cam.enabled = active;
    }

    /// <summary>
    /// 対象 Canvas に対応するスケール値（playCanvasScale / playCanvas2Scale / tutorialCanvasScale）を返します。
    /// </summary>
    private float GetScaleForCanvas(Canvas canvas)
    {
        if (canvas == playCanvas2) return playCanvas2Scale;
        if (canvas == tutorialCanvas) return tutorialCanvasScale;
        return playCanvasScale;
    }

    /// <summary>
    /// 指定した Canvas 内の全子要素を __PlayCanvasScaleContainer にまとめ、scale 倍にスケーリングしてカメラ内に収めます。
    /// Play_Canvas / Play_Canvas2 / Tutorial_Canvas で共通して使用します（それぞれ playCanvasScale / playCanvas2Scale / tutorialCanvasScale で調整）。
    /// </summary>
    private void ScaleCanvasContent(Canvas canvas, float scale)
    {
        if (canvas == null) return;

        RectTransform canvasRT = canvas.GetComponent<RectTransform>();
        if (canvasRT == null) return;

        // Canvas直下のコンテナを探す or 作成する
        Transform container = canvasRT.Find("__PlayCanvasScaleContainer");
        if (container == null)
        {
            // 初回のみ: 既存の子要素をすべてコンテナの下に移動する
            GameObject containerObj = new GameObject("__PlayCanvasScaleContainer", typeof(RectTransform));
            RectTransform containerRT = containerObj.GetComponent<RectTransform>();
            containerRT.SetParent(canvasRT, false);

            // コンテナを Canvas 全体に広げる
            containerRT.anchorMin = Vector2.zero;
            containerRT.anchorMax = Vector2.one;
            containerRT.offsetMin = Vector2.zero;
            containerRT.offsetMax = Vector2.zero;
            containerRT.pivot = new Vector2(0.5f, 0.5f);

            // 既存の子要素をコンテナ配下に移動
            var children = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < canvasRT.childCount; i++)
            {
                Transform child = canvasRT.GetChild(i);
                if (child != containerRT.transform)
                {
                    children.Add(child);
                }
            }
            foreach (var child in children)
            {
                child.SetParent(containerRT, false);
            }

            container = containerRT;
        }

        // スケールを適用
        container.localScale = new Vector3(scale, scale, 1f);

        // PosZ を固定
        Vector3 pos = container.localPosition;
        pos.z = 10.69f;
        container.localPosition = pos;
    }

    /// <summary>
    /// カメラ切り替え（Q/Eキー等）を有効化します。
    /// 有効化後は OnSubCameraChanged イベントに応じて Canvas が切り替わります。
    /// </summary>
    public void EnableCameraSwitching()
    {
        _cameraSwitchingEnabled = true;
        Debug.Log("[TelevisionStaticController] カメラ切り替えを有効化しました。");
    }

    /// <summary>
    /// カメラ切り替えを無効化し、Play_Canvas を表示します。
    /// </summary>
    public void DisableCameraSwitching()
    {
        _cameraSwitchingEnabled = false;
        ShowPlayCanvas();
        Debug.Log("[TelevisionStaticController] カメラ切り替えを無効化し、Play_Canvas を表示しました。");
    }

    /// <summary>
    /// カメラ切り替えが現在有効かどうかを返します。
    /// </summary>
    public bool IsCameraSwitchingEnabled => _cameraSwitchingEnabled;

    /// <summary>
    /// カメラ切り替え（Q/Eキー等）の有効・無効だけを切り替える（Canvas表示には触れない）。
    /// TutorialCraneController など、Canvas切り替えを自前で管理している側から使用する
    /// （EnableCameraSwitching/DisableCameraSwitching は Play_Canvas 表示も兼ねてしまうため使えない）。
    /// </summary>
    public void SetCameraSwitchingEnabled(bool enabled)
    {
        _cameraSwitchingEnabled = enabled;
    }

    /// <summary>
    /// 初期表示用の Play Canvas を外部（TouchPanelOutlineController 等）から参照するための公開プロパティ。
    /// </summary>
    public Canvas PlayCanvas => playCanvas;

    /// <summary>
    /// 30/60/90 選択後に切り替わる Play Canvas2 を外部から参照するための公開プロパティ。
    /// </summary>
    public Canvas PlayCanvas2 => playCanvas2;

    /// <summary>
    /// チュートリアルのYes/Noを尋ねる Canvas を外部（TutorialPromptController等）から参照するための公開プロパティ。
    /// </summary>
    public Canvas TutorialCanvas => tutorialCanvas;

    /// <summary>
    /// Play Canvas2 が現在表示中かどうか。UFOCameraController が Esc/F キーの挙動を切り替える際に参照する。
    /// </summary>
    public bool IsPlayCanvas2Active => playCanvas2 != null && playCanvas2.gameObject.activeSelf;

    /// <summary>
    /// Screen Space - Camera モード使用時、各 Canvas の worldCamera を対応するサブカメラ / NoiseCamera に動的割り当てします。
    /// </summary>
    public void SyncCanvasWorldCameras(UFOCameraController.UfoSubCameraState state)
    {
        ISubCameraSource source = _subCameraSourceOverride;
        if (source == null)
        {
            if (UFOCameraController.Instance == null) return;
            source = UFOCameraController.Instance;
        }

        Camera leftCam = source.GetSubCamera(UFOCameraController.UfoSubCameraState.Left);
        Camera rightCam = source.GetSubCamera(UFOCameraController.UfoSubCameraState.Right);
        Camera backCam = source.GetSubCamera(UFOCameraController.UfoSubCameraState.Back);
        Camera activeCam = source.GetSubCamera(state);

        if (canvasLeft != null && leftCam != null && canvasLeft.renderMode == RenderMode.ScreenSpaceCamera)
            canvasLeft.worldCamera = leftCam;

        if (canvasRight != null && rightCam != null && canvasRight.renderMode == RenderMode.ScreenSpaceCamera)
            canvasRight.worldCamera = rightCam;

        if (canvasBack != null && backCam != null && canvasBack.renderMode == RenderMode.ScreenSpaceCamera)
            canvasBack.worldCamera = backCam;

        if (noiseCanvas != null && noiseCanvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            noiseCanvas.worldCamera = noiseCamera != null ? noiseCamera : activeCam;
        }
    }

    private void SetStaticActive(bool active)
    {
        // 1. 専用カメラ (NoiseCamera) の制御
        if (noiseCamera != null)
        {
            noiseCamera.enabled = active;
        }

        // 2. リアルタイム自動砂嵐生成スクリプトの制御
        if (proceduralStatic != null)
        {
            proceduralStatic.enabled = active;
            proceduralStatic.gameObject.SetActive(active);
        }

        // 3. 最上位の親コンテナ (Noise Canvas > Panel > RawImage) を優先制御
        if (noiseCanvas != null)
        {
            noiseCanvas.gameObject.SetActive(active);
        }
        else if (staticNoisePanel != null)
        {
            staticNoisePanel.SetActive(active);
        }
        else if (staticRawImage != null)
        {
            staticRawImage.gameObject.SetActive(active);
        }
        else if (staticVideoPlayer != null)
        {
            staticVideoPlayer.gameObject.SetActive(active);
        }

        // 4. 砂嵐 SE の再生・停止
        PlayStaticSound(active);
    }

    private void PlayStaticSound(bool active)
    {
        if (active)
        {
            if (loopStaticSound)
            {
                DevilSEManager.Instance?.StartTelevisionStaticLoop();
            }
            else
            {
                DevilSEManager.Instance?.PlayTelevisionStaticOneShot();
            }
        }
        else if (loopStaticSound)
        {
            DevilSEManager.Instance?.StopTelevisionStaticLoop();
        }
    }

    /// <summary>
    /// 冒頭のデコード遅延（白画面/カクつき）を避けるため、videoStartOffsetSeconds 秒目までシークしてから再生する。
    /// </summary>
    private void PlayStaticVideoFromOffset()
    {
        if (staticVideoPlayer == null) return;
        staticVideoPlayer.time = videoStartOffsetSeconds;
        staticVideoPlayer.Play();
    }

    /// <summary>
    /// ループ再生で先頭（0秒）に戻った瞬間に呼ばれる。毎回 videoStartOffsetSeconds 秒目へシークし直す。
    /// </summary>
    private void OnStaticVideoLoopPointReached(VideoPlayer vp)
    {
        if (vp == null) return;
        vp.time = videoStartOffsetSeconds;
    }

    // --- コンテキストメニュー / テスト用メソッド ---

    [ContextMenu("1. テスト: 砂嵐再生 (0.5秒)")]
    public void TestPlayStatic()
    {
        if (_staticCoroutine != null) StopCoroutine(_staticCoroutine);
        _staticCoroutine = StartCoroutine(PlayStaticRoutine(UFOCameraController.UfoSubCameraState.Back));
    }

    [ContextMenu("2. テスト: Left Canvas 表示")]
    public void TestShowLeft()
    {
        UpdateCanvasVisibility(UFOCameraController.UfoSubCameraState.Left);
    }

    [ContextMenu("3. テスト: Right Canvas 表示")]
    public void TestShowRight()
    {
        UpdateCanvasVisibility(UFOCameraController.UfoSubCameraState.Right);
    }

    [ContextMenu("4. テスト: Back Canvas 表示")]
    public void TestShowBack()
    {
        UpdateCanvasVisibility(UFOCameraController.UfoSubCameraState.Back);
    }
}
