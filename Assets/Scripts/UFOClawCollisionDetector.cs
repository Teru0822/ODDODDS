using UnityEngine;

/// <summary>
/// アーム（爪）が何かにぶつかったことを検知し、UFOArmControllerに「降下ストップ」を知らせるスクリプト
/// 判定を持たせたい爪先（001〜004）や爪の土台（finger）にアタッチして使います。
/// </summary>
public class UFOClawCollisionDetector : MonoBehaviour
{
    [Tooltip("司令塔であるUFOArmController (3番) をセットしてください")]
    public UFOArmController armController;

    private void Start()
    {
        // もしセットされていなければ、自動的に探す
        if (armController == null)
        {
            armController = FindAnyObjectByType<UFOArmController>();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (armController == null) return;

        // 爪同士やUFOキャッチャー本体との衝突は無視する（ただし、即時掴み判定エリアであれば無視しない）
        bool isImmediateArea = armController.immediateGrabArea != null && 
            (collision.collider == armController.immediateGrabArea || 
             collision.gameObject == armController.immediateGrabArea.gameObject || 
             collision.transform.IsChildOf(armController.immediateGrabArea.transform));

        if (!isImmediateArea && collision.transform.IsChildOf(armController.transform.root))
            return;

        Debug.Log($"[UFOClawCollisionDetector] Collided with: {collision.gameObject.name}");
        armController.OnClawCollided(collision.collider); 
    }

    // IsTrigger にチェックを入れている場合（すり抜けながら検知したい場合）はこちらが呼ばれる
    private void OnTriggerEnter(Collider other)
    {
        if (armController == null) return;

        // 爪同士やUFOキャッチャー本体との衝突は無視する（ただし、即時掴み判定エリアであれば無視しない）
        bool isImmediateArea = armController.immediateGrabArea != null && 
            (other == armController.immediateGrabArea || 
             other.gameObject == armController.immediateGrabArea.gameObject || 
             other.transform.IsChildOf(armController.immediateGrabArea.transform));

        if (!isImmediateArea && other.transform.IsChildOf(armController.transform.root))
            return;

        Debug.Log($"[UFOClawCollisionDetector] Triggered with: {other.gameObject.name}");
        armController.OnClawCollided(other); 
    }
}
