using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// アーム（爪）の中に掴み込まれたコインや景品を、アームの移動に合わせて一緒に運ぶためのスクリプト。
/// アタッチされた BoxCollider の形状に完全に一致する空間を FixedUpdate 内でスキャン（Physics.OverlapBox）し、
/// アームの物理的な移動差分（deltaPosition）に「追従率（滑り）」を掛け合わせて運ぶことで、
/// リアルな「時々滑り落ちる」挙動を再現します。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class UFOClawCarrier : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("感知するレイヤー（無指定の場合はすべて検知）")]
    public LayerMask carryLayerMask = ~0;

    [Header("掴み調整（落ちやすさ）")]
    [Range(0f, 1f)]
    [Tooltip("アームの移動をどれだけコインに追従させるか（1 = 完全に固定されて落ちない, 0.98 = わずかに滑りラグが生じて少しずつ落ちる）")]
    public float carryEfficiency = 0.98f;

    [Tooltip("アーム移動時の速度に応じて滑りやすさを増すか（急な移動や揺れでさらに落ちやすくなる）")]
    public bool speedBasedSlippage = true;

    [Tooltip("スリップが最大になる基準速度")]
    public float maxSpeedForSlippage = 3.0f;

    [Range(0f, 1f)]
    [Tooltip("最高速度移動時・激しい揺れ時の最小追従率（これ以下になるとズリズリ滑り落ちます）")]
    public float minCarryEfficiency = 0.85f;

    [Header("デバッグ表示")]
    [Tooltip("コンソールへのデバッグログ出力を有効にするか")]
    public bool showDebugLogs = false;

    private Vector3 _lastPosition;
    private UFOArmController _armController;
    private BoxCollider _boxCollider;
    private int _logThrottleCounter = 0;

    private void Start()
    {
        _boxCollider = GetComponent<BoxCollider>();
        Debug.Log($"[UFOClawCarrier] Start: {gameObject.name} initialized. BoxCollider Size: {_boxCollider.size}, Parent: {transform.parent?.name}");
        
        _lastPosition = transform.position;
        _armController = FindAnyObjectByType<UFOArmController>();

        // インスペクターの設定に関わらず、物理衝突を防ぐため Trigger は強制オンにする
        if (!_boxCollider.isTrigger)
        {
            _boxCollider.isTrigger = true;
        }
    }

    private void FixedUpdate()
    {
        if (_boxCollider == null) return;

        // 爪が開いている状態（WantFingerOpen = true）の時はコライダーを無効化し、閉じている時は有効化する
        if (_armController != null)
        {
            _boxCollider.enabled = !_armController.WantFingerOpen;
        }

        // コライダーが無効化されている（爪が開いている）場合は、運搬処理をすべてスキップ
        if (!_boxCollider.enabled)
        {
            _lastPosition = transform.position; // 基準点のみ更新して終了
            return;
        }

        // 1. 今フレームの移動差分（deltaPosition）を計算
        Vector3 currentPosition = transform.position;
        Vector3 delta = currentPosition - _lastPosition;
        _lastPosition = currentPosition;

        // BoxCollider のワールド座標における中心点と、スケールを反映した半分のサイズ（Half Extents）を計算
        Vector3 worldCenter = transform.TransformPoint(_boxCollider.center);
        Vector3 lossyScale = transform.lossyScale;
        Vector3 halfExtents = new Vector3(
            Mathf.Abs(_boxCollider.size.x * 0.5f * lossyScale.x),
            Mathf.Abs(_boxCollider.size.y * 0.5f * lossyScale.y),
            Mathf.Abs(_boxCollider.size.z * 0.5f * lossyScale.z)
        );
        Quaternion worldRotation = transform.rotation;

        // 2. OverlapBox による範囲内のコライダーの強制検出（アタッチされたBoxColliderと完全に同じ範囲）
        Collider[] hits = Physics.OverlapBox(worldCenter, halfExtents, worldRotation, carryLayerMask);

        // ログの間引き（30フレームに1回アームの移動状況を出力）
        _logThrottleCounter++;
        bool shouldLogThisFrame = showDebugLogs && (_logThrottleCounter % 30 == 0);

        if (shouldLogThisFrame)
        {
            Debug.Log($"[UFOClawCarrier] FixedUpdate Tick. Position: {currentPosition.ToString("F3")}, Delta: {delta.ToString("F4")}, RawHitsCount: {hits.Length}, BoxSize: {_boxCollider.size}");
            for (int i = 0; i < hits.Length; i++)
            {
                bool isChild = hits[i].transform.IsChildOf(transform.parent);
                Rigidbody rb = hits[i].attachedRigidbody;
                Debug.Log($"[UFOClawCarrier] Hit[{i}]: {hits[i].name}, Layer: {LayerMask.LayerToName(hits[i].gameObject.layer)}, IsChildOfClawParent: {isChild}, HasRigidbody: {rb != null}");
            }
        }

        // 移動していない場合は何もしない
        if (delta.sqrMagnitude < 0.000001f) return;

        // 3. 追従効率（滑りやすさ）の算出
        float effectiveEfficiency = carryEfficiency;
        if (speedBasedSlippage)
        {
            float dt = Time.fixedDeltaTime;
            float currentSpeed = (dt > 0) ? (delta.magnitude / dt) : 0f;
            float speedRatio = Mathf.Clamp01(currentSpeed / maxSpeedForSlippage);
            effectiveEfficiency = Mathf.Lerp(carryEfficiency, minCarryEfficiency, speedRatio);
        }

        // 実際の移動量に追従率（滑り）を乗算
        Vector3 appliedDelta = delta * effectiveEfficiency;

        int carriedCount = 0;
        foreach (Collider hit in hits)
        {
            // 爪自体のパーツやアームの構成オブジェクト（親やアームコントローラーの管轄下）は運ばない
            if (hit.transform.IsChildOf(transform.parent)) continue;
            if (hit.GetComponentInParent<UFOArmController>() != null) continue;
            if (hit.GetComponentInParent<StretchRope>() != null) continue;

            Rigidbody rb = hit.attachedRigidbody;
            if (rb != null)
            {
                // アームのルートRigidbody（armRoot）は運ばない
                if (_armController != null && rb == _armController.armRoot?.GetComponent<Rigidbody>()) continue;

                // コイン最適化（スリープ）を解除して物理演算を確実に有効にする
                CoinOptimizer coin = rb.GetComponent<CoinOptimizer>();
                if (coin != null)
                {
                    coin.WakeUp();
                }

                // 追従率を加味した差分を座標移動させる
                Vector3 newPos = rb.position + appliedDelta;
                rb.MovePosition(newPos);
                
                // 見た目の同期 (★物理的な瞬間ワープによる処理スパイクを防ぐため、MovePositionのみにします)
                // rb.transform.position = newPos;

                carriedCount++;
                if (showDebugLogs)
                {
                    Debug.Log($"[UFOClawCarrier] Carried object: {rb.name} by {appliedDelta.ToString("F4")} (Efficiency: {effectiveEfficiency:F2}) to {newPos.ToString("F3")}");
                }
            }
        }

        if (carriedCount > 0 && showDebugLogs)
        {
            Debug.Log($"[UFOClawCarrier] Total carried this frame: {carriedCount} objects. (Applied Delta: {appliedDelta.ToString("F4")})");
        }
    }
}
