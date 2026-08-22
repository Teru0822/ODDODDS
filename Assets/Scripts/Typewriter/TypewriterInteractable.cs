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

    [Tooltip("DevilCatcher/ATM未使用時の確認パネルを出すゲート。null なら確認なしで即使用可能")]
    public TypewriterUsageGate usageGate;

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

    [Tooltip("ダイヤ売却時に金額の変化を見せる演出UI。null なら演出なしで即座に売却する")]
    [SerializeField] private BlackDiamondSellDisplay _diamondSellDisplay;

    [Header("デバッグ")]
    [Tooltip("ONにするとInspectorにローグライクスキルのオンオフパネルが表示される（Playモードのみ有効）")]
    [SerializeField] private bool _debugMode = false;

    private bool _busy;
    private bool _lookedAt;
    private bool _keyboardSubscribed;

    /// <summary>いずれかのタイプライターが占有中か（UI 表示・アニメーション含む）。MouseHoverOutline のブロック判定に使用。</summary>
    private static int _globalBusyCount = 0;
    public static bool IsAnyBusy => _globalBusyCount > 0;

    private void SetBusy(bool value)
    {
        if (_busy == value) return;
        _busy = value;
        _globalBusyCount = Mathf.Max(0, _globalBusyCount + (value ? 1 : -1));
    }

    private DebtCollectionManager _debtCollectionManager;
    private FirstPersonController _fpsController;

    [Header("カメラ固定")]
    [Tooltip("タイプライター操作中のカメラ固定先。シーンに空 GameObject を置いてアサイン")]
    [SerializeField] private Transform _cameraTargetTransform;
    [Tooltip("カメラの移動時間（秒）")]
    [SerializeField] private float _cameraTransitionDuration = 1.0f;
    [SerializeField] private AnimationCurve _cameraTransitionEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Camera _playerCamera;
    private Vector3 _originalCamPos;
    private Quaternion _originalCamRot;
    private CupPickupController _pickupController;
    private bool _cameraLocked;

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
        if (_busy) SetBusy(false);
    }

    private void OnSelectionCancelled()
    {
        if (!_busy) return;
        SetBusy(false);
        ApplyHighlight(false);
        StartCoroutine(RestoreCameraCoroutine());
    }

    private IEnumerator TransitionToTypewriter()
    {
        if (_playerCamera == null) _playerCamera = Camera.main;
        if (_pickupController == null) _pickupController = FindAnyObjectByType<CupPickupController>();
        if (_fpsController == null) _fpsController = FindFirstObjectByType<FirstPersonController>();

        if (_fpsController != null) _fpsController.enabled = false;
        if (_pickupController != null) _pickupController.enabled = false;

        if (_playerCamera != null)
        {
            _originalCamPos = _playerCamera.transform.position;
            _originalCamRot = _playerCamera.transform.rotation;
        }
        _cameraLocked = true;

        if (_playerCamera != null && _cameraTargetTransform != null)
        {
            Vector3 startPos = _originalCamPos;
            Quaternion startRot = _originalCamRot;
            Vector3 targetPos = _cameraTargetTransform.position;
            Quaternion targetRot = _cameraTargetTransform.rotation;

            float elapsed = 0f;
            while (elapsed < _cameraTransitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _cameraTransitionDuration);
                _playerCamera.transform.position = Vector3.Lerp(startPos, targetPos, _cameraTransitionEase.Evaluate(t));
                _playerCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, _cameraTransitionEase.Evaluate(t));
                yield return null;
            }
            _playerCamera.transform.position = targetPos;
            _playerCamera.transform.rotation = targetRot;
        }
    }

    private IEnumerator RestoreCameraCoroutine()
    {
        if (!_cameraLocked) yield break;
        _cameraLocked = false;

        if (_playerCamera != null && _cameraTargetTransform != null)
        {
            Vector3 startPos = _playerCamera.transform.position;
            Quaternion startRot = _playerCamera.transform.rotation;

            float elapsed = 0f;
            while (elapsed < _cameraTransitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _cameraTransitionDuration);
                _playerCamera.transform.position = Vector3.Lerp(startPos, _originalCamPos, _cameraTransitionEase.Evaluate(t));
                _playerCamera.transform.rotation = Quaternion.Slerp(startRot, _originalCamRot, _cameraTransitionEase.Evaluate(t));
                yield return null;
            }
            _playerCamera.transform.position = _originalCamPos;
            _playerCamera.transform.rotation = _originalCamRot;
        }

        if (_fpsController != null) _fpsController.enabled = true;
        if (_pickupController != null) _pickupController.enabled = true;
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
        if (DebtCollectionManager.IsCollecting) return false;
        if (BookOpenController.IsBookVisible) return false;
        if (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsTransitioning) return false;
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
        if (usageGate != null)
        {
            usageGate.RequestUse(OnUsageAllowed);
            return;
        }
        OnUsageAllowed();
    }

    /// <summary>usageGate の確認が済んだ（または不要だった）ときに実際の打鍵処理へ進む。</summary>
    private void OnUsageAllowed()
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

        SetBusy(true);
        ApplyHighlight(false);
        try
        {
            selectionUI.Show(picks, OnRewardSelected);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TypewriterInteractable] RewardSelectionUI.Show() で例外が発生しました: {e}", this);
            SetBusy(false);
            ApplyHighlight(true);
            return;
        }

        // Show() が UI を開けなかった場合（Prefab 未設定など）は即座に解放
        if (!selectionUI.IsActive)
        {
            SetBusy(false);
            ApplyHighlight(false);
            Debug.LogWarning("[TypewriterInteractable] RewardSelectionUI の表示に失敗しました。Inspector で _scrollContentPrefab または optionButtons を設定してください", this);
        }
        else
        {
            StartCoroutine(TransitionToTypewriter());
        }
    }

    private void OnRewardSelected(RoguelikeData chosen)
    {
        var mgr = FindFirstObjectByType<RoguelikeManager>();
        if (mgr != null)
            mgr.UnlockSkill(chosen);
        else
            Debug.LogWarning("[TypewriterInteractable] RoguelikeManager が見つかりません。スキルは反映されません", this);

        if (controller == null)
        {
            Debug.LogWarning("[TypewriterInteractable] TypewriterController が未設定 - 打鍵をスキップ", this);
            SetBusy(false);
            return;
        }

        // ブラックダイヤの売却はターンが実際に進んだ後に行うため、ここでは呼ばない（TypeAndUnblock 参照）
        StartCoroutine(TypeAndUnblock(chosen.skillName, mgr));
    }

    private IEnumerator SellBlackDiamonds(RoguelikeManager mgr)
    {
        var wallet = PlayerWallet.Local;
        if (wallet == null) yield break;

        int count = wallet.BlackDiamonds;
        if (count <= 0) yield break;

        int stage = mgr != null ? mgr.GetDiamondPolishStage() : 0;

        if (_diamondSellRates == null || stage >= _diamondSellRates.Length)
        {
            Debug.LogWarning($"[TypewriterInteractable] ダイヤ売却: stage={stage} に対応するレートが未設定です", this);
            yield break;
        }

        float rate = _diamondSellRates[stage].rate;
        float moneyBefore = wallet.WashedAmount;
        float totalChange = moneyBefore * rate * count;
        float moneyAfter = moneyBefore + totalChange;

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

        if (_diamondSellDisplay != null)
            yield return StartCoroutine(_diamondSellDisplay.PlaySellAnimation(count, stage, rate, moneyBefore, moneyAfter));
        else
            Debug.LogWarning("[TypewriterInteractable] _diamondSellDisplay が未アサインです。InspectorでBlackDiamondSellDisplayをアサインしてください", this);
    }

    private IEnumerator TypeAndUnblock(string text, RoguelikeManager mgr)
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

        yield return new WaitForSeconds(0.5f);

        var moneyMgr = MoneyManager.Instance;
        int turnBeforeAdvance = moneyMgr != null ? moneyMgr.CurrentTurnCount : -1;
        if (moneyMgr == null)
        {
            Debug.LogError("[TypewriterInteractable] MoneyManager が見つかりません。ターン処理をスキップします", this);
        }
        else if(moneyMgr.NextDebtCollectionTurnCount == 0)
        {
            yield return new WaitForSeconds(3.0f);
            if(_debtCollectionManager == null)
                _debtCollectionManager = FindFirstObjectByType<DebtCollectionManager>();

            // DebtCollectionManager がカメラ現在位置を _originPosition として保存する前に戻しておく
            yield return StartCoroutine(RestoreCameraCoroutine());

            if(_debtCollectionManager != null && moneyMgr.DebtClearTimes == 0)
                _debtCollectionManager.StartConversationCoroutine("Conversation_00");
            else if(_debtCollectionManager != null && moneyMgr.DebtClearTimes != 0)
                _debtCollectionManager.StartConversationCoroutine();
            else
                Debug.LogError("[TypewriterInteractable] DebtCollectionManager が見つかりません", this);
        }
        else
        {
            if(_fpsController == null)
                _fpsController = FindFirstObjectByType<FirstPersonController>();

            yield return new WaitForSeconds(3.0f);
            yield return StartCoroutine(RestoreCameraCoroutine());
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

        // ブラックダイヤの売却は、取り立て・通常どちらの経路でも MoneyManager.AdvanceTurn() で
        // ターンが実際に進んだ後に行う（取り立てが発生した場合は取り立て後、その後のローディングが
        // 終わってターンが進んでから売却する）
        if (moneyMgr != null)
        {
            float turnWaitElapsed = 0f;
            while (moneyMgr.CurrentTurnCount <= turnBeforeAdvance && turnWaitElapsed < 60f)
            {
                turnWaitElapsed += Time.deltaTime;
                yield return null;
            }
            if (turnWaitElapsed >= 60f)
                Debug.LogWarning("[TypewriterInteractable] ターン進行待ちがタイムアウトしました。ブラックダイヤ売却を強制実行します", this);
        }
        yield return StartCoroutine(SellBlackDiamonds(mgr));

        // ターン遷移か会話が完全に終わってから占有を解放する。
        // シーンリロードで先に OnDestroy が呼ばれた場合は OnDestroy 側でクリアされる。
        // 取り立て・ターン遷移ブランチではここより前で RestoreCameraCoroutine を呼んでいるため
        // _cameraLocked フラグにより重複実行は自動でスキップされる。
        yield return StartCoroutine(RestoreCameraCoroutine());
        SetBusy(false);
        ApplyHighlight(false);
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
