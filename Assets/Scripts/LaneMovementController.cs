using UnityEngine;

/// <summary>
/// 3枚の板（レール）の間を3Dオブジェクトのボタン入力によって移動し、ジャンプや自動前進を行い、
/// 1つのカメラがレーンごとの目標地点へスライド追従するキャラクター制御スクリプト
/// </summary>
public class LaneMovementController : MonoBehaviour
{
    [Header("レールの参照（左から順に3つアタッチしてください）")]
    [SerializeField] private Transform[] rails;

    [Header("3Dオブジェクトボタンの参照（アタッチすると端で無効化されます）")]
    [SerializeField] private RunningMachineButton leftButton;
    [SerializeField] private RunningMachineButton rightButton;

    [Header("追従カメラの設定")]
    [Tooltip("動かす単一のカメラのTransform")]
    [SerializeField] private Transform singleCamera;
    
    [Header("レーンごとのカメラ位置（要素0:左, 1:中, 2:右）")]
    [Tooltip("カメラが目指す空のオブジェクト(Transform)を3つアタッチしてください")]
    [SerializeField] private Transform[] cameraPoints;

    [Tooltip("カメラが目標座標へスライドするスピード")]
    [SerializeField] private float cameraLerpSpeed = 5f;

    [Header("左右移動速度")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("現在のレール初期位置（0が左、1が中央、2が右）")]
    [SerializeField] private int currentLaneIndex = 1;

    [Header("ジャンプ設定")]
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float gravity = -20f;

    [Header("自動前進（Z軸）設定")]
    [Tooltip("毎秒Z軸方向に進む速度")]
    [SerializeField] private float forwardSpeed = 1f;
    [Tooltip("前進できるZ座標の最大限界値")]
    [SerializeField] private float maxZLimit = 10f;

    private float yVelocity = 0f;
    private bool isGrounded = true;
    private float groundY;
    private bool isDead = false; // 死亡状態のフラグ

    void Start()
    {
        // 地面の高さを初期Y位置として記録
        groundY = transform.position.y;

        // レールのセットアップ確認
        if (rails == null || rails.Length == 0)
        {
            Debug.LogError("レール（Rails）がアタッチされていません。インスペクターで3つのレールをアタッチしてください。");
            return;
        }

        // 初期位置を現在のレーンにスナップ
        SnapToLane(currentLaneIndex);

        // ボタンの初期状態を更新
        UpdateButtonStates();

        // 開始時にカメラを現在のレーンのカメラポイントへ即座に配置
        SnapCameraToCurrentPoint();
    }

    void Update()
    {
        // 死亡している場合はすべての移動・更新処理を停止
        if (isDead) return;

        if (rails == null || rails.Length == 0) return;

        // --- 1. 自動前進（Z軸）の計算 ---
        float nextZ = transform.position.z + forwardSpeed * Time.deltaTime;
        nextZ = Mathf.Min(nextZ, maxZLimit);

        // --- 2. 左右のレーン移動（X軸）の計算 ---
        Transform targetRail = rails[Mathf.Clamp(currentLaneIndex, 0, rails.Length - 1)];
        float nextX = transform.position.x;
        if (targetRail != null)
        {
            nextX = Mathf.MoveTowards(transform.position.x, targetRail.position.x, moveSpeed * Time.deltaTime);
        }

        // 新しい位置を設定（Y座標はジャンプの物理処理が単独で制御するためここでは維持）
        Vector3 newPos = new Vector3(nextX, transform.position.y, nextZ);
        transform.position = newPos;

        // --- 3. 縦のジャンプ処理（簡易物理演算・Y軸） ---
        if (!isGrounded)
        {
            yVelocity += gravity * Time.deltaTime;
            
            Vector3 pos = transform.position;
            pos.y += yVelocity * Time.deltaTime;

            // 着地判定
            if (pos.y <= groundY)
            {
                pos.y = groundY;
                yVelocity = 0f;
                isGrounded = true;
                Debug.Log("[LaneMovementController] 着地しました。");
            }
            transform.position = pos;
        }
    }

    // カメラの追従は LateUpdate で行うことで、ガタつきをなくします
    void LateUpdate()
    {
        if (singleCamera == null || cameraPoints == null || cameraPoints.Length == 0) return;

        // 現在のレーンに応じたカメラポイントを取得
        int targetIndex = Mathf.Clamp(currentLaneIndex, 0, cameraPoints.Length - 1);
        Transform targetPoint = cameraPoints[targetIndex];

        if (targetPoint != null)
        {
            singleCamera.position = Vector3.Lerp(singleCamera.position, targetPoint.position, cameraLerpSpeed * Time.deltaTime);
            singleCamera.rotation = Quaternion.Slerp(singleCamera.rotation, targetPoint.rotation, cameraLerpSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// 障害物に衝突した際に後ろに押し戻される処理（障害物スクリプトから呼ばれます）
    /// </summary>
    /// <param name="amount">後ろに戻る距離</param>
    public void PushBack(float amount)
    {
        if (isDead) return;

        // デッドゾーンに到達できるように、下限の制限（startZ）を取り除き、後ろに押し戻します
        float newZ = transform.position.z - amount;
        transform.position = new Vector3(transform.position.x, transform.position.y, newZ);
        
        Debug.Log($"[LaneMovementController] 障害物に衝突！後ろに押し戻されました。現在のZ: {transform.position.z}");
    }

    /// <summary>
    /// キャラクターを死亡状態にする（デッドゾーン衝突時に呼び出されます）
    /// </summary>
    public void Die()
    {
        if (isDead) return;
        
        isDead = true;
        Debug.Log("<color=red>[LaneMovementController] キャラクターが死亡しました（ゲームオーバー）。</color>");
        
        // ボタン入力をすべて無効化する
        if (leftButton != null) leftButton.SetActiveState(false);
        if (rightButton != null) rightButton.SetActiveState(false);
    }

    /// <summary>
    /// ジャンプを実行する（ジャンプボタンから呼ばれます）
    /// </summary>
    public void Jump()
    {
        if (isDead) return;

        if (isGrounded)
        {
            yVelocity = jumpForce;
            isGrounded = false;
            Debug.Log("[LaneMovementController] ジャンプしました！");
        }
    }

    /// <summary>
    /// 左のボタンを押したときの処理（左ボタンから呼ばれます）
    /// </summary>
    public void MoveLeft()
    {
        if (isDead) return;

        if (currentLaneIndex > 0)
        {
            currentLaneIndex--;
            UpdateButtonStates();
            Debug.Log($"左に移動しました。現在のレーン: {currentLaneIndex}");
        }
    }

    /// <summary>
    /// 右のボタンを押したときの処理（右ボタンから呼ばれます）
    /// </summary>
    public void MoveRight()
    {
        if (isDead) return;

        if (rails != null && currentLaneIndex < rails.Length - 1)
        {
            currentLaneIndex++;
            UpdateButtonStates();
            Debug.Log($"右に移動しました。現在のレーン: {currentLaneIndex}");
        }
    }

    /// <summary>
    /// ボタンの有効/無効状態を更新する
    /// </summary>
    private void UpdateButtonStates()
    {
        if (isDead) return;
        if (rails == null || rails.Length == 0) return;

        if (leftButton != null)
        {
            leftButton.SetActiveState(currentLaneIndex > 0);
        }

        if (rightButton != null)
        {
            rightButton.SetActiveState(currentLaneIndex < rails.Length - 1);
        }
    }

    /// <summary>
    /// カメラを現在のレーンの目標カメラポイントへ即座に配置する（瞬間移動）
    /// </summary>
    private void SnapCameraToCurrentPoint()
    {
        if (singleCamera == null || cameraPoints == null || cameraPoints.Length == 0) return;

        int targetIndex = Mathf.Clamp(currentLaneIndex, 0, cameraPoints.Length - 1);
        Transform targetPoint = cameraPoints[targetIndex];

        if (targetPoint != null)
        {
            singleCamera.position = targetPoint.position;
            singleCamera.rotation = targetPoint.rotation;
        }
    }

    /// <summary>
    /// 指定したレーンに瞬間移動する（スタート時などに使用）
    /// </summary>
    public void SnapToLane(int laneIndex)
    {
        if (rails == null || rails.Length == 0) return;
        
        currentLaneIndex = Mathf.Clamp(laneIndex, 0, rails.Length - 1);
        Transform targetRail = rails[currentLaneIndex];
        if (targetRail != null)
        {
            transform.position = new Vector3(targetRail.position.x, transform.position.y, transform.position.z);
        }

        SnapCameraToCurrentPoint();
    }
}
