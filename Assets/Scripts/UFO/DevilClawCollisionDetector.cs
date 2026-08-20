using UnityEngine;

/// <summary>
/// アーム（爪）が何かにぶつかったことを検知し、UFOArmControllerに「降下ストップ」を知らせるスクリプト
/// 判定を持たせたい爪先（001〜004）や爪の土台（finger）にアタッチして使います。
/// </summary>
public class DevilClawCollisionDetector : MonoBehaviour
{
    [Tooltip("司令塔であるUFOArmController (3番) をセットしてください")]
    public UFOArmController armController;

    [Tooltip("衝突判定のログを出力する。毎フレーム大量に呼ばれるため、既定はオフ。調査時だけ有効にしてください")]
    public bool showDebugLogs = false;

    private void Start()
    {
        if (armController == null)
        {
            armController = FindAnyObjectByType<UFOArmController>();
        }

        // 子オブジェクトのすべてのコライダーに対して、自動的に検知スクリプトをアタッチする
        // これにより、物理イベントが子オブジェクト（Rigidbody持ち）に直接送られた場合でも自動で中継されます
        Collider[] childColliders = GetComponentsInChildren<Collider>(true);
        int autoAttachedCount = 0;
        foreach (var col in childColliders)
        {
            if (col.gameObject == gameObject) continue;

            DevilClawCollisionDetector detector = col.gameObject.GetComponent<DevilClawCollisionDetector>();
            if (detector == null)
            {
                detector = col.gameObject.AddComponent<DevilClawCollisionDetector>();
                autoAttachedCount++;
            }
            detector.armController = this.armController;
        }

        if (showDebugLogs) Debug.Log($"[DevilClawCollisionDetector] Initialized on {gameObject.name}. Auto-attached to {autoAttachedCount} child colliders. armController references: {(armController != null ? armController.name : "NULL")}");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (armController == null) return;

        // 【最優先デバッグログ】接触した瞬間に必ず出力
        if (showDebugLogs) Debug.Log($"[DevilClawCollisionDetector] OnCollisionEnter called on {gameObject.name}: other={collision.gameObject.name} (Parent: {collision.transform.parent?.name})");

        // 爪同士やDevilキャッチャー本体との衝突は無視する
        if (collision.transform.IsChildOf(armController.transform.root))
        {
            // ただし、もし相手が clawRiseCollider（ステージ側）または clawCarrieZone（アーム側）である場合は無視しない
            bool isRiseOrZone = (armController.clawRiseCollider != null && (collision.collider == armController.clawRiseCollider || collision.gameObject == armController.clawRiseCollider.gameObject || collision.transform.IsChildOf(armController.clawRiseCollider.transform))) ||
                                (armController.clawCarrieZone != null && (collision.collider == armController.clawCarrieZone || collision.gameObject == armController.clawCarrieZone.gameObject || collision.transform.IsChildOf(armController.clawCarrieZone.transform)));

            if (!isRiseOrZone)
            {
                if (showDebugLogs) Debug.Log($"[DevilClawCollisionDetector] Ignored OnCollisionEnter with {collision.gameObject.name} because it is part of the arm structure.");
                return;
            }
        }

        // 1. 従来の immediateGrabArea の判定
        bool isImmediateArea = false;
        if (armController.immediateGrabArea != null)
        {
            // コインや景品アイテムの場合は、即時掴みエリアとしての接触判定から除外する
            bool isCoinOrItem = collision.collider.GetComponent<UFOItem>() != null || 
                               collision.collider.GetComponentInParent<UFOItem>() != null ||
                               collision.collider.GetComponent<CoinOptimizer>() != null ||
                               collision.collider.GetComponentInParent<CoinOptimizer>() != null;

            if (showDebugLogs) Debug.Log($"[DevilClawCollisionDetector] OnCollisionEnter with {collision.gameObject.name} -> isCoinOrItem: {isCoinOrItem}, immediateGrabArea is {armController.immediateGrabArea.name}");

            if (!isCoinOrItem)
            {
                bool matchesCollider = collision.collider == armController.immediateGrabArea;
                bool matchesGO = collision.gameObject == armController.immediateGrabArea.gameObject;
                bool isChild = collision.transform.IsChildOf(armController.immediateGrabArea.transform);

                if (showDebugLogs) Debug.Log($"[DevilClawCollisionDetector] Matches: matchesCollider={matchesCollider}, matchesGO={matchesGO}, isChild={isChild}");

                if (matchesCollider || matchesGO || isChild)
                {
                    isImmediateArea = true;
                }
            }
        }

        // 2. ClawCarrieZone と Claw_RiseCollider の接触判定
        bool isClawRiseContact = false;
        if (armController.clawCarrieZone != null && armController.clawRiseCollider != null)
        {
            // 自分が clawCarrieZone で相手が clawRiseCollider
            if (gameObject == armController.clawCarrieZone.gameObject && 
                (collision.collider == armController.clawRiseCollider || 
                 collision.gameObject == armController.clawRiseCollider.gameObject || 
                 collision.transform.IsChildOf(armController.clawRiseCollider.transform)))
            {
                isClawRiseContact = true;
            }
            // または、自分が clawRiseCollider で相手が clawCarrieZone
            else if (gameObject == armController.clawRiseCollider.gameObject && 
                     (collision.collider == armController.clawCarrieZone || 
                      collision.gameObject == armController.clawCarrieZone.gameObject || 
                      collision.transform.IsChildOf(armController.clawCarrieZone.transform)))
            {
                isClawRiseContact = true;
            }
            // または、自分（子コライダー含む）が collision の相手と直接マッチする場合
            else
            {
                bool isMeZone = (collision.collider == armController.clawCarrieZone || 
                                 collision.gameObject == armController.clawCarrieZone.gameObject || 
                                 collision.transform.IsChildOf(armController.clawCarrieZone.transform));
                bool isOtherRise = (collision.collider == armController.clawRiseCollider || 
                                    collision.gameObject == armController.clawRiseCollider.gameObject || 
                                    collision.transform.IsChildOf(armController.clawRiseCollider.transform));

                // 自身にアタッチされたコライダーそのものが clawCarrieZone または clawRiseCollider である場合
                Collider myCollider = GetComponent<Collider>();
                if (myCollider != null)
                {
                    bool myIsZone = (myCollider == armController.clawCarrieZone || myCollider.gameObject == armController.clawCarrieZone.gameObject);
                    bool myIsRise = (myCollider == armController.clawRiseCollider || myCollider.gameObject == armController.clawRiseCollider.gameObject);

                    if ((myIsZone && isOtherRise) || (myIsRise && isMeZone))
                    {
                        isClawRiseContact = true;
                    }
                }
            }
        }

        if (isImmediateArea || isClawRiseContact)
        {
            if (showDebugLogs) Debug.Log($"[DevilClawCollisionDetector] Collided with grab trigger (immediateArea={isImmediateArea}, clawRiseContact={isClawRiseContact}): {collision.gameObject.name}");
            armController.OnClawCollided(collision.collider); 
        }
    }

    // IsTrigger にチェックを入れている場合（すり抜けながら検知したい場合）はこちらが呼ばれる
    private void OnTriggerEnter(Collider other)
    {
        if (armController == null) return;

        // 【最優先デバッグログ】接触した瞬間に必ず出力
        if (showDebugLogs) Debug.Log($"[DevilClawCollisionDetector] OnTriggerEnter called on {gameObject.name}: other={other.gameObject.name} (Parent: {other.transform.parent?.name})");

        // 爪同士やDevilキャッチャー本体との衝突は無視する
        if (other.transform.IsChildOf(armController.transform.root))
        {
            // ただし、もし相手が clawRiseCollider（ステージ側）または clawCarrieZone（アーム側）である場合は無視しない
            bool isRiseOrZone = (armController.clawRiseCollider != null && (other == armController.clawRiseCollider || other.gameObject == armController.clawRiseCollider.gameObject || other.transform.IsChildOf(armController.clawRiseCollider.transform))) ||
                                (armController.clawCarrieZone != null && (other == armController.clawCarrieZone || other.gameObject == armController.clawCarrieZone.gameObject || other.transform.IsChildOf(armController.clawCarrieZone.transform)));

            if (!isRiseOrZone)
            {
                if (showDebugLogs) Debug.Log($"[DevilClawCollisionDetector] Ignored OnTriggerEnter with {other.gameObject.name} because it is part of the arm structure.");
                return;
            }
        }

        // 1. 従来の immediateGrabArea の判定
        bool isImmediateArea = false;
        if (armController.immediateGrabArea != null)
        {
            // コインや景品アイテムの場合は、即時掴みエリアとしての接触判定から除外する
            bool isCoinOrItem = other.GetComponent<UFOItem>() != null || 
                               other.GetComponentInParent<UFOItem>() != null ||
                               other.GetComponent<CoinOptimizer>() != null ||
                               other.GetComponentInParent<CoinOptimizer>() != null;

            if (showDebugLogs) Debug.Log($"[DevilClawCollisionDetector] OnTriggerEnter with {other.gameObject.name} -> isCoinOrItem: {isCoinOrItem}, immediateGrabArea is {armController.immediateGrabArea.name}");

            if (!isCoinOrItem)
            {
                bool matchesCollider = other == armController.immediateGrabArea;
                bool matchesGO = other.gameObject == armController.immediateGrabArea.gameObject;
                bool isChild = other.transform.IsChildOf(armController.immediateGrabArea.transform);

                if (showDebugLogs) Debug.Log($"[DevilClawCollisionDetector] Matches: matchesCollider={matchesCollider}, matchesGO={matchesGO}, isChild={isChild}");

                if (matchesCollider || matchesGO || isChild)
                {
                    isImmediateArea = true;
                }
            }
        }

        // 2. ClawCarrieZone と Claw_RiseCollider の接触判定
        bool isClawRiseContact = false;
        if (armController.clawCarrieZone != null && armController.clawRiseCollider != null)
        {
            // 自分が clawCarrieZone で相手が clawRiseCollider
            if (gameObject == armController.clawCarrieZone.gameObject && 
                (other == armController.clawRiseCollider || 
                 other.gameObject == armController.clawRiseCollider.gameObject || 
                 other.transform.IsChildOf(armController.clawRiseCollider.transform)))
            {
                isClawRiseContact = true;
            }
            // または、自分が clawRiseCollider で相手が clawCarrieZone
            else if (gameObject == armController.clawRiseCollider.gameObject && 
                     (other == armController.clawCarrieZone || 
                      other.gameObject == armController.clawCarrieZone.gameObject || 
                      other.transform.IsChildOf(armController.clawCarrieZone.transform)))
            {
                isClawRiseContact = true;
            }
            // または、自分（子コライダー含む）が collision の相手と直接マッチする場合
            else
            {
                bool isMeZone = (other == armController.clawCarrieZone || 
                                 other.gameObject == armController.clawCarrieZone.gameObject || 
                                 other.transform.IsChildOf(armController.clawCarrieZone.transform));
                bool isOtherRise = (other == armController.clawRiseCollider || 
                                    other.gameObject == armController.clawRiseCollider.gameObject || 
                                    other.transform.IsChildOf(armController.clawRiseCollider.transform));

                // 自身にアタッチされたコライダーそのものが clawCarrieZone または clawRiseCollider である場合
                Collider myCollider = GetComponent<Collider>();
                if (myCollider != null)
                {
                    bool myIsZone = (myCollider == armController.clawCarrieZone || myCollider.gameObject == armController.clawCarrieZone.gameObject);
                    bool myIsRise = (myCollider == armController.clawRiseCollider || myCollider.gameObject == armController.clawRiseCollider.gameObject);

                    if ((myIsZone && isOtherRise) || (myIsRise && isMeZone))
                    {
                        isClawRiseContact = true;
                    }
                }
            }
        }

        if (isImmediateArea || isClawRiseContact)
        {
            if (showDebugLogs) Debug.Log($"[DevilClawCollisionDetector] Triggered with grab trigger (immediateArea={isImmediateArea}, clawRiseContact={isClawRiseContact}): {other.gameObject.name}");
            armController.OnClawCollided(other); 
        }
    }
}
