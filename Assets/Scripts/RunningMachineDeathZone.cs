using UnityEngine;

/// <summary>
/// プレイヤーの後方に置かれた「死亡エリア」オブジェクト用スクリプト。
/// プレイヤーがこれに触れるとゲームオーバーとなり、ゲームを一時停止します。
/// </summary>
public class RunningMachineDeathZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 衝突した相手がプレイヤーか判定
        LaneMovementController player = other.GetComponent<LaneMovementController>();
        if (player == null)
        {
            player = other.GetComponentInParent<LaneMovementController>();
        }

        // プレイヤーに衝突した場合
        if (player != null)
        {
            // プレイヤーの死亡メソッドを呼び出す
            player.Die();

            // ゲームオーバー演出（一時停止）
            TriggerGameOver();
        }
    }

    private void TriggerGameOver()
    {
        Debug.Log("<color=red>【GAME OVER】 プレイヤーがデッドゾーンに到達しました！ゲーム終了。</color>");
        
        // ゲームの時間を一時停止（ゲーム内のすべての物理やUpdateの一部が止まります）
        Time.timeScale = 0f;
    }
}
