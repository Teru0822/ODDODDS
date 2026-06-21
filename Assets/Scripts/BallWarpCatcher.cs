using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ピンボール下部の吸い込み口（BallWarpPoint1）に置く、見えないトリガー。
/// トリガーに入った PinballBallController ボールを「消す」が、破棄ではなく SetActive(false) で
/// 非アクティブ化して内部リストに保持する（currentValue 等の状態を保ったまま後で再出現させるため）。
///
/// PinballExchangeSequence がこの Caught リストを使って BallWarpPoint2 から 1 個ずつ再放出する。
///
/// 設置方法:
///   1. 空の GameObject を作り、BoxCollider を付けて「Is Trigger = ON」にする
///   2. MeshRenderer を付けない（＝ゲーム内では見えない）。位置・サイズを吸い込み口に合わせる
///   3. 本コンポーネントをアタッチ
/// </summary>
[RequireComponent(typeof(Collider))]
public class BallWarpCatcher : MonoBehaviour
{
    [Tooltip("ボール判定タグ。空なら PinballBallController コンポーネントで判定する")]
    public string ballTag = "";

    [Tooltip("捕獲を有効にするか。終了シーケンス中は PinballExchangeSequence が false にして二重捕獲を防ぐ")]
    public bool catching = true;

    [Tooltip("捕獲イベントを Console に出力")]
    public bool logEvents = false;

    private readonly List<GameObject> _caught = new List<GameObject>();

    /// <summary>これまでに捕獲（非アクティブ化）したボール一覧。</summary>
    public IReadOnlyList<GameObject> Caught => _caught;

    public int CaughtCount
    {
        get { _caught.RemoveAll(b => b == null); return _caught.Count; }
    }

    /// <summary>ボールを 1 個捕獲した瞬間に発火（引数 = 捕獲したボール GameObject）。</summary>
    public System.Action<GameObject> OnCaught;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"[BallWarpCatcher] '{name}' の Collider は Is Trigger=OFF です。ON にしてください。", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!catching) return;
        if (!IsBall(other)) return;

        var go = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
        if (go == null || !go.activeSelf) return;

        var rb = go.GetComponent<Rigidbody>();
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }

        go.SetActive(false); // 「消える」= 非アクティブ化（状態は保持して後で再出現）

        if (!_caught.Contains(go)) _caught.Add(go);
        if (logEvents) Debug.Log($"[BallWarpCatcher] '{name}' 捕獲: {go.name} (count={_caught.Count})", this);
        OnCaught?.Invoke(go);
    }

    private bool IsBall(Collider other)
    {
        if (!string.IsNullOrEmpty(ballTag)) return other.CompareTag(ballTag);
        return other.GetComponentInParent<PinballBallController>() != null;
    }

    /// <summary>捕獲リストを空にする（次ラウンド開始時など）。</summary>
    public void ClearCaught() => _caught.Clear();
}
