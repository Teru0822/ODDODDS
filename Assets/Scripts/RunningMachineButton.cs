using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 3Dオブジェクトの物理ボタン用のコントローラー。
/// マウスクリック時に押し込まれる演出を行い、プレイヤーのレーン移動またはジャンプをトリガーします。
/// </summary>
public class RunningMachineButton : MonoBehaviour
{
    public enum ButtonType { Left, Right, Jump }

    [Header("移動コントローラーへの参照")]
    [SerializeField] private LaneMovementController movementController;

    [Header("ボタンの動作タイプ")]
    [SerializeField] private ButtonType buttonType = ButtonType.Left;

    [Header("使用するカメラ（空欄の場合は有効なカメラが自動で選ばれます）")]
    [SerializeField] private Camera targetCamera;

    [Header("ボタンの押し込み設定")]
    [Tooltip("ボタンが押し込まれるローカルY方向 of 量")]
    [SerializeField] private float pressDepth = 0.05f;
    [Tooltip("押し込み／戻りのスピード")]
    [SerializeField] private float pressSpeed = 20f;

    [Header("無効化時のビジュアル")]
    [Tooltip("ボタンが無効化されたときの色（元のマテリアルカラーとブレンドされます）")]
    [SerializeField] private Color disabledColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    private Vector3 _originalLocalPos;
    private bool _isPressed = false;
    private bool _isActive = true;
    private Collider _collider;
    private Renderer _renderer;
    private Color _originalColor;

    void Start()
    {
        _originalLocalPos = transform.localPosition;
        
        // 自身または子オブジェクトからコライダーを取得
        _collider = GetComponent<Collider>();
        if (_collider == null)
        {
            _collider = GetComponentInChildren<Collider>();
        }

        // 自身または子オブジェクトからレンダラーを取得
        _renderer = GetComponent<Renderer>();
        if (_renderer == null)
        {
            _renderer = GetComponentInChildren<Renderer>();
        }
        
        if (_renderer != null)
        {
            _originalColor = _renderer.material.color;
        }

        if (_collider == null)
        {
            Debug.LogError($"[RunningMachineButton] {gameObject.name} またはその子オブジェクトに Collider が見つかりません。マウスのクリックを検知するためにはコライダーが必須です。");
        }

        if (movementController == null)
        {
            Debug.LogWarning($"[RunningMachineButton] {gameObject.name} に MovementController が設定されていません。インスペクターでアタッチしてください。");
        }
    }

    void Update()
    {
        // 非アクティブ（無効化）時の挙動
        if (!_isActive)
        {
            _isPressed = false;
            float disabledTargetY = _originalLocalPos.y - pressDepth * 0.4f;
            Vector3 disabledPos = transform.localPosition;
            disabledPos.y = Mathf.Lerp(disabledPos.y, disabledTargetY, Time.deltaTime * pressSpeed);
            transform.localPosition = disabledPos;
            return;
        }

#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null)
        {
            // クリック開始
            if (mouse.leftButton.wasPressedThisFrame)
            {
                Debug.Log($"[RunningMachineButton] クリック検知しました。判定開始。マウス位置: {mouse.position.ReadValue()}");
                if (IsMouseOverThis(mouse.position.ReadValue()))
                {
                    Debug.Log($"[RunningMachineButton] クリックがボタン「{gameObject.name}」にヒット！アクションを実行します。");
                    _isPressed = true;
                    TriggerAction();
                }
                else
                {
                    Debug.Log($"[RunningMachineButton] クリックがボタン「{gameObject.name}」にヒットしませんでした。(Collider={(_collider != null)})");
                }
            }

            // クリック終了
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                _isPressed = false;
            }
        }
#else
        // 旧入力システムのフォールバック
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log($"[RunningMachineButton] (旧)クリック検知しました。判定開始。マウス位置: {Input.mousePosition}");
            if (IsMouseOverThis(Input.mousePosition))
            {
                Debug.Log($"[RunningMachineButton] (旧)クリックがボタン「{gameObject.name}」にヒット！アクションを実行します。");
                _isPressed = true;
                TriggerAction();
            }
            else
            {
                Debug.Log($"[RunningMachineButton] (旧)クリックがボタン「{gameObject.name}」にヒットしませんでした。(Collider={(_collider != null)})");
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            _isPressed = false;
        }
#endif

        // 押し込みアニメーションの計算
        float targetY = _isPressed ? _originalLocalPos.y - pressDepth : _originalLocalPos.y;
        Vector3 pos = transform.localPosition;
        pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * pressSpeed);
        transform.localPosition = pos;
    }

    private void TriggerAction()
    {
        if (movementController == null)
        {
            Debug.LogError($"[RunningMachineButton] {gameObject.name} に MovementController がアタッチされていないため、アクションを実行できません。");
            return;
        }

        switch (buttonType)
        {
            case ButtonType.Left:
                movementController.MoveLeft();
                break;
            case ButtonType.Right:
                movementController.MoveRight();
                break;
            case ButtonType.Jump:
                movementController.Jump();
                break;
        }
    }

    /// <summary>
    /// ボタンの有効・無効状態を設定する
    /// </summary>
    public void SetActiveState(bool active)
    {
        _isActive = active;
        
        if (_collider != null)
        {
            _collider.enabled = active;
        }

        if (_renderer != null)
        {
            _renderer.material.color = active ? _originalColor : disabledColor;
        }
    }

    /// <summary>
    /// マウスの座標からこのボタンがクリックされたか判定する
    /// </summary>
    private bool IsMouseOverThis(Vector2 screenPos)
    {
        if (_collider == null) return false;

        // 現在有効なカメラを取得する
        Camera activeCam = targetCamera;
        
        // 登録されているカメラがない、または非アクティブの場合は、現在画面を描画しているアクティブなカメラを検索する
        if (activeCam == null || !activeCam.gameObject.activeInHierarchy)
        {
            activeCam = Camera.main;
        }
        
        if (activeCam == null || !activeCam.gameObject.activeInHierarchy)
        {
            // シーン内のアクティブなすべてのカメラから検索
            Camera[] cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in cams)
            {
                if (cam.gameObject.activeInHierarchy)
                {
                    activeCam = cam;
                    break;
                }
            }
        }

        if (activeCam == null)
        {
            Debug.LogError("[RunningMachineButton] シーン内に有効なアクティブカメラが見つかりません。");
            return false;
        }

        Ray ray = activeCam.ScreenPointToRay(screenPos);
        
        // デバッグ用にRayをシーンビューに描画
        Debug.DrawRay(ray.origin, ray.direction * 1000f, Color.red, 1f);

        return _collider.Raycast(ray, out _, 1000f);
    }
}
