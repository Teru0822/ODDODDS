using UnityEngine;

/// <summary>
/// ローグライク報酬で得たゲームプレイ強化を保持するグローバル状態。
/// セッション中(シーンをまたいで)保持され、RewardEffects.Apply で更新される。
/// </summary>
public static class RoguelikeUpgrades
{
    /// <summary>分裂ピンに当たった際にボールが分裂する数の既定値。</summary>
    public const int DefaultSplitPinBallCount = 2;

    /// <summary>分裂ピンに当たった際にボールが分裂する数。報酬で 3 等に増える。</summary>
    public static int SplitPinBallCount { get; set; } = DefaultSplitPinBallCount;

    /// <summary>全強化を初期値に戻す (ニューゲーム時などに呼ぶ)。</summary>
    public static void ResetAll()
    {
        SplitPinBallCount = DefaultSplitPinBallCount;
    }
}

/// <summary>
/// 報酬テキスト → ゲームプレイ効果のマッピング。
/// TypewriterInteractable が報酬選択時に Apply を呼ぶ。
/// 文字列定数は Resources/RewardOptions.txt の該当行と完全一致させること。
/// </summary>
public static class RewardEffects
{
    /// <summary>分裂ピンの分裂数を 2→3 に増やす報酬テキスト (RewardOptions.txt と一致させる)。</summary>
    public const string SplitPinTripleText = "When a ball hits a split pin, it now splits into 3 instead of 2.";

    /// <summary>選択された報酬テキストに対応する効果を適用する。</summary>
    public static void Apply(string rewardText)
    {
        if (string.IsNullOrEmpty(rewardText)) return;
        string t = rewardText.Trim();

        if (t == SplitPinTripleText)
        {
            RoguelikeUpgrades.SplitPinBallCount = 3;
            Debug.Log("[RewardEffects] 分裂ピンの分裂数を 3 に強化しました");
            return;
        }

        // 未対応 (テキストのみの報酬) は何もしない
        Debug.Log($"[RewardEffects] 効果未定義の報酬を選択: \"{t}\" (テキストのみ)");
    }
}
