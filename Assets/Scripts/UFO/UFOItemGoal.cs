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

    [Header("セッション獲得枚数 (UFO終了時に加算確定されます)")]
    [SerializeField] private int _sessionBronzeCoins = 0;
    [SerializeField] private int _sessionSilverCoins = 0;
    [SerializeField] private int _sessionGoldCoins = 0;
    [SerializeField] private int _sessionBlackDiamonds = 0;

    /// <summary>
    /// ランプの獲得演出が実行中かどうかを示します。
    /// UFOCameraController や PatoLampController から参照されます。
    /// </summary>
    public static bool IsFlashing { get; private set; } = false;





    [Header("効果音")]
    [Tooltip("再生用のAudioSource。未設定の場合は自動でGetComponentします")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("コイン獲得時の効果音")]
    [SerializeField] private AudioClip coinGetSound;

    [Tooltip("時計獲得時の効果音（未設定の場合はコインと同じ音が鳴ります）")]
    [SerializeField] private AudioClip watchGetSound;

    [Tooltip("ブラックダイヤモンド獲得時の効果音（未設定の場合はコインと同じ音が鳴ります）")]
    [SerializeField] private AudioClip blackDiamondGetSound;

    [Tooltip("効果音の音量調整 (1.0より大きい値で音量増幅可能)")]
    [Range(0f, 10f)]
    [SerializeField] private float soundVolume = 1.0f;

    [Header("時計効果")]
    [Tooltip("時計を落とし口に入れたときにUFOキャッチャーの残り時間を何秒延長するか")]
    [SerializeField] public float watchTimeExtension = 20f;

    [Header("ランプ連動設定")]
    [Tooltip("時計獲得時に緑色に光らせる対象 of ランプについたタグ名")]
    [SerializeField] private string lampTag = "InsertableItem";

    [Tooltip("ランプが光る時間（秒）")]
    [SerializeField] private float lampGreenDuration = 3.0f;

    [Tooltip("時計獲得時にランプが光る色（HDRカラー）")]
    [ColorUsage(true, true)]
    [SerializeField] private Color lampFlashColor = Color.green * 3.0f;

    [Tooltip("ルーレット獲得時にランプが光る色（HDRカラー）")]
    [ColorUsage(true, true)]
    [SerializeField] private Color rouletteFlashColor = new Color(1.0f, 0.85f, 0.2f, 1.0f) * 3.0f;

    [Tooltip("ブラックダイヤモンド獲得時にランプが光る色（HDRカラー）")]
    [ColorUsage(true, true)]
    [SerializeField] private Color blackDiamondFlashColor = new Color(0.8f, 0.0f, 1.0f, 1.0f) * 3.0f;

    [Tooltip("ルーレット獲得時のランプ点滅間隔（秒）")]
    [SerializeField] private float rouletteBlinkInterval = 0.15f;

    [Header("フィーバータイム設定")]
    [Tooltip("フィーバータイム有効時間（秒）")]
    [SerializeField] private float feverDuration = 10f;

    [Tooltip("フィーバータイム用の金貨プレハブ")]
    [SerializeField] private GameObject feverGoldPrefab;
    [Tooltip("フィーバータイム用の銀貨プレハブ")]
    [SerializeField] private GameObject feverSilverPrefab;
    [Tooltip("フィーバータイム用の銅貨プレハブ")]
    [SerializeField] private GameObject feverCopperPrefab;

    [Tooltip("金貨の降る割合（比率）")]
    [Range(0f, 100f)]
    [SerializeField] private float feverGoldRatio = 20f;

    [Tooltip("銀貨の降る割合（比率）")]
    [Range(0f, 100f)]
    [SerializeField] private float feverSilverRatio = 30f;

    [Tooltip("銅貨の降る割合（比率）")]
    [Range(0f, 100f)]
    [SerializeField] private float feverCopperRatio = 50f;

    [Tooltip("フィーバータイム中に降らせるコインの総枚数")]
    [SerializeField] private int feverRainCoinCount = 100;

    [Tooltip("フィーバータイム中に降らせる範囲のスケール")]
    [SerializeField] private Vector2 feverRainAreaScale = new Vector2(0.5f, 0.5f);

    [Tooltip("フィーバータイム中に降らせる範囲 of オフセット")]
    [SerializeField] private Vector2 feverRainAreaOffset = Vector2.zero;

    [Header("ルーレット設定")]
    [Tooltip("ルーレット発生時に降らせるオブジェクトのプレハブリスト（空の場合はゴールドコインになります）")]
    public System.Collections.Generic.List<GameObject> rouletteRainPrefabs = new System.Collections.Generic.List<GameObject>();
    [Tooltip("ルーレット発生時に降らせるコインの枚数")]
    public int rouletteRainCoinCount = 100;
    [Tooltip("ルーレット発生時に降らせる時間（秒）")]
    public float rouletteRainDuration = 5.0f;
    [Tooltip("ルーレット発生時に降らせる範囲のスケール（1でエリア0の全域、小さいほど中心部に狭まります）")]
    public Vector2 rouletteRainAreaScale = new Vector2(0.5f, 0.5f);
    [Tooltip("ルーレット発生時に降らせる範囲のオフセット（エリア0の中心からの位置のズレ）")]
    public Vector2 rouletteRainAreaOffset = Vector2.zero;

    [Tooltip("スピンさせるルーレットのコントローラー")]
    [SerializeField] private RouletteController rouletteController;

    public static UFOItemGoal Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        // AudioSourceの自動取得
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        // ルーレットのイベント購読
        if (rouletteController != null)
        {
            rouletteController.OnSpinComplete.AddListener(HandleRouletteSpinComplete);
        }

        // UnwashedMoneyManagerの初期値との同期、および変更イベントの購読
        if (UnwashedMoneyManager.Instance != null)
        {
            unwashedMoney = UnwashedMoneyManager.Instance.CurrentAmount;
            UnwashedMoneyManager.Instance.OnAmountChanged += HandleUnwashedMoneyChanged;
        }

        // UFOカメラのプレイ開始・終了イベントを購読
        UFOCameraController.OnUfoModeChanged += HandleUfoModeChanged;
    }

    private Coroutine _lampCoroutine;
    private System.Collections.Generic.Dictionary<Light, Color> _originalColors = new System.Collections.Generic.Dictionary<Light, Color>();
    private System.Collections.Generic.Dictionary<Light, bool> _originalEnabled = new System.Collections.Generic.Dictionary<Light, bool>();
    private System.Collections.Generic.Dictionary<Renderer, Color> _originalEmissionColors = new System.Collections.Generic.Dictionary<Renderer, Color>();

    private void OnDisable()
    {
        ResetLampsToOriginal();
    }

    private void OnDestroy()
    {
        if (UnwashedMoneyManager.Instance != null)
        {
            UnwashedMoneyManager.Instance.OnAmountChanged -= HandleUnwashedMoneyChanged;
        }
        if (rouletteController != null)
        {
            rouletteController.OnSpinComplete.RemoveListener(HandleRouletteSpinComplete);
        }
        UFOCameraController.OnUfoModeChanged -= HandleUfoModeChanged;
        ResetLampsToOriginal();
    }

    private void HandleUfoModeChanged(bool isPlayingUfo)
    {
        if (!isPlayingUfo)
        {
            // セッション終了時、とった分だけ所持枚数（PlayerWallet）を増やす
            if (UnwashedMoneyManager.Instance != null)
            {
                UnwashedMoneyManager.Instance.AddCoins(_sessionBronzeCoins, _sessionSilverCoins, _sessionGoldCoins, _sessionBlackDiamonds);
                Debug.Log($"[UFOキャッチャー終了] 獲得コイン・ダイヤを所持金に加算しました: 銅{_sessionBronzeCoins}枚, 銀{_sessionSilverCoins}枚, 金{_sessionGoldCoins}枚, 黒ダイヤ{_sessionBlackDiamonds}個");
            }
            
            // セッション用カウンターをリセット
            _sessionBronzeCoins = 0;
            _sessionSilverCoins = 0;
            _sessionGoldCoins = 0;
            _sessionBlackDiamonds = 0;
        }
    }

    /// <summary>
    /// Spawner内のメンバ変数またはアクティブ・候補枠のリストから、キーワード部分一致でプレハブを自動検出するフォールバックメソッド
    /// </summary>
    private GameObject FindPrefabFromSpawner(string nameKeyword)
    {
        if (ItemSpawner.Instance == null) return null;

        string kw = nameKeyword.ToLower();

        // 1. 直属のメンバ変数
        if (kw.Contains("copper") && ItemSpawner.Instance.copperCoinPrefab != null)
        {
            Debug.Log($"[UFOItemGoal] ItemSpawner メンバ変数から銅貨プレハブを取得しました: {ItemSpawner.Instance.copperCoinPrefab.name}");
            return ItemSpawner.Instance.copperCoinPrefab;
        }
        if (kw.Contains("silver") && ItemSpawner.Instance.silverCoinPrefab != null)
        {
            Debug.Log($"[UFOItemGoal] ItemSpawner メンバ変数から銀貨プレハブを取得しました: {ItemSpawner.Instance.silverCoinPrefab.name}");
            return ItemSpawner.Instance.silverCoinPrefab;
        }
        if (kw.Contains("gold") && ItemSpawner.Instance.goldCoinPrefab != null)
        {
            Debug.Log($"[UFOItemGoal] ItemSpawner メンバ変数から金貨プレハブを取得しました: {ItemSpawner.Instance.goldCoinPrefab.name}");
            return ItemSpawner.Instance.goldCoinPrefab;
        }
        if (kw.Contains("black") && ItemSpawner.Instance.blackDiamondPrefab != null)
        {
            Debug.Log($"[UFOItemGoal] ItemSpawner メンバ変数から黒ダイヤプレハブを取得しました: {ItemSpawner.Instance.blackDiamondPrefab.name}");
            return ItemSpawner.Instance.blackDiamondPrefab;
        }

        // 2. initialActiveItems (初期アクティブリスト) から検索
        if (ItemSpawner.Instance.initialActiveItems != null)
        {
            foreach (var item in ItemSpawner.Instance.initialActiveItems)
            {
                if (item != null && item.prefab != null && item.prefab.name.ToLower().Contains(kw))
                {
                    Debug.Log($"[UFOItemGoal] initialActiveItems からプレハブを自動検出しました: {item.prefab.name}");
                    return item.prefab;
                }
            }
        }

        // 3. itemCandidates (候補リスト) から検索
        if (ItemSpawner.Instance.itemCandidates != null)
        {
            foreach (var item in ItemSpawner.Instance.itemCandidates)
            {
                if (item != null && item.prefab != null && item.prefab.name.ToLower().Contains(kw))
                {
                    Debug.Log($"[UFOItemGoal] itemCandidates からプレハブを自動検出しました: {item.prefab.name}");
                    return item.prefab;
                }
            }
        }

        return null;
    }

    private void HandleRouletteSpinComplete(int winningIndex)
    {
        Debug.Log($"[UFOItemGoal] HandleRouletteSpinComplete: 当選結果インデックス = {winningIndex}");

        // 1. 時間の一時停止を解除する
        if (UFOCameraController.Instance != null)
        {
            UFOCameraController.Instance.IsRouletteTimePaused = false;
            Debug.Log("[UFOItemGoal] ルーレット完了につき時間の一時停止を解除しました。");
        }

        // 2. 当選インデックスに応じたアイテムを降らせる
        if (ItemSpawner.Instance != null)
        {
            GameObject rewardPrefab = null;

            // 1. まずはアタッチされている RouletteController のスロット設定から直接プレハブを取得する
            if (rouletteController != null)
            {
                var slot = rouletteController.GetSlotEntry(winningIndex);
                if (slot != null && slot.rewardPrefab != null)
                {
                    rewardPrefab = slot.rewardPrefab;
                    Debug.Log($"[UFOItemGoal] slots[{winningIndex}] のアタッチ設定からプレハブを読み込みました: {rewardPrefab.name}");
                }
            }

            // 2. もしアタッチされていない場合の自動検索フォールバック
            if (rewardPrefab == null)
            {
                Debug.LogWarning($"[UFOItemGoal] 当選スロット [{winningIndex}] に rewardPrefab が設定されていません。自動検索フォールバックを開始します。");
                switch (winningIndex)
                {
                    case 0:
                    case 1:
                        rewardPrefab = FindPrefabFromSpawner("copper");
                        break;
                    case 2:
                        rewardPrefab = FindPrefabFromSpawner("silver");
                        break;
                    case 3:
                        rewardPrefab = FindPrefabFromSpawner("gold");
                        break;
                    case 4:
                        rewardPrefab = FindPrefabFromSpawner("black");
                        break;
                }
            }

            if (rewardPrefab != null)
            {
                System.Collections.Generic.List<GameObject> prefabsToSpawn = new System.Collections.Generic.List<GameObject> { rewardPrefab };
                
                // インスペクターの設定値が0以下の場合はデフォルト値でフォールバックする
                int rainCount = rouletteRainCoinCount > 0 ? rouletteRainCoinCount : 100;
                float rainDuration = rouletteRainDuration > 0f ? rouletteRainDuration : 5.0f;
                Vector2 rainScale = rouletteRainAreaScale.sqrMagnitude > 0.001f ? rouletteRainAreaScale : new Vector2(0.5f, 0.5f);

                Debug.Log($"[UFOItemGoal] ルーレット当選結果 [{winningIndex}]: {rewardPrefab.name} (枚数: {rainCount}, 時間: {rainDuration}秒, 範囲スケール: {rainScale}) の降雨を開始しました！");

                ItemSpawner.Instance.StartRouletteRain(
                    prefabsToSpawn, 
                    rainCount, 
                    rainDuration, 
                    rainScale, 
                    rouletteRainAreaOffset
                );
            }
            else
            {
                Debug.LogError($"[UFOItemGoal] 当選インデックス {winningIndex} に対応するプレハブが Spawner 内から見つかりません！");
            }
        }
        else
        {
            Debug.LogError("[UFOItemGoal] ItemSpawner.Instance が null のため、ドロップを生成できません！");
        }
    }

    private void HandleUnwashedMoneyChanged(float newAmount)
    {
        unwashedMoney = newAmount;
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
            
            // 銅・銀・金・ブラックダイヤモンド・ルーレットアイテム以外のアイテムが投入された場合に、変換枚数をセッション用カウンターに加算する
            if (item.itemType != UFOItemType.CopperCoin &&
                item.itemType != UFOItemType.SilverCoin &&
                item.itemType != UFOItemType.GoldCoin &&
                item.itemType != UFOItemType.RouletteItem &&
                item.itemType != UFOItemType.BlackDiamond)
            {
                int goldConvert = 0;
                int silverConvert = 0;
                int bronzeConvert = 0;
                bool foundConversion = false;

                if (ItemSpawner.Instance != null)
                {
                    // オブジェクトのプレハブ名をクローン表記などを除いて一致させる
                    string itemName = other.gameObject.name.Replace("(Clone)", "").Trim();

                    // initialActiveItems から検索
                    var settings = ItemSpawner.Instance.initialActiveItems.Find(x => x.prefab != null && x.prefab.name == itemName);
                    if (settings == null)
                    {
                        // itemCandidates から検索
                        settings = ItemSpawner.Instance.itemCandidates.Find(x => x.prefab != null && x.prefab.name == itemName);
                    }

                    if (settings != null)
                    {
                        goldConvert = settings.goldConvertCount;
                        silverConvert = settings.silverConvertCount;
                        bronzeConvert = settings.bronzeConvertCount;
                        foundConversion = true;
                    }
                }

                if (foundConversion)
                {
                    _sessionGoldCoins += goldConvert;
                    _sessionSilverCoins += silverConvert;
                    _sessionBronzeCoins += bronzeConvert;
                    Debug.Log($"[獲得変換] {item.itemType} (名前: {other.gameObject.name}) を獲得！ (金貨+{goldConvert}, 銀貨+{silverConvert}, 銅貨+{bronzeConvert})");
                }
                else
                {
                    // フォールバック: もしインスペクターで何も設定されていなかった場合のデフォルト
                    if (item.itemType == UFOItemType.Watch)
                    {
                        // デフォルト設定
                        _sessionBronzeCoins += 10;
                        Debug.Log($"[獲得変換] {item.itemType} (名前: {other.gameObject.name}) を獲得！ (デフォルトフォールバック: 銅貨+10)");
                    }
                }
            }

            // 種類ごとにカウントや特別な処理をする
            switch (item.itemType)
            {
                case UFOItemType.CopperCoin:
                    _sessionBronzeCoins++;
                    Debug.Log($"[獲得] 銅貨！ (今回セッション累計: 銅{_sessionBronzeCoins})");
                    // コイン獲得音の再生
                    PlaySound(coinGetSound);
                    break;
                case UFOItemType.SilverCoin:
                    _sessionSilverCoins++;
                    Debug.Log($"[獲得] 銀貨！ (今回セッション累計: 銀{_sessionSilverCoins})");
                    // コイン獲得音の再生
                    PlaySound(coinGetSound);
                    break;
                case UFOItemType.GoldCoin:
                    _sessionGoldCoins++;
                    Debug.Log($"[獲得] 金貨！ (今回セッション累計: 金{_sessionGoldCoins})");
                    // コイン獲得音の再生
                    PlaySound(coinGetSound);
                    break;
                    TriggerRouletteItemGoalEffect(finalValue);
                    break;
                case UFOItemType.Watch:
                    collectedWatches++;
                    Debug.Log($"[獲得] 時計！ (累計: {collectedWatches}個)");

                    // UFOキャッチャーの残り時間を延長
                    if (UFOCameraController.Instance != null)
                    {
                        UFOCameraController.Instance.AddPlayTime(watchTimeExtension);
                    }

                    // 時計獲得音の再生（未設定ならコイン音で代用）
                    PlaySound(watchGetSound != null ? watchGetSound : coinGetSound);

                    // ランプを時計用の色に光らせる（常灯）
                    TriggerLampFlash(lampFlashColor, false);
                    break;
                case UFOItemType.BlackDiamond:
                    _sessionBlackDiamonds++;
                    Debug.Log($"[獲得] BlackDiamond！ (今回セッション累計: 黒{_sessionBlackDiamonds})");
                    PlaySound(blackDiamondGetSound != null ? blackDiamondGetSound : coinGetSound);

                    // ランプを紫に光らせる（常灯）
                    TriggerLampFlash(blackDiamondFlashColor, false);
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

    private void TriggerLampFlash(Color color, bool isBlink)
    {
        IsFlashing = true; // 演出開始
        if (_lampCoroutine != null)
        {
            StopCoroutine(_lampCoroutine);
        }
        _lampCoroutine = StartCoroutine(FlashLampsCoroutine(color, isBlink));
    }

    /// <summary>
    /// プレゼントボックス / ルーレットアイテム投入時と同等のルーレット獲得処理を実行する。
    /// デバッグキー (Kキー等) や外部トリガーからの実行用。
    /// </summary>
    public void TriggerRouletteItemGoalEffect(float finalValue = 1000f)
    {
        // メインのお金（MoneyManager）ではなく、未洗浄メダルとして別に貯める
        if (UnwashedMoneyManager.Instance != null)
        {
            UnwashedMoneyManager.Instance.Add(finalValue);
        }
        else
        {
            unwashedMoney += finalValue;
        }
        Debug.Log($"[獲得] RouletteItem (トリガー実行)！ (未洗浄メダル総額: {unwashedMoney}円)");
        
        // コイン獲得音の再生
        PlaySound(coinGetSound);

        // ランプを金（ゴールド）に点滅させる
        TriggerLampFlash(rouletteFlashColor, true);

        // 時間の一時停止を開始する
        if (UFOCameraController.Instance != null)
        {
            UFOCameraController.Instance.IsRouletteTimePaused = true;
            Debug.Log("[UFOItemGoal] ルーレットアイテム投入につき、UFO残り時間の減少を一時停止しました。");
        }

        // ルーレットを回転させる
        if (rouletteController != null)
        {
            // リスナーの多重登録を防ぎつつ、イベントを購読
            rouletteController.OnSpinComplete.RemoveListener(HandleRouletteSpinComplete);
            rouletteController.OnSpinComplete.AddListener(HandleRouletteSpinComplete);
            
            rouletteController.Spin();
        }
        else
        {
            Debug.LogError("[UFOItemGoal] rouletteController がアタッチされていません！インスペクターで RouletteController を必ずアタッチしてください。時間停止を解除します。");
            if (UFOCameraController.Instance != null)
            {
                UFOCameraController.Instance.IsRouletteTimePaused = false;
            }
        }
    }

    private System.Collections.IEnumerator FlashLampsCoroutine(Color color, bool isBlink)
    {
        GameObject[] lampObjects = GameObject.FindGameObjectsWithTag(lampTag);

        // フォールバック: 大文字小文字の揺らぎ対策
        if ((lampObjects == null || lampObjects.Length == 0) && (lampTag == "InsertableItem" || lampTag == "insertableItem"))
        {
            string alternativeTag = (lampTag == "InsertableItem") ? "insertableItem" : "InsertableItem";
            try
            {
                lampObjects = GameObject.FindGameObjectsWithTag(alternativeTag);
                if (lampObjects != null && lampObjects.Length > 0)
                {
                    Debug.LogWarning($"[ランプ連動] タグ '{lampTag}' ではオブジェクトが見つかりませんでしたが、代替タグ '{alternativeTag}' で {lampObjects.Length} 個検出しました。こちらを使用します。");
                }
            }
            catch (System.Exception) { }
        }

        int totalObjects = lampObjects != null ? lampObjects.Length : 0;
        System.Collections.Generic.List<Light> targetLights = new System.Collections.Generic.List<Light>();
        System.Collections.Generic.List<Renderer> targetRenderers = new System.Collections.Generic.List<Renderer>();

        if (lampObjects != null)
        {
            foreach (var obj in lampObjects)
            {
                if (obj != null)
                {
                    // 子オブジェクトの中から名前が "spotlight" (大文字小文字・スペース無視) である Light を収集
                    Light[] childLights = obj.GetComponentsInChildren<Light>(true);
                    foreach (var light in childLights)
                    {
                        if (light != null)
                        {
                            string nameClean = light.gameObject.name.ToLower().Replace(" ", "");
                            if (nameClean.Contains("spotlight"))
                            {
                                targetLights.Add(light);
                            }
                        }
                    }

                    // Rendererも収集する
                    targetRenderers.AddRange(obj.GetComponentsInChildren<Renderer>(true));
                }
            }
        }

        Debug.Log($"[ランプ連動] 対象オブジェクトを {totalObjects} 個検出。制御対象: Light={targetLights.Count}個, Renderer={targetRenderers.Count}個");

        // 元の状態をあらかじめ記録（Blink時の復元用にも使用）
        foreach (var light in targetLights)
        {
            if (light != null && !_originalColors.ContainsKey(light))
            {
                _originalColors[light] = light.color;
                _originalEnabled[light] = light.enabled;
            }
        }

        // 元のエミッション色を保存する
        foreach (var r in targetRenderers)
        {
            if (r != null)
            {
                // マテリアルが _EmissionColor プロパティを持っているか確認
                Color origEmission = Color.black;
                bool hasEmission = false;
                if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_EmissionColor"))
                {
                    origEmission = r.sharedMaterial.GetColor("_EmissionColor");
                    hasEmission = true;
                }

                if (hasEmission && !_originalEmissionColors.ContainsKey(r))
                {
                    _originalEmissionColors[r] = origEmission;
                }
            }
        }

        if (isBlink)
        {
            // 点滅処理の実行（周期的にオン・オフ）
            float elapsed = 0f;
            bool isOn = true;

            while (elapsed < lampGreenDuration)
            {
                if (isOn)
                {
                    // 点灯（スポットライトとマテリアルを演出色へ）
                    foreach (var light in targetLights)
                    {
                        if (light != null)
                        {
                            light.color = color;
                            light.enabled = true;
                        }
                    }
                    foreach (var r in targetRenderers)
                    {
                        if (r != null && _originalEmissionColors.ContainsKey(r))
                        {
                            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                            r.GetPropertyBlock(mpb);
                            mpb.SetColor("_EmissionColor", color);
                            r.SetPropertyBlock(mpb);
                        }
                    }
                }
                else
                {
                    // 消灯（スポットライトを消灯、マテリアルも黒へ）
                    foreach (var light in targetLights)
                    {
                        if (light != null)
                        {
                            light.color = _originalColors[light];
                            light.enabled = _originalEnabled[light];
                        }
                    }
                    foreach (var r in targetRenderers)
                    {
                        if (r != null && _originalEmissionColors.ContainsKey(r))
                        {
                            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                            r.GetPropertyBlock(mpb);
                            mpb.SetColor("_EmissionColor", Color.black);
                            r.SetPropertyBlock(mpb);
                        }
                    }
                }

                isOn = !isOn;
                yield return new WaitForSeconds(rouletteBlinkInterval);
                elapsed += rouletteBlinkInterval;
            }
        }
        else
        {
            // 通常の常灯処理（スポットライトとマテリアルを演出色へ）
            foreach (var light in targetLights)
            {
                if (light != null)
                {
                    light.color = color;
                    light.enabled = true;
                }
            }
            foreach (var r in targetRenderers)
            {
                if (r != null && _originalEmissionColors.ContainsKey(r))
                {
                    MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor("_EmissionColor", color);
                    r.SetPropertyBlock(mpb);
                }
            }

            yield return new WaitForSeconds(lampGreenDuration);
        }

        // 最後に確実に元の状態に復元
        ResetLampsToOriginal();

        // 演出終了後、現在のUFOCameraControllerのライトアクティブ状態に合わせて消灯/点灯を再反映する
        if (UFOCameraController.Instance != null)
        {
            SetInsertableItemsLights(UFOCameraController.IsPlaySpotlightActive);
        }
    }

    private void ResetLampsToOriginal()
    {
        if (_lampCoroutine != null)
        {
            StopCoroutine(_lampCoroutine);
            _lampCoroutine = null;
        }

        // Light の復元
        foreach (var kvp in _originalColors)
        {
            Light light = kvp.Key;
            if (light != null)
            {
                light.color = kvp.Value;
                if (_originalEnabled.TryGetValue(light, out bool wasEnabled))
                {
                    light.enabled = wasEnabled;
                }
            }
        }
        _originalColors.Clear();
        _originalEnabled.Clear();

        // Renderer の復元（元のエミッション色に戻す）
        foreach (var kvp in _originalEmissionColors)
        {
            Renderer r = kvp.Key;
            if (r != null)
            {
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", kvp.Value);
                r.SetPropertyBlock(mpb);
            }
        }
        _originalEmissionColors.Clear();

        IsFlashing = false; // 演出終了
    }

    // ランプ用スポットライトの元強度保存用
    private System.Collections.Generic.Dictionary<Light, float> _lampLightIntensities = new System.Collections.Generic.Dictionary<Light, float>();
    // ランプ用スポットライトの元範囲保存用
    private System.Collections.Generic.Dictionary<Light, float> _lampLightRanges = new System.Collections.Generic.Dictionary<Light, float>();

    /// <summary>
    /// UFOキャッチャー全体のライト点灯状態と連動して、InsertableItemのライトとマテリアルのエミッションを明るさ倍率を考慮してON/OFFします。
    /// active = false で完全消灯、active = true で指定倍率の明るさに戻します。
    /// </summary>
    public void SetInsertableItemsLights(bool active, float brightnessMultiplier = 1.0f)
    {
        // 演出中（IsFlashing）であれば演出側の点灯制御を優先させるため、処理をスキップします
        if (IsFlashing) return;

        GameObject[] lampObjects = GameObject.FindGameObjectsWithTag(lampTag);

        // フォールバック: 大文字小文字の揺らぎ対策
        if ((lampObjects == null || lampObjects.Length == 0) && (lampTag == "InsertableItem" || lampTag == "insertableItem"))
        {
            string alternativeTag = (lampTag == "InsertableItem") ? "insertableItem" : "InsertableItem";
            try
            {
                lampObjects = GameObject.FindGameObjectsWithTag(alternativeTag);
            }
            catch (System.Exception) { }
        }

        if (lampObjects == null) return;

        foreach (var obj in lampObjects)
        {
            if (obj == null) continue;

            // 子オブジェクトから spotlight Light を取得してON/OFF・輝度・範囲制御
            Light[] childLights = obj.GetComponentsInChildren<Light>(true);
            foreach (var light in childLights)
            {
                if (light != null)
                {
                    string nameClean = light.gameObject.name.ToLower().Replace(" ", "");
                    if (nameClean.Contains("spotlight"))
                    {
                        light.enabled = active;
                        if (active)
                        {
                            if (!_lampLightIntensities.ContainsKey(light))
                            {
                                _lampLightIntensities[light] = light.intensity;
                                _lampLightRanges[light] = light.range;
                            }
                            light.intensity = _lampLightIntensities[light] * brightnessMultiplier;
                            light.range = _lampLightRanges[light] * brightnessMultiplier;
                        }
                    }
                }
            }

            // RendererのマテリアルエミッションをON/OFF・輝度制御
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_EmissionColor"))
                {
                    MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                    r.GetPropertyBlock(mpb);

                    if (active)
                    {
                        // オンにする場合：通常設定されている元のエミッション色（sharedMaterialの色）に明るさ倍率を掛けて戻す
                        Color targetColor = r.sharedMaterial.GetColor("_EmissionColor");
                        mpb.SetColor("_EmissionColor", targetColor * brightnessMultiplier);
                    }
                    else
                    {
                        // オフにする場合：エミッションを黒にして発光を消す
                        mpb.SetColor("_EmissionColor", Color.black);
                    }
                    r.SetPropertyBlock(mpb);
                }
            }
        }
    }

    /// <summary>
    /// フィーバータイムを起動します。制限時間がストップし、設定された比率でコインが降ります。
    /// </summary>
    public void StartFeverTime()
    {
        // 1. 制限時間のストップ（UFOCameraControllerに通知）
        if (UFOCameraController.Instance != null)
        {
            UFOCameraController.Instance.StartFeverTime(feverDuration);
        }

        // 2. コイン雨の発生
        if (ItemSpawner.Instance != null)
        {
            System.Collections.Generic.List<GameObject> prefabsToSpawn = new System.Collections.Generic.List<GameObject>();
            
            float total = feverGoldRatio + feverSilverRatio + feverCopperRatio;
            if (total <= 0f) total = 1f;

            // 比率をプール内の個数として再現
            int poolSize = 100;
            int goldCount = Mathf.RoundToInt((feverGoldRatio / total) * poolSize);
            int silverCount = Mathf.RoundToInt((feverSilverRatio / total) * poolSize);
            int copperCount = poolSize - (goldCount + silverCount);

            for (int i = 0; i < goldCount; i++) if (feverGoldPrefab != null) prefabsToSpawn.Add(feverGoldPrefab);
            for (int i = 0; i < silverCount; i++) if (feverSilverPrefab != null) prefabsToSpawn.Add(feverSilverPrefab);
            for (int i = 0; i < copperCount; i++) if (feverCopperPrefab != null) prefabsToSpawn.Add(feverCopperPrefab);

            if (prefabsToSpawn.Count > 0)
            {
                ItemSpawner.Instance.StartRouletteRain(
                    prefabsToSpawn, 
                    feverRainCoinCount, 
                    feverDuration, 
                    feverRainAreaScale, 
                    feverRainAreaOffset
                );
            }
            else
            {
                Debug.LogWarning("[UFOItemGoal] フィーバータイム用のプレハブが設定されていません。");
            }
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
        float rainCenterX = cellCenterX + rouletteRainAreaOffset.x;
        float rainCenterZ = cellCenterZ + rouletteRainAreaOffset.y;
        float halfW = (bounds.maxX - bounds.minX) * 0.5f * rouletteRainAreaScale.x;
        float halfH = (bounds.maxZ - bounds.minZ) * 0.5f * rouletteRainAreaScale.y;

        Vector3 rainCenter = spawnerCenter + new Vector3(rainCenterX, 0f, rainCenterZ);
        Vector3 rainSize = new Vector3(halfW * 2f, 0.1f, halfH * 2f);

        // シーンビューに水色のボックスを表示
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.18f);
        Gizmos.DrawCube(rainCenter, rainSize);
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.85f);
        Gizmos.DrawWireCube(rainCenter, rainSize);

        // ラベルの描画
        UnityEditor.Handles.color = new Color(0f, 0.8f, 1f, 0.95f);
        UnityEditor.Handles.Label(rainCenter + Vector3.up * 0.1f, "Roulette Rain Area (Cell 0)");
    }
#endif
}
