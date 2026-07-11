using UnityEngine;

/// <summary>
/// Triggerコライダーに侵入した特定のタグを持つオブジェクトを破棄するスクリプト。
/// </summary>
[RequireComponent(typeof(Collider))]
public class CoinDestroyerZone : MonoBehaviour
{
    [Header("破棄設定")]
    [Tooltip("破棄対象とするオブジェクトのタグ名")]
    [SerializeField] private string targetTag = "Coin";

    [Header("デバッグ設定")]
    [Tooltip("オブジェクトを破棄した際にコンソールにログを出力するかどうか")]
    [SerializeField] private bool showDebugLog = true;

    private void Start()
    {
        // アタッチされたコライダーの IsTrigger が有効になっているかチェックし、警告を出す
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[{gameObject.name}] コライダーの 'Is Trigger' が無効になっています。Triggerイベントを発生させるために有効にしてください。");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other != null && other.CompareTag(targetTag))
        {
            if (showDebugLog)
            {
                Debug.Log($"[{gameObject.name}] タグ '{targetTag}' のオブジェクト '{other.gameObject.name}' を破棄しました。");
            }
            
            // UFOCameraController への通知を追加
            if (UFOCameraController.Instance != null)
            {
                UFOCameraController.Instance.NotifyCoinDestroyed();
            }
            
            Destroy(other.gameObject);
        }
    }
}
