using System.Collections;
using System.Collections.Generic;
using App.Player;
using MiniGames.Transitions;
using Unity.VisualScripting;
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

    // 物理キーボードでの打鍵は一旦無効化中。
    // シーンに保存済みの linkPhysicalKeyboard が true でも、ここが false なら購読しない。
    // 機能を戻すときは true にするだけでよい（const にすると到達不能コード警告が出るため static readonly）
    private static readonly bool EnablePhysicalKeyboardTyping = false;

    [Header("ターン遷移")]
    [Tooltip("スキル取得後のローディング画面最低表示時間（秒）")]
    [SerializeField] private float _turnTransitionDuration = 2f;

    [Header("ブラックダイヤ強制売却")]
    [Tooltip("磨き段階(0〜3)ごとの現所持金への増減率。rate>0で増加、rate<0で減少（例: -0.15 = 15%減）")]
    [SerializeField] private DiamondSellRate[] _diamondSellRates = new DiamondSellRate[]
    {
        new DiamondSellRate { label = "呪われたダイヤモンド",      rate = -0.15f },
        new DiamondSellRate { label = "封印されしダイヤモンド",     rate = -0.10f },
        new DiamondSellRate { label = "解放されそうなダイヤモンド", rate = -0.05f },
        new DiamondSellRate { label = "ゴッドダイヤモンド",         rate =  0.10f },
    };

    [Header("デバッグ")]
    [Tooltip("ONにするとInspectorにローグライクスキルのオンオフパネルが表示される（Playモードのみ有効）")]
    [SerializeField] private bool _debugMode = false;

    private bool _busy;
    private bool _lookedAt;
    private bool _keyboardSubscribed;

    private DebtCollectionManager _debtCollectionManager;
    private FirstPersonController _fpsController;
    protected override void Awake()
    {
        base.Awake();
        if (controller == null) controller = GetComponentInChildren<TypewriterController>();
        EnsureSelectionUI();
        WarnIfNoColliders();
        RewardSelectionUI.OnTypewriterUICancelled += OnSelectionCancelled;
    }

    private void OnDestroy()
    {
        RewardSelectionUI.OnTypewriterUICancelled -= OnSelectionCancelled;
    }

    private void OnSelectionCancelled()
    {
        if (!_busy) return;
        _busy = false;
        ApplyHighlight(true);
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
        if (!EnablePhysicalKeyboardTyping) return;   // 一旦無効化中
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
        ApplyHighlight(false);
        try
        {
            selectionUI.Show(picks, OnRewardSelected);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TypewriterInteractable] RewardSelectionUI.Show() で例外が発生しました: {e}", this);
            _busy = false;
            ApplyHighlight(true);
            return;
        }

        // Show() が UI を開けなかった場合（Prefab 未設定など）は即座に解放
        if (!selectionUI.IsActive)
        {
            _busy = false;
            ApplyHighlight(true);
            Debug.LogWarning("[TypewriterInteractable] RewardSelectionUI の表示に失敗しました。Inspector で _scrollContentPrefab または optionButtons を設定してください", this);
        }
    }

    private void OnRewardSelected(RoguelikeData chosen)
    {
        var mgr = FindFirstObjectByType<RoguelikeManager>();
        if (mgr != null)
            mgr.UnlockSkill(chosen);
        else
            Debug.LogWarning("[TypewriterInteractable] RoguelikeManager が見つかりません。スキルは反映されません", this);

        SellBlackDiamonds(mgr);

        if (controller == null)
        {
            Debug.LogWarning("[TypewriterInteractable] TypewriterController が未設定 - 打鍵をスキップ", this);
            _busy = false;
            return;
        }

        StartCoroutine(TypeAndUnblock(chosen.skillName));
    }

    private void SellBlackDiamonds(RoguelikeManager mgr)
    {
        var wallet = PlayerWallet.Local;
        if (wallet == null) return;

        int count = wallet.BlackDiamonds;
        if (count <= 0) return;

        int stage = mgr != null ? mgr.GetDiamondPolishStage() : 0;

        if (_diamondSellRates == null || stage >= _diamondSellRates.Length)
        {
            Debug.LogWarning($"[TypewriterInteractable] ダイヤ売却: stage={stage} に対応するレートが未設定です", this);
            return;
        }

        float rate = _diamondSellRates[stage].rate;
        float totalChange = wallet.WashedAmount * rate * count;

        if (totalChange >= 0f)
            wallet.AddWashed(totalChange);
        else
            wallet.ReduceWashed(-totalChange);

        wallet.BlackDiamonds -= count;

        var itemMgr = ItemPanelManager.Instance;
        if (itemMgr != null)
            itemMgr.RemoveItem(105, ItemType.CraneItem, count);
        else
            Debug.LogWarning("[TypewriterInteractable] ItemPanelManager が見つかりません", this);
    }

    private IEnumerator TypeAndUnblock(string text)
    {
        var c = controller.TypeText(text);
        if (c != null) yield return c;

        // 紙のローンチアニメーション（飛んでいく演出）が終わるまで待つ（最大10秒）
        var paper = controller.paperOutput;
        if (paper != null)
        {
            float launchWait = 0f;
            while (paper.IsLaunching && launchWait < 10f)
            {
                launchWait += Time.deltaTime;
                yield return null;
            }
            if (launchWait >= 10f)
                Debug.LogWarning("[TypewriterInteractable] IsLaunching タイムアウト: 強制続行します", this);
        }

        _busy = false;
        ApplyHighlight(true);

        if(MoneyManager.Instance.NextDebtCollectionTurnCount == 0)
        {
            yield return new WaitForSeconds(3.0f);
            if(_debtCollectionManager == null)
                _debtCollectionManager = FindFirstObjectByType<DebtCollectionManager>();

            if(_debtCollectionManager != null && MoneyManager.Instance.DebtClearTimes == 0)
                _debtCollectionManager.StartConversationCoroutine("Conversation_00");
            else if(_debtCollectionManager != null && MoneyManager.Instance.DebtClearTimes != 0)
                _debtCollectionManager.StartConversationCoroutine();
            else
                Debug.LogError("[TypewriterInteractable] DebtCollectionManager が見つかりません", this);
        }
        else
        {
            if(_fpsController == null)
                _fpsController = FindFirstObjectByType<FirstPersonController>();

            yield return new WaitForSeconds(3.0f);
            var stm = SceneTransitionManager.Instance;
            if (stm != null)
            {
                bool done = false;
                if (_fpsController != null) _fpsController.enabled = false;
                stm.ShowTurnTransition(
                    _turnTransitionDuration,
                    onDuringLoading: () => Debug.Log("Now Loading"),
                    onComplete:      () => { done = true; MoneyManager.Instance?.AdvanceTurn();}
                );
                // ShowTurnTransition が完了しない場合に備えてタイムアウト（最大60秒）
                float elapsed = 0f;
                while (!done && elapsed < 60f)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                if (!done)
                    Debug.LogWarning("[TypewriterInteractable] ShowTurnTransition タイムアウト: 強制続行します", this);
                if (_fpsController != null) _fpsController.enabled = true;
            }
        }
    }
}

/// <summary>ブラックダイヤ磨き段階ごとの強制売却レート設定</summary>
[System.Serializable]
public class DiamondSellRate
{
    [Tooltip("Inspector 上の見出し（動作には影響しません）")]
    public string label;

    [Tooltip("現所持金に対する増減率。正で増加・負で減少（例: -0.15 = 15%減、0.10 = 10%増）")]
    public float rate;
}
