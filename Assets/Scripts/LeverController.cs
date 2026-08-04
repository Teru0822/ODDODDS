using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// レバーメッシュにアタッチ。
/// カメラのスクリーン空間を基準に、マウスの動き方向へ単一軸で傾ける。
/// 複数軸の組み合わせによる干渉をなくした実装。
/// </summary>
public class LeverController : MonoBehaviour
{
    [Header("連携")]
    [Tooltip("UFOArmController が付いているオブジェクト")]
    public UFOArmController armController;

    [Header("レバー設定")]
    [Tooltip("レバーの最大傾き角度（度）")]
    public float leverMaxAngle = 30f;
    [Tooltip("マウス移動量に対する感度")]
    public float sensitivity = 120f;
    [Tooltip("離したときに中央へ戻る速さ")]
    public float returnSpeed = 8f;

    [Tooltip("操作時の重さ（慣性）。小さいほど重く・遅れて動き、大きいほど軽く・キビキビ動きます")]
    public float weightDamping = 10f;

    [Header("操作アシスト（斜めブレ防止）")]
    [Tooltip("縦・横の入力がどちらかに偏っている時、弱い方のブレ入力を無効化する強さ。\n0で完全な斜め自由、1で完全に十字方向（上下または左右専用）に固定します。")]
    [Range(0f, 1f)]
    public float axisSnapAssist = 0.6f;

    [Header("方向反転（見た目が逆の場合にチェック）")]
    public bool invertHorizontal = false;
    public bool invertVertical   = false;

    [Header("ピボット補正")]
    [Tooltip("回転の基準（ピボット）とするオブジェクト。未指定の場合は自身のピボットを使用します。")]
    public Transform pivotOverride;

    // 内部状態
    private bool       _isDragging      = false;
    private float      _targetAngleH    = 0f;   // マウス入力による目標角度
    private float      _targetAngleV    = 0f;
    private float      _angleH          = 0f;   // 実際の現在の角度（Lerpで追従）
    private float      _angleV          = 0f;
    private Quaternion _initialWorldRot;         // レバーの初期ワールド回転
    private Vector3    _leverInitialUp;          // レバー棒の初期ワールド上方向
    private Collider   _collider;
    private Vector3    _localOffsetFromPivot;    // ピボットからの初期ローカル座標オフセット

    void Start()
    {
        _initialWorldRot = transform.rotation;
        _leverInitialUp  = transform.up;          // レバーが +Y 以外を向いていても対応
        _collider        = GetComponent<Collider>();

        if (pivotOverride != null)
        {
            // ピボット基準点から見た自身の初期ローカル座標オフセットを記録
            _localOffsetFromPivot = pivotOverride.InverseTransformPoint(transform.position);
        }
    }

    void Update()
    {
        // UFOプレイ中かつアクティブなプレイセッション中のみ操作を許可する
        if (!UFOCameraController.IsPlayingUfo || !UFOCameraController.IsControlActive)
        {
            _isDragging = false;
            _targetAngleH = 0f;
            _targetAngleV = 0f;
            _angleH = Mathf.Lerp(_angleH, _targetAngleH, Time.deltaTime * returnSpeed);
            _angleV = Mathf.Lerp(_angleV, _targetAngleV, Time.deltaTime * returnSpeed);
            ApplyLeverRotation();
            UpdateArmInput();
            return;
        }

        var mouse = Mouse.current;
        var keyboard = Keyboard.current;

        // クリック検出
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame && IsMouseOverThis(mouse.position.ReadValue()))
            {
                _isDragging = true;
                UFOCameraController.Instance?.NotifyControlInputUsed();
            }
            if (mouse.leftButton.wasReleasedThisFrame)
                _isDragging = false;
        }

        // キーボード（WASD）入力の検出。マウスでドラッグ中でない間、レバーの見た目も連動して倒す
        float keyH = 0f; // 左右（A/D）
        float keyV = 0f; // 前後（W/S）
        if (keyboard != null && !_isDragging)
        {
            if (keyboard.dKey.isPressed) keyH += 1f;
            if (keyboard.aKey.isPressed) keyH -= 1f;
            if (keyboard.wKey.isPressed) keyV += 1f;
            if (keyboard.sKey.isPressed) keyV -= 1f;
        }
        bool isKeyboardActive = (keyH != 0f || keyV != 0f);

        float dirH = invertHorizontal ? -1f : 1f;
        float dirV = invertVertical   ? -1f : 1f;

        if (_isDragging)
        {
            Vector2 delta = mouse.delta.ReadValue();

            _targetAngleH += delta.x * dirH * sensitivity * Time.deltaTime;
            _targetAngleV += delta.y * dirV * sensitivity * Time.deltaTime;
            _targetAngleH  = Mathf.Clamp(_targetAngleH, -leverMaxAngle, leverMaxAngle);
            _targetAngleV  = Mathf.Clamp(_targetAngleV, -leverMaxAngle, leverMaxAngle);
        }
        else if (isKeyboardActive)
        {
            UFOCameraController.Instance?.NotifyControlInputUsed();

            // キーを押している間は最大角度までレバーを倒す（マウスドラッグの最大到達状態と同じ）
            _targetAngleH = keyH * dirH * leverMaxAngle;
            _targetAngleV = keyV * dirV * leverMaxAngle;
        }
        else
        {
            // 手を離したら目標角度を0（中央）に戻す
            _targetAngleH = 0f;
            _targetAngleV = 0f;
        }

        // 常にLerpで追従させることで、手ごたえ（重さ・慣性）を表現
        float damping = (_isDragging || isKeyboardActive) ? weightDamping : returnSpeed;
        _angleH = Mathf.Lerp(_angleH, _targetAngleH, Time.deltaTime * damping);
        _angleV = Mathf.Lerp(_angleV, _targetAngleV, Time.deltaTime * damping);

        ApplyLeverRotation();
        UpdateArmInput();
    }

    void ApplyLeverRotation()
    {
        Camera activeCam = UFOCameraController.Instance != null ? UFOCameraController.Instance.GetActiveCamera() : Camera.main;
        if (activeCam == null) return;
        var cam = activeCam.transform;

        // 【修正ポイント1】カメラの描画空間ではなく、フィールド上の「右」と「奥」を取得（アーム移動と同じ基準）
        Vector3 camXZRight   = Vector3.ProjectOnPlane(cam.right,   Vector3.up).normalized;
        Vector3 camXZForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;

        float normH = _angleH / leverMaxAngle;
        float normV = _angleV / leverMaxAngle;

        // レバーを倒したいワールド空間上の向き（XZ平面上のベクトル）
        Vector3 moveDir = camXZRight * normH + camXZForward * normV;
        
        float tiltMagnitude = new Vector2(_angleH, _angleV).magnitude;
        
        Quaternion targetWorld;
        Quaternion additionalRot = Quaternion.identity;
        float tiltAngle = Mathf.Min(tiltMagnitude, leverMaxAngle);

        if (tiltMagnitude < 0.001f || moveDir.sqrMagnitude < 0.001f)
        {
            // 完全に中立: 初期回転に戻す
            targetWorld = _initialWorldRot;
        }
        else
        {
            // 【修正ポイント2】FBXのローカル軸（_leverInitialUp）がズレているとコマ回転してしまうため、
            // どんなFBXモデルでも常に純粋なワールドの真上(Vector3.up)を直交計算の基準にして、倒れる回転軸を作ります
            Vector3 rotAxis = Vector3.Cross(Vector3.up, moveDir.normalized).normalized;
            additionalRot = Quaternion.AngleAxis(tiltAngle, rotAxis);
            targetWorld = additionalRot * _initialWorldRot;
        }

        if (pivotOverride != null)
        {
            // ピボット基準点の位置から傾きを適用した位置を算出
            Vector3 neutralMeshPos = pivotOverride.TransformPoint(_localOffsetFromPivot);
            Vector3 tiltedOffset = additionalRot * (neutralMeshPos - pivotOverride.position);
            
            transform.position = pivotOverride.position + tiltedOffset;
            transform.rotation = targetWorld;
        }
        else
        {
            ApplyWorldRotation(targetWorld);
        }
    }

    void ApplyWorldRotation(Quaternion worldRot)
    {
        if (transform.parent != null)
            transform.localRotation = Quaternion.Inverse(transform.parent.rotation) * worldRot;
        else
            transform.rotation = worldRot;
    }

    void UpdateArmInput()
    {
        Camera activeCam = UFOCameraController.Instance != null ? UFOCameraController.Instance.GetActiveCamera() : Camera.main;
        if (activeCam == null || armController == null) return;
        var cam = activeCam.transform;

        // カメラの右/前方向を XZ 平面（地面）に投影 → アームの移動方向
        Vector3 camXZRight   = Vector3.ProjectOnPlane(cam.right,   Vector3.up).normalized;
        Vector3 camXZForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;

        float normH = _angleH / leverMaxAngle;
        float normV = _angleV / leverMaxAngle;

        // 【斜め入力のブレ防止（スナップ）】
        // アームに送る値だけに適用し、レバーの見た目は360度自由に傾くようにする
        if (axisSnapAssist > 0f)
        {
            float absH = Mathf.Abs(normH);
            float absV = Mathf.Abs(normV);

            if (absH > absV)
            {
                if (absV < absH * axisSnapAssist) normV = 0f; // 完全に横移動
            }
            else if (absV > absH)
            {
                if (absH < absV * axisSnapAssist) normH = 0f; // 完全に縦移動
            }
        }

        Vector3 moveDir = camXZRight * normH + camXZForward * normV;
        armController.SetLeverInput(moveDir.x, moveDir.z);
    }

    bool IsMouseOverThis(Vector2 screenPos)
    {
        Camera activeCam = UFOCameraController.Instance != null ? UFOCameraController.Instance.GetActiveCamera() : Camera.main;
        if (_collider == null || activeCam == null) return false;
        Ray ray = activeCam.ScreenPointToRay(screenPos);
        return _collider.Raycast(ray, out _, 1000f);
    }
}
