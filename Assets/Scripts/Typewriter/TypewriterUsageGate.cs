using System;
using App.ATM;
using UnityEngine;
using UnityEngine.Events;

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

    private Action _pendingAllow;

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

        bool isFirstTurn = MoneyManager.Instance == null || MoneyManager.Instance.CurrentTurnCount <= 1;

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

    /// <summary>表示中のパネルの「はい」ボタンから呼ぶ。タイプライターの使用を許可する。</summary>
    public void OnYes()
    {
        var allow = _pendingAllow;
        _pendingAllow = null;
        allow?.Invoke();
    }

    /// <summary>③④⑤パネルの「いいえ」ボタンから呼ぶ。タイプライターは使わせない。</summary>
    public void OnNo()
    {
        _pendingAllow = null;
    }
}
