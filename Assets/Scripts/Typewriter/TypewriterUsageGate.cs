using System;
using App.ATM;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// タイプライター使用前に「DevilCatcherをプレイしたか」「ATMを使用したか」を判定し、
/// 未使用の項目に応じた確認パネル(①〜⑤)の表示をUnityEvent経由で要求するゲート。
/// パネルの実体（UI・ボタン）は持たない。実際のパネルからは OnYes / OnNo を呼び出してもらう。
///
/// ①DevilCatcherをプレイしてください（はいのみ）
/// ②ATMを使用してください（はいのみ）
/// ③DevilCatcherとATMを両方使用していませんが良いですか？（はい/いいえ）
/// ④DevilCatcherを使用していませんが良いですか？（はい/いいえ）
/// ⑤ATMを使用していませんが良いですか？（はい/いいえ）
///
/// 1ターン目は①②、2ターン目以降は③④⑤で場合分けする。
/// はいが押されるとタイプライターを使用可能にする。③④⑤でいいえが押された場合は使用させない。
/// </summary>
public class TypewriterUsageGate : MonoBehaviour
{
    /// <summary>確認パネル(①〜⑤)のいずれかを表示中か。MouseHoverOutline 等が他オブジェクトの
    /// ホバー/クリックを止めるために参照する。</summary>
    public static bool IsPanelShowing { get; private set; }

    [Header("1ターン目: 未使用の項目を案内する（はいのみ）")]
    [Tooltip("①DevilCatcherをプレイしてください")]
    public UnityEvent OnShowPanel1_PlayDevilCatcher;
    [Tooltip("②ATMを使用してください")]
    public UnityEvent OnShowPanel2_UseATM;

    [Header("2ターン目以降: 未使用のまま進んでよいか確認する（はい/いいえ）")]
    [Tooltip("③DevilCatcherとATMを使用していませんが良いですか？")]
    public UnityEvent OnShowPanel3_BothUnused;
    [Tooltip("④DevilCatcherを使用していませんが良いですか？")]
    public UnityEvent OnShowPanel4_DevilCatcherUnused;
    [Tooltip("⑤ATMを使用していませんが良いですか？")]
    public UnityEvent OnShowPanel5_ATMUnused;

    [Header("1ターン目: ATMを先に触った場合の案内（はいのみ、使用許可はしない）")]
    [Tooltip("先にDevilCatcherを使用してください")]
    public UnityEvent OnShowATMPanel_PlayDevilCatcherFirst;

    [Header("パネル本体 (自動で閉じるための参照)")]
    [Tooltip("①〜⑤およびATM用パネルまで、全てのパネルGameObjectをここにドラッグしてください。" +
             "はい/いいえが押された時、ボタン側の配線に関わらずここに入っている全パネルを自動でSetActive(false)します")]
    [SerializeField] private GameObject[] _panels;

    private Action _pendingAllow;
    private CursorLockMode _prevCursorLockState;
    private bool _prevCursorVisible;

    /// <summary>タイプライターを使おうとした時に呼ぶ。案内が不要ならその場で onAllowed を実行し、
    /// 必要ならパネル表示イベントを発火して「はい」が押されるまで待つ。</summary>
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
        BeginShowPanel();

        bool isFirstTurn = IsFirstTurn;

        if (isFirstTurn)
        {
            if (!devilPlayed) OnShowPanel1_PlayDevilCatcher?.Invoke();
            else OnShowPanel2_UseATM?.Invoke();
        }
        else
        {
            if (!devilPlayed && !atmUsed) OnShowPanel3_BothUnused?.Invoke();
            else if (!devilPlayed) OnShowPanel4_DevilCatcherUnused?.Invoke();
            else OnShowPanel5_ATMUnused?.Invoke();
        }
    }

    /// <summary>ATMを使おうとした時に呼ぶ。1ターン目にDevilCatcherが未プレイなら案内パネルを出して
    /// trueを返す(ATM側はこの戻り値を見て起動を中止すること)。それ以外はfalseを返し、そのまま開いてよい。
    /// はいを押しても使用は許可されない(＝①②と同じ、案内のみ)。</summary>
    public bool TryBlockATM()
    {
        if (!IsFirstTurn || UFOCameraController.HasPlayedThisRound) return false;

        _pendingAllow = null;
        BeginShowPanel();
        OnShowATMPanel_PlayDevilCatcherFirst?.Invoke();
        return true;
    }

    private static bool IsFirstTurn =>
        MoneyManager.Instance == null || MoneyManager.Instance.CurrentTurnCount <= 1;

    private void BeginShowPanel()
    {
        IsPanelShowing = true;
        _prevCursorLockState = Cursor.lockState;
        _prevCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        App.Input.GameInputGate.CaptureEscape(this);
    }

    /// <summary>③④⑤パネルの「はい(使わずに進む)」ボタンから呼ぶ。タイプライターの使用を許可する。</summary>
    public void OnYes()
    {
        ClosePanel();
        var allow = _pendingAllow;
        _pendingAllow = null;
        allow?.Invoke();
    }

    /// <summary>③④⑤パネルの「いいえ」ボタンから呼ぶ。タイプライターは使わせない。</summary>
    public void OnNo()
    {
        ClosePanel();
        _pendingAllow = null;
    }

    /// <summary>①②パネル(1ターン目の案内のみ、いいえが無い)の「はい」ボタンから呼ぶ。
    /// 使用は許可せず、案内を確認したものとしてパネルを閉じるだけ（挙動はOnNoと同じ）。</summary>
    public void OnAcknowledge()
    {
        ClosePanel();
        _pendingAllow = null;
    }

    private void ClosePanel()
    {
        IsPanelShowing = false;
        if (_panels != null)
        {
            foreach (var panel in _panels)
            {
                if (panel != null) panel.SetActive(false);
            }
        }
        Cursor.lockState = _prevCursorLockState;
        Cursor.visible = _prevCursorVisible;
        App.Input.GameInputGate.ReleaseEscape(this);
    }

    private void Update()
    {
        if (!IsPanelShowing) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // ESCで一旦パネルを閉じるだけ（設定は開かせない）。もう一度ESCを押した時に開けるようにする
            OnNo();
        }
    }

    private void OnDisable()
    {
        // 表示中に破棄/無効化された場合、Escapeの専有が残り続けないようにする
        if (IsPanelShowing) ClosePanel();
    }
}
