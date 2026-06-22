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



    [Header("効果音")]
    [Tooltip("再生用のAudioSource。未設定の場合は自動でGetComponentします")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("コイン獲得時の効果音")]
    [SerializeField] private AudioClip coinGetSound;

    [Tooltip("時計獲得時の効果音（未設定の場合はコインと同じ音が鳴ります）")]
    [SerializeField] private AudioClip watchGetSound;

    [Tooltip("効果音の音量調整 (1.0より大きい値で音量増幅可能)")]
    [Range(0f, 10f)]
    [SerializeField] private float soundVolume = 1.0f;

    [Header("時計効果")]
    [Tooltip("時計を落とし口に入れたときにUFOキャッチャーの残り時間を何秒延長するか")]
    [SerializeField] public float watchTimeExtension = 20f;

    [Header("ジャックポット設定")]
    [Tooltip("ジャックポット発生時に降らせるオブジェクトのプレハブリスト（空の場合はゴールドコインになります）")]
    public System.Collections.Generic.List<GameObject> jackpotRainPrefabs = new System.Collections.Generic.List<GameObject>();
    [Tooltip("ジャックポット発生時に降らせるコインの枚数")]
    public int jackpotRainCoinCount = 100;
    [Tooltip("ジャックポット発生時に降らせる時間（秒）")]
    public float jackpotRainDuration = 5.0f;
    [Tooltip("ジャックポット発生時に降らせる範囲のスケール（1でエリア0の全域、小さいほど中心部に狭まります）")]
    public Vector2 jackpotRainAreaScale = new Vector2(0.5f, 0.5f);
    [Tooltip("ジャックポット発生時に降らせる範囲のオフセット（エリア0の中心からの位置のズレ）")]
    public Vector2 jackpotRainAreaOffset = Vector2.zero;

    private void Start()
    {
        // AudioSourceの自動取得
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }



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
                    
                    // コイン獲得音の再生
                    PlaySound(coinGetSound);
                    break;
                case UFOItemType.Jackpot:
                    // メインのお金（MoneyManager）ではなく、未洗浄メダルとして別に貯める
                    if (UnwashedMoneyManager.Instance != null)
                    {
                        UnwashedMoneyManager.Instance.Add(finalValue);
                    }
                    else
                    {
                        unwashedMoney += finalValue;
                        UpdateUnwashedMoneyText();
                    }
                    Debug.Log($"[獲得] Jackpot！ (未洗浄メダル総額: {unwashedMoney}円)");
                    
                    // コイン獲得音の再生
                    PlaySound(coinGetSound);

                    // ジャックポット発生時にコイン雨を降らせる
                    if (ItemSpawner.Instance != null)
                    {
                        System.Collections.Generic.List<GameObject> prefabsToSpawn = new System.Collections.Generic.List<GameObject>();
                        if (jackpotRainPrefabs != null && jackpotRainPrefabs.Count > 0)
                        {
                            prefabsToSpawn.AddRange(jackpotRainPrefabs);
                        }
                        else
                        {
                            // フォールバック
                            prefabsToSpawn.Add(ItemSpawner.Instance.goldCoinPrefab);
                        }
                        ItemSpawner.Instance.StartJackpotRain(prefabsToSpawn, jackpotRainCoinCount, jackpotRainDuration, jackpotRainAreaScale, jackpotRainAreaOffset);
                    }
                    break;
                case UFOItemType.Watch:
                    collectedWatches++;
                    Debug.Log($"[獲得] 時計！ (累計: {collectedWatches}個)");

                    // UFOキャッチャーの残り時間を延長
                    if (UFOCameraController.Instance != null)
                    {
                        UFOCameraController.Instance.AddPlayTime(watchTimeExtension);
                    }

                    // 画面のUIテキストが設定されていれば表示を更新する
                    if (watchCountText != null)
                    {
                        watchCountText.text = $"Watch: {collectedWatches}";
                    }

                    // 時計獲得音の再生（未設定ならコイン音で代用）
                    PlaySound(watchGetSound != null ? watchGetSound : coinGetSound);
                    break;
            }

            // アイテムを消去する
            Destroy(other.gameObject);
        }
    }

    /// <summary>
    /// 効果音を再生するヘルパー関数
    /// </summary>
    private void PlaySound(AudioClip clip)
    {
        if (clip != null)
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0f; // 2D音響にして距離減衰を無視する
                }
            }
            audioSource.PlayOneShot(clip, soundVolume);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // 開発中にジャックポットのコイン雨の発生範囲を可視化します
        ItemSpawner spawner = FindObjectOfType<ItemSpawner>();
        if (spawner == null) return;

        // エリア0（左上）の基本バウンズを取得
        var bounds = spawner.ComputeCellBounds(0, spawner.gridType);

        Vector3 spawnerCenter = (spawner.armRoot != null) ? spawner.armRoot.position : spawner.transform.position;
        spawnerCenter.y += spawner.spawnYOffset;

        // エリア0の中心
        float cellCenterX = (bounds.minX + bounds.maxX) * 0.5f;
        float cellCenterZ = (bounds.minZ + bounds.maxZ) * 0.5f;

        // オフセットとスケールを適用
        float rainCenterX = cellCenterX + jackpotRainAreaOffset.x;
        float rainCenterZ = cellCenterZ + jackpotRainAreaOffset.y;
        float halfW = (bounds.maxX - bounds.minX) * 0.5f * jackpotRainAreaScale.x;
        float halfH = (bounds.maxZ - bounds.minZ) * 0.5f * jackpotRainAreaScale.y;

        Vector3 rainCenter = spawnerCenter + new Vector3(rainCenterX, 0f, rainCenterZ);
        Vector3 rainSize = new Vector3(halfW * 2f, 0.1f, halfH * 2f);

        // シーンビューに水色のボックスを表示
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.18f);
        Gizmos.DrawCube(rainCenter, rainSize);
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.85f);
        Gizmos.DrawWireCube(rainCenter, rainSize);

        // ラベルの描画
        UnityEditor.Handles.color = new Color(0f, 0.8f, 1f, 0.95f);
        UnityEditor.Handles.Label(rainCenter + Vector3.up * 0.1f, "Jackpot Rain Area (Cell 0)");
    }
#endif
}
