using UnityEngine;
using TMPro; // 画面の文字（UI）を操作するために追加

/// <summary>
/// UFOキャッチャーの落とし口（透明なTriggerBox）にアタッチするクラス
/// 落とし口の拡張（強化要素）にも対応しやすい設計
/// </summary>
public class UFOItemGoal : MonoBehaviour
{
    [Header("強化要素用（外部から変更可能）")]
    [Tooltip("アイテム獲得時の金額倍率。強化で1.5倍などに変更できる")]
    public float scoreMultiplier = 1.0f;

    [Header("獲得カウント")]
    [Tooltip("獲得した時計の数")]
    public int collectedWatches = 0;

    [Tooltip("獲得した未洗浄メダルの総額")]
    public float unwashedMoney = 0f;

    [Header("画面表示(UI)")]
    [Tooltip("時計の数を表示するUIテキスト")]
    public TextMeshProUGUI watchCountText;

    [Tooltip("未洗浄メダルのお金を表示するUIテキスト")]
    public TextMeshProUGUI unwashedMoneyText;

    [Header("【新規】落とし口の箱の切り替え（強化要素）")]
    [Tooltip("レベルが上がるごとに切り替わる箱のオブジェクトを登録します（Lv1, Lv2, Lv3...の順）")]
    public GameObject[] goalBoxObjects;

    private void Start()
    {
        // 最初はLv1の箱だけを表示し、残りを非表示にする（要素があれば）
        SetGoalLevel(0); // 0が最初のレベル(Lv1)

        // UnwashedMoneyManagerの初期値との同期、および変更イベントの購読
        if (UnwashedMoneyManager.Instance != null)
        {
            unwashedMoney = UnwashedMoneyManager.Instance.CurrentAmount;
            UpdateUnwashedMoneyText();
            UnwashedMoneyManager.Instance.OnAmountChanged += HandleUnwashedMoneyChanged;
        }
    }

    private void OnDestroy()
    {
        if (UnwashedMoneyManager.Instance != null)
        {
            UnwashedMoneyManager.Instance.OnAmountChanged -= HandleUnwashedMoneyChanged;
        }
    }

    private void HandleUnwashedMoneyChanged(float newAmount)
    {
        unwashedMoney = newAmount;
        UpdateUnwashedMoneyText();
    }

    private void UpdateUnwashedMoneyText()
    {
        if (unwashedMoneyText != null)
        {
            unwashedMoneyText.text = $"Unwashed: ¥{Mathf.FloorToInt(unwashedMoney):N0}";
        }
    }

    // 自分の直接のコライダーに入った場合
    private void OnTriggerEnter(Collider other)
    {
        HandleItemDrop(other);
    }

    /// <summary>
    /// アイテムが入ったときの実際の処理（子オブジェクトからも呼ばれる）
    /// </summary>
    public void HandleItemDrop(Collider other)
    {
        // ぶつかった相手が UFOItem コンポーネントを持っているか確認
        UFOItem item = other.GetComponent<UFOItem>();
        
        if (item != null)
        {
            // 獲得金額の計算（基本額 × 強化倍率）
            float finalValue = item.baseValue * scoreMultiplier;
            
            // 種類ごとにカウントや特別な処理をする
            switch (item.itemType)
            {
                case UFOItemType.CopperCoin:
                case UFOItemType.SilverCoin:
                case UFOItemType.GoldCoin:
                    // メインのお金（MoneyManager）ではなく、未洗浄メダルとして別に貯める
                    // 一元管理シングルトンがあればそちらに加算（ピンボールショップ等から参照される）
                    if (UnwashedMoneyManager.Instance != null)
                    {
                        UnwashedMoneyManager.Instance.Add(finalValue);
                    }
                    else
                    {
                        // 旧 API 互換: ローカルの累計とゴール表示も維持
                        unwashedMoney += finalValue;
                        UpdateUnwashedMoneyText();
                    }
                    Debug.Log($"[獲得] {item.itemType}！ (未洗浄メダル総額: {unwashedMoney}円)");
                    break;
                case UFOItemType.Watch:
                    collectedWatches++;
                    Debug.Log($"[獲得] 時計！ (累計: {collectedWatches}個)");
                    
                    // 画面のUIテキストが設定されていれば表示を更新する
                    if (watchCountText != null)
                    {
                        watchCountText.text = $"Watch: {collectedWatches}";
                    }
                    break;
            }

            // アイテムを消去する
            Destroy(other.gameObject);
        }
    }

    /// <summary>
    /// 落とし口の大きさを強化要素で広げるための関数
    /// </summary>
    public void ExpandGoalSize(float expandRate)
    {
        // 現在の大きさに倍率を掛けて広げる
        transform.localScale *= expandRate;
        Debug.Log($"落とし口のサイズが {expandRate}倍 に拡張されました！");
    }

    /// <summary>
    /// 強化要素（UpgradeItemController）から呼ばれる。
    /// レベルに応じて箱の表示を切り替える関数
    /// </summary>
    /// <param name="level">現在のレベル（1回強化したら 1 が渡ってくる想定）</param>
    public void SetGoalLevel(int level)
    {
        if (goalBoxObjects == null || goalBoxObjects.Length == 0) return;

        // すべての箱をチェックし、現在のレベルと同じインデックスの箱だけを表示(true)にする
        for (int i = 0; i < goalBoxObjects.Length; i++)
        {
            if (goalBoxObjects[i] != null)
            {
                // i == level なら true（表示）、それ以外は false（非表示）
                bool shouldShow = (i == level);
                goalBoxObjects[i].SetActive(shouldShow);
            }
        }

        Debug.Log($"落とし口のレベルが {level} に切り替わり、箱の見た目が変化しました！");
    }
}
