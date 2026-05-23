using System.Collections;
using UnityEngine;

/// <summary>
/// exchange の子オブジェクト ex_button にアタッチする「金額払い出し」ボタン。
/// レティクル照準で水色ハイライト (InteractableHighlight 経由)。
/// クリックで owner.DispenseMoney() を呼び、累計値の桁数に応じて money_big/middle/small が落ちてくる。
/// 累計値が 0 / プレイヤーが Bin 保持中 の時は非インタラクティブ。
/// </summary>
[DisallowMultipleComponent]
public class ExchangeButton : InteractableHighlight
{
    [Header("接続")]
    [Tooltip("コマンド送信先 ExchangeStation。null なら親階層から自動取得")]
    public ExchangeStation owner;

    [Header("ボタン押し込み演出 (任意)")]
    [Tooltip("押し込みのローカル Y オフセット (0 で演出なし)")]
    public float pressDepth = 0.01f;

    [Tooltip("押し込み演出の長さ (秒)")]
    public float pressDuration = 0.15f;

    private Vector3 _originalLocalPos;
    private bool _animating;

    protected override void Awake()
    {
        base.Awake();
        if (owner == null) owner = GetComponentInParent<ExchangeStation>();
        if (owner == null)
        {
            Debug.LogWarning($"[ExchangeButton] '{name}': owner となる ExchangeStation が親階層に見つかりません。Inspector で指定してください。", this);
        }
        _originalLocalPos = transform.localPosition;
    }

    public override bool IsInteractable(CupPickupController pickup)
    {
        if (pickup == null) return false;
        if (pickup.IsHoldingBin) return false;        // Bin 保持中は不可
        if (owner == null) return false;
        if (RoundManager.Instance != null && !RoundManager.Instance.CanBuyBalls()) return false;
        return owner.CurrentTotalValue > 0f;          // 累計値が 0 なら不可
    }

    /// <summary>クリック時に CupPickupController から呼ばれる</summary>
    public void OnPressed()
    {
        if (owner == null) return;
        owner.DispenseMoney();
        if (pressDepth > 0f && !_animating) StartCoroutine(PressAnimation());
    }

    private IEnumerator PressAnimation()
    {
        _animating = true;
        Vector3 down = _originalLocalPos + Vector3.down * pressDepth;
        float halfDur = Mathf.Max(0.001f, pressDuration * 0.5f);

        float t = 0f;
        while (t < halfDur)
        {
            t += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(_originalLocalPos, down, Mathf.Clamp01(t / halfDur));
            yield return null;
        }
        t = 0f;
        while (t < halfDur)
        {
            t += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(down, _originalLocalPos, Mathf.Clamp01(t / halfDur));
            yield return null;
        }
        transform.localPosition = _originalLocalPos;
        _animating = false;
    }
}
