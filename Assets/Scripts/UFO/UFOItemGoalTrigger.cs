using UnityEngine;

/// <summary>
/// 各穴（小・中・大）の中に入っている「透明な判定用Cube」に付けるスクリプト。
/// アイテムが入ったことを、親オブジェクトにいる本物のUFOItemGoalに伝えるだけの役割です。
/// </summary>
public class UFOItemGoalTrigger : MonoBehaviour
{
    [Tooltip("一番親玉（空オブジェクト）についている UFOItemGoal を登録してください")]
    public UFOItemGoal mainGoalManager;

    private void OnTriggerEnter(Collider other)
    {
        // もし本物のマネージャーが登録されていれば、そっちに処理を任せる
        if (mainGoalManager != null)
        {
            mainGoalManager.HandleItemDrop(other);
        }
    }
}
