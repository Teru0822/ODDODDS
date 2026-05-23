using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーの手に持たれている Bin。
/// 元の cup から受け取ったボール参照 (SetActive(false) 状態) を保持する。
/// </summary>
[DisallowMultipleComponent]
public class CupBin : MonoBehaviour
{
    /// <summary>元の cup から受け継いだボールたち (非アクティブ状態で保持される)</summary>
    [System.NonSerialized] public List<GameObject> heldBalls = new List<GameObject>();

    /// <summary>由来の cup プレハブ (Exchange 側が参照しない場合のフォールバック)</summary>
    [System.NonSerialized] public GameObject sourceCupPrefab;

    public int BallCount
    {
        get
        {
            if (heldBalls == null) return 0;
            heldBalls.RemoveAll(b => b == null);
            return heldBalls.Count;
        }
    }

    private void OnDestroy()
    {
        // 持ったまま破棄された場合、内部ボールも破棄して残骸を防ぐ
        if (heldBalls == null) return;
        foreach (var b in heldBalls)
        {
            if (b != null) Destroy(b);
        }
        heldBalls.Clear();
    }
}
