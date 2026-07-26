using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;

/// <summary>
/// television オブジェクトや UI 用にアタッチして使用するスクリプト。
/// サブカメラ（Left, Right, Back）を Q/E キーで切り替えた際に、対応する Canvas の表示切替と 0.5 秒間の砂嵐演出を実行します。
/// </summary>
public class TelevisionStaticController : MonoBehaviour
{
    [Header("カメラ別 Canvas 設定")]
    [Tooltip("Ufo_Camera_Left に対応する Canvas")]
    [SerializeField] private Canvas canvasLeft;

    [Tooltip("Ufo_Camera_Right に対応する Canvas")]
    [SerializeField] private Canvas canvasRight;

    [Tooltip("Ufo_Camera_Back に対応する Canvas")]
    [SerializeField] private Canvas canvasBack;

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

    private Coroutine _staticCoroutine;

    private void OnEnable()
    {
        UFOCameraController.OnSubCameraChanged += HandleSubCameraChanged;
    }

    private void OnDisable()
    {
        UFOCameraController.OnSubCameraChanged -= HandleSubCameraChanged;
    }

    private void Start()
    {
        // 動画の事前読み込み・バックグラウンドループ準備（白画面遅延の防止）
        if (staticVideoPlayer != null)
        {
            staticVideoPlayer.isLooping = true;
            if (keepVideoPlayingInBackground)
            {
                staticVideoPlayer.Play();
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

        // 初期カメラの Canvas 可視化（Back をデフォルトとする）
        UpdateCanvasVisibility(UFOCameraController.UfoSubCameraState.Back);
    }

    /// <summary>
    /// サブカメラ切り替え通知を受け取り、指定 Canvas の表示と 0.5 秒の砂嵐演出を実行します。
    /// </summary>
    public void HandleSubCameraChanged(UFOCameraController.UfoSubCameraState newState)
    {
        if (this == null || gameObject == null || !gameObject.activeInHierarchy) return;

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
            staticVideoPlayer.Play();
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
    /// </summary>
    public void UpdateCanvasVisibility(UFOCameraController.UfoSubCameraState state)
    {
        if (canvasLeft != null)  canvasLeft.gameObject.SetActive(state == UFOCameraController.UfoSubCameraState.Left);
        if (canvasRight != null) canvasRight.gameObject.SetActive(state == UFOCameraController.UfoSubCameraState.Right);
        if (canvasBack != null)  canvasBack.gameObject.SetActive(state == UFOCameraController.UfoSubCameraState.Back);
    }

    /// <summary>
    /// Screen Space - Camera モード使用時、各 Canvas の worldCamera を対応するサブカメラ / NoiseCamera に動的割り当てします。
    /// </summary>
    public void SyncCanvasWorldCameras(UFOCameraController.UfoSubCameraState state)
    {
        if (UFOCameraController.Instance == null) return;

        Camera leftCam = UFOCameraController.Instance.GetSubCamera(UFOCameraController.UfoSubCameraState.Left);
        Camera rightCam = UFOCameraController.Instance.GetSubCamera(UFOCameraController.UfoSubCameraState.Right);
        Camera backCam = UFOCameraController.Instance.GetSubCamera(UFOCameraController.UfoSubCameraState.Back);
        Camera activeCam = UFOCameraController.Instance.GetSubCamera(state);

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
