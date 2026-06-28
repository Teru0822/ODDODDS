using UnityEngine;

/// <summary>
/// ボックスコライダー（Trigger）の領域に接しているオブジェクトの isKinematic を制御するクラス。
/// インスペクターで指定した特定のオブジェクト（コインやジャックポットアイテムなど）が領域に入った場合のみ、
/// `isKinematic = false`（物理有効）に切り替えます。
/// </summary>
[RequireComponent(typeof(Collider))]
public class KinematicTriggerZone : MonoBehaviour
{
    [Header("対象設定")]
    [Tooltip("この領域に触れた際に isKinematic を false（物理有効）にするオブジェクトまたはプレハブのリスト")]
    [SerializeField] private GameObject[] targetList;

    private void Start()
    {
        // アタッチされたコライダーが Trigger に設定されていることを保証する
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
            Debug.Log($"[KinematicTriggerZone] {gameObject.name} の Collider を Trigger に自動設定しました。");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryActivatePhysics(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryActivatePhysics(other);
    }

    /// <summary>
    /// 触れたオブジェクトが対象リストに含まれている場合、物理挙動を有効にします。
    /// </summary>
    private void TryActivatePhysics(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;

        // すでに isKinematic が false なら処理不要
        if (!rb.isKinematic) return;

        // targetList に含まれているかどうかをチェック
        if (IsInTargetList(other.gameObject))
        {
            rb.isKinematic = false; // 物理演算を有効化（kinematicを解除）
        }
    }

    /// <summary>
    /// 指定されたオブジェクトが対象リストに含まれているかを判定します。
    /// </summary>
    private bool IsInTargetList(GameObject go)
    {
        if (targetList == null || targetList.Length == 0)
            return false;

        foreach (var target in targetList)
        {
            if (target == null) continue;

            // 1. 直接のゲームオブジェクト一致
            if (go == target)
            {
                return true;
            }

            // 2. プレハブ名（Clone含む）の一致チェック
            if (go.name.StartsWith(target.name))
            {
                return true;
            }
        }
        return false;
    }
}
