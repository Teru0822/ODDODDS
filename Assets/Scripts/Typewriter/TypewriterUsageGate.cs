using System;
using App.ATM;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// タイプライター使用前に「DevilCatcherをプレイしたか」「ATMを使用したか」を判定し、
/// 未使用の項目に応じた確認パネル(①〜⑤)を表示するゲート。
///
/// ①DevilCatcherをプレイしてください（はいのみ）
/// ②ATMを使用してください（はいのみ）
/// ③DevilCatcherとATMを両方使用していませんが良いですか？（はい/いいえ）
/// ④DevilCatcherを使用していませんが良いですか？（はい/いいえ）
/// ⑤ATMを使用していませんが良いですか？（はい/いいえ）
///
/// 1ターン目は①②、2ターン目以降は③④⑤で場合分けする。
/// はいが押されるとタイプライターを使用可能にする。③④⑤でいいえが押された場合は使用させない。
///
/// パネルの参照はこのコンポーネントが直接持ち、表示/非表示と はい/いいえ の購読まで行う。
/// UnityEvent(OnShowPanelN) は追加演出用のフックとして残してあり、パネル参照と併用できる。
/// </summary>
public class TypewriterUsageGate : MonoBehaviour
{
    /// <summary>確認パネル 1 枚分。いいえが無いパネル（①②）は noButton を空にしておく。</summary>
    [Serializable]
    public class WarningPanel
    {
        [Tooltip("表示/非表示を切り替えるパネル本体")]
        public GameObject root;
        [Tooltip("「はい」ボタン。押すと使用を許可する")]
        public Button yesButton;
        [Tooltip("「いいえ」ボタン。①②のように いいえ が無いパネルでは空でよい")]
        public Button noButton;

        public void SetVisible(bool visible)
        {
            if (root != null) root.SetActive(visible);
        }
    }

    [Header("1ターン目: 未使用の項目を案内する（はいのみ）")]
    [Tooltip("①DevilCatcherをプレイしてください")]
    public WarningPanel Panel1_PlayDevilCatcher;
    [Tooltip("②ATMを使用してください")]
    public WarningPanel Panel2_UseATM;

    [Header("2ターン目以降: 未使用のまま進んでよいか確認する（はい/いいえ）")]
    [Tooltip("③DevilCatcherとATMを使用していませんが良いですか？")]
    public WarningPanel Panel3_BothUnused;
    [Tooltip("④DevilCatcherを使用していませんが良いですか？")]
    public WarningPanel Panel4_DevilCatcherUnused;
    [Tooltip("⑤ATMを使用していませんが良いですか？")]
    public WarningPanel Panel5_ATMUnused;

    [Header("追加演出フック（任意）")]
    [Tooltip("①表示時に追加で行いたい処理があれば繋ぐ")]
    public UnityEvent OnShowPanel1_PlayDevilCatcher;
    [Tooltip("②表示時に追加で行いたい処理があれば繋ぐ")]
    public UnityEvent OnShowPanel2_UseATM;
    [Tooltip("③表示時に追加で行いたい処理があれば繋ぐ")]
    public UnityEvent OnShowPanel3_BothUnused;
    [Tooltip("④表示時に追加で行いたい処理があれば繋ぐ")]
    public UnityEvent OnShowPanel4_DevilCatcherUnused;
    [Tooltip("⑤表示時に追加で行いたい処理があれば繋ぐ")]
    public UnityEvent OnShowPanel5_ATMUnused;

    private Action _pendingAllow;

    private WarningPanel[] AllPanels => new[]
    {
        Panel1_PlayDevilCatcher, Panel2_UseATM,
        Panel3_BothUnused, Panel4_DevilCatcherUnused, Panel5_ATMUnused,
    };

    private void Awake()
    {
        // シーンにパネルを表示状態のまま保存してしまっても、起動時に必ず閉じる。
        // （確認が要求されていないのにパネルが出っぱなしになる事故を防ぐ）
        HideAll();

        foreach (var panel in AllPanels)
        {
            if (panel == null) continue;
            if (panel.yesButton != null) panel.yesButton.onClick.AddListener(OnYes);
            if (panel.noButton != null) panel.noButton.onClick.AddListener(OnNo);
        }
    }

    private void OnDestroy()
    {
        foreach (var panel in AllPanels)
        {
            if (panel == null) continue;
            if (panel.yesButton != null) panel.yesButton.onClick.RemoveListener(OnYes);
            if (panel.noButton != null) panel.noButton.onClick.RemoveListener(OnNo);
        }
    }

    /// <summary>タイプライターを使おうとした時に呼ぶ。案内が不要ならその場で onAllowed を実行し、
    /// 必要なら確認パネルを表示して「はい」が押されるまで待つ。</summary>
    public void RequestUse(Action onAllowed)
    {
        bool devilPlayed = UFOCameraController.HasPlayedThisRound;
        bool atmUsed = ATMController.HasUsedATMThisRound;

        if (devilPlayed && atmUsed)
        {
            onAllowed?.Invoke();
            return;
        }

        _pendingAllow = onAllowed;

        bool isFirstTurn = MoneyManager.Instance == null || MoneyManager.Instance.CurrentTurnCount <= 1;

        if (isFirstTurn)
        {
            if (!devilPlayed) Show(Panel1_PlayDevilCatcher, OnShowPanel1_PlayDevilCatcher);
            else Show(Panel2_UseATM, OnShowPanel2_UseATM);
        }
        else
        {
            if (!devilPlayed && !atmUsed) Show(Panel3_BothUnused, OnShowPanel3_BothUnused);
            else if (!devilPlayed) Show(Panel4_DevilCatcherUnused, OnShowPanel4_DevilCatcherUnused);
            else Show(Panel5_ATMUnused, OnShowPanel5_ATMUnused);
        }
    }

    /// <summary>表示中のパネルの「はい」ボタンから呼ぶ。タイプライターの使用を許可する。</summary>
    public void OnYes()
    {
        HideAll();
        var allow = _pendingAllow;
        _pendingAllow = null;
        allow?.Invoke();
    }

    /// <summary>③④⑤パネルの「いいえ」ボタンから呼ぶ。タイプライターは使わせない。</summary>
    public void OnNo()
    {
        HideAll();
        _pendingAllow = null;
    }

    private void Show(WarningPanel panel, UnityEvent extra)
    {
        HideAll();

        if (panel != null && panel.root != null)
        {
            panel.SetVisible(true);
        }
        else
        {
            // パネル未設定のまま待ち続けるとタイプライターが永久に使えなくなるため、
            // 案内を出せない場合は素通りさせる
            Debug.LogWarning("[TypewriterUsageGate] 表示するパネルが未設定のため、確認を省略して使用を許可します。", this);
            var allow = _pendingAllow;
            _pendingAllow = null;
            allow?.Invoke();
            return;
        }

        extra?.Invoke();
    }

    private void HideAll()
    {
        foreach (var panel in AllPanels)
            panel?.SetVisible(false);
    }
}
