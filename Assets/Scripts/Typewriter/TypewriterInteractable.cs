using System.Collections;
using UnityEngine;

/// <summary>
/// タイプライターのルートにアタッチして「クリック → 報酬選択 UI → 選択肢のテキストを打鍵」を駆動する。
/// </summary>
[DisallowMultipleComponent]
public class TypewriterInteractable : InteractableHighlight
{
    [Header("接続")]
    [Tooltip("打鍵対象のコントローラ。null なら自身/子から自動検索")]
    public TypewriterController controller;

    [Tooltip("選択 UI。null ならシーン内検索 → 無ければ自動生成")]
    public RewardSelectionUI selectionUI;

    [Header("自動生成 (selectionUI が null の場合)")]
    [Tooltip("自動生成する RewardSelectionUI を DontDestroyOnLoad に乗せる")]
    public bool persistAutoCreatedUI = false;

    private bool _busy;

    protected override void Awake()
    {
        base.Awake();
        if (controller == null) controller = GetComponentInChildren<TypewriterController>();
        EnsureSelectionUI();
        WarnIfNoColliders();
    }

    private void EnsureSelectionUI()
    {
        if (selectionUI != null) return;
        selectionUI = FindAnyObjectByType<RewardSelectionUI>();
        if (selectionUI != null) return;
        var go = new GameObject("RewardSelectionUI");
        if (persistAutoCreatedUI) DontDestroyOnLoad(go);
        selectionUI = go.AddComponent<RewardSelectionUI>();
    }

    public override bool IsInteractable(CupPickupController pickup)
    {
        if (_busy) return false;
        if (controller != null && controller.IsTyping) return false;
        if (selectionUI != null && selectionUI.IsActive) return false;
        // Bin 保持中はインタラクト不可 (他の操作と競合させない)
        if (pickup != null && pickup.IsHoldingBin) return false;
        return true;
    }

    /// <summary>CupPickupController からクリック時に呼ばれる。</summary>
    public void OnPressed()
    {
        if (_busy) return;
        if (selectionUI == null)
        {
            Debug.LogWarning("[TypewriterInteractable] RewardSelectionUI が未設定", this);
            return;
        }
        var picks = RewardOptionsRepository.PickRandom(2);
        if (picks.Count < 2)
        {
            Debug.LogWarning($"[TypewriterInteractable] 未選択の報酬が 2 個未満 (残り {picks.Count})", this);
            return;
        }
        _busy = true;
        // 選択肢が出た時点でレティクル照準のハイライトは不要 (この後 IsInteractable=false になるので
        // CupPickupController 側で再ハイライトされることもない)
        ApplyHighlight(false);
        selectionUI.Show(picks.ToArray(), OnRewardSelected);
    }

    private void OnRewardSelected(string chosen)
    {
        Debug.Log($"[TypewriterInteractable] OnRewardSelected: \"{chosen}\"", this);
        RewardOptionsRepository.MarkSelected(chosen);
        if (controller == null)
        {
            Debug.LogWarning("[TypewriterInteractable] TypewriterController が未設定 - 打鍵をスキップ", this);
            _busy = false;
            return;
        }
        StartCoroutine(TypeAndUnblock(chosen));
    }

    private IEnumerator TypeAndUnblock(string text)
    {
        Debug.Log($"[TypewriterInteractable] TypeText 開始: \"{text}\"", this);
        var c = controller.TypeText(text);
        if (c != null) yield return c;
        Debug.Log("[TypewriterInteractable] TypeText 完了", this);
        _busy = false;
    }
}
