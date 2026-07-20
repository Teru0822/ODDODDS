using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("物理キーボード連動")]
    [Tooltip("このタイプライターに照準している間、物理キーボード入力で対応キーを打鍵させる")]
    public bool linkPhysicalKeyboard = true;

    [Header("デバッグ")]
    [Tooltip("ONにするとInspectorにローグライクスキルのオンオフパネルが表示される（Playモードのみ有効）")]
    [SerializeField] private bool _debugMode = false;

    private bool _busy;
    private bool _lookedAt;
    private bool _keyboardSubscribed;

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

    public override void OnLookEnter()
    {
        _lookedAt = true;
        SubscribeKeyboard();
    }

    public override void OnLookExit()
    {
        _lookedAt = false;
        UnsubscribeKeyboard();
    }

    private void OnDisable()
    {
        _lookedAt = false;
        UnsubscribeKeyboard();
    }

    private void SubscribeKeyboard()
    {
        if (!linkPhysicalKeyboard || _keyboardSubscribed) return;
        if (Keyboard.current == null) return;
        Keyboard.current.onTextInput += OnPhysicalTextInput;
        _keyboardSubscribed = true;
    }

    private void UnsubscribeKeyboard()
    {
        if (!_keyboardSubscribed) return;
        if (Keyboard.current != null) Keyboard.current.onTextInput -= OnPhysicalTextInput;
        _keyboardSubscribed = false;
    }

    private void OnPhysicalTextInput(char c)
    {
        // 照準中 + UI非表示 + 自動打鍵中でない時だけ手動打鍵
        if (!_lookedAt || _busy) return;
        if (controller == null || controller.IsTyping) return;
        controller.StrikeKey(c);
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

        var mgr = FindFirstObjectByType<RoguelikeManager>();
        List<RoguelikeData> picks;
        if (_debugMode)
        {
            picks = mgr != null ? mgr.GetAllSkills() : null;
            if (picks == null || picks.Count == 0)
            {
                Debug.LogWarning("[TypewriterInteractable] デバッグ: スキルデータが取得できません", this);
                return;
            }
        }
        else
        {
            //TODO;将来的にはマルチプレイに対応する必要あり
            int choiceCount = 2;
            if (mgr != null)
            {
                var unlocked = mgr.GetUnlockSkillDictionary;
                if (unlocked.ContainsKey(SkillId.Typewriter_ExpandChoice2))      choiceCount = 4;
                else if (unlocked.ContainsKey(SkillId.Typewriter_ExpandChoice1)) choiceCount = 3;
            }

            picks = mgr?.GetLockSkills(choiceCount);
            if (picks == null || picks.Count < 1)
            {
                Debug.LogWarning($"[TypewriterInteractable] 未選択の報酬が残っていません", this);
                return;
            }
        }

        _busy = true;
        // 選択肢が出た時点でレティクル照準のハイライトは不要 (この後 IsInteractable=false になるので
        // CupPickupController 側で再ハイライトされることもない)
        ApplyHighlight(false);
        selectionUI.Show(picks, OnRewardSelected);
    }

    private void OnRewardSelected(RoguelikeData chosen)
    {
        Debug.Log($"[TypewriterInteractable] OnRewardSelected: \"{chosen}\"", this);
        FindFirstObjectByType<RoguelikeManager>().UnlockSkill(chosen);
        //RewardOptionsRepository.MarkSelected(chosen);
        //RewardEffects.Apply(chosen); // 選択した報酬のゲームプレイ効果を反映 (分裂数強化など)
        if (controller == null)
        {
            Debug.LogWarning("[TypewriterInteractable] TypewriterController が未設定 - 打鍵をスキップ", this);
            _busy = false;
            return;
        }

        StartCoroutine(TypeAndUnblock(chosen.skillName));
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
