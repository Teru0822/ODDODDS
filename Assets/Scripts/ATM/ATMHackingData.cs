using System.Collections.Generic;
using UnityEngine;

namespace App.ATM
{
    /// <summary>ハッキングモードを実行できる条件。</summary>
    public enum HackUnlockMode
    {
        /// <summary>いつでも実行できる。デバッグ用。</summary>
        Debug_Always = 0,

        /// <summary>取り立てが今ターン終了時に来て、かつコインを全部売っても届かない時だけ実行できる。</summary>
        Release_WhenDebtUnpayable = 1
    }

    /// <summary>ハッキングの難易度。送金額の大小に対応する。</summary>
    public enum HackDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    /// <summary>
    /// 傍受する送金 1 件分。銀行名は実在しない架空のもの。
    /// Inspector から名前・金額・難易度を自由に変更できる。
    /// </summary>
    [System.Serializable]
    public class HackTransferJob
    {
        [Tooltip("送金元の架空銀行名")]
        public string fromBank = "MAMMON MUTUAL BANK";

        [Tooltip("送金先の架空銀行名")]
        public string toBank = "STYX FEDERAL SAVINGS";

        [Tooltip("横取りできる送金額")]
        public float amount = 80000f;

        [Tooltip("この送金に設定された難易度")]
        public HackDifficulty difficulty = HackDifficulty.Easy;

        /// <summary>表示用の口座番号。実在の書式に見えないよう毎回ランダムに作る。</summary>
        [System.NonSerialized] public string accountNumber;

        /// <summary>送金中プログレスバーの進捗(0-1)。一覧画面で流れ続ける演出用。</summary>
        [System.NonSerialized] public float progress;

        /// <summary>プログレスバーの進む速さ。行ごとにばらけさせて同期して見えないようにする。</summary>
        [System.NonSerialized] public float progressSpeed = 0.2f;
    }

    /// <summary>
    /// 難易度ごとのミニゲーム設定。階層数は layerLabels の要素数で決まる。
    /// </summary>
    [System.Serializable]
    public class HackDifficultySettings
    {
        [Tooltip("対応する難易度")]
        public HackDifficulty difficulty = HackDifficulty.Easy;

        [Tooltip("一覧画面に出す難易度表記")]
        public string label = "EASY";

        [Tooltip("階層名。ここに並べた数だけ階層が続く")]
        public string[] layerLabels = { "FIREWALL Lv.1", "BANK ADMIN" };

        [Tooltip("最初の階層の安全地帯の半幅。バー全体の幅を 1 とした割合")]
        [Range(0.02f, 0.4f)]
        public float safeZoneHalfWidth = 0.13f;

        [Tooltip("最終階層で安全地帯をどこまで縮めるか。0.3 なら最初の 30% の幅まで細くなる")]
        [Range(0.1f, 1f)]
        public float finalSafeZoneScale = 0.4f;

        [Tooltip("最初の階層のカーソル速度。1.0 でバーを 1 秒に 1 往復ぶん進む")]
        [Range(0.2f, 3f)]
        public float cursorSpeed = 0.7f;

        [Tooltip("最終階層でのカーソル速度の倍率")]
        [Range(1f, 3f)]
        public float finalCursorSpeedScale = 1.25f;

        [Tooltip("終盤の画面揺れの最大量(画面座標)。0 で揺れなし")]
        [Range(0f, 40f)]
        public float maxShake = 8f;

        [Tooltip("最初のステージの制限時間(秒)。0以下にすると制限なし")]
        public float timeLimit = 9f;

        [Tooltip("最終ステージでの制限時間の倍率。0.6 なら最初の6割の時間しかない")]
        [Range(0.1f, 1f)]
        public float finalTimeLimitScale = 0.6f;

        /// <summary>階層数。</summary>
        public int LayerCount => layerLabels != null ? layerLabels.Length : 0;

        /// <summary>
        /// 指定階層のパラメータを組み立てる。
        /// 後半ほど「安全地帯が狭い・カーソルが速い・フェイクが増える・画面が揺れる」ようにする。
        /// </summary>
        public HackLayer BuildLayer(int index)
        {
            int count = Mathf.Max(1, LayerCount);
            index = Mathf.Clamp(index, 0, count - 1);

            // 進行度。単階層なら 0 として最も易しい設定にする
            float progress = count > 1 ? index / (float)(count - 1) : 0f;

            return new HackLayer
            {
                label = layerLabels != null && index < layerLabels.Length ? layerLabels[index] : $"LAYER {index + 1}",
                safeHalfWidth = safeZoneHalfWidth * Mathf.Lerp(1f, finalSafeZoneScale, progress),
                cursorSpeed = cursorSpeed * Mathf.Lerp(1f, finalCursorSpeedScale, progress),
                fakeZoneCount = CountFakeZones(progress),
                shakeAmount = progress >= 0.5f ? maxShake * Mathf.InverseLerp(0.5f, 1f, progress) : 0f,
                timeLimit = timeLimit > 0f ? timeLimit * Mathf.Lerp(1f, finalTimeLimitScale, progress) : 0f
            };
        }

        /// <summary>フェイクの安全地帯の数。中盤から出はじめ、最終階層で最も多くなる。</summary>
        private int CountFakeZones(float progress)
        {
            if (progress >= 0.999f)
            {
                if (difficulty == HackDifficulty.Easy) return 1;
                return difficulty == HackDifficulty.Hard ? 3 : 2;
            }
            if (progress >= 0.6f) return 2;
            if (progress >= 0.3f) return 1;
            return 0;
        }
    }

    /// <summary>ミニゲーム 1 階層ぶんのパラメータ。</summary>
    public struct HackLayer
    {
        public string label;
        public float safeHalfWidth;
        public float cursorSpeed;
        public int fakeZoneCount;
        public float shakeAmount;

        /// <summary>このステージの制限時間(秒)。0以下なら制限なし。</summary>
        public float timeLimit;
    }

    /// <summary>
    /// ATM で扱う通貨の表記。この世界の通貨は DevilCoin (DC)。
    /// YAML 側の画面テキストも同じ単位で書くこと。
    /// </summary>
    public static class DevilCurrency
    {
        public const string Unit = "DC";

        public static string Format(float amount) => $"{amount:N0} {Unit}";
    }

    /// <summary>既定値の置き場。Inspector 未設定のときにここから補う。</summary>
    public static class HackDefaults
    {
        /// <summary>
        /// 送金一覧の既定値。銀行名はいずれも実在しない造語（神話由来の地獄・冥界の名前）。
        /// 送金額は 小 / 中 / 大 で、それぞれ Easy / Normal / Hard に対応する。
        /// </summary>
        public static List<HackTransferJob> CreateTransfers()
        {
            return new List<HackTransferJob>
            {
                new HackTransferJob
                {
                    fromBank = "MAMMON",
                    toBank = "STYX",
                    amount = 80000f,
                    difficulty = HackDifficulty.Easy
                },
                new HackTransferJob
                {
                    fromBank = "GEHENNA",
                    toBank = "KERBEROS",
                    amount = 420000f,
                    difficulty = HackDifficulty.Normal
                },
                new HackTransferJob
                {
                    fromBank = "ABADDON",
                    toBank = "LETHE",
                    amount = 1850000f,
                    difficulty = HackDifficulty.Hard
                }
            };
        }

        /// <summary>難易度ごとの既定設定。階層構成は仕様どおり Easy=2 / Normal=3 / Hard=4 段。</summary>
        public static List<HackDifficultySettings> CreateDifficulties()
        {
            return new List<HackDifficultySettings>
            {
                new HackDifficultySettings
                {
                    difficulty = HackDifficulty.Easy,
                    label = "EASY",
                    layerLabels = new[] { "FIREWALL Lv.1", "BANK ADMIN" },
                    safeZoneHalfWidth = 0.18f,
                    finalSafeZoneScale = 0.40f,
                    cursorSpeed = 0.85f,
                    finalCursorSpeedScale = 1.30f,
                    maxShake = 8f,
                    timeLimit = 9f,
                    finalTimeLimitScale = 0.65f
                },
                new HackDifficultySettings
                {
                    difficulty = HackDifficulty.Normal,
                    label = "NORMAL",
                    layerLabels = new[] { "FIREWALL Lv.1", "FIREWALL Lv.2", "BANK ADMIN" },
                    safeZoneHalfWidth = 0.17f,
                    finalSafeZoneScale = 0.33f,
                    cursorSpeed = 1.00f,
                    finalCursorSpeedScale = 1.40f,
                    maxShake = 13f,
                    timeLimit = 8f,
                    finalTimeLimitScale = 0.58f
                },
                new HackDifficultySettings
                {
                    difficulty = HackDifficulty.Hard,
                    label = "HARD",
                    layerLabels = new[] { "FIREWALL Lv.1", "FIREWALL Lv.2", "FIREWALL Lv.3", "BANK ADMIN" },
                    safeZoneHalfWidth = 0.16f,
                    finalSafeZoneScale = 0.30f,
                    cursorSpeed = 1.15f,
                    finalCursorSpeedScale = 1.45f,
                    maxShake = 20f,
                    timeLimit = 7f,
                    finalTimeLimitScale = 0.5f
                }
            };
        }

        /// <summary>口座番号の見た目を作る。実在の書式と紛れないよう記号入りにしている。</summary>
        public static string CreateAccountNumber()
        {
            return $"{Random.Range(100, 1000)}-{Random.Range(0, 100):00}-{Random.Range(100000, 1000000)}";
        }
    }
}
