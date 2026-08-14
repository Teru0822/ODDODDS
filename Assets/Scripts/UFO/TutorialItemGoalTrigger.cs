using UnityEngine;

/// <summary>
/// 練習UFOキャッチャー側の各穴（小・中・大）の中に入っている「透明な判定用Cube」に付けるスクリプト。
/// アイテムが入ったことを、親オブジェクトにいる本物のTutorialItemGoalに伝えるだけの役割です。
/// 実機の UFOItemGoalTrigger と同じ構造で、参照先だけチュートリアル用に分けている。
/// </summary>
public class TutorialItemGoalTrigger : MonoBehaviour
{
    [Tooltip("一番親玉（空オブジェクト）についている TutorialItemGoal を登録してください")]
    public TutorialItemGoal mainGoalManager;

    private void OnTriggerEnter(Collider other)
    {
        if (mainGoalManager != null)
        {
            mainGoalManager.HandleItemDrop(other);
        }
    }
}
