using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// UFOキャッチャーのカメラ切り替えおよびキーボード操作モードを管理するクラス。
/// </summary>
public class UFOCameraController : MonoBehaviour
{
    public static bool IsPlayingUfo { get; private set; } = false;

    [Header("カメラ設定")]
    [Tooltip("プレイヤーの1人称カメラ")]
    [SerializeField] private Camera playerCamera;

    [Tooltip("UFOキャッチャーの正面カメラ（未指定の場合は自動取得）")]
    [SerializeField] private Camera frontCamera;

    [Tooltip("左側カメラ（未指定の場合は自動生成）")]
    [SerializeField] private Camera leftCamera;

    [Tooltip("右側カメラ（未指定の場合は自動生成）")]
    [SerializeField] private Camera rightCamera;

    [Tooltip("上部カメラ（未指定の場合は自動生成）")]
    [SerializeField] private Camera topCamera;

    [Header("インタラクション設定")]
    [Tooltip("プレイを開始できる距離")]
    [SerializeField] private float interactionDistance = 3.5f;

    [Tooltip("UFOキャッチャーの操作の中心となるTransform（アームの動くエリアの基準など。未指定の場合はこのスクリプトのObject）")]
    [SerializeField] private Transform ufoCenterTransform;

    private App.Player.FirstPersonController _fpController;
    private UFOArmController _ufoController;
    private Camera _activeCamera;
    private bool _showPrompt = false;
    private Texture2D _bgTexture;

    private void Start()
    {
        // プレイヤーとUFOキャッチャーのアームコントローラーを自動検出
        _fpController = FindAnyObjectByType<App.Player.FirstPersonController>();
        _ufoController = FindAnyObjectByType<UFOArmController>();

        if (ufoCenterTransform == null)
        {
            ufoCenterTransform = transform;
        }

        if (_fpController != null && playerCamera == null)
        {
            playerCamera = _fpController.GetComponentInChildren<Camera>(true);
        }

        SetupDynamicCameras();

        // 開始時はUFOプレイモードではない状態にする
        SetUfoMode(false);
    }

    private void SetupDynamicCameras()
    {
        if (frontCamera == null)
        {
            frontCamera = transform.Find("Ufo_camera")?.GetComponent<Camera>();
            if (frontCamera == null)
            {
                // 子オブジェクトからカメラを検索
                frontCamera = GetComponentInChildren<Camera>(true);
            }
        }

        if (frontCamera == null)
        {
            Debug.LogWarning("[UFOCameraController] 正面カメラ (frontCamera) が見つかりません。自動生成用カメラをベースにできません。");
            return;
        }

        // オーディオマネージャー等の警告対策として、UFO側のカメラのAudioListenerは無効化しておく
        var listener = frontCamera.GetComponent<AudioListener>();
        if (listener != null) listener.enabled = false;

        // 左側カメラの自動生成
        if (leftCamera == null)
        {
            leftCamera = CreateCopyCamera("Ufo_camera_left");
            leftCamera.transform.SetParent(ufoCenterTransform, false);
            // 正面 (9.98, 6.86, -3.465) から見て左側 (Z負方向) に配置
            leftCamera.transform.localPosition = new Vector3(0f, 6.86f, -13.465f);
            leftCamera.transform.localEulerAngles = new Vector3(25f, 0f, 0f);
        }

        // 右側カメラの自動生成
        if (rightCamera == null)
        {
            rightCamera = CreateCopyCamera("Ufo_camera_right");
            rightCamera.transform.SetParent(ufoCenterTransform, false);
            // 正面から見て右側 (Z正方向) に配置
            rightCamera.transform.localPosition = new Vector3(0f, 6.86f, 6.535f);
            rightCamera.transform.localEulerAngles = new Vector3(25f, 180f, 0f);
        }

        // 上部カメラの自動生成
        if (topCamera == null)
        {
            topCamera = CreateCopyCamera("Ufo_camera_top");
            topCamera.transform.SetParent(ufoCenterTransform, false);
            // 真上から見下ろす配置
            topCamera.transform.localPosition = new Vector3(0f, 12f, -3.465f);
            topCamera.transform.localEulerAngles = new Vector3(90f, -90f, 0f);
        }
    }

    private Camera CreateCopyCamera(string name)
    {
        GameObject go = new GameObject(name);
        Camera newCam = go.AddComponent<Camera>();

        // 正面カメラの設定を複製
        newCam.fieldOfView = frontCamera.fieldOfView;
        newCam.nearClipPlane = frontCamera.nearClipPlane;
        newCam.farClipPlane = frontCamera.farClipPlane;
        newCam.cullingMask = frontCamera.cullingMask;
        newCam.clearFlags = frontCamera.clearFlags;
        newCam.backgroundColor = frontCamera.backgroundColor;
        newCam.depth = frontCamera.depth - 1; // プレイヤーカメラの下にするため低めに設定
        newCam.enabled = false;

        // URP対応：UniversalAdditionalCameraData のコピー
        var srcData = frontCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (srcData != null)
        {
            var destData = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            destData.renderShadows = srcData.renderShadows;
            destData.requiresDepthOption = srcData.requiresDepthOption;
            destData.requiresColorOption = srcData.requiresColorOption;
            destData.renderPostProcessing = srcData.renderPostProcessing;
            destData.antialiasing = srcData.antialiasing;
        }

        // AudioListenerがあれば無効化
        var listener = go.GetComponent<AudioListener>();
        if (listener != null) listener.enabled = false;

        return newCam;
    }

    private void Update()
    {
        if (_fpController == null)
        {
            _fpController = FindAnyObjectByType<App.Player.FirstPersonController>();
            if (_fpController == null) return;
        }

        // プレイヤーとUFOキャッチャー筐体中心との距離を計算
        float distance = Vector3.Distance(_fpController.transform.position, ufoCenterTransform.position);
        bool isClose = distance <= interactionDistance;

        if (!IsPlayingUfo)
        {
            // プレイ中でなく、かつ近ければプロンプトを表示。Fキーで開始
            if (isClose)
            {
                _showPrompt = true;
                if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                {
                    EnterUfoMode();
                }
            }
            else
            {
                _showPrompt = false;
            }
        }
        else
        {
            _showPrompt = false;
            HandleUfoInput();
        }
    }

    private void EnterUfoMode()
    {
        SetUfoMode(true);
        SwitchToCamera(frontCamera);
    }

    private void ExitUfoMode()
    {
        SetUfoMode(false);
        SwitchToCamera(playerCamera);
    }

    private void SetUfoMode(bool active)
    {
        IsPlayingUfo = active;

        if (_fpController != null)
        {
            if (active)
            {
                // キャラクターの移動・回転アニメーションをリセットしてからスクリプトを停止する
                var animator = _fpController.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.SetFloat("Speed", 0f);
                }
            }
            _fpController.enabled = !active;
        }

        // カーソルの表示状態を切り替え
        if (active)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void SwitchToCamera(Camera targetCamera)
    {
        if (targetCamera == null) return;

        // 全てのカメラを一旦非アクティブにする
        if (playerCamera != null) playerCamera.enabled = false;
        if (frontCamera != null) frontCamera.enabled = false;
        if (leftCamera != null) leftCamera.enabled = false;
        if (rightCamera != null) rightCamera.enabled = false;
        if (topCamera != null) topCamera.enabled = false;

        // ターゲットカメラを有効化
        targetCamera.enabled = true;
        _activeCamera = targetCamera;
    }

    private void HandleUfoInput()
    {
        if (Keyboard.current == null) return;

        // モード終了（F, Escape, Q）
        if (Keyboard.current.fKey.wasPressedThisFrame ||
            Keyboard.current.escapeKey.wasPressedThisFrame ||
            Keyboard.current.qKey.wasPressedThisFrame)
        {
            ExitUfoMode();
            return;
        }

        // 視点切り替えキー (1:前, 2:左, 3:右, 4:上)
        if (Keyboard.current.digit1Key.wasPressedThisFrame) SwitchToCamera(frontCamera);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) SwitchToCamera(leftCamera);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) SwitchToCamera(rightCamera);
        if (Keyboard.current.digit4Key.wasPressedThisFrame) SwitchToCamera(topCamera);
    }

    private void OnGUI()
    {
        if (_showPrompt)
        {
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            float boxWidth = 350f;
            float boxHeight = 50f;
            float boxX = (screenWidth - boxWidth) / 2f;
            float boxY = screenHeight - boxHeight - 80f;

            if (_bgTexture == null)
            {
                _bgTexture = new Texture2D(1, 1);
                _bgTexture.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.1f, 0.8f));
                _bgTexture.Apply();
            }

            GUIStyle boxStyle = new GUIStyle();
            boxStyle.normal.background = _bgTexture;
            boxStyle.alignment = TextAnchor.MiddleCenter;

            GUIStyle textStyle = new GUIStyle();
            textStyle.fontStyle = FontStyle.Bold;
            textStyle.fontSize = 18;
            textStyle.normal.textColor = Color.white;
            textStyle.alignment = TextAnchor.MiddleCenter;

            GUI.Box(new Rect(boxX, boxY, boxWidth, boxHeight), "", boxStyle);
            GUI.Label(new Rect(boxX, boxY, boxWidth, boxHeight), "[F] UFOキャッチャーをプレイする", textStyle);
        }
    }

    private void OnDestroy()
    {
        if (_bgTexture != null)
        {
            Destroy(_bgTexture);
        }
    }
}
